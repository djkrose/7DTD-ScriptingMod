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
            var sw = new MicroStopwatch();
            sw.Start();

            Log.Debug($"Executing patch prefix for {typeof(EntityAlive)}.{nameof(EntityAlive.ProcessDamageResponse)} ...");

            ScriptEvents eventType;
            switch (__instance.entityType)
            {
                case EntityType.Player:
                    eventType = ScriptEvents.playerDied;
                    break;
                case EntityType.Animal:
                    eventType = ScriptEvents.animalDied;
                    break;
                case EntityType.Zombie:
                    eventType = ScriptEvents.zombieDied;
                    break;
                default:
                    return true;
            }

            //ScriptEvents eventType;
            //if (__instance is EntityPlayer)
            //    eventType = ScriptEvents.playerDamaged;
            //else if (__instance is EntityAnimal || __instance is EntityZombieDog || __instance is EntityEnemyAnimal || __instance is EntityHornet)
            //    eventType = ScriptEvents.animalDamaged;
            //else if (__instance is EntityZombie)
            //    eventType = ScriptEvents.zombieDamaged;
            //else
            //    return true;

            Log.Debug("Determining event Type took " + sw.ElapsedMicroseconds + " µs.");
            sw.ResetAndRestart();

            CommandTools.InvokeScriptEvents(eventType, new { entity = __instance, damageResponse = _dmResponse });

            Log.Debug("Processing patch took " + sw.ElapsedMicroseconds + " µs.");

            return true;
        }
    }
}
