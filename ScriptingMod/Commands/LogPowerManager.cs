using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ScriptingMod.Commands
{

#if DEBUG
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
            PowerManager.Instance.LogPowerManager();
        }
    }
#endif

}
