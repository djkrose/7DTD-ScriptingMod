using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ScriptingMod;
using ScriptingMod.ScriptEngines;

namespace UnitTests
{
    [TestFixture]
    public class ScriptEngineTests
    {

        private static string AssemblyDirectory
        {
            get
            {
                string codeBase = typeof(ScriptEngineTests).Assembly.CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);
            }
        }

        [TestCase("LoadMetadataTest.js")]
        public void LoadMetadataTest(string filePath)
        {
            var fileExt = Path.GetExtension(filePath);
            var scriptEngine = ScriptEngine.GetInstance(fileExt);
            filePath = Path.Combine(AssemblyDirectory, filePath);
            var metadata = scriptEngine.LoadMetadata(filePath);

            Assert.AreEqual("test", metadata["commands"]);
            Assert.AreEqual("0", metadata["defaultPermission"]);
            Assert.AreEqual("Some example command", metadata["description"]);
            Console.WriteLine(Dumper.Dump(metadata, "metadata"));
            var help = Regex.Split(metadata["help"], "(?:\r\n|\r|\n)");
            Console.WriteLine(Dumper.Dump(help, "help"));

            Assert.AreEqual(6, help.Length);
            Assert.IsTrue(help[0].StartsWith("This"));
            Assert.AreEqual("Examples:", help[1]);
            Assert.IsTrue(help[2].StartsWith("  1."));
            Assert.IsTrue(help[3].StartsWith("2."));
            Assert.IsEmpty(help[4]);
            Assert.IsTrue(help[5].StartsWith("Empty lines"));

            Assert.IsFalse(metadata.ContainsKey("shouldBeIgnored"));
        }
    }
}
