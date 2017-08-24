using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ScriptingMod.Extensions
{
    internal static class EnumExtensionscs
    {
        /// <summary>
        /// Source: https://stackoverflow.com/a/18879775/785111
        /// </summary>
        public static bool HasFlag(this Enum e, Enum flag)
        {
            // Check whether the flag was given
            if (flag == null)
            {
                throw new ArgumentNullException(nameof(flag));
            }

            // Compare the types of both enumerations
            if (e.GetType() != flag.GetType())
            {
                throw new ArgumentException($@"The type of the given flag is not of type {e.GetType()}", nameof(flag));
            }

            // Get the type code of the enumeration
            var typeCode = e.GetTypeCode();

            // If the underlying type of the flag is signed
            if (typeCode == TypeCode.SByte || typeCode == TypeCode.Int16 || typeCode == TypeCode.Int32 || typeCode == TypeCode.Int64)
            {
                return (Convert.ToInt64(e) & Convert.ToInt64(flag)) != 0;
            }

            // If the underlying type of the flag is unsigned
            if (typeCode == TypeCode.Byte || typeCode == TypeCode.UInt16 || typeCode == TypeCode.UInt32 || typeCode == TypeCode.UInt64)
            {
                return (Convert.ToUInt64(e) & Convert.ToUInt64(flag)) != 0;
            }

            // Unsupported flag type
            throw new Exception($"The comparison of the type {e.GetType().Name} is not implemented.");
        }
    }
}
