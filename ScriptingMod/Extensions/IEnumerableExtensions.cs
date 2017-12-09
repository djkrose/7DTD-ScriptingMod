using System.Collections.Generic;
using System.Linq;

namespace ScriptingMod.Extensions
{
    internal static class IEnumerableExtensions
    {
        public static string Join<T>(this IEnumerable<T> self, string separator)
        {
            return string.Join(separator, self.Select(e => e.ToString()).ToArray());
        }
    }
}