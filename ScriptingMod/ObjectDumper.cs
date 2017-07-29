/*
 * Copyright (c) 2010-2011, Lasse V. Karlsen
 * All rights reserved. 
 * 
 * Redistribution and use in source and binary forms, with or without modification, 
 * are permitted provided that the following conditions are met: 
 * 
 * * Redistributions of source code must retain the above copyright notice, this list of 
 *   conditions and the following disclaimer. 
 * 
 * * Redistributions in binary form must reproduce the above copyright notice, this list 
 *   of conditions and the following disclaimer in the documentation and/or other materials 
 *   provided with the distribution. 
 * 
 * * Neither the name of Lasse V. Karlsen nor the names of its contributors may be used 
 *   to endorse or promote products derived from this software without specific prior 
 *   written permission. 
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY 
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES 
 * OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT 
 * SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, 
 * INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED 
 * TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR 
 * BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN 
 * CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN 
 * ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF 
 * SUCH DAMAGE. 
 * 
 * New BSD License (BSD)
 * http://en.wikipedia.org/wiki/BSD_licenses#3-clause_license_.28.22New_BSD_License.22_or_.22Modified_BSD_License.22.29 
 */

using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Remoting;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;

namespace ScriptingMod
{
    /// <summary>
    /// Class is based on ObjectDumper 1.0.0.12 but adjusted for ScriptingMod by djkrose.
    /// See: https://www.nuget.org/packages/ObjectDumper/
    /// </summary>
    public static class ObjectDumper
    {
        private const int INDENTATION_SPACE = 2;

        /// <summary>
        /// Dumps the contents of the specified value and returns the dumped contents as a string.
        /// </summary>
        /// <param name="value">The object to dump</param>
        /// <param name="name">Optional name to give to the object in the dump; if omitted the name is just "object"</param>
        /// <returns>The dumped contents of the object as multi-line string</returns>
        /// <exception cref="ArgumentNullException">name is null or empty.</exception>
        public static string Dump(object value, string name = null)
        {
            return Dump(value, name, ObjectDumperOptions.Default);
        }

        /// <summary>
        /// Dumps the contents of the specified value and returns the dumped contents as a string.
        /// </summary>
        /// <param name="value">The object to dump</param>
        /// <param name="maxDepth">The maximum levels to go down the object hierarchy; anything deeper will not be dumped</param>
        /// <returns>The dumped contents of the object as multi-line string</returns>
        public static string Dump(object value, int maxDepth)
        {
            return Dump(value, null, new ObjectDumperOptions() { MaxDepth = maxDepth });
        }

        /// <summary>
        /// Dumps the contents of the specified value and returns the dumped contents as a string.
        /// </summary>
        /// <param name="value">The object to dump</param>
        /// <param name="name">Name to give to the object in the dump; if null the name is just "object"</param>
        /// <param name="maxDepth">The maximum levels to go down the object hierarchy; anything deeper will not be dumped</param>
        /// <returns>The dumped contents of the object as multi-line string</returns>
        public static string Dump(object value, string name, int maxDepth)
        {
            return Dump(value, name, new ObjectDumperOptions() { MaxDepth = maxDepth });
        }

        /// <summary>
        /// Dumps the contents of the specified value and returns the dumped contents as a string
        /// </summary>
        /// <param name="value">The object to dump</param>
        /// <param name="name">Name to give to the object in the dump; if null the name is just "object"</param>
        /// <param name="options">A DumpOptions object that defines options for what the dump contains</param>
        /// <returns>The dumped contents of the object as multi-line string</returns>
        public static string Dump(object value, string name, ObjectDumperOptions options)
        {
            using (var writer = new StringWriter(CultureInfo.InvariantCulture))
            {
                Dump(value, name, writer, options);
                return writer.ToString();
            }
        }

        /// <summary>
        /// Dumps the specified value to the given TextWriter using the specified name
        /// </summary>
        /// <param name="value">The value to dump to the writer</param>
        /// <param name="name">The name of the value being dumped</param>
        /// <param name="writer">The TextWriter to dump the value to</param>
        /// <exception cref="ArgumentNullException">name is null or empty, or writer is null</exception>
        public static void Dump(object value, string name, TextWriter writer)
        {
            Dump(value, name, writer, ObjectDumperOptions.Default);
        }

