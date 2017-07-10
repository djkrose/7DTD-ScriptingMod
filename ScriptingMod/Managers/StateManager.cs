using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ScriptingMod.NativeCommands;
using ScriptingMod.ScriptEngines;

namespace ScriptingMod.Managers
{
    internal static class StateManager
    {
        public static void Awake()
        {
            try
            {
                LoadDynamicCommands();
            }
            catch (Exception e)
            {
                Log.Error("Error in StateManager.Awake: " + e);
            }
        }

        public static void Shutdown()
        {
            try
            {
                // TODO
            }
            catch (Exception e)
            {
                Log.Error("Error in StateManager.Shutdown: " + e);
            }
        }

    }
}
