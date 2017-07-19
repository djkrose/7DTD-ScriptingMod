using System;
using System.Collections.Generic;
using System.IO;
using ScriptingMod.Exceptions;
using ScriptingMod.Extensions;
using ScriptingMod.Managers;

namespace ScriptingMod.Commands
{
    /*
     * TODO [P2]: Save my own current fileformat version with it and verify/adjust on import
     * TODO [P3]: Copy trader entity and protected area correctly
     */

    public class Export : ConsoleCmdAbstract
    {
        private static Dictionary<int, Vector3i> savedPos = new Dictionary<int, Vector3i>(); // entityId => position

        public override string[] GetCommands()
        {
            return new[] {"dj-export"};
        }

        public override string GetDescription()
        {
            return "Exports a prefab including all container content, sign texts, ownership, etc.";
        }

        public override string GetHelp()
        {
            // ----------------------------------(max length: 100 char)--------------------------------------------|
            return @"
                Exports an area as prefab into the /Data/Prefabs folder. Additional block metadata like container
                content, sign texts, ownership, etc. is stored in a separate ""tile entity"" file (.te).
                Usage:
                    1. dj-export <name> <x1> <y1> <z1> <x2> <y2> <z2>
                    2. dj-export
                    3. dj-export <name>
                1. Exports the prefab within the area from x1 y1 z1 to x2 y2 z2.
                2. Saves the current player's position for usage 3.
                3. Exports the prefab within the area from the saved position to current players position.
                ".Unindent();
        }

        public override void Execute(List<string> paramz, CommandSenderInfo senderInfo)
        {
            try
            {
                (string fileName, Vector3i pos1, Vector3i pos2) = ParseParams(paramz, senderInfo);
                FixOrder(ref pos1, ref pos2);
                SavePrefab(fileName, pos1, pos2);
                SaveTileEntities(fileName, pos1, pos2);

                SdtdConsole.Instance.Output($"Prefab {fileName} with block metadata exported. Area mapped from {pos1} to {pos2}.");
            }
            catch (FriendlyMessageException ex)
            {
                SdtdConsole.Instance.Output(ex.Message);
            }
            catch (Exception ex)
            {
                SdtdConsole.Instance.Output("Error occured during command execution: " + ex.Message + " [details in server log]");
                Log.Exception(ex);
            }
        }

        private static (string fileName, Vector3i pos1, Vector3i pos2) ParseParams(List<string> paramz, CommandSenderInfo senderInfo)
        {
            if (paramz.Count == 0)
            {
                var ci = PlayerManager.GetClientInfo(senderInfo);
                savedPos[ci.entityId] = PlayerManager.GetPosition(ci);
                throw new FriendlyMessageException("Your current position was saved: " + savedPos[ci.entityId]);
            }

            var fileName = paramz[0];
            Vector3i pos1, pos2;

            if (paramz.Count == 1)
            {
                var ci = PlayerManager.GetClientInfo(senderInfo);
                if (!savedPos.ContainsKey(ci.entityId))
                    throw new FriendlyMessageException("Please save start point of the area first. See help for details.");
                pos1 = savedPos[ci.entityId];
                pos2 = PlayerManager.GetPosition(ci);
            }
            else
            {
                try {
                    pos1 = new Vector3i(int.Parse(paramz[1]), int.Parse(paramz[2]), int.Parse(paramz[3]));
                    pos2 = new Vector3i(int.Parse(paramz[4]), int.Parse(paramz[5]), int.Parse(paramz[6]));
                } catch (Exception) {
                    throw new FriendlyMessageException("At least one of the given coordinates is not a valid integer.");
                }
            }

            return (fileName, pos1, pos2);
        }

        private static void SavePrefab(string fileName, Vector3i pos1, Vector3i pos2)
        {
            var size = new Vector3i(pos2.x - pos1.x + 1, pos2.y - pos1.y + 1, pos2.z - pos1.z + 1);
            var prefab = new Prefab(size);
            prefab.CopyFromWorld(GameManager.Instance.World, new Vector3i(pos1.x, pos1.y, pos1.z), new Vector3i(pos2.x, pos2.y, pos2.z));
            prefab.bCopyAirBlocks = true;
            // DO NOT SET THIS! It crashes the server!
            //prefab.bSleeperVolumes = true;
            prefab.filename = fileName;
            prefab.addAllChildBlocks();
            prefab.Save(fileName);
            Log.Out($"Exported prefab {fileName} from area {pos1} to {pos2}.");
        }

        private static void SaveTileEntities(string fileName, Vector3i pos1, Vector3i pos2)
        {
            var world = GameManager.Instance.World;
            var tileEntities = new Dictionary<Vector3i, TileEntity>(); // posInWorld => TileEntity

            // Collect all tile entities in the area
            for (int x = pos1.x; x <= pos2.x; x++)
            {
                for (int z = pos1.z; z <= pos2.z; z++)
                {
                    var chunk = world.GetChunkFromWorldPos(x, 0, z) as Chunk;
                    if (chunk == null)
                        throw new FriendlyMessageException("Area to export is too far away. Chunk not loaded on that area.");

                    for (int y = pos1.y; y <= pos2.y; y++)
                    {
                        var posInChunk = World.toBlock(x, y, z);
                        var posInWorld = new Vector3i(x, y, z);
                        var tileEntity = chunk.GetTileEntity(posInChunk);

                        if (tileEntity == null)
                            continue; // so container/door/sign block

                        tileEntities.Add(posInWorld, tileEntity);
                    }
                }
            }

            var filePath = Utils.GetGameDir("Data/Prefabs/") + fileName + ".te";

            // Save all tile entities
            using (var writer = new BinaryWriter(new FileStream(filePath, FileMode.Create)))
            {
                // see Assembly-CSharp::Chunk.write() -> search "tileentity.write"
                writer.Write(tileEntities.Count); // [Int32]   number of tile entities
                foreach (var keyValue in tileEntities)
                {
                    var posInWorld = keyValue.Key;
                    var tileEntity = keyValue.Value;
                    var posInPrefab = new Vector3i(posInWorld.x - pos1.x, posInWorld.y - pos1.y, posInWorld.z - pos1.z);

                    NetworkUtils.Write(writer, posInPrefab); // [3xInt32] position relative to prefab
                    writer.Write((int) tileEntity.GetTileEntityType()); // [Int32]   TileEntityType enum
                    tileEntity.write(writer, TileEntity.StreamModeWrite.Persistency); // [dynamic] tile entity data depending on type
                }
            }

            Log.Out($"Exported {tileEntities.Count} tile entities for prefab {fileName} from area {pos1} to {pos2}.");
        }

        /// <summary>
        /// Fix the order of xyz1 xyz2, so that the first is always smaller or equal to the second.
        /// </summary>
        private static void FixOrder(ref Vector3i pos1, ref Vector3i pos2)
        {
            if (pos2.x < pos1.x)
            {
                int val = pos1.x;
                pos1.x = pos2.x;
                pos2.x = val;
            }

            if (pos2.y < pos1.y)
            {
                int val = pos1.y;
                pos1.y = pos2.y;
                pos2.y = val;
            }

            if (pos2.z < pos1.z)
            {
                int val = pos1.z;
                pos1.z = pos2.z;
                pos2.z = val;
            }
        }
    }
}