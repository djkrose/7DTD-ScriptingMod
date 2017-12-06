using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Harmony;
using JetBrains.Annotations;
using ScriptingMod.Extensions;
using ScriptingMod.ScriptEngines;
using ScriptingMod.Tools;

namespace ScriptingMod.Patches
{
    [HarmonyPatch(typeof(NetPackagePlayerStats), "ProcessPackage")]
    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    public class PlayerStatsChanged
    {
        public class State
        {
            public int EntityId;
            public int Level;
            public int ExpToNextLevel;
        }

        public static bool Prepare()
        {
            if (!CommandTools.IsAnyEventActive(ScriptEvent.playerLevelUp, ScriptEvent.playerExpGained))
            {
                Log.Debug($"Patch {nameof(PlayerStatsChanged)} is disabled.");
                return false;
            }

            Log.Out($"Injecting event tracker {nameof(PlayerStatsChanged)} ...");
            return true;
        }

        public static bool Prefix(NetPackagePlayerStats __instance, ref State __state, [CanBeNull] World _world)
        {
            Log.Debug($"Executing patch prefix {nameof(PlayerStatsChanged)} ...");

            if (_world == null)
                return true;

            if (_world.GetEntity(__instance.GetEntityId()) is EntityPlayer player)
            {
                // Remember last values for comparison later
                __state = new State()
                {
                    EntityId = player.entityId,
                    Level = player.Level,
                    ExpToNextLevel = player.ExpToNextLevel,
                };
            }
            return true;
        }

        public static void Postfix(NetPackagePlayerStats __instance, State __state, [CanBeNull] World _world)
        {
            Log.Debug($"Executing patch postfix {nameof(PlayerStatsChanged)} ...");

            if (_world == null || __state == null)
                return;

            if (_world.GetEntity(__state.EntityId) is EntityPlayer player)
            {
                // Track level increase
                if (player.Level > __state.Level)
                {
                    //CommandTools.InvokeScriptEvents(ScriptEvent.playerLevelUp, eventType => new PlayerLevelUpEventArgs(ScriptEvent.playerLevelUp, player, __state.Level, player.Level));
                    CommandTools.InvokeScriptEvents(new PlayerLevelUpEventArgs(ScriptEvent.playerLevelUp, player, __state.Level, player.Level));
                }

                // Track gained xp, including level-up by 1 (can't easily calculate multiple levels into xp)
                if (player.ExpToNextLevel != __state.ExpToNextLevel && (player.Level == __state.Level || player.Level == __state.Level + 1))
                {
                    int expGained = player.Level == __state.Level 
                        ? __state.ExpToNextLevel - player.ExpToNextLevel 
                        : __state.ExpToNextLevel + (player.GetExpForNextLevel() - player.ExpToNextLevel);
                    CommandTools.InvokeScriptEvents(new PlayerExpGainedEventArgs(ScriptEvent.playerExpGained, player, expGained, player.Level > __state.Level));
                }
            }
        }
    }
}
