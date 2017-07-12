using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ScriptingMod.ScriptEngines
{
    internal class JsEngine : ScriptEngine
    {
        protected override string CommentPrefix => "//";

        private static JsEngine _instance;
        public static JsEngine Instance => _instance ?? (_instance = new JsEngine());

        public override void ExecuteInline(string script)
        {
            throw new NotImplementedException();
        }

        public override void ExecuteFile(string filePath)
        {
            throw new NotImplementedException();
        }

        public override void SetValue(string name, object value)
        {
            throw new NotImplementedException();
        }
    }
}
