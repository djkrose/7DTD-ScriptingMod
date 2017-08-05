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
    public class LogPowerManager : ConsoleCmdAbstract
    {
        public override string[] GetCommands()
        {
            return new [] { "dj-log-power-manager" };
        }

        public override string GetDescription()
        {
            return "Logs the power manager data.";
        }

        public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
        {
            try
            {
                PowerManager.Instance.LogPowerManager();
            }
            catch (Exception ex)
            {
                CommandManager.HandleCommandException(ex);
            }
        }
    }
#endif

}
