using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModInfo;
using ScriptingMod.Managers;

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
            try
            {
                var modInfo = ModManager.GetMod(Constants.ModNameFull)?.ModInfo;
                if (modInfo == null)
                {
                    SdtdConsole.Instance.Output(Constants.ModNameFull);
                    SdtdConsole.Instance.Output("Could not load mod infos. Have you modified the ModInfo.xml?");
                    return;
                }

                SdtdConsole.Instance.Output($"{Constants.ModNameFull} - Version {modInfo.Version.Value}");
                SdtdConsole.Instance.Output(modInfo.Description.Value);
                if (!string.IsNullOrEmpty(modInfo.Website.Value))
                    SdtdConsole.Instance.Output(modInfo.Website.Value);
            }
            catch (Exception ex)
            {
                CommandManager.HandleCommandException(ex);
            }
        }
    }
}
