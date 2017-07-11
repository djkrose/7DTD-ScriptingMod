using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ScriptingMod.ScriptEngines
{
    internal enum ScriptTypeEnum { LUA, JS }

    internal abstract class ScriptEngine
    {
        public static ScriptEngine GetInstance(ScriptTypeEnum scriptType)
        {
            switch (scriptType)
            {
                case ScriptTypeEnum.LUA:
                    return LuaEngine.Instance;
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
                default:
                    throw new NotImplementedException();
            }
        }

        public abstract ScriptTypeEnum ScriptType { get; }

        public abstract void ExecuteInline(string script);

        public abstract void ExecuteFile(string filePath);

        public abstract void SetValue(string name, object value);
    }
}
