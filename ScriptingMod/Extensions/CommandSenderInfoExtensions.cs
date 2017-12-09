using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using ScriptingMod.Exceptions;

namespace ScriptingMod.Extensions
{
    internal static class CommandSenderInfoExtensions
    {
        /// <summary>
        /// Returns the RemoteClientInfo of the senderInfo object, or throws an exception if it is null
        /// </summary>
        /// <returns>The RemoteClientInfo; never null</returns>
        /// <exception cref="FriendlyMessageException">If the player isn't logged with a proper client, or the position cannot be found for other reasons</exception>
        [NotNull]
        public static ClientInfo GetRemoteClientInfo(this CommandSenderInfo si)
        {
            return si.RemoteClientInfo ?? throw new FriendlyMessageException(Resources.ErrorNotRemotePlayer);
        }
    }
}
