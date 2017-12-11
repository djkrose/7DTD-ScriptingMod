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
    public static class EntityDied
    {
        public static bool Prepare()
        {
            if (!CommandTools.IsAnyEventActive(ScriptEvent.playerDied, ScriptEvent.animalDied, ScriptEvent.zombieDied))
            {
                Log.Debug($"Patch {nameof(EntityDied)} is disabled.");
                return false;
            }

            Log.Out($"Injecting event tracker {nameof(EntityDied)} ...");
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

            CommandTools.InvokeScriptEvents(eventType, () =>
            {
                var sourceEntity = GameManager.Instance.World?.GetEntity(_dmResponse.Source?.getEntityId() ?? -1) as EntityAlive;
                return new EntityDiedEventArgs
                {
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
