using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Jint;

namespace ScriptingMod.ScriptEngines
{
    internal class JsEngine : ScriptEngine
    {
        private static JsEngine _instance;
        public static JsEngine Instance => _instance ?? (_instance = new JsEngine());

        private Engine _jint;
        protected override string CommentPrefix => "//";

        private JsEngine()
        {
            InitJs();
        }

        private void InitJs()
        {
            _jint = new Engine(cfg => cfg.AllowClr());
            _jint.SetValue("console", new Console());
            _jint.SetValue("require", new Action<object>(Require));
            InitValues();
        }

        public override void ExecuteFile(string filePath)
        {
            var fileName = FileHelper.GetRelativePath(filePath, Constants.ScriptsFolder);

            Log.Debug($"Starting JS script {fileName} ...");

            var script = File.ReadAllText(filePath);

            _jint.Execute(script);
            Log.Debug($"JS script {fileName} ended.");
        }

        public override void SetValue(string name, object value)
        {
            _jint.SetValue(name, value);
        }

        public void Reset()
        {
            _jint = null;
            InitJs();
        }

        #region Methods exposed in JS

        private class Console
        {
            public void debug(object v)
            {
                Log.Debug(v.ToString());
            }
            public void info(object v)
            {
                Log.Out(v.ToString());
            }
            public void warn(object v)
            {
                Log.Warning(v.ToString());
            }
            public void error(object v)
            {
                Log.Error(v.ToString());
            }
            public void log(object v)
            {
                SdtdConsole.Instance.Output(v.ToString());
                Log.Debug("[CONSOLE] " + v.ToString());
            }
        }

        private void Require(object filename)
        {
            try
            {
                ExecuteFile(filename.ToString());
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
            }
        }

        #endregion

    }
}
