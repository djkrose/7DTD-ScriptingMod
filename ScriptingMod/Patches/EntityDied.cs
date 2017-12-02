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
    [HarmonyPatch(typeof(EntityAlive))]
    [HarmonyPatch("Kill")]
    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    public class EntityDied
    { 
        public static bool Prefix([NotNull] EntityAlive __instance, DamageResponse _dmResponse)
        {
            Log.Debug($"Executing patch prefix for {typeof(EntityAlive)}.{nameof(EntityAlive.Kill)} ...");

            ScriptEvents eventType;
            if (__instance is EntityPlayer)
                eventType = ScriptEvents.playerDied;
            else if (__instance is EntityAnimal || __instance is EntityZombieDog || __instance is EntityEnemyAnimal || __instance is EntityHornet)
                eventType = ScriptEvents.animalDied;
            else if (__instance is EntityZombie)
                eventType = ScriptEvents.zombieDied;
            else
                return true;

            CommandTools.InvokeScriptEvents(new EntityDiedEventArgs(eventType, __instance, _dmResponse));

            return true;
        }
    }
}
