using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLua;
using NLua.Exceptions;
using ObjectDumper;

namespace ScriptingMod.ScriptEngines
{
    internal class LuaEngine : ScriptEngine
    {
        private static LuaEngine _instance;
        public static LuaEngine Instance => _instance ?? (_instance = new LuaEngine());

        private Lua _lua;
        protected override string CommentPrefix => "--";

        private LuaEngine()
        {
            InitLua();
        }

        private void InitLua()
        {
            _lua = new Lua();
            _lua.LoadCLRPackage();
            _lua["print"] = new Action<object[]>(Print);
            _lua["dump"] = new Action<object, int>(Dump);
            //_lua["GameManager"] = GameManager.Instance;
        }

        public override void ExecuteFile(string filePath)
        {
            var fileName = FileHelper.GetRelativePath(filePath, Api.CommandsFolder);

            Log.Debug($"Starting Lua script {fileName} ...");

            // We are not using _lua.DoFile(..) because it does not support UTF-8 w/ BOM encoding
            // TODO [P3]: Fix UTF-8 for require()'d files too, e.g. by adjusting all scripts on start
            string script = File.ReadAllText(filePath);

            try
            {
                _lua.DoString(script);
                Log.Debug($"Lua script {fileName} ended.");
            }
            catch (LuaScriptException ex)
            {
                // LuaScriptException.ToString() does not - against convention - print stack trace or inner exceptions

                // Create short message for console
                var shortMessage = (ex.Source ?? "") + ex.Message;
                Exception curr = ex;
                while (curr.InnerException != null)
                {
                    curr = curr.InnerException;
                    shortMessage += " ---> " + curr.GetType().FullName + ": " + curr.Message;
                }
                if (ex.InnerException != null)
                    shortMessage += " [details in server log]";
                SdtdConsole.Instance.Output($"Lua script {fileName} failed: " + shortMessage);

                // Create long message with stack trace for log file
                var fullMessage = ex.GetType().FullName + ": " + (ex.Source ?? "") + ex.Message;
                if (ex.InnerException != null)
                    fullMessage += " ---> " + ex.InnerException + Environment.NewLine + "   --- End of inner exception stack trace ---";
                if (ex.StackTrace != null)
                    fullMessage += Environment.NewLine + ex.StackTrace;
                Log.Error($"Lua script {fileName} failed: " + fullMessage);

                // Dump only for me
                Log.Dump(ex);
            }
        }

        public override void SetValue(string name, object value)
        {
            _lua[name] = value;
        }

        public void Reset()
        {
            _lua?.Dispose();
            InitLua();
        }

        #region Methods exposed in Lua

        private void Print(params object[] values)
        {
            if (values == null || values.Length == 0)
                return;
            string output = values.Select(v => v.ToString()).Aggregate((s, s1) => s + s1);
            SdtdConsole.Instance.Output(output);
            Log.Debug(output);
        }

        private void Dump(object obj, int depth = 4)
        {
            var output = obj.DumpToString("object", new DumpOptions() {MaxDepth = depth});
            var truncated = output.Substring(0, 1024);

            if (output.Length > truncated.Length)
            {
                SdtdConsole.Instance.Output(truncated + " [...]\r\n[output truncated; full output in log file]");
                Log.Debug(output);
            }
            else
            {
                SdtdConsole.Instance.Output(output);
            }
        }

        #endregion
    }
}
