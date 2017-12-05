using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ScriptingMod.Tools
{
    internal static class EnumHelper
    {

        public static bool TryParse<TEnum>(string value, out TEnum result, bool ignoreCase = false) where TEnum : struct
        {
            try
            {
                result = (TEnum)Enum.Parse(typeof(TEnum), value, ignoreCase);
            }
            catch
            {
                result = default(TEnum);
                return false;
            }

            return true;
        }
    }
}
