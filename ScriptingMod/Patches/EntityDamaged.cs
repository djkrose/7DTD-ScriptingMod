using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Harmony;
using JetBrains.Annotations;
using ScriptingMod.Tools;

namespace ScriptingMod.Patches
{
    [HarmonyPatch(typeof(EntityAlive))]
    [HarmonyPatch("ProcessDamageResponse")]
    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    public class EntityDamaged
    {
        public static bool Prefix([NotNull] EntityAlive __instance, DamageResponse _dmResponse)
        {
            Log.Debug($"Executing patch prefix for {typeof(EntityAlive)}.{nameof(EntityAlive.ProcessDamageResponse)} ...");

            ScriptEvents eventType;
            if (__instance is EntityPlayer)
                eventType = ScriptEvents.playerDamaged;
            else if (__instance is EntityAnimal || __instance is EntityZombieDog || __instance is EntityEnemyAnimal || __instance is EntityHornet)
                eventType = ScriptEvents.animalDamaged;
            else if (__instance is EntityZombie)
                eventType = ScriptEvents.zombieDamaged;
            else
                return true;

            CommandTools.InvokeScriptEvents(eventType, new { entity = __instance, damageResponse = _dmResponse });

            return true;
        }
    }
}
