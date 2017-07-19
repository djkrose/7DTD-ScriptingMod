using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;

namespace ScriptingMod.Extensions
{
    internal static class IDictionaryExtensions
    {
        /// <summary>
        /// Tries to find the value with the given key in the dictionary and returns it.
        /// If it cannot be found, the default value is returned instead. If no default
        /// value is given, the type's default is returned, e.g. null for objects, 0 for ints, etc.
        /// </summary>
        /// <returns>The value for the key, or the default value</returns>
        [CanBeNull]
        public static TV GetValue<TK, TV>(this IDictionary<TK, TV> dict, TK key, TV defaultValue = default(TV))
        {
            TV value;
            return dict.TryGetValue(key, out value) ? value : defaultValue;
        }
    }
}
