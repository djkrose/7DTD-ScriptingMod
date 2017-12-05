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
    [HarmonyPatch(typeof(EntityAlive), "ProcessDamageResponse")]
    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    public class EntityDamaged
    {
        public static bool Prepare()
        {
            if (!CommandTools.IsAnyEventActive(ScriptEvent.playerDamaged, ScriptEvent.animalDamaged, ScriptEvent.zombieDamaged))
            {
                Log.Debug($"Patch is disabled: {nameof(EntityDamaged)}");
                return false;
            }

            Log.Out($"Applying event tracker patch {nameof(EntityDamaged)} ...");
            return true;
        }

        public static bool Prefix([NotNull] EntityAlive __instance, DamageResponse _dmResponse)
        {
            Log.Debug($"Executing patch prefix for {nameof(EntityDamaged)} ...");

            ScriptEvent eventType;
            if (__instance is EntityPlayer)
                eventType = ScriptEvent.playerDamaged;
            else if (__instance is EntityAnimal || __instance is EntityZombieDog || __instance is EntityEnemyAnimal || __instance is EntityHornet)
                eventType = ScriptEvent.animalDamaged;
            else if (__instance is EntityZombie)
                eventType = ScriptEvent.zombieDamaged;
            else
                return true;

            CommandTools.InvokeScriptEvents(new EntityDamagedEventArgs(eventType, __instance, _dmResponse));

            return true;
        }
    }
}
