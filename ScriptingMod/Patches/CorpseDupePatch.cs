using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Harmony;
using JetBrains.Annotations;
using ScriptingMod.Extensions;

namespace ScriptingMod.Patches
{
    [HarmonyPatch(typeof(EntityZombie))]
    [HarmonyPatch("dropCorpseBlock")]
    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    public static class CorpseDupePatch
    {
        public const string PatchName = "zombie corpse item dupe exploit";
        private static bool IsPatched = false;

        public static bool Prepare()
        {
            if (!PersistentData.Instance.PatchCorpseItemDupeExploit)
            {
                Log.Debug($"Patch is disabled: {PatchName}");
                return false;
            }

            if (IsPatched)
            {
                Log.Debug($"Patch already applied: {PatchName}");
                return false;
            }

            Log.Out($"Patching {PatchName} ...");
            IsPatched = true;
            return true;
        }

        public static bool Prefix(EntityZombie __instance)
        {
            if (!PersistentData.Instance.PatchCorpseItemDupeExploit)
            {
                Log.Debug($"Skipping disabled patch prefix for {PatchName}.");
                return true;
            }

            Log.Debug($"Executing patch prefix for {PatchName} ...");

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
