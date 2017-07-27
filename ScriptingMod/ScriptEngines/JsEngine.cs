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
            FixTimeZoneInfoLocal();
            _jint = new Engine(cfg => cfg.AllowClr());
            _jint.SetValue("console", new Console());
            _jint.SetValue("require", new Action<object>(Require));
            InitValues();
        }

        private void FixTimeZoneInfoLocal()
        {
            // Mono CLR's implementation of System.TimeZoneInfo.Local has a _bug that throws System.TimeZoneNotFoundException
            // on Windows: https://bugzilla.xamarin.com/show_bug.cgi?id=11817
            // This can be circumvented by manually setting the private field "local" to Utc. See source code:
            // https://github.com/mono/mono/blob/master/mcs/class/corlib/System/TimeZoneInfo.cs
            if (Type.GetType("Mono.Runtime") != null)
            {
                try
                {
                    typeof(TimeZoneInfo).GetField("local", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, TimeZoneInfo.Utc);
                }
                catch (Exception ex)
                {
                    Log.Warning("Could not apply workaround for Mono bug in TimeZoneInfo.Local. Jint initialization will probably fail.");
                    Log.Debug(ex.ToString());
                }
            }
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
