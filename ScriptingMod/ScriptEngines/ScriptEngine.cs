using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ScriptingMod.Extensions;

namespace ScriptingMod.ScriptEngines
{
    internal abstract class ScriptEngine
    {
        private Regex metaDataRegex;

        protected ScriptEngine()
        {
            // ReSharper disable once VirtualMemberCallInConstructor
            var commentPrefix = GetCommentPrefix();
            var metaDataPattern = string.Format(@"(?xmn)
                # Lookbehind from beginning of file (\A)
                (?<=\A
                    # File can start with some spaces and newlines
                    \s*
                    # Then all lines must start with '--'
                    (^{0}.*(\r\n|\r|\n))*
                )
                # Matching line must start like '-- @key ' ...
                ^{0}[\ \t]*@(?<key>[a-zA-Z0-9_-]+)[\ \t]+
                # ... followed by a value, which can span multiple lines ([\s\S])
                [\s\S]+?
                # Value ends before (look-ahead) either ...
                (?=(
                    # ... a new key->value line starts, or
                    ^{0}[\ \t]*@|
                    # ... the next line starts with something else than '--', or
                    ^(?!{0})|
                    # ... the file ends (so it only has metadata).
                    \Z
                ))",
                Regex.Escape(commentPrefix));
            metaDataRegex = new Regex(metaDataPattern);
        }

        public static ScriptEngine GetInstance(string fileExtension)
        {
            switch (fileExtension.ToLowerInvariant())
            {
                case LuaEngine.FileExtension:
                    return LuaEngine.Instance;
                case JsEngine.FileExtension:
                    return JsEngine.Instance;
                default:
                    throw new NotImplementedException();
            }
        }

        public void ExecuteCommand(string filePath, List<string> parameters, CommandSenderInfo senderInfo)
        {
            ResetEngine();
            SetValue("dump", new Action<object, int?>(Dump));
            SetValue("params", parameters.ToArray());
            SetValue("sender", senderInfo);

            World world = GameManager.Instance.World;
            EntityPlayer player = world?.Players.dict.GetValue(senderInfo.RemoteClientInfo?.entityId ?? -1);
            SetValue("player", world?.Players.dict.GetValue(senderInfo.RemoteClientInfo?.entityId ?? -1));

            var oldDirectory = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(Path.GetDirectoryName(filePath) ?? Path.PathSeparator.ToString());

            try
            {
                ExecuteFile(filePath);
            }
            // LuaScriptException is already handled in LuaEngine
            // JavaScriptException is already handled in JsEngine
            catch (Exception ex)
            {
                var fileName = FileHelper.GetRelativePath(filePath, Constants.ScriptsFolder);
                SdtdConsole.Instance.Output($"Script {fileName} failed: " + ex.GetType().FullName + ": " + ex.Message + " [details in server log]");
                Log.Error($"Script {fileName} failed: " + ex);
            }

            Directory.SetCurrentDirectory(oldDirectory);
        }

        public Dictionary<string, string> LoadMetadata(string filePath)
        {
            var commentPrefix = GetCommentPrefix();
            var script = File.ReadAllText(filePath);
            var metadata = new Dictionary<string, string>();

            var match = metaDataRegex.Match(script);
            while (match.Success)
            {
                var key = match.Groups["key"].Value;
                var value = match.Value;

                // Remove comment markers from all value lines
                value = Regex.Replace(value, "^" + Regex.Escape(commentPrefix), "", RegexOptions.Multiline);

                // Replace @key in match with equal amount of spaces to keep indentation
                value = new Regex("@" + Regex.Escape(key)).Replace(value, new string(' ', key.Length + 1), 1);

                // Replace tabs with 4 spaces to make the following easier
                value = value.Replace("\t", "    ");

                // Remove common indentation to all lines but keep indentation relative to first line
                value = value.Unindent();

                metadata.Add(key, value);

                match = match.NextMatch();
            }

            return metadata;
        }

        protected abstract void ResetEngine();

        protected abstract void ExecuteFile(string filePath);

        protected abstract void SetValue(string name, object value);

        protected abstract object GetValue(string name);

        protected abstract string GetCommentPrefix();

        #region Exposed in scripts

        private void Dump(object obj, int? depth)
        {
            // TODO: Test and fix the SdtdConsole output for asynchronous/callbacks

            // We cannot use optional parameter "int depth = 1" because that doesn't work with Mono's dynamic invocation
            if (depth == null)
                depth = 1;

            var output = Dumper.Dump(obj, depth.Value);

            if (output.Length > 1024)
            {
                var truncated = output.Substring(0, 1024);
                SdtdConsole.Instance.Output(truncated + " [...]\r\n[output truncated; full output in log file]");
            }
            else
            {
                SdtdConsole.Instance.Output(output);
            }
            Log.Out(output);
        }

        #endregion
    }
}