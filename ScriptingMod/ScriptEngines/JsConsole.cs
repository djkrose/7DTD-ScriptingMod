using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;

namespace ScriptingMod.ScriptEngines
{
    [UsedImplicitly(ImplicitUseTargetFlags.Members)]
    // TODO: Could be made a bit more consistent; introduce something else to log?
    public class JsConsole
    {
        public void debug(object v)
        {
            Log.Debug(v.ToString());
        }
        public void info(object v)
        {
            Log.Out(v.ToString());
        }
        public void warn(object v)
        {
            Log.Warning(v.ToString());
        }
        public void error(object v)
        {
            Log.Error(v.ToString());
        }
        public void log(object v)
        {
            // TODO: Test and fix the SdtdConsole output for asynchronous/callbacks in JavaScript or Lua
            SdtdConsole.Instance.Output(v.ToString());
            Log.Debug("[CONSOLE] " + v);
        }
    }
}
