using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using ScriptingMod.Extensions;
using ScriptingMod.Tools;

namespace ScriptingMod.ScriptEngines
{

    internal abstract class ScriptEngine
    {

        public static ScriptEngine GetInstance(string fileExtension)
        {
            switch (fileExtension.ToLowerInvariant())
            {
                case LuaEngine.FileExtension:
                    return LuaEngine.Instance;
                case JsEngine.FileExtension:
                    return JsEngine.Instance;
                default:
                    throw new ArgumentException("Unsupported fileExtension: " + fileExtension, nameof(fileExtension));
            }
        }

        public void ExecuteEvent(string filePath, [CanBeNull] object eventArgs)
        {
            ResetEngine();
            InitCommonValues();
            SetValue("event", eventArgs);

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
                var fileRelativePath = FileTools.GetRelativePath(filePath, Constants.ScriptsFolder);
                Log.Error($"Script {fileRelativePath} failed: " + ex);
            }

            Directory.SetCurrentDirectory(oldDirectory);
        }

        public void ExecuteCommand(string filePath, List<string> parameters, CommandSenderInfo senderInfo)
        {
            ResetEngine();
            InitCommonValues();
            SetValue("params", parameters.ToArray());
            SetValue("sender", senderInfo);

            EntityPlayer player = GameManager.Instance.World?.Players.dict.GetValue(senderInfo.RemoteClientInfo?.entityId ?? -1);
            SetValue("player", player);

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
                var fileRelativePath = FileTools.GetRelativePath(filePath, Constants.ScriptsFolder);
                SdtdConsole.Instance.Output($"Script {fileRelativePath} failed: " + ex.GetType().FullName + ": " + ex.Message + " [details in server log]");
                Log.Error($"Script {fileRelativePath} failed: " + ex);
            }

            Directory.SetCurrentDirectory(oldDirectory);
        }

        /// <summary>
        /// Extracts metadata from the first comment block of a script. Metadata must be formatted as
        /// a series of @key value lines, where value can span multiple lines. See example files.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public Dictionary<string, string> LoadMetadata(string filePath)
        {
            var commentPrefix = GetCommentPrefix();
            var metadata = new Dictionary<string, string>();
            var lines = File.ReadAllLines(filePath);

            bool contentStarted = false;
            string currentTag = null;

            // ReSharper disable once ForCanBeConvertedToForeach
            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index].Trim().Replace("\t", "    ");

                // Ignore empty lines before first comment block
                if (!contentStarted)
                {
                    if (line == string.Empty)
                        continue;
                    contentStarted = true;
                }

                // Stop parsing when comment block ends
                if (!line.StartsWith(commentPrefix))
                    break;

                // Remove comment prefixes
                line = line.Substring(commentPrefix.Length);

                // Extract tag if any
                var match = Regex.Match(line, "^ *@([a-zA-Z0-9_-]+) ");
                if (match.Success)
                {
                    currentTag = match.Groups[1].Value;
                    if (metadata.ContainsKey(currentTag))
                    {
                        var fileRelativePath = FileTools.GetRelativePath(filePath, Constants.ScriptsFolder);
                        Log.Warning($"Tag @{currentTag} appears more han once in {fileRelativePath}. Only the last occurence is considered.");
                    }
                    metadata[currentTag] = "";
                }

                // Ignore comment lines before the first tag
                if (currentTag == null)
                    continue;

                // Replace @key in line with equal amount of spaces to keep indentation
                line = line.ReplaceFirst("@" + currentTag, new string(' ', currentTag.Length + 1));

                // Add line as value for the tag; dealing with whitespace later...
                metadata[currentTag] += line + Environment.NewLine;
            }

            // Remove space indentation relative to first line of values; also removes trailing newline
            foreach (var key in metadata.Keys.ToList())
                metadata[key] = metadata[key].Unindent();

            return metadata;
        }

        protected abstract void ResetEngine();

        protected abstract void ExecuteFile(string filePath);

        protected abstract void SetValue(string name, object value);

        protected abstract object GetValue(string name);

        protected abstract string GetCommentPrefix();

        private void InitCommonValues()
        {
            SetValue("dump", new Action<object, int?>(Dump));

        }

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