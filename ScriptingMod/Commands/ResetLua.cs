using System;
using System.Collections.Generic;
using System.Linq;
using ScriptingMod.ScriptEngines;

namespace ScriptingMod.Commands
{
    public class ResetLua : ConsoleCmdAbstract
    {
        public override string[] GetCommands()
        {
            return new string[] { "dj-reset-lua" };
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
