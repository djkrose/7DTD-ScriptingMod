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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Remoting;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

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
            InternalDump(0, name, value, writer, idGenerator, true, options);
        }

        [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
        private static void InternalDump(int indentationLevel, string name, object value, TextWriter writer,
            ObjectIDGenerator idGenerator, bool recursiveDump, ObjectDumperOptions options)
        {
            var indentation = new string(' ', indentationLevel * INDENTATION_SPACE);
            var indentation2 = new string(' ', (indentationLevel + 1) * INDENTATION_SPACE);

            if (value == null)
            {
                writer.WriteLine("{0}{1} = <null>", indentation, name);
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
            string typeName = value.GetType().FullName;
            string formattedValue = value.ToString();

            var exception = value as Exception;
            if (exception != null)
            {
                formattedValue = exception.GetType().Name + ": " + exception.Message;
            }

            if (formattedValue == typeName)
                formattedValue = string.Empty;
            else
            {
                // escape tabs and line feeds
                formattedValue = formattedValue.Replace("\t", "\\t").Replace("\n", "\\n").Replace("\r", "\\r");

                // chop at 80 characters
                int length = formattedValue.Length;
                if (length > 80)
                    formattedValue = formattedValue.Substring(0, 80);
                if (isString)
                    formattedValue = string.Format(CultureInfo.InvariantCulture, "\"{0}\"", formattedValue);
                if (length > 80)
                    formattedValue += " (+" + (length - 80) + " chars)";
                formattedValue = " = " + formattedValue;
            }

            writer.WriteLine("{0}{1}{2}{3} [{4}]{5}", indentation, keyPrefix, name, formattedValue, value.GetType(), keyRef);

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

            if (value is System.Security.Principal.SecurityIdentifier)
                return;

            if (!recursiveDump)
                return;

            if (indentationLevel / 2 > options.MaxDepth - 1)
                return;

            var nonPublic = options.NonPublic ? BindingFlags.NonPublic : BindingFlags.Default;

            PropertyInfo[] properties =
            (from property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | nonPublic)
                where property.GetIndexParameters().Length == 0
                      && property.CanRead
                select property).ToArray();
            IEnumerable<FieldInfo> fields = options.NoFields 
                ? Enumerable.Empty<FieldInfo>() 
                : type.GetFields(BindingFlags.Instance | BindingFlags.Public | nonPublic);

            if (!properties.Any() && !fields.Any())
                return;

            writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0}{{", indentation));
            if (properties.Any())
            {
                writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0}properties {{", indentation2));
                foreach (PropertyInfo pi in properties)
                {
                    try
                    {
                        object propertyValue = pi.GetValue(value, null);
                        InternalDump(indentationLevel + 2, pi.Name, propertyValue, writer, idGenerator, true, options);
                    }
                    catch (TargetInvocationException ex)
                    {
                        InternalDump(indentationLevel + 2, pi.Name, ex, writer, idGenerator, false, options);
                    }
                    catch (ArgumentException ex)
                    {
                        InternalDump(indentationLevel + 2, pi.Name, ex, writer, idGenerator, false, options);
                    }
                    catch (RemotingException ex)
                    {
                        InternalDump(indentationLevel + 2, pi.Name, ex, writer, idGenerator, false, options);
                    }
                }
                writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0}}}", indentation2));
            }
            if (fields.Any())
            {
                writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0}fields {{", indentation2));
                foreach (FieldInfo field in fields)
                {
                    try
                    {
                        object fieldValue = field.GetValue(value);
                        InternalDump(indentationLevel + 2, field.Name, fieldValue, writer, idGenerator, true, options);
                    }
                    catch (TargetInvocationException ex)
                    {
                        InternalDump(indentationLevel + 2, field.Name, ex, writer, idGenerator, false, options);
                    }
                }
                writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0}}}", indentation2));
            }
            writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0}}}", indentation));
        }
    }
}
