using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using ScriptingMod.Exceptions;
using ScriptingMod.Extensions;
using ScriptingMod.Tools;

namespace ScriptingMod.Commands
{
    [UsedImplicitly]
    public class Power : ConsoleCmdAbstract
    {

        public override string[] GetCommands()
        {
            return new[] { "dj-power" };
        }

        public override string GetDescription()
        {
            return "Replaced by command: dj-repair";
        }

        private const string Help =
            "This command got replaced by \"dj-repair\", which now supports multiple different fixes.\r\n"+
            "Note that \"/fix\" mode is now default. If you want to report only, use \"/sim\" to simulate.\r\n" +
            "See \"help dj-repair\" for details.";

        public override string GetHelp()
        {
            // ----------------------------------(max length: 120 char)----------------------------------------------------------------|
            return Help;
        }

        public override void Execute(List<string> parameters, CommandSenderInfo senderInfo)
        {
            SdtdConsole.Instance.Output(Help);
        }

    }
}
