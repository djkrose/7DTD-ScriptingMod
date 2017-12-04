using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LitJson;

namespace ScriptingMod.Extensions
{
    // source: https://github.com/Mervill/UnityLitJson/blob/master/Source/Extensions.cs
    internal static class LintJsonExtensions
    {
        public static void WriteProperty(this JsonWriter w, string name, long value)
        {
            w.WritePropertyName(name);
            w.Write(value);
        }

        public static void WriteProperty(this JsonWriter w, string name, string value)
        {
            w.WritePropertyName(name);
            w.Write(value);
        }

        public static void WriteProperty(this JsonWriter w, string name, bool value)
        {
            w.WritePropertyName(name);
            w.Write(value);
        }

        public static void WriteProperty(this JsonWriter w, string name, double value)
        {
            w.WritePropertyName(name);
            w.Write(value);
        }
    }
}
