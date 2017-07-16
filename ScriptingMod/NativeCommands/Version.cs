﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;

namespace ScriptingMod.NativeCommands
{
    public class Version : ConsoleCmdAbstract
    {

        public override string[] GetCommands()
        {
            return new string[] {"lua-version", "js-version"};
        }

        public override string GetDescription()
        {
            return "Shows Scripting Mod version information.";
        }

        public override string GetHelp()
        {
            return "Tests if djkrose's Scripting Mod is correctly installed and prints out version information.\n" +
                   "Usage:\n" +
                   $"   lua-version\r\n" +
                   $"   js-version\r\n";
        }

        public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
        {
            SdtdConsole.Instance.Output("djkrose's Scripting Mod - v0.2"); // TODO [P3]: make dynamic
        }
    }
}
