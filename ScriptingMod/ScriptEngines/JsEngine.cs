using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using JetBrains.Annotations;
using Jint;
using Jint.Parser;
using Jint.Runtime;
using Jint.Runtime.Interop;
using ScriptingMod.Extensions;

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
            // Only with "limited recursion" Jint tracks and prints JS callstacks on errors
            _jint = new Engine(cfg => cfg.AllowClr().LimitRecursion(int.MaxValue));
            _jint.SetValue("console", new Console());
            _jint.SetValue("require", new Action<object>(Require));
            _jint.SetValue("importAssembly", new Action<string>(ImportAssembly));
            InitValues();
        }

        public override void ExecuteFile(string filePath)
        {
            var fileName = FileHelper.GetRelativePath(filePath, Constants.ScriptsFolder);

            Log.Debug($"Starting JavaScript {fileName} ...");

            var script = File.ReadAllText(filePath);

            try
            {
                _jint.Execute(script, new ParserOptions { Source = fileName });
            }
            catch (JavaScriptException ex)
            {
                // Send short message to console
                SdtdConsole.Instance.Output($"JavaScript error in {fileName} line {ex.LineNumber}: {ex.Error} [details in server log]");

                // Log full javascript Error object with JS callstack
                Log.Error($"JavaScript error in {fileName} line {ex.LineNumber} column {ex.Column}: {ex.Error}" +
                          (string.IsNullOrEmpty(ex.CallStack) ? "" : Environment.NewLine + ex.CallStack.Indent(1).TrimEnd()));

                // JavaScriptException.ToString() does not - against convention - print stack trace or inner exceptions
                Log.Error("Underlying .Net exception: " + ex.ToStringDefault());
            }

            Log.Debug($"JavaScript {fileName} ended.");
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

        #region Exposed in JS

        [UsedImplicitly(ImplicitUseTargetFlags.Members)]
        // TODO: Could be made a bit more consistent; introduce something else to log?
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
                // TODO: Test and fix the SdtdConsole output for asynchronous/callbacks in JavaScript or Lua
                SdtdConsole.Instance.Output(v.ToString());
                Log.Debug("[CONSOLE] " + v);
            }
        }

        private void ImportAssembly(string assemblyName)
        {
            Assembly assembly;
            try
            {
                assembly = Assembly.Load(assemblyName);
                _jint.AddLookupAssembly(assembly);
            }
            catch (Exception ex)
            {
                throw new JavaScriptException(_jint.ReferenceError, ex.Message, ex)
                    .SetCallstack(_jint, _jint.GetLastSyntaxNode().Location);
            }

            foreach (Type type in assembly.GetExportedTypes())
            {
                if (type.Namespace == null)
                {
                    // Put types without namespace directly in the global scope
                    _jint.Global.Put(type.FullName, TypeReference.CreateTypeReference(_jint, type), true);
                }
                else
                {
                    // For types with namespace add the first namespace part as NamespaceReference to global,
                    // which will then be able to find all subsequent types in all sub namespaces from the lookup assemblies.
                    int firstDot = type.Namespace.IndexOf(".", StringComparison.Ordinal);
                    string firstNamespace = firstDot == -1 ? type.Namespace : type.Namespace.Substring(0, firstDot);
                    if (_jint.Global.Get(firstNamespace).As<NamespaceReference>() == null)
                        _jint.Global.Put(firstNamespace, new NamespaceReference(_jint, firstNamespace), true);
                }
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
                // TODO: Throw JavaScriptException when file could not be loaded...
                Log.Exception(ex);
            }
        }

        #endregion

    }
}
