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
                int chunkX = World.toChunkXZ(worldPos.x);
                int chunkZ = World.toChunkXZ(worldPos.z);
                Vector3i areaMasterChunkPos = new Vector3i(World.toChunkXZ(worldPos.x) / 5 * 5, World.toChunkY(worldPos.y), World.toChunkXZ(worldPos.z) / 5 * 5);
                string mapPos = Math.Abs(worldPos.z) + (worldPos.z >= 0 ? "N " : "S ") + Math.Abs(worldPos.x) + (worldPos.x >= 0 ? "E " : "W "); // todo: add elevation
                int areaX = (int)Math.Floor(chunkX / 32.0d);
                int areaZ = (int)Math.Floor(chunkZ / 32.0d);
                // todo: distance below ground

                SdtdConsole.Instance.Output($"Precise position in world (x y z): {precisePos.x} {precisePos.y} {precisePos.z}");
                SdtdConsole.Instance.Output($"Block position in world (x y z): {worldPos.x} {worldPos.y} {worldPos.z}");
                //SdtdConsole.Instance.Output($"Map position in world: {mapPos}"); // todo: incorrect
                SdtdConsole.Instance.Output($"Position in chunk (x y z): {chunkPos.x} {chunkPos.y} {chunkPos.z}");
                SdtdConsole.Instance.Output($"Chunk (x z): {chunkX} {chunkZ}");
                SdtdConsole.Instance.Output($"Area master chunk (x z): {areaMasterChunkPos.x} {areaMasterChunkPos.z}");
                SdtdConsole.Instance.Output($"Region file: r.{areaX}.{areaZ}.7rg");
            }
            catch (Exception ex)
            {
                CommandTools.HandleCommandException(ex);
            }
        }
    }
}
