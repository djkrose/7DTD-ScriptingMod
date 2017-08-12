using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using ScriptingMod.Tools;
using ScriptingMod.ScriptEngines;

namespace ScriptingMod.Commands
{
    [UsedImplicitly]
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
            try
            {
                LuaEngine.Instance.Reset();
                SdtdConsole.Instance.Output("Lua engine was reset.");
            }
            catch (Exception ex)
            {
                CommandTools.HandleCommandException(ex);
            }
        }
    }
}
