using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ScriptingMod.Extensions
{
    internal static class SdtdConsoleExtensions
    {
        public static void OutputAndLog(this SdtdConsole target, string text)
        {
            target.Output(text);
            Log.Out(text);
        }
    }
}
