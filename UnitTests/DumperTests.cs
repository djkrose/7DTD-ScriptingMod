using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ScriptingMod;

namespace UnitTests
{
    [TestFixture]
    public class DumperTests
    {
        [Test]
        public void DumperTest()
        {
            // Test object without any properties or fields
            Console.WriteLine(Dumper.Dump(this));

            // Test standard behavior
            Console.WriteLine(Dumper.Dump(CultureInfo.CurrentCulture, "CurrentCulture"));

            // Test all options
            Console.WriteLine(Dumper.Dump(CultureInfo.CurrentCulture, "CurrentCulture",
                new DumperOptions() {
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
