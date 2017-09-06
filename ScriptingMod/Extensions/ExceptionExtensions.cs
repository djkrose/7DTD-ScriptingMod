using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace ScriptingMod.Extensions
{
    internal static class ExceptionExtensions
    {
        /// <summary>
        /// Returns the string representation of the entire exception incl. inner exceptions and stack traces just like Exception.ToString(),
        /// even if ToString() is overwritten in the derived exception with something else non-standard.
        /// </summary>
        public static string ToStringDefault(this Exception ex)
        {
            var typeName = ex.GetType().FullName;

            string s = (string.IsNullOrEmpty(ex.Message) ? typeName : typeName + ": " + ex.Message);

            if (ex.InnerException != null)
                s += " ---> " + ex.InnerException + Environment.NewLine + "   --- End of inner exception stack trace ---";

            if (ex.StackTrace != null)
                s += Environment.NewLine + ex.StackTrace;

            return s;
        }
    }
}