        /// <summary>
        /// Dumps the specified value to the given TextWriter using the specified name
        /// </summary>
        /// <param name="value">The value to dump to the writer</param>
        /// <param name="name">The name of the value being dumped</param>
        /// <param name="writer">The TextWriter to dump the value to</param>
        /// <param name="options">A DumpOptions object that defines options for what the dump contains</param>
        public static void Dump(object value, string name, TextWriter writer, ObjectDumperOptions options)
        {
            if (name == null)
                name = "object";
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            var idGenerator = new ObjectIDGenerator();
            InternalDump(0, name, value, null, null, writer, idGenerator, true, options);
        }

        [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
        private static void InternalDump(int indentationLevel, string name, object value, MemberInfo memberInfo, int? index,
            TextWriter writer, ObjectIDGenerator idGenerator, bool recursiveDump, ObjectDumperOptions options)
        {
            var indentation = new string(' ', indentationLevel * INDENTATION_SPACE);
            var indentation2 = new string(' ', (indentationLevel + 1) * INDENTATION_SPACE);

            string accessModifiers = GetAccessModifiers(memberInfo);
            string declaredTypeName = ImproveTypeName(GetDeclaredTypeName(memberInfo));
            string propertyMethods = PropertyMethods(memberInfo);

            if (value == null)
            {
                writer.WriteLine($"{indentation}{accessModifiers}{declaredTypeName}{name}{propertyMethods} = <null>");
                return;
            }

            Type type = value.GetType();

            // figure out if this is an object that has already been dumped, or is currently being dumped
            string keyRef = string.Empty;
            string keyPrefix = string.Empty;
            if (!type.IsValueType)
            {
                bool firstTime;
                long key = idGenerator.GetId(value, out firstTime);
                if (!firstTime)
                    keyRef = string.Format(CultureInfo.InvariantCulture, " (see #{0})", key);
                else
                {
                    keyPrefix = string.Format(CultureInfo.InvariantCulture, "#{0}: ", key);
                }
            }

            // work out how a simple dump of the value should be done
            bool isString = value is string;
            string typeName = ImproveTypeName(value.GetType().ToString());
            typeName = (declaredTypeName.TrimEnd() == typeName) ? "" : " [" + typeName + "]";
            string formattedValue = value.ToString();

            var exception = value as Exception;
            if (exception != null)
            {
                formattedValue = exception.GetType().Name + ": " + exception.Message;
            }

            if (value is bool)
                formattedValue = value.ToString().ToLowerInvariant();

            // escape tabs and line feeds
            formattedValue = formattedValue.Replace("\t", "\\t").Replace("\n", "\\n").Replace("\r", "\\r");

            // Give non-numerical values double-quotes
            if (!(value is int || value is float || value is byte || value is decimal || value is double || value is long ||
                  value is short || value is uint || value is ulong || value is ushort || value is bool || value is sbyte))
                formattedValue = "\"" + formattedValue + "\"";

            // chop at 80 characters
            int length = formattedValue.Length;
            if (length > 80)
                formattedValue = formattedValue.Substring(0, 80);
            if (length > 80)
                formattedValue += " (+" + (length - 80) + " chars)";

            var collection = value as ICollection;
            if (collection != null)
                formattedValue += " (Count = " + collection.Count + ")";

            //writer.WriteLine($"{indentation}{keyPrefix}{accessModifiers}{declaredTypeName}{name}{propertyMethods} = {formattedValue}{typeName}{keyRef}");
            // Removed keyref info for now, because it makes output cluttered; let's think about some better visualization later
            writer.WriteLine($"{indentation}{accessModifiers}{declaredTypeName}{name}{propertyMethods} = {formattedValue}{typeName}");

            // Avoid dumping objects we've already dumped, or is already in the process of dumping
            if (keyRef.Length > 0)
                return;

            // don't dump strings, we already got at around 80 characters of those dumped
            if (isString)
                return;

            // don't dump value-types in the System namespace
            if (type.IsValueType && type.FullName == "System." + type.Name)
                return;

            // Avoid certain types that will result in endless recursion
            if (type.FullName == "System.Reflection." + type.Name)
                return;

            // Avoid types that are excluded in options
            if (options.DontDumpTypes.Contains(type))
                return;

            if (value is System.Security.Principal.SecurityIdentifier)
                return;

            if (!recursiveDump)
                return;

            if (indentationLevel / 2 > options.MaxDepth - 1)
                return;

            // Iterate enumerable
            var enumerable = value as IEnumerable;
            if (enumerable != null)
            {
                if (options.IterateEnumerable)
                {
                    int i = 0;
                    foreach (var element in enumerable)
                    {
                        InternalDump(indentationLevel + 2, "[" + i + "]", element, null, i, writer, idGenerator, true, options);
                        i++;
                        if (i >= 100)
                        {
                            writer.WriteLine(new string(' ', (indentationLevel + 2) * INDENTATION_SPACE) + "[ enumeration truncated ]");
                            break;
                        }
                    }
                }

                // Don't show members of enumerable if option is set
                if (!options.WithEnumerableMembers)
                    return;
            }


            var bfPublic = options.WithNonPublic ? BindingFlags.NonPublic : BindingFlags.Default;
            var bfStatic = options.WithStatic ? BindingFlags.Static : BindingFlags.Default;

            PropertyInfo[] properties =
            (from property in type.GetProperties(BindingFlags.Instance | bfStatic | BindingFlags.Public | bfPublic)
                where property.GetIndexParameters().Length == 0
                      && property.CanRead
                select property).ToArray();
            IEnumerable<FieldInfo> fields = !options.WithFields 
                ? Enumerable.Empty<FieldInfo>() 
                : type.GetFields(BindingFlags.Instance | bfStatic | BindingFlags.Public | bfPublic);

            if (!properties.Any() && !fields.Any())
                return;

            writer.WriteLine($"{indentation}{{");

            if (fields.Any())
            {
                //writer.WriteLine($"{indentation2}fields {{");
                foreach (FieldInfo field in fields)
                {
                    try
                    {
                        object fieldValue = field.GetValue(value);
                        InternalDump(indentationLevel + 2, field.Name, fieldValue, field, null, writer, idGenerator, true, options);
                    }
                    catch (TargetInvocationException ex)
                    {
                        InternalDump(indentationLevel + 2, field.Name, ex, field, null, writer, idGenerator, false, options);
                    }
                }
                //writer.WriteLine($"{indentation2}}}");
            }

            if (properties.Any())
            {
                //writer.WriteLine($"{indentation2}properties {{");
                foreach (PropertyInfo property in properties)
                {
                    try
                    {
                        object propertyValue = property.GetValue(value, null);
                        InternalDump(indentationLevel + 2, property.Name, propertyValue, property, null, writer, idGenerator, true, options);
                    }
                    catch (TargetInvocationException ex)
                    {
                        InternalDump(indentationLevel + 2, property.Name, ex, property, null, writer, idGenerator, false, options);
                    }
                    catch (ArgumentException ex)
                    {
                        InternalDump(indentationLevel + 2, property.Name, ex, property, null, writer, idGenerator, false, options);
                    }
                    catch (RemotingException ex)
                    {
                        InternalDump(indentationLevel + 2, property.Name, ex, property, null, writer, idGenerator, false, options);
                    }
                }
                //writer.WriteLine($"{indentation2}}}");
            }
            writer.WriteLine($"{indentation}}}");
        }

        private static string GetAccessModifiers(MemberInfo memberInfo)
        {
            // Prepare access modifier prefix
            var propertyInfo = memberInfo as PropertyInfo;
            var fieldInfo = memberInfo as FieldInfo;

            StringBuilder sb = new StringBuilder();
            if (propertyInfo != null)
            {
                var getMethod = propertyInfo.GetGetMethod(true);
                var setMethod = propertyInfo.GetSetMethod(true);

                // Get the OPENEST accessor for get and set
                var isPublic = (getMethod?.IsPublic ?? false) || (setMethod?.IsPublic ?? false);
                var isInternal = !isPublic && ((getMethod?.IsAssembly ?? false) || (setMethod?.IsAssembly ?? false));
                var isPrivate = (getMethod?.IsPrivate ?? true) && (setMethod?.IsPrivate ?? true);

                // Check if both have additional modifier to be added in front
                var isProtected = (getMethod?.IsFamily ?? true) && (setMethod?.IsFamily ?? true);
                var isStatic = (getMethod?.IsStatic ?? true) && (setMethod?.IsStatic ?? true);
                var isAbstract = (getMethod?.IsAbstract ?? true) && (setMethod?.IsAbstract ?? true);
                var isVirtual = (getMethod?.IsVirtual ?? true) && (setMethod?.IsVirtual ?? true);
                var isFinal = (getMethod?.IsFinal ?? true) && (setMethod?.IsFinal ?? true);

                if (isPublic) sb.Append("public ");
                else if (isInternal) sb.Append("internal ");
                else if (isPrivate) sb.Append("private ");

                if (isProtected) sb.Append("protected ");
                if (isStatic) sb.Append("static ");
                if (isAbstract) sb.Append("abstract ");
                if (isVirtual) sb.Append("virtual ");
                if (isFinal) sb.Append("final ");
            }
            else if (fieldInfo != null)
            {
                if (fieldInfo.IsPublic) sb.Append("public ");
                if (fieldInfo.IsAssembly) sb.Append("internal ");
                if (fieldInfo.IsPrivate) sb.Append("private ");
                if (fieldInfo.IsFamily) sb.Append("protected ");
                if (fieldInfo.IsStatic) sb.Append("static ");
                if (fieldInfo.IsInitOnly && fieldInfo.IsLiteral) sb.Append("readonly ");
                if (!fieldInfo.IsInitOnly && fieldInfo.IsLiteral) sb.Append("const ");
            }

            return sb.ToString();
        }

        private static string GetDeclaredTypeName(MemberInfo memberInfo)
        {
            var propertyInfo = memberInfo as PropertyInfo;
            var fieldInfo = memberInfo as FieldInfo;
            if (propertyInfo != null)
                return propertyInfo.PropertyType + " ";
            if (fieldInfo != null)
                return fieldInfo.FieldType + " ";
            return "";
        }

        private static string ImproveTypeName(string typeName)
        {
            var lb = "(?<![.a-zA-Z0-9_])"; // look-behind to check that type name doesn't continue to left
            var la = "(?![.a-zA-Z0-9_])";  // look-ahead to check that type doesn't continue to right
            // See: https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/built-in-types-table
            typeName = Regex.Replace(typeName, lb + "System\\.Boolean" + la, "bool");
            typeName = Regex.Replace(typeName, lb + "System\\.Byte" + la, "byte");
            typeName = Regex.Replace(typeName, lb + "System\\.SByte" + la, "sbyte");
            typeName = Regex.Replace(typeName, lb + "System\\.Char" + la, "char");
            typeName = Regex.Replace(typeName, lb + "System\\.Decimal" + la, "decimal");
            typeName = Regex.Replace(typeName, lb + "System\\.Double" + la, "double");
            typeName = Regex.Replace(typeName, lb + "System\\.Single" + la, "float");
            typeName = Regex.Replace(typeName, lb + "System\\.Int32" + la, "int");
            typeName = Regex.Replace(typeName, lb + "System\\.UInt32" + la, "uint");
            typeName = Regex.Replace(typeName, lb + "System\\.Int64" + la, "long");
            typeName = Regex.Replace(typeName, lb + "System\\.UInt64" + la, "ulong");
            typeName = Regex.Replace(typeName, lb + "System\\.Object" + la, "object");
            typeName = Regex.Replace(typeName, lb + "System\\.Int16" + la, "short");
            typeName = Regex.Replace(typeName, lb + "System\\.UInt16" + la, "ushort");
            typeName = Regex.Replace(typeName, lb + "System\\.String" + la, "string");

            typeName = Regex.Replace(typeName, lb + @"System(\.[a-zA-Z0-9_]+)*\.([a-zA-Z0-9_]+)" + la, "$2");

            return typeName;
        }

        private static string PropertyMethods(MemberInfo memberInfo)
        {
            var propertyInfo = memberInfo as PropertyInfo;
            if (propertyInfo == null)
                return "";

            var getMethod = propertyInfo.GetGetMethod(true);
            var setMethod = propertyInfo.GetSetMethod(true);

            // Get the OPENEST accessor for get and set
            var isPublic = (getMethod?.IsPublic ?? false) || (setMethod?.IsPublic ?? false);
            var isInternal = !isPublic && ((getMethod?.IsAssembly ?? false) || (setMethod?.IsAssembly ?? false));

            // Check if a LOWER accessor is on one of the get/set methods
            var isGetInternal = isPublic && (getMethod?.IsAssembly ?? false);
            var isSetInternal = isPublic && (setMethod?.IsAssembly ?? false);
            var isGetPrivate = (isPublic || isInternal) && (getMethod?.IsPrivate ?? false);
            var isSetPrivate = (isPublic || isInternal) && (setMethod?.IsPrivate ?? false);

            var sb = new StringBuilder(" { ");

            if (getMethod != null)
            {
                if (isGetInternal)
                    sb.Append("internal ");
                else if (isGetPrivate)
                    sb.Append("private ");
                sb.Append("get");
            }

            if (getMethod != null && setMethod != null)
                sb.Append("; ");

            if (setMethod != null)
            {
                if (isSetInternal)
                    sb.Append("internal ");
                else if (isSetPrivate)
                    sb.Append("private ");
                sb.Append("set");
            }

            sb.Append(" }");

            return sb.ToString();
        }
    }
}
