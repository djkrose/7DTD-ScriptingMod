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
                Log.Debug($"Patch {nameof(EntityDamaged)} is disabled.");
                return false;
            }

            Log.Out($"Injecting event tracker {nameof(EntityDamaged)} ...");
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

            CommandTools.InvokeScriptEvents(eventType, t =>
            {
                var sourceEntity = GameManager.Instance.World?.GetEntity(_dmResponse.Source?.getEntityId() ?? -1) as EntityAlive;
                return new EntityDamagedEventArgs
                {
                    eventType        = t.ToString(),
                    position         = __instance.GetBlockPosition(),
                    entityId         = __instance.entityId,
                    entityName       = __instance.EntityName,
                    sourceEntityId   = sourceEntity?.entityId,
                    sourceEntityName = sourceEntity?.EntityName,
                    damageType       = _dmResponse.Source?.GetName().ToString(),
                    hitBodyPart      = _dmResponse.HitBodyPart.ToString(),
                    hitDirection     = _dmResponse.HitDirection.ToString(),
                    damage           = _dmResponse.Strength,
                    armorDamage      = _dmResponse.ArmorDamage,
                    armorSlot        = _dmResponse.ArmorSlot.ToString(),
                    stunType         = _dmResponse.Stun.ToString(),
                    stunDuration     = _dmResponse.StunDuration,
                    critical         = _dmResponse.Critical,
                    fatal            = _dmResponse.Fatal,
                    crippleLegs      = _dmResponse.CrippleLegs,
                    dismember        = _dmResponse.Dismember,
                    turnIntoCrawler  = _dmResponse.TurnIntoCrawler,
                    painHit          = _dmResponse.PainHit,
                };
            });

            return true;
        }
    }
}
