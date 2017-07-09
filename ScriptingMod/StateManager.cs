using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Logx = global::Log;

namespace ScriptingMod
{
    internal class StateManager
    {
        public static void Awake()
        {
            try
            {
                // TODO
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
