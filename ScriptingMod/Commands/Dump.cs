using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;

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
            SdtdConsole.Instance.Output("Nothing to test.");
        }
    }
#endif

}
