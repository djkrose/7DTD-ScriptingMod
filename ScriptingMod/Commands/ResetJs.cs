using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using ScriptingMod.Tools;
using ScriptingMod.ScriptEngines;

namespace ScriptingMod.Commands
{
    [UsedImplicitly]
    public class ResetJs : ConsoleCmdAbstract
    {
        public override string[] GetCommands()
        {
            return new string[] { "dj-reset-js" };
        }

        public override string GetDescription()
        {
            return "Clears the JavaScript engine from all variables and loaded modules.";
        }

        public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
        {
            try
            {
                JsEngine.Instance.Reset();
                SdtdConsole.Instance.Output("JavaScript engine was reset.");
            }
            catch (Exception ex)
            {
                CommandTools.HandleCommandException(ex);
            }
        }
    }
}
