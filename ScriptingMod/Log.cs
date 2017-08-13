using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace ScriptingMod
{
    /// <summary>
    /// Extends the global::Log class from LogLibrary.dll with a Debug log type that only shows output in debug mode
    /// </summary>
    internal class Log : global::Log
    {
        private const string PREFIX = "[SCRIPTING MOD] ";
        private const string DEBUG_PREFIX = "[DEBUG] ";

#if DEBUG
        public const bool IsDebug = true;
#else
        public const bool IsDebug = false;
#endif

        [Conditional("DEBUG")]
        public static void Dump(object obj)
        {
            Debug(Dumper.Dump(obj));
        }

        [Conditional("DEBUG")]
        public static void Dump(object obj, int depth)
        {
            Debug(Dumper.Dump(obj, depth));
        }

        [Conditional("DEBUG")]
        public static void Dump(object obj, DumperOptions options)
        {
            Debug(Dumper.Dump(obj, options));
        }

        [Conditional("DEBUG")]
        public static void Dump(object obj, string name, DumperOptions options)
        {
            Debug(Dumper.Dump(obj, name, options));
        }

        [Conditional("DEBUG")]
        public static void Debug(string _format, params object[] _values)
        {
            global::Log.Out(PREFIX + DEBUG_PREFIX + _format, _values);
        }

        [Conditional("DEBUG")]
        public static void Debug(string _s)
        {
            global::Log.Out(PREFIX + DEBUG_PREFIX + _s);
        }

        public new static void Out(string _format, params object[] _values)
        {
            global::Log.Out(PREFIX + _format, _values);
        }

        public new static void Out(string _s)
        {
            global::Log.Out(PREFIX + _s);
        }

        public new static void Warning(string _format, params object[] _values)
        {
            global::Log.Warning(PREFIX + _format, _values);
        }

        public new static void Warning(string _s)
        {
            global::Log.Warning(PREFIX + _s);
        }

        public new static void Error(string _format, params object[] _values)
        {
            global::Log.Error(PREFIX + _format, _values);
        }

        public new static void Error(string _s)
        {
            global::Log.Error(PREFIX + _s);
        }

        public new static void Error(string _s, UnityEngine.Object _context)
        {
            global::Log.Error(PREFIX + _s, _context);
        }

        public new static void Exception(Exception _e)
        {
            global::Log.Error(PREFIX + _e);
        }

    }
}
