extern alias unmerged;
using NUnit.Framework;
using unmerged::ObjectDumper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ScriptingMod.ScriptEngines;

namespace UnitTests
{
    [TestFixture()]
    public class ObjectDumperExtensionsTests
    {
        [Test()]
        public void DumpToStringTest()
        {
            Console.WriteLine(this.DumpToString());

            Console.WriteLine(DateTime.Now.DumpToString("DateTime.Now"));

            Console.WriteLine(CultureInfo.CurrentCulture.DumpToString("CurrentCulture"));

            Console.WriteLine(CultureInfo.CurrentCulture.DumpToString("CurrentCulture", 1));

            Console.WriteLine(ScriptEngine.GetInstance(ScriptTypeEnum.LUA).DumpToString("LuaEngine", 2));

        }
    }
}
