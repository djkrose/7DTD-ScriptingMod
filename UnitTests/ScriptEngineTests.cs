using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
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

            Assert.AreEqual("js-test", metadata["commands"]);
            Assert.AreEqual("0", metadata["defaultPermission"]);
            Assert.IsTrue(metadata["description"].Length > 0);
            Assert.IsTrue(metadata["help"].Length > 0);
            Assert.IsTrue(metadata["help"].StartsWith("This"));
            Assert.IsTrue(metadata["help"].Contains(Environment.NewLine + "  1. js-test"));
            Assert.IsFalse(metadata.ContainsKey("shouldBeIgnored"));
        }
    }
}
