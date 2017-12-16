using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ScriptingMod;
using ScriptingMod.Tools;

namespace UnitTests
{
    [TestFixture]
    public class TelemetryToolsTests
    {
        [Test]
        public void ShortExceptionMessageTest()
        {
            var miShortenStackTrace = typeof(TelemetryTools).GetMethod("ShortExceptionMessage", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new MethodAccessException("Could not find method through reflection.");

            try
            {
                ExceptionSimulator.ThrowOwnException();
            }
            catch (Exception ex)
            {
                var result = (string)miShortenStackTrace.Invoke(null, new object[] { ex });
                Console.WriteLine("Short own exception message: \n" + result);
                Assert.AreEqual("ApplicationE at ExceptionSimulator.ThrowOwnException in ExceptionSimulator.cs:14 -> This exception was thrown for testing.", result);
            }

            try
            {
                ExceptionSimulator.ThrowSystemException();
            }
            catch (Exception ex)
            {
                var result = (string)miShortenStackTrace.Invoke(null, new object[] { ex });
                Console.WriteLine("Short system exception message: \n" + result);
                // system exception message is localized so we don't really know the message and ignore it for this checks
                Assert.IsTrue(result.StartsWith("KeyNotFoundE at ThrowHelper.ThrowKeyNotFoundException at ExceptionSimulator.ThrowSystemException in ExceptionSimulator.cs:20"));
            }
        }
    }
}
