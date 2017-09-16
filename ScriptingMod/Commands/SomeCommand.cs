using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using AllocsFixes.JSON;
using AllocsFixes.NetConnections.Servers.Web;
using AllocsFixes.NetConnections.Servers.Web.API;
using JetBrains.Annotations;

namespace ScriptingMod.Commands
{
    [UsedImplicitly] // calms ReSharper
    public class SomeCommand : ConsoleCmdAbstract
    {
        public override string[] GetCommands()
        {
            return new[] {"some-command"};
        }

        public override string GetDescription()
        {
            return @"";
        }

        public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
        {
            var conn = _senderInfo.NetworkConnection as WebCommandResultAsync;
            if (conn == null)
            {
                SdtdConsole.Instance.Output("Sorry, this command is so heavy that you are only allowed to call it with ExecuteConsoleCommandAsync!");
                return;
            }

            GameManager.Instance.World.ChunkCache.OnChunksFinishedLoadingDelegates += delegate
            {
                if (GameManager.Instance.World.ChunkCache.ContainsChunkSync(1234567L)) // wait for my chunk
                {
                    // Sends the output to browser and close connection
                    conn.SendLinesAsync(new List<string> { "Hello Browser! All done!" });
                }

                // Make sure to ALWAYS call SendLines, otherwise the web request is never answered or closed
            };
        }

    }

    public class ExecuteConsoleCommandAsync : WebAPI
    {
        static ExecuteConsoleCommandAsync()
        {
            // TODO:
            // AllocsFixes.NetConnections.Servers.Web.Handlers.ApiHandler doesn't pick up this command automatically,
            // because it only looks for types extended from WebAPI in it's own DLL. So we must force our web command into the
            // handler through dirty private reflections.

            // --> SdtdConsole -> private List<IConsoleServer> list_2   // dynamic name; find by type!
            //     Find AllocsFixes.NetConnections.Servers.Web.Web in the list
            // --> AllocsFixes.NetConnections.Servers.Web.Web -> private Dictionary<string, PathHandler> handlers
            //     Find element with key "/api/" or value of type AllocsFixes.NetConnections.Servers.Web.Handlers.ApiHandler
            // --> AllocsFixes.NetConnections.Servers.Web.Handlers.ApiHandler -> private Dictionary<String, WebAPI> apis
            Dictionary<String, WebAPI> apis = null; // <------ put in here -----------------------------------------/

            var apiInstance = new ExecuteConsoleCommandAsync();
            apis.Add(apiInstance.GetType().Name.ToLower(), apiInstance);
        }

        public override void HandleRequest(HttpListenerRequest req, HttpListenerResponse resp, WebConnection user, int permissionLevel)
        {
            if (string.IsNullOrEmpty(req.QueryString["command"]))
            {
                resp.StatusCode = (int)HttpStatusCode.BadRequest;
                Web.SetResponseTextContent(resp, "No command given");
                return;
            }

            string commandline = req.QueryString["command"];
            string commandPart = commandline.Split(' ')[0];
            string argumentsPart = commandline.Substring(Math.Min(commandline.Length, commandPart.Length + 1));

            IConsoleCommand command = SdtdConsole.Instance.GetCommand(commandline);

            if (command == null)
            {
                resp.StatusCode = (int)HttpStatusCode.NotImplemented;
                Web.SetResponseTextContent(resp, "Unknown command");
                return;
            }

            AdminToolsCommandPermissions atcp = GameManager.Instance.adminTools.GetAdminToolsCommandPermission(command.GetCommands());

            if (permissionLevel > atcp.PermissionLevel)
            {
                resp.StatusCode = (int)HttpStatusCode.Forbidden;
                Web.SetResponseTextContent(resp, "You are not allowed to execute this command");
                return;
            }

            // TODO: Execute command (store resp as IConsoleConnection instance to deliver response to the single client?)

            resp.SendChunked = true;
            
            // Here we create our own result object instead!
            WebCommandResultAsync wcr = new WebCommandResultAsync(commandPart, argumentsPart, resp);
            SdtdConsole.Instance.ExecuteAsync(commandline, wcr);
        }

        public override int DefaultPermissionLevel()
        {
            return 2000;
        }
    }

    public class WebCommandResultAsync : IConsoleConnection
    {
        public static int handlingCount = 0;
        public static int currentHandlers = 0;
        public static long totalHandlingTime = 0;

        private HttpListenerResponse response;
        private string command;
        private string parameters;

        public WebCommandResultAsync(string _command, string _parameters, HttpListenerResponse _response)
        {
            Interlocked.Increment(ref handlingCount);
            Interlocked.Increment(ref currentHandlers);

            response = _response;
            command = _command;
            parameters = _parameters;
        }

        // Everything that commands output with SdtdConsole.Instance.Output() goes just into the log ...
        public void SendLines(List<string> _output)
        {
            Log.Out("Synchronous command output: \r\n" + string.Join("\r\n", _output.ToArray()));

            // you could also store the output in a field and later add it to the output int SendLinesAsync
        }

        // This must be called to send an async response to the browser. It will also close the request!
        public void SendLinesAsync(List<string> _output)
        {
            MicroStopwatch msw = new MicroStopwatch();

            StringBuilder sb = new StringBuilder();
            foreach (string line in _output)
            {
                sb.AppendLine(line);
            }

            JSONObject result = new JSONObject();

            result.Add("command", new JSONString(command));
            result.Add("parameters", new JSONString(parameters));
            result.Add("result", new JSONString(sb.ToString()));

            response.SendChunked = false;

            try
            {
                WriteJSON(response, result);
            }
            catch (IOException e)
            {
                if (e.InnerException is SocketException)
                {
                    Log.Out("Error in WebCommandResultAsync.SendLines(): Remote host closed connection: " + e.InnerException.Message);
                }
                else
                {
                    Log.Out("Error (IO) in WebCommandResultAsync.SendLines(): " + e);
                }
            }
            catch (Exception e)
            {
                Log.Out("Error in WebCommandResultAsync.SendLines(): " + e);
            }
            finally
            {
                if (response != null)
                {
                    response.Close();
                }

                msw.Stop();
                totalHandlingTime += msw.ElapsedMicroseconds;
                Log.Out("WebCommandResultAsync.SendLines(): Took {0} µs", msw.ElapsedMicroseconds);
                Interlocked.Decrement(ref currentHandlers);
            }
        }

        public void WriteJSON(HttpListenerResponse resp, AllocsFixes.JSON.JSONNode root)
        {
            byte[] buf = Encoding.UTF8.GetBytes(root.ToString());
            resp.ContentLength64 = buf.Length;
            resp.ContentType = "application/json";
            resp.ContentEncoding = Encoding.UTF8;
            resp.OutputStream.Write(buf, 0, buf.Length);
        }

        public void SendLine(string _text)
        {
            // throw new NotImplementedException();
        }

        public void SendLog(string _msg, string _trace, UnityEngine.LogType _type)
        {
            //throw new NotImplementedException ();
        }

        public void EnableLogLevel(UnityEngine.LogType _type, bool _enable)
        {
            //throw new NotImplementedException ();
        }

        public string GetDescription()
        {
            return "WebCommandResult_for_" + command;
        }
    }

}
