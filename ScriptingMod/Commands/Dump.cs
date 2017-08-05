using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using ScriptingMod.Managers;

namespace ScriptingMod.Commands
{

#if DEBUG
    [UsedImplicitly]
    public class Dump : ConsoleCmdAbstract
    {

        public override string[] GetCommands()
        {
            return new [] { "dj-test" };
        }

        public override string GetDescription()
        {
            return "Internal tests for Scripting Mod";
        }

        public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
        {
            try
            {
                SdtdConsole.Instance.Output("Nothing to test.");

            }
            catch (Exception ex)
            {
                CommandManager.HandleCommandException(ex);
            }
        }
    }
#endif

}
