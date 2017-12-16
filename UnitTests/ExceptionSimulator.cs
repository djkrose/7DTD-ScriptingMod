using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

// MUST be in ScriptingMod namespace to simulate exception coming from there
// ReSharper disable once CheckNamespace
namespace ScriptingMod
{
    public static class ExceptionSimulator
    {
        public static void ThrowOwnException()
        {
            throw new ApplicationException("This exception was thrown for testing.");
        }

        public static void ThrowSystemException()
        {
            var dict = new Dictionary<string, string>();
            var ret = dict["key does not exist"]; // throws!
        }
    }
}