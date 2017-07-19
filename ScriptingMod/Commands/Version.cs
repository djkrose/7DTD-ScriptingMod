using System;
using System.Collections.Generic;
using System.Linq;

namespace ScriptingMod.Commands
{
    public class Version : ConsoleCmdAbstract
    {

        public override string[] GetCommands()
        {
            return new string[] {"dj-version"};
        }

        public override string GetDescription()
        {
            return "Shows djkrose's Scripting Mod version information.";
        }

        public override string GetHelp()
        {
            return @"Tests if djkrose's Scripting Mod is correctly installed and prints out version information.";
        }

        public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
        {
            SdtdConsole.Instance.Output("djkrose's Scripting Mod - v0.2"); // TODO [P3]: make dynamic
        }
    }
}
