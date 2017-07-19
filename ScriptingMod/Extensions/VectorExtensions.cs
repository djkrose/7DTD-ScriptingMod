using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ScriptingMod.Exceptions;
using UnityEngine;

namespace ScriptingMod.Extensions
{
    internal static class VectorExtensions
    {

        /// <summary>
        /// Converts the float vector into an integer vector.
        /// </summary>
        /// <remarks>
        /// This method is much better than "new Vector3i(vector3)" because it correctly uses Math.Floor,
        /// while the other one just casts to (int) which cuts the decimal numbers. Cutting the decimal
        /// numbers for negative numbers actually rounds UP, which returns the wrong value (one too high).
        /// 
        /// Compare with the "focused block" view when hitting F3, which shows the position correctly.
        /// </remarks>
        /// <example>
        /// -123.2, 17.9, -198.8 => -124, 17, -199
        /// </example>
        public static Vector3i ToVector3i(this Vector3 v)
        {
            return new Vector3i((int)Math.Floor(v.x), (int)Math.Floor(v.y), (int)Math.Floor(v.z));
        }
    }

    /// <summary>
    /// Static "extensions" to the Vector3i class
    /// </summary>
    internal static class Vector3iEx
    {
        public static Vector3i Parse(string x, string y, string z)
        {
            try
            {
                return new Vector3i(int.Parse(x), int.Parse(y), int.Parse(z));
            }
            catch (Exception ex) when (ex is FormatException || ex is OverflowException)
            {
                throw new FriendlyMessageException("At least one of the given coordinates is not a valid integer.", ex);
            }
        }
    }
}
