using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using ScriptingMod.Extensions;

namespace ScriptingMod.Tools
{
    internal static class EacTools
    {
        //private static bool _isInitialized = false;

        /// <summary>
        /// Event is fired whenever a player was kicked because of EAC violations, e.g. when EAC is not activated on an EAC-ebaled server.
        /// Is not called for players who are exempt from EAC kicks but would otherwise be kicked.
        /// </summary>
        public static event KickPlayerDelegate PlayerKicked;

        /// <summary>
        /// Event is fired whenever a player successfully passes EAC validation.
        /// Is also called for players who are exempt from EAC checks, regardless of the check result.
        /// </summary>
        public static event AuthenticationSuccessfulCallbackDelegate AuthenticationSuccessful;

        /// <summary>
        /// Hooks into EAC checks and exposes events for it;
        /// MUST be called after in GameStartDone or later
        /// </summary>
        public static void Init()
        {
            //if (_isInitialized)
            //    throw new InvalidOperationException(nameof(EacTools) + "." + nameof(Init) + " may only be called once.");

            Log.Debug("Hooking into the EAC response callbacks ...");

            if (EACServer.Instance == null)
                throw new ApplicationException("Cannot activate EAC monitoring because EAC server is not (yet) started.");

            var successDelegate = EACServer.Instance.GetSuccessDelegate();
            var kickDelegate = EACServer.Instance.GetKickDelegate();

            if (successDelegate == null || kickDelegate == null)
                throw new ApplicationException("Cannot activate EAC monitoring because success and kick delegates are not (yet) set.");

            var kickDelegateNew = new KickPlayerDelegate(delegate (ClientInfo info, GameUtils.KickPlayerData data)
            {
                if (PersistentData.Instance.EacWhitelist.Contains(info.playerId))
                {
                    Log.Out($"EAC check failed but player \"{info.playerName}\" ({info.playerId}) is exempt from EAC kicks.");
                    // Call success delegate instead
                    successDelegate(info);
                }
                else
                {
                    // Let original kick delegate handle it
                    kickDelegate(info, data);
                    PlayerKicked?.Invoke(info, data);
                }
            });

            // Replace original kick delegate with our new modified one
            EACServer.Instance.SetKickDelegate(kickDelegateNew);

            var successDelegateNew = new AuthenticationSuccessfulCallbackDelegate(delegate(ClientInfo info)
            {
                // Let original kick delegate handle it
                successDelegate(info);
                AuthenticationSuccessful?.Invoke(info);
            });

            // Replace original success delegate with our new modified one
            EACServer.Instance.SetSuccessDelegate(successDelegateNew);

            //_isInitialized = true;
            Log.Debug("EAC monitoring activated.");
        }
    }
}
