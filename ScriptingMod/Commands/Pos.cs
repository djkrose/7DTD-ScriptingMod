using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using ScriptingMod.Tools;
using UnityEngine;

namespace ScriptingMod.Commands
{
    [UsedImplicitly]
    public class Pos : ConsoleCmdAbstract
    {
        public override string[] GetCommands()
        {
            return new[] { "dj-pos" };
        }

        public override string GetDescription()
        {
            return @"Shows the current player's position in various units and formats.";
        }

        public override void Execute(List<string> parameters, CommandSenderInfo senderInfo)
        {
            try
            {
                Vector3 precisePos = PlayerTools.GetPrecisePosition(senderInfo);
                Vector3i worldPos = PlayerTools.GetPosition(senderInfo);
                Vector3i chunkPos = World.toBlock(worldPos);
                var chunkXZ = ChunkTools.WorldPosToChunkXZ(worldPos);
                string mapPos = Math.Abs(worldPos.z) + (worldPos.z >= 0 ? "N " : "S ") + Math.Abs(worldPos.x) + (worldPos.x >= 0 ? "E " : "W "); // todo: add elevation
                var areaXZ = ChunkTools.ChunkXZToAreaXZ(chunkXZ);
                // todo: distance below ground

                SdtdConsole.Instance.Output($"Precise position in world (x y z): {precisePos.x} {precisePos.y} {precisePos.z}");
                SdtdConsole.Instance.Output($"Block position in world (x y z): {worldPos.x} {worldPos.y} {worldPos.z}");
                //SdtdConsole.Instance.Output($"Map position in world: {mapPos}"); // todo: incorrect
                SdtdConsole.Instance.Output($"Position in chunk (x y z): {chunkPos.x} {chunkPos.y} {chunkPos.z}");
                SdtdConsole.Instance.Output($"Chunk (x z): {chunkXZ.x} {chunkXZ.z}");
                SdtdConsole.Instance.Output($"Region file: r.{areaXZ.x}.{areaXZ.z}.7rg");
            }
            catch (Exception ex)
            {
                CommandTools.HandleCommandException(ex);
            }
        }
    }
}
