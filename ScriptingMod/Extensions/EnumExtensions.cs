using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ScriptingMod.Extensions
{
    internal static class EnumExtensions
    {
        /// <summary>
        /// Source: https://stackoverflow.com/a/18879775/785111
        /// </summary>
        public static bool HasFlag(this Enum en, Enum flag)
        {
            // Check whether the flag was given
            if (flag == null)
            {
                throw new ArgumentNullException(nameof(flag));
            }

            // Compare the types of both enumerations
            if (en.GetType() != flag.GetType())
            {
                throw new ArgumentException($@"The type of the given flag is not of type {en.GetType()}", nameof(flag));
            }

            // Get the type code of the enumeration
            var typeCode = en.GetTypeCode();

            // If the underlying type of the flag is signed
            if (typeCode == TypeCode.SByte || typeCode == TypeCode.Int16 || typeCode == TypeCode.Int32 || typeCode == TypeCode.Int64)
            {
                return (Convert.ToInt64(en) & Convert.ToInt64(flag)) != 0;
            }

            // If the underlying type of the flag is unsigned
            if (typeCode == TypeCode.Byte || typeCode == TypeCode.UInt16 || typeCode == TypeCode.UInt32 || typeCode == TypeCode.UInt64)
            {
                return (Convert.ToUInt64(en) & Convert.ToUInt64(flag)) != 0;
            }

            // Unsupported flag type
            throw new Exception($"The comparison of the type {en.GetType().Name} is not implemented.");
        }

        /// <summary>
        /// Gets an attribute on an enum field value
        /// Source: https://stackoverflow.com/a/9276348/785111
        /// </summary>
        /// <typeparam name="T">The type of the attribute you want to retrieve</typeparam>
        /// <param name="enumVal">The enum value</param>
        /// <returns>The attribute of type T that exists on the enum value</returns>
        /// <example>string desc = myEnumVariable.GetAttributeOfType&lt;DescriptionAttribute&gt;().Description;</example>
        public static T GetAttributeOfType<T>(this Enum enumVal) where T : System.Attribute
        {
            var type = enumVal.GetType();
            var memInfo = type.GetMember(enumVal.ToString());
            var attributes = memInfo[0].GetCustomAttributes(typeof(T), false);
            return (attributes.Length > 0) ? (T)attributes[0] : null;
        }
    }
}
