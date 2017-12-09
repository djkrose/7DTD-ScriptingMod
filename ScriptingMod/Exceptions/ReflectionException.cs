using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ScriptingMod.Exceptions
{
    internal class ReflectionException : ApplicationException
    {

        public ReflectionException(string message) : base(message) { }

        public ReflectionException(string message, Exception ex) : base(message, ex) { }

    }
}
