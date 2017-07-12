using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ScriptingMod.Managers
{
    internal static class StateManager
    {
        public static void Awake()
        {
            try
            {
                ScriptManager.LoadCommands();
            }
            catch (Exception e)
            {
                Log.Exception(e);
            }
        }

        public static void Shutdown()
        {
            try
            {
                // Nothing to do yet
            }
            catch (Exception e)
            {
                Log.Exception(e);
            }
        }

    }
}
