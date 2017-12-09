using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using ScriptingMod.Exceptions;
using ScriptingMod.Extensions;
using ScriptingMod.ScriptEngines;
using ScriptingMod.Tools;
using UnityEngine;

namespace ScriptingMod.Commands
{

#if DEBUG
    [UsedImplicitly]
    public class Test : ConsoleCmdAbstract
    {

        public override string[] GetCommands()
        {
            return new [] { "dj-test" };
        }

        public override string GetDescription()
        {
            return "Internal tests for Scripting Mod";
        }

        public override void Execute(List<string> parameters, CommandSenderInfo senderInfo)
        {
            try
            {
                var ci = senderInfo.RemoteClientInfo ?? throw new FriendlyMessageException(Resources.ErrorNotRemotePlayer);
                var world = GameManager.Instance.World ?? throw new FriendlyMessageException(Resources.ErrorWorldNotReady);
                var player = senderInfo.RemoteClientInfo.GetEntityPlayer();
            }
            catch (Exception ex)
            {
                CommandTools.HandleCommandException(ex);
            }
        }

    }
#endif

}
