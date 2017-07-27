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
            Console.WriteLine(ObjectDumper.Dump(this));

            Console.WriteLine(ObjectDumper.Dump(DateTime.Now, "DateTime.Now"));

            Console.WriteLine(ObjectDumper.Dump(CultureInfo.CurrentCulture, "CurrentCulture"));

            Console.WriteLine(ObjectDumper.Dump(CultureInfo.CurrentCulture, "CurrentCulture", 1));

            Console.WriteLine(ObjectDumper.Dump(ScriptEngine.GetInstance(ScriptTypeEnum.LUA), "LuaEngine", 2));
        }
    }
}
