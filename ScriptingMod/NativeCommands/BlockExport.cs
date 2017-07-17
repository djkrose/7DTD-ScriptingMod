using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ScriptingMod.NativeCommands
{
    public class BlockExport : ConsoleCmdAbstract
    {
        public class Params
        {
            public string fileName;
            public int x1, y1, z1, x2, y2, z2;
        }

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
                   "additional .te file next to the prefab .tts and .xml.\r\n" +
                   "Usage:\r\n" +
                   "  1. block-export <x1> <y1> <z1> <x2> <y2> <z2>\r\n" +
                   "\r\n" +
                   "1. Exports everything within the 3-dimensional area from x1 y1 z1 to x2 y2 z2.";
        }

        public override void Execute(List<string> paramz, CommandSenderInfo senderInfo)
        {
            try
            {
                Params p = ParseParams(paramz, senderInfo);
                FixOrder(p);
                SavePrefab(p);
                SaveTileEntities(p);
                SdtdConsole.Instance.Output($"Prefab {p.fileName} exported. Area mapped from {p.x1} {p.y1} {p.z1} to {p.x2} {p.y2} {p.z2}.");
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

        private static Params ParseParams(List<string> paramz, CommandSenderInfo senderInfo)
        {
            var p = new Params();

            p.fileName = paramz[0];

            bool parseSuccess =
                int.TryParse(paramz[1], out p.x1) &&
                int.TryParse(paramz[2], out p.y1) &&
                int.TryParse(paramz[3], out p.z1) &&

                int.TryParse(paramz[4], out p.x2) &&
                int.TryParse(paramz[5], out p.y2) &&
                int.TryParse(paramz[6], out p.z2);

            if (!parseSuccess)
                throw new FriendlyMessageException("At least one of the given coordinates is not valid.");

            return p;
        }

        private static void SavePrefab(Params p)
        {
            var size = new Vector3i(p.x2 - p.x1 + 1, p.y2 - p.y1 + 1, p.z2 - p.z1 + 1);
            var prefab = new Prefab(size);
            prefab.CopyFromWorld(GameManager.Instance.World, new Vector3i(p.x1, p.y1, p.z1), new Vector3i(p.x2, p.y2, p.z2));
            prefab.bCopyAirBlocks = true;
            // DO NOT SET THIS! It crashes the server!
            //prefab.bSleeperVolumes = true;
            prefab.filename = p.fileName;
            prefab.addAllChildBlocks();
            prefab.Save(p.fileName);
            Log.Out($"Exported prefab from area {p.x1} {p.y1} {p.z1} to {p.x2} {p.y2} {p.z2} into {p.fileName}.tts/.xml");
        }

        private static void SaveTileEntities(Params p)
        {
            var world = GameManager.Instance.World;

            // Collect all tile entities in the area
            var tileEntities = new Dictionary<Vector3i, TileEntity>(); // posInWorld => TileEntity
            for (int x = p.x1; x <= p.x2; x++)
            {
                for (int z = p.z1; z <= p.z2; z++)
                {
                    Chunk chunk = (Chunk)world.GetChunkFromWorldPos(x, 0, z);
                    if (chunk == null)
                        throw new FriendlyMessageException("The area to export is far away. Chunk not loaded on that area.");

                    for (int y = p.y1; y <= p.y2; y++)
                    {
                        var posInChunk = World.toBlock(x, y, z);
                        var posInWorld = new Vector3i(x, y, z);
                        TileEntity tileEntity = chunk.GetTileEntity(posInChunk);
                        if (tileEntity != null)
                        {
                            tileEntities.Add(posInWorld, tileEntity);
                            Log.Debug($"Exported tile entity: {tileEntity}\r\n" +
                                      $"posInChunk:  {posInChunk.x} {posInChunk.y} {posInChunk.z}\r\n" +
                                      $"posInWorld:  {x} {y} {z}");
                        }
                    }
                }
            }

            // Save all tile entities
            var filePath = Utils.GetGameDir("Data/Prefabs/") + p.fileName + ".te";
            using (var writer = new BinaryWriter(new FileStream(filePath, FileMode.Create)))
            {
                // see Assembly-CSharp::Chunk.write() -> search "tileentity.write"
                writer.Write(tileEntities.Count);                                           // [Int32]   number of tile entities
                foreach (var keyValue in tileEntities)
                {
                    var posInWorld  = keyValue.Key;
                    var tileEntity  = keyValue.Value;
                    var posInPrefab = new Vector3i(posInWorld.x - p.x1, posInWorld.y - p.y1, posInWorld.z - p.z1);
                    NetworkUtils.Write(writer, posInPrefab);                                // [3xInt32] position relative to prefab
                    writer.Write((int)tileEntity.GetTileEntityType());                      // [Int32]   TileEntityType enum
                    tileEntity.write(writer, TileEntity.StreamModeWrite.Persistency);       // [dynamic] tile entity data depending on type
                    Log.Debug($"Wrote tile entity: {tileEntity}\r\n" +
                              $"posInPrefab: {posInPrefab.x} {posInPrefab.y} {posInPrefab.z}\r\n" +
                              $"posInWorld:  {posInWorld.x} {posInWorld.y} {posInWorld.z}");
                }
            }

            Log.Out($"Exported {tileEntities.Count} tile entities from area {p.x1} {p.y1} {p.z1} to {p.x2} {p.y2} {p.z2} into {p.fileName}.te");
        }

        /// <summary>
        /// Fix the order of xyz1 xyz2, so that the first is always smaller or equal to the second.
        /// </summary>
        private static void FixOrder(Params p)
        {
            if (p.x2 < p.x1)
            {
                int val = p.x1;
                p.x1 = p.x2;
                p.x2 = val;
            }

            if (p.y2 < p.y1)
            {
                int val = p.y1;
                p.y1 = p.y2;
                p.y2 = val;
            }

            if (p.z2 < p.z1)
            {
                int val = p.z1;
                p.z1 = p.z2;
                p.z2 = val;
            }
        }

    }
}
