using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ScriptingMod;
using ScriptingMod.ScriptEngines;

namespace UnitTests
{
    [TestFixture()]
    public class ObjectDumperTests
    {
        [Test()]
        public void ObjectDumperTest()
        {
            // Test object without any properties or fields
            Console.WriteLine(ObjectDumper.Dump(this));

            // Test standard behavior
            Console.WriteLine(ObjectDumper.Dump(CultureInfo.CurrentCulture, "CurrentCulture"));

            // Test all options
            Console.WriteLine(ObjectDumper.Dump(CultureInfo.CurrentCulture, "CurrentCulture",
                new ObjectDumperOptions() {
                    MaxDepth = 2,
                    WithNonPublic = false,
                    WithStatic = true,
                    WithFields = false,
                    IterateEnumerable = true,
                    WithEnumerableMembers = false,
                }));
        }
    }
}
