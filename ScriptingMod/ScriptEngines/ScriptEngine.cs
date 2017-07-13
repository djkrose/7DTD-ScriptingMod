using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using ScriptingMod.Extensions;

namespace ScriptingMod.ScriptEngines
{
    internal enum ScriptTypeEnum { LUA, JS }

    internal abstract class ScriptEngine
    {
        private readonly Regex _metadataRegex;

        protected ScriptEngine()
        {
            var metaDataPattern = string.Format(@"
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
                // ReSharper disable once VirtualMemberCallInConstructor
                Regex.Escape(CommentPrefix));

            _metadataRegex = new Regex(metaDataPattern,
                RegexOptions.Compiled |
                RegexOptions.ExplicitCapture |
                RegexOptions.Multiline |
                RegexOptions.IgnorePatternWhitespace);
        }

        // Used in constructor, so derived class MUST NOT use the object; just assume it's static
        protected abstract string CommentPrefix { get; }

        public static ScriptEngine GetInstance(ScriptTypeEnum scriptType)
        {
            switch (scriptType)
            {
                case ScriptTypeEnum.LUA:
                    return LuaEngine.Instance;
                case ScriptTypeEnum.JS:
                    return JsEngine.Instance;
                default:
                    throw new NotImplementedException();
            }
        }

        public static ScriptEngine GetInstance(string fileExtension)
        {
            switch (fileExtension.ToLowerInvariant())
            {
                case ".lua":
                    return LuaEngine.Instance;
                case ".js":
                    return JsEngine.Instance;
                default:
                    throw new NotImplementedException();
            }
        }

        public abstract void ExecuteFile(string filePath);

        public abstract void SetValue(string name, object value);

        public virtual Dictionary<string, string> LoadMetadata(string filePath)
        {
            var script = File.ReadAllText(filePath);

            var metadata = new Dictionary<string, string>();
            var match = _metadataRegex.Match(script);
            while (match.Success)
            {
                var key = match.Groups["key"].Value;
                var value = match.Value;

                // Remove comment markers from all value lines
                value = Regex.Replace(value, "^--", "", RegexOptions.Multiline);

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
    }
}