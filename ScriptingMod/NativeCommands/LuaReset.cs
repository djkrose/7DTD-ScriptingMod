using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ScriptingMod.ScriptEngines;

namespace ScriptingMod.NativeCommands
{
    public class LuaReset : ConsoleCmdAbstract
    {
        public override string[] GetCommands()
        {
            return new string[] { "lua-reset" };
        }

        public override string GetDescription()
        {
            return "Clears the Lua engine from all variables and loaded modules.";
        }

        public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
        {
            LuaEngine.Instance.Reset();
            SdtdConsole.Instance.Output("Lua engine was reset.");
        }
    }
}
