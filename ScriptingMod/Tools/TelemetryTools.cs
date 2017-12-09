using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using ScriptingMod.Extensions;

namespace ScriptingMod.Tools
{
    internal static class TelemetryTools
    {
        private const int HeartbeatInterval = 1000 * 60 * 10; // ms; 10 minutes
        private static System.Threading.Timer _heartbeatTimer;

        private static string _clientIdCache;
        private static string ClientId
        {
            get
            {
                if (_clientIdCache == null)
                {
                    const string salt = "Z$5CQ3Ku2XY.fnZD.o=gi0%wm?.:IPr?04*c";
                    var uid = Environment.MachineName + ";" + NetworkInterface.GetAllNetworkInterfaces()
                        .Select(n => n.GetPhysicalAddress().ToString()).Where(s => s != string.Empty).Join(",");
                    _clientIdCache = new Guid(MD5.Create().ComputeHash(Encoding.Default.GetBytes(uid + salt))).ToString();
                }
                return _clientIdCache;
            }
        }

        private static string _modVersionCache;
        private static string ModVersion
        {
            get
            {
                if (_modVersionCache == null)
                {
                    _modVersionCache = Api.GetExecutingMod()?.ModInfo?.Version.Value ?? "";
                }
                return _modVersionCache;
            }
        }

        private static string _userAgentCache;
        private static string UserAgent
        {
            get
            {
                if (_userAgentCache == null)
                {
                    _userAgentCache = $"Mozilla/5.0 (compatible; {Environment.OSVersion.VersionString}; {(IntPtr.Size == 4 ? "x86" : "x64")}) {Constants.ModId}/{ModVersion}";
                }
                return _userAgentCache;
            }
        }

        public static void Init()
        {
            if (!PersistentData.Instance.Telemetry)
                return;

            // Must be done after server was registered in steam because only then
            // we know Internet access is available and the server has a public IP
            Steam.Masterserver.Server.AddEventServerRegistered(delegate
            {
                CollectEvent("app", "start", sessionControl: "start");
                _heartbeatTimer = new System.Threading.Timer(delegate { CollectEvent("app", "heartbeat"); }, null, HeartbeatInterval, HeartbeatInterval);
                Log.Debug("Telemetry heartbeat started.");
            });
        }

        public static void Shutdown()
        {
            if (!PersistentData.Instance.Telemetry)
                return;

            _heartbeatTimer?.Dispose();
            Log.Debug("Telemetry heartbeat stopped.");
            CollectEvent("app", "stop", sessionControl: "end");
        }

        public static void CollectEvent(string eventCategory, string eventAction, string eventLabel = null, int? eventValue = null, string sessionControl = null)
        {
            if (!PersistentData.Instance.Telemetry)
                return;

            var payload = new NameValueCollection();
            payload["t"]      = "event"; // hitType
            payload["ec"]     = eventCategory;
            payload["ea"]     = eventAction;
            if (eventLabel != null)
                payload["el"] = eventLabel;
            if (eventValue != null)
                payload["ev"] = eventValue.Value.ToString();
            if (sessionControl != null)
                payload["sc"] = sessionControl; // "start" or "end"

            Collect(payload);
        }

        /// <summary>
        /// Must be called at ServerRegistered event or later because only then it got the external ip address
        /// </summary>
        private static void Collect(NameValueCollection payload)
        {
            try
            {
                ServicePointManager.ServerCertificateValidationCallback += ServerCertificateValidationHandler;
                var gameInfo = Steam.Masterserver.Server.LocalGameInfo;

                payload["v"]   = "1"; // version
                payload["tid"] = "UA-110885986-1"; // trackingId
                payload["cid"] = ClientId; // clientId
                payload["an"]  = Constants.ModName; // applicationName
                payload["aid"] = Constants.ModId; // applicationId;
                payload["av"]  = ModVersion; // applicationVersion
                payload["cd1"] = gameInfo.GetValue(GameInfoString.GameHost); // ServerName
                payload["cd3"] = gameInfo.GetValue(GameInfoString.IP) + ":" + gameInfo.GetValue(GameInfoInt.Port); // IPPort
                payload["cd5"] = global::Constants.cVersion + " " + global::Constants.cVersionBuild; // GameVersion
                payload["cd6"] = ClientId; // clientId

                // Execute asynchronously to avoid blocking the main thread
                ThreadManager.AddSingleTask(CollectTask, payload, null, false);
            }
            catch (Exception ex)
            {
                Log.Debug("Error while preparing telemetry data: " + ex);
            }
        }

        /// <summary>
        /// Task to be invoked asynchronously; taskInfo.parameter contains NameValueCollection payload.
        /// </summary>
        /// <param name="taskInfo"></param>
        private static void CollectTask(ThreadManager.TaskInfo taskInfo)
        {
            try
            {
                var payload = (NameValueCollection) taskInfo.parameter;
                //Log.Debug("Executing telemetry call with payload: " + Environment.NewLine +
                //    payload.AllKeys.Select(k => k + " = " + payload[k]).Join(Environment.NewLine).Indent(4));
                using (var wc = new WebClient())
                {
                    wc.Headers.Add(HttpRequestHeader.UserAgent, UserAgent);
                    var responseBytes = wc.UploadValues("https://www.google-analytics.com/collect", payload);
#if DEBUG
                    var responseString = Encoding.Default.GetString(responseBytes);
                    if (!responseString.StartsWith("GIF"))
                        Log.Debug("Unexpected telemetry response: " + responseString);
                    else
                        Log.Debug("Telemetry collected.");
#endif
                }
            }
            catch (Exception ex)
            {
                Log.Debug("Error while collecting telemetry: " + ex);
            }
            finally
            {
                ServicePointManager.ServerCertificateValidationCallback -= ServerCertificateValidationHandler;
            }
        }

        /// <summary>
        /// Allow valid certificates even if the machine doesn't trust the cert chain,
        /// which seems to be the case with all valid SSL certificates in Mono.
        /// </summary>
        private static bool ServerCertificateValidationHandler(object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return sslPolicyErrors == SslPolicyErrors.None || sslPolicyErrors == SslPolicyErrors.RemoteCertificateChainErrors;
        }
    }
}
