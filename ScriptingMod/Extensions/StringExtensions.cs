using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ScriptingMod.Extensions
{
    internal static class StringExtensions
    {

        /// <summary>
        /// Tries to convert the string to int; returns null on parse error.
        /// Usage: int foo = text.ToInt() ?? 42;
        /// </summary>
        public static int? ToInt(this string value)
        {
            return int.TryParse(value, out int i) ? (int?) i : null;
        }

        /// <summary>
        /// Tries to convert the string to long; returns null on parse error.
        /// Usage: long foo = text.ToLong() ?? 42;
        /// </summary>
        public static long? ToLong(this string value)
        {
            return long.TryParse(value, out long i) ? (long?)i : null;
        }

        /// <summary>
        /// Tries to convert the string to float; returns null on parse error.
        /// Usage: float foo = text.ToFloat() ?? 42f;
        /// </summary>
        public static float? ToFloat(this string value)
        {
            return float.TryParse(value, out float i) ? (float?)i : null;
        }

        /// <summary>
        /// Tries to convert the string to double; returns null on parse error.
        /// Usage: double foo = text.ToDouble() ?? 42d;
        /// </summary>
        public static double? ToDouble(this string value)
        {
            return double.TryParse(value, out double i) ? (double?)i : null;
        }

        /// <summary>
        /// Tries to convert the string to decimal; returns null on parse error.
        /// Usage: decimal foo = text.ToDecimal() ?? 42m;
        /// </summary>
        public static decimal? ToDecimal(this string value)
        {
            return decimal.TryParse(value, out decimal i) ? (decimal?)i : null;
        }

        /// <summary>
        /// Removes the indentation of the first non-empty line from all the lines,
        /// effectively keeping relative indentation but removing common indentation.
        /// Also empty lines at start and end are removed.
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static string Unindent(this string source)
        {
            IEnumerable<string> lines = source.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            lines = TrimEmptyLines(lines);
            string indent = new string((lines.FirstOrDefault() ?? "").TakeWhile(char.IsWhiteSpace).ToArray());
            lines = lines.Select(l => l.StartsWith(indent) ? l.Substring(indent.Length) : l);
            return string.Join(Environment.NewLine, lines.ToArray());
        }

        private static IEnumerable<string> TrimEmptyLines(IEnumerable<string> source)
        {
            string[] sourceArr = source.ToArray();

            // Remove empty lines one by one from beginning and end
            int start = 0, end = sourceArr.Length - 1;
            while (start < end && sourceArr[start].All(char.IsWhiteSpace)) start++;
            while (end >= start && sourceArr[end].All(char.IsWhiteSpace)) end--;
            return sourceArr.Skip(start).Take(end - start + 1);
        }
    }
}
