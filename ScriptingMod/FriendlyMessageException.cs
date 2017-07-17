using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ScriptingMod
{
    internal class FriendlyMessageException : ApplicationException
    {
        public FriendlyMessageException(string message) : base(message)
        {
        }
    }
}
