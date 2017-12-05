using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Harmony;
using JetBrains.Annotations;
using ScriptingMod.ScriptEngines;
using ScriptingMod.Tools;

namespace ScriptingMod.Patches
{
    [HarmonyPatch(typeof(EntityAlive), "ClientKill")]
    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    public class EntityDied
    {
        public static bool Prepare()
        {
            if (!CommandTools.IsAnyEventActive(ScriptEvent.playerDied, ScriptEvent.animalDied, ScriptEvent.zombieDied))
            {
                Log.Debug($"Patch {nameof(EntityDied)} is disabled.");
                return false;
            }

            Log.Out($"Applying event tracker patch {nameof(EntityDied)} ...");
            return true;
        }

        public static bool Prefix([NotNull] EntityAlive __instance, DamageResponse _dmResponse)
        {
            Log.Debug($"Executing patch prefix for {nameof(EntityDied)} ...");

            ScriptEvent eventType;
            if (__instance is EntityPlayer)
                eventType = ScriptEvent.playerDied;
            else if (__instance is EntityAnimal || __instance is EntityZombieDog || __instance is EntityEnemyAnimal || __instance is EntityHornet)
                eventType = ScriptEvent.animalDied;
            else if (__instance is EntityZombie)
                eventType = ScriptEvent.zombieDied;
            else
                return true;

            CommandTools.InvokeScriptEvents(new EntityDiedEventArgs(eventType, __instance, _dmResponse));

            return true;
        }
    }
}
