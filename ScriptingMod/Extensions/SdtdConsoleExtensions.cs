using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ScriptingMod.Extensions
{
    internal static class SdtdConsoleExtensions
    {
        /// <summary>
        /// Sends the given message asynchronously and immediately to the sender of the command.
        /// Note: SdtdConsole.Instance.Out does NOT work asynchronously!
        /// </summary>
        /// <param name="target"></param>
        /// <param name="senderInfo">Either NetworkConnection (telnet) or RemoteClientInfo (game client) must be set.</param>
        /// <param name="msg">Message to send; newline is added automatically</param>
        public static void OutputAsync(this SdtdConsole target, CommandSenderInfo senderInfo, string msg)
        {
            if (senderInfo.NetworkConnection != null) // telnet
                senderInfo.NetworkConnection.SendLine(msg);
            else if (senderInfo.RemoteClientInfo != null) // 7dtd client
                senderInfo.RemoteClientInfo.SendPackage(new NetPackageConsoleCmdClient(msg, false));
            else
                Log.Warning("Could not find a way to send output to console asynchronously: " + msg);
        }
    }
}
