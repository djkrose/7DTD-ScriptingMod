using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using ScriptingMod.Exceptions;
using ScriptingMod.Extensions;
using UnityEngine;

namespace ScriptingMod.Managers
{
    internal class PlayerManager
    {
        /// <summary>
        /// Returns the RemoteClientInfo of the given senderInfo object, or throws an exception if it is null
        /// </summary>
        /// <returns>The RemoteClientInfo; never null</returns>
        /// <exception cref="FriendlyMessageException">If the player isn't logged with a proper client, or the position cannot be found for other reasons</exception>
        [NotNull]
        public static ClientInfo GetClientInfo(CommandSenderInfo senderInfo)
        {
            return senderInfo.RemoteClientInfo
                   ?? throw new FriendlyMessageException("Unable to get your remote client info. You must be logged in as a regular player.");
        }

        /// <summary>
        /// Returns the current world position of the remote client,
        /// or throws an exception if no position can be found.
        /// </summary>
        /// <exception cref="FriendlyMessageException">If the client cannot be found in the world's list of players or it is null</exception>
        public static Vector3i GetPosition(ClientInfo ci)
        {
            EntityPlayer ep = GameManager.Instance.World.Players.dict.GetValue(ci.entityId)
                ?? throw new FriendlyMessageException("Unable to get your position.");

            // Do NOT use "ep.position" because that doesn't consider underground positions when using noclip (thanks StompiNZ)
            //return new Vector3i(
            //    (int)Math.Floor(ep.position.x),
            //    (int)Math.Floor(ep.position.y),
            //    (int)Math.Floor(ep.position.z));

            // Do NOT use "new Vector3i(Vector3 v)", because it calculates incorrectly by just casting to int, which rounds UP on negative numbers.

            return new Vector3i(
                (int)Math.Floor(ep.serverPos.x / 32f),
                (int)Math.Floor(ep.serverPos.y / 32f),
                (int)Math.Floor(ep.serverPos.z / 32f));
        }

        /// <summary>
        /// Returns the current world position of the command sending client,
        /// or throws an exception if no position can be found.
        /// </summary>
        /// <exception cref="FriendlyMessageException">If the player isn't logged with a proper client, or the position cannot be found for other reasons</exception>
        public static Vector3i GetPosition(CommandSenderInfo senderInfo)
        {
            return GetPosition(GetClientInfo(senderInfo));
        }

    }
}
