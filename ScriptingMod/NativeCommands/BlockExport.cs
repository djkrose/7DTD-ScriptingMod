using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ScriptingMod.NativeCommands
{
    public class BlockExport : ConsoleCmdAbstract
    {

        public override string[] GetCommands()
        {
            return new [] {"block-export"};
        }

        public override string GetDescription()
        {
            return "Exports an area of blocks including all container and ownership data.";
        }

        public override string GetHelp()
        {
            return "Exports an area into the /Data/Prefabs folder including all container and ownership data by using an\r\n" +
                   "additional .te file next to the prefab .tts.\r\n" +
                   "Usage:\r\n" +
                   "  1. block-export <x1> <y1> <z1> <x2> <y2> <z2>\r\n" +
                   "\r\n" +
                   "1. Exports everything within the 3-dimensional area from x1 y1 z1 to x2 y2 z2.";
        }

        public override void Execute(List<string> paramz, CommandSenderInfo senderInfo)
        {
            try
            {
                (string fileName, Vector3i pos1, Vector3i pos2) = ParseParams(paramz, senderInfo);
                FixOrder(ref pos1, ref pos2);
                SavePrefab(fileName, pos1, pos2);
                SaveTileEntities(fileName, pos1, pos2);

                SdtdConsole.Instance.Output($"Prefab {fileName} exported. Area mapped from {pos1} to {pos2}.");
            }
            catch (FriendlyMessageException ex)
            {
                SdtdConsole.Instance.Output(ex.Message);
            }
            catch (Exception ex)
            {
                SdtdConsole.Instance.Output("An error occured during command execution: " + ex.Message + " [details in server log]");
                Log.Exception(ex);
            }
        }

        private static (string fileName, Vector3i pos1, Vector3i pos2) ParseParams(List<string> paramz, CommandSenderInfo senderInfo)
        {
            var fileName = paramz[0];
            var pos1 = Vector3i.zero;
            var pos2 = Vector3i.zero;

            bool parseSuccess =
                int.TryParse(paramz[1], out pos1.x) &&
                int.TryParse(paramz[2], out pos1.y) &&
                int.TryParse(paramz[3], out pos1.z) &&

                int.TryParse(paramz[4], out pos2.x) &&
                int.TryParse(paramz[5], out pos2.y) &&
                int.TryParse(paramz[6], out pos2.z);

            if (!parseSuccess)
                throw new FriendlyMessageException("At least one of the given coordinates is not valid.");

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
            Log.Out($"Exported prefab from area {pos1} to {pos2} into {fileName}.tts");
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
                        throw new FriendlyMessageException("The area to export is too far away. Chunk not loaded on that area.");

                    for (int y = pos1.y; y <= pos2.y; y++)
                    {
                        var posInChunk = World.toBlock(x, y, z);
                        var posInWorld = new Vector3i(x, y, z);
                        var tileEntity = chunk.GetTileEntity(posInChunk);

                        if (tileEntity == null)
                            continue; // so container/door/sign block

                        tileEntities.Add(posInWorld, tileEntity);
                        //Log.Debug($"Exported tile entity: {tileEntity}\r\n" +
                        //          $"posInChunk:  {posInChunk.x} {posInChunk.y} {posInChunk.z}\r\n" +
                        //          $"posInWorld:  {x} {y} {z}");
                    }
                }
            }

            var filePath = Utils.GetGameDir("Data/Prefabs/") + fileName + ".te";

            // Save all tile entities
            using (var writer = new BinaryWriter(new FileStream(filePath, FileMode.Create)))
            {
                // see Assembly-CSharp::Chunk.write() -> search "tileentity.write"
                writer.Write(tileEntities.Count);                                           // [Int32]   number of tile entities
                foreach (var keyValue in tileEntities)
                {
                    var posInWorld  = keyValue.Key;
                    var tileEntity  = keyValue.Value;
                    var posInPrefab = new Vector3i(posInWorld.x - pos1.x, posInWorld.y - pos1.y, posInWorld.z - pos1.z);

                    NetworkUtils.Write(writer, posInPrefab);                                // [3xInt32] position relative to prefab
                    writer.Write((int)tileEntity.GetTileEntityType());                      // [Int32]   TileEntityType enum
                    tileEntity.write(writer, TileEntity.StreamModeWrite.Persistency);       // [dynamic] tile entity data depending on type
                    //Log.Debug($"Wrote tile entity: {tileEntity}\r\n" +
                    //          $"posInPrefab: {posInPrefab.x} {posInPrefab.y} {posInPrefab.z}\r\n" +
                    //          $"posInWorld:  {posInWorld.x} {posInWorld.y} {posInWorld.z}");
                }
            }

            Log.Out($"Exported {tileEntities.Count} tile entities from area {pos1} to {pos2} into {fileName}.te");
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
