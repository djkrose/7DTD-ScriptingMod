using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using ScriptingMod.Exceptions;

namespace ScriptingMod.Extensions
{
    internal static class ClientInfoExtensions
    {
        /// <summary>
        /// Returns the EntityPlayer object from the ClientInfo object, or throws an exception
        /// </summary>
        /// <returns>The EntityPlayer; never null</returns>
        /// <exception cref="ApplicationException">If the no player for the given clientInfo exists in the world</exception>
        [NotNull]
        public static EntityPlayer GetEntityPlayer(this ClientInfo ci)
        {
            return GameManager.Instance.World?.Players.dict.GetValue(ci.entityId)
                   ?? throw new ApplicationException($"Unable to get player with entityId {ci.entityId}.");
        }
    }
}
