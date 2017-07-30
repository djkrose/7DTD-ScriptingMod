using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace UnitTests
{
    [TestFixture]
    public class ReflectionSpeedTests
    {
        [Test]
        public void TestRepeatedReflection()
        {
            var obj = new PowerTrigger();
            for (int i = 0; i < 1000000; i++)
            {
                bool isTriggered = (bool)typeof(PowerTrigger).GetField("isTriggered", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
            }
        }

        [Test]
        public void TestOneTimeReflection()
        {
            var obj = new PowerTrigger();
            var field = typeof(PowerTrigger).GetField("isTriggered", BindingFlags.Instance | BindingFlags.NonPublic);
            for (int i = 0; i < 1000000; i++)
            {
                bool isTriggered = (bool)field.GetValue(obj);
            }
        }

    }
}
