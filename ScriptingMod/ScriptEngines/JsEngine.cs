﻿using System;
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
using ScriptingMod.Tools;

namespace ScriptingMod.ScriptEngines
{
    internal sealed class JsEngine : ScriptEngine
    {
        public const string FileExtension = ".js";
        private static JsEngine _instance;
        public static JsEngine Instance => _instance ?? (_instance = new JsEngine());

        private Engine _jint;

        private JsEngine()
        {
            // intentionally not initializing _jint because that's done before every command execution
        }

        protected override void ResetEngine()
        {
            // Only with "limited recursion" Jint tracks and prints JS callstacks on errors
            _jint = new Engine(cfg => cfg.AllowClr().LimitRecursion(int.MaxValue));
            // TODO: Fix problem where dump(glo0bal) or dump(this) hangs the server
            //       https://github.com/djkrose/7DTD-ScriptingMod/issues/22
            _jint.SetValue("global", _jint.Global);
            _jint.SetValue("console", new JsConsole());
            _jint.SetValue("require", new Func<object, bool?, object>(Require));
            _jint.SetValue("importAssembly", new Action<string>(ImportAssembly));
        }

        protected override void ExecuteFile(string filePath)
        {
            var fileRelativePath = FileTools.GetRelativePath(filePath, Constants.ScriptsFolder);

            Log.Debug($"Starting JavaScript {fileRelativePath} ...");

            var script = File.ReadAllText(filePath);

            try
            {
                _jint.Execute(script, new ParserOptions { Source = fileRelativePath });
            }
            catch (JavaScriptException ex)
            {
                // Send short message to console
                SdtdConsole.Instance.Output($"JavaScript error in {fileRelativePath} line {ex.LineNumber}: {ex.Error} [details in server log]");

                // Log full javascript Error object with JS callstack
                Log.Error($"JavaScript error in {fileRelativePath} line {ex.LineNumber} column {ex.Column}: {ex.Error}" +
                          (string.IsNullOrEmpty(ex.CallStack) ? "" : Environment.NewLine + ex.CallStack.Indent(1).TrimEnd()));

                // JavaScriptException.ToString() does not - against convention - print stack trace or inner exceptions
                Log.Error("Underlying .Net exception: " + ex.ToStringDefault());
            }

            Log.Debug($"JavaScript {fileRelativePath} ended.");
        }

        protected override void SetValue(string name, object value)
        {
            _jint.SetValue(name, value);
        }

        protected override object GetValue(string name)
        {
            return _jint.GetValue(name).ToObject();
        }

        // WARNING: Gets called by base before constructor is called! Must behave static!
        protected override string GetCommentPrefix()
        {
            return "//";
        }

        #region Exposed in JS

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

        private object Require(object filename, bool? passthrough)
        {
            if (passthrough == null)
                passthrough = false;

            string filePath = Constants.ScriptsFolder + Path.DirectorySeparatorChar + filename.ToString();
            if (!filePath.EndsWith(".js"))
            {
                filePath += ".js";
            }

            JsEngine tmpEngine = new JsEngine();
            tmpEngine.ResetEngine();

            //Grab the dump function.
            tmpEngine._jint.SetValue("dump", new Action<object, int?>(Dump));


            //We have to "steal" everything below from the parent script to insert them into the child script.
            //Use the _jint SetValue/GetValue functions directly.
            if (passthrough.Value)
            {
                //Grab the event data if it exists.
                tmpEngine._jint.SetValue("eventType", _jint.GetValue("eventType"));
                tmpEngine._jint.SetValue("event", _jint.GetValue("event"));

                //Grab the command data if it exists.
                tmpEngine._jint.SetValue("params", _jint.GetValue("params"));
                tmpEngine._jint.SetValue("sender", _jint.GetValue("sender"));
                tmpEngine._jint.SetValue("player", _jint.GetValue("player"));
            }

            //Create the module.exports variable. Similar to nodejs.
            tmpEngine._jint.Execute("module={};module.exports=null");

            object tmpValue = null;

            try
            {
                tmpEngine.ExecuteFile(filePath);
                tmpValue = tmpEngine._jint.GetValue(tmpEngine._jint.GetValue("module"), "exports").ToObject();
            }
            catch (Exception ex)
            {
                throw new JavaScriptException(_jint.ReferenceError, ex.Message, ex);
            }

            return tmpValue;
        }

        #endregion

    }
}
