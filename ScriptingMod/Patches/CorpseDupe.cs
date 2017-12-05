using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Harmony;
using JetBrains.Annotations;
using ScriptingMod.Extensions;

namespace ScriptingMod.Patches
{
    [HarmonyPatch(typeof(EntityZombie), "dropCorpseBlock")]
    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    public static class CorpseDupe
    {
        public const string PatchName = "zombie corpse item dupe exploit";

        public static bool Prepare()
        {
            if (!PersistentData.Instance.PatchCorpseItemDupeExploit)
            {
                Log.Debug($"Patch {nameof(CorpseDupe)} is disabled.");
                return false;
            }

            Log.Out($"Injecting patch {nameof(CorpseDupe)} ...");
            return true;
        }

        public static bool Prefix([NotNull] EntityZombie __instance)
        {
            if (!PersistentData.Instance.PatchCorpseItemDupeExploit)
            {
                Log.Debug($"Skipping disabled patch prefix for {nameof(CorpseDupe)}.");
                return true;
            }

            Log.Debug($"Executing patch prefix for {nameof(CorpseDupe)} ...");

            if (__instance.lootContainer != null && __instance.lootContainer.bTouched && !__instance.lootContainer.IsEmpty())
            {
                __instance.lootContainer.SetEmpty();

                // EntityAlive.entityThatKilledMe and EntityAlive.GetRevengeTarget() are always null, but this isn't:
                var sourceEntityId   = __instance.GetDamageResponse().Source?.getEntityId() ?? -1;
                //var sourceEntity   = GameManager.Instance.World?.GetEntity(sourceEntityId);
                var sourceClientInfo = ConnectionManager.Instance?.GetClientInfoForEntityId(sourceEntityId);
                
                var pos = __instance.GetPosition().ToVector3i();

                Log.Out($"Cleared touched zombie corpse at {pos} killed by '{sourceClientInfo?.playerName ?? "[unknown]"}'.");
            }
            return true;
        }
    }
}
