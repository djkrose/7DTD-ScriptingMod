using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using ScriptingMod.Extensions;
using ScriptingMod.Tools;
using UnityEngine;

namespace ScriptingMod.Commands
{
    /*
     *  TODO [P3]: distance below ground
     */
     
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
                var player = senderInfo.GetRemoteClientInfo().GetEntityPlayer();
                var world = GameManager.Instance.World;

                var worldPosExact = player.GetServerPos();
                SdtdConsole.Instance.Output($"Exact player position in world (x y z): {worldPosExact.x} {worldPosExact.y} {worldPosExact.z}");

                var worldPos = worldPosExact.ToVector3i();
                SdtdConsole.Instance.Output($"Player block position in world (x y z): {worldPos.x} {worldPos.y} {worldPos.z}");

                //string mapPos = Math.Abs(worldPos.z) + (worldPos.z >= 0 ? "N " : "S ") + Math.Abs(worldPos.x) + (worldPos.x >= 0 ? "E " : "W "); // todo: add elevation
                //SdtdConsole.Instance.Output($"Map position in world: {mapPos}"); // todo: incorrect

                var chunkPos = World.toBlock(worldPos);
                SdtdConsole.Instance.Output($"Position in chunk (x y z): {chunkPos.x} {chunkPos.y} {chunkPos.z}");

                var chunkXZ = ChunkTools.WorldPosToChunkXZ(worldPos);
                SdtdConsole.Instance.Output($"Chunk (x z): {chunkXZ.x} {chunkXZ.z}");
                var chunk = world.GetChunkSync(chunkXZ.x, chunkXZ.z) as Chunk;
                if (chunk != null && chunk.ChunkCustomData.Count > 0)
                SdtdConsole.Instance.Output(DumpCustomChunkData(chunk));

                var areaMasterChunkXY = Chunk.ToAreaMasterChunkPos(worldPos);
                //var areaMasterChunkXY = new Vector3i(World.toChunkXZ(worldPos.x) / 5 * 5, World.toChunkY(worldPos.y), World.toChunkXZ(worldPos.z) / 5 * 5);
                SdtdConsole.Instance.Output($"Area master chunk (x z): {areaMasterChunkXY.x} {areaMasterChunkXY.z}");
                var araeMasterChunk = world.GetChunkSync(areaMasterChunkXY.x, areaMasterChunkXY.z) as Chunk;
                if (araeMasterChunk != null && araeMasterChunk.ChunkCustomData.Count > 0)
                    SdtdConsole.Instance.Output(DumpCustomChunkData(araeMasterChunk));

                var regionXZ = ChunkTools.ChunkXZToRegionXZ(chunkXZ);
                SdtdConsole.Instance.Output($"Region file: r.{regionXZ.x}.{regionXZ.z}.7rg");
            }
            catch (Exception ex)
            {
                CommandTools.HandleCommandException(ex);
            }
        }

        public static string DumpCustomChunkData(Chunk chunk)
        {
            var str = new StringBuilder();
            foreach (var key in chunk.ChunkCustomData.Keys)
            {
                var ccd = chunk.ChunkCustomData[key];
                str.Append($"    ChunkCustomData[{key}]=");
                if (key == "bspd.main")
                    str.AppendLine(chunk.GetChunkBiomeSpawnData().ToString());
                else
                    str.AppendLine(ccd.data.Length + " bytes, starting with: " + Encoding.Default.GetString(ccd.data).Substring(0, 10));
                var expires = ccd.expiresInWorldTime == ulong.MaxValue ? "never" : "in " + (ccd.expiresInWorldTime - GameManager.Instance.World.worldTime) + " ticks";
                str.AppendLine($"        expires {expires}");
                str.AppendLine($"        is{(ccd.isSavedToNetwork ? "" : " not")} saved to network");
            } 
            return str.ToString().TrimEnd();
        }
    }
}
