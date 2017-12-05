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
    [HarmonyPatch(typeof(Chunk), "AddEntityToChunk")]
    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    public class PlayerEnteredChunk
    {

        public static bool Prepare()
        {
            if (!CommandTools.IsAnyEventActive(ScriptEvent.playerEnteredChunk))
            {
                Log.Debug($"Patch {nameof(PlayerEnteredChunk)} is disabled.");
                return false;
            }

            Log.Out($"Applying event tracker patch {nameof(PlayerEnteredChunk)} ...");
            return true;
        }

        public static bool Prefix(Chunk __instance, Entity _entity)
        {
            Log.Debug($"Executing patch prefix {nameof(PlayerEnteredChunk)} ...");

            if (_entity is EntityPlayer player)
            {
                CommandTools.InvokeScriptEvents(new PlayerEnteredChunkEventArgs(ScriptEvent.playerEnteredChunk, player, __instance, _entity.chunkPosAddedEntityTo));
            }
            return true;
        }
    }
}
