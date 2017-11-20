using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLua;
using NLua.Exceptions;
using ScriptingMod.Extensions;

namespace ScriptingMod.ScriptEngines
{
    internal sealed class LuaEngine : ScriptEngine
    {
        public const string FileExtension = ".lua";
        private static LuaEngine _instance;
        public static LuaEngine Instance => _instance ?? (_instance = new LuaEngine());
		
        private Lua _lua;
		
        private LuaEngine()
        {
            // intentionally not initializing _lua because that's done before every command execution
        }

        protected override void ResetEngine()
        {
            _lua = new Lua();
            _lua.LoadCLRPackage();
            _lua["print"] = new Action<object[]>(Print);
        }

        protected override void ExecuteFile(string filePath)
        {
            var fileName = FileHelper.GetRelativePath(filePath, Constants.ScriptsFolder);

            Log.Debug($"Starting Lua script {fileName} ...");

            // We are not using _lua.DoFile(..) because it does not support UTF-8 w/ BOM encoding
            // TODO: Fix UTF-8 for require()'d files too, e.g. by adjusting all scripts on start
            //       https://github.com/djkrose/7DTD-ScriptingMod/issues/23
            string script = File.ReadAllText(filePath);

            try
            {
                _lua.DoString(script);
                Log.Debug($"Lua script {fileName} ended.");
            }
            catch (LuaScriptException ex)
            {
                SdtdConsole.Instance.Output($"Lua script error in {fileName}: " + GetShortErrorMessage(ex) + " [details in server log]");

                // LuaScriptException.ToString() does not - against convention - print stack trace or inner exceptions
                Log.Error($"Lua script error in {fileName}: " + (ex.Source ?? "") + ex.ToStringDefault());

                // Dump only for me
                Log.Dump(ex, 2);
            }
        }

        protected override void SetValue(string name, object value)
        {
            _lua[name] = value;
        }

        protected override object GetValue(string name)
        {
            return _lua[name];
        }

        // WARNING: Gets called by base before constructor is called! Must behave static!
        protected override string GetCommentPrefix()
        {
            return "--";
        }

        /// <summary>
        /// Returns type and message of the exception and all inner exceptions, but not the stack trace
        /// </summary>
        private string GetShortErrorMessage(LuaScriptException ex)
        {
            var shortMessage = (ex.Source ?? "") + ex.Message;
            Exception curr = ex;
            while (curr.InnerException != null)
            {
                curr = curr.InnerException;
                shortMessage += " ---> " + curr.GetType().FullName + ": " + curr.Message;
            }
            return shortMessage;
        }

        #region Exposed in Lua

        private void Print(params object[] values)
        {
            if (values == null || values.Length == 0)
                return;
            string output = values.Select(v => v.ToString()).Aggregate((s, s1) => s + s1);
            // TODO: Test and fix the Print output for asynchronous/callbacks/events in Lua
            SdtdConsole.Instance.Output(output);
            Log.Debug("[CONSOLE] " + output);
        }

        #endregion
    }
}
