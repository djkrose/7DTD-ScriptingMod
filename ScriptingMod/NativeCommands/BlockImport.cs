using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ScriptingMod.NativeCommands
{
    // TODO [P3]: Allow omitting the x y z to import prefab at previous position. Previous position must be saved for this first!

    // TODO [P1]: Fix reloading chunks after import
    // TODO [P1]: Fix stability 
    // TODO [P1]: Fix tile entities
    public class BlockImport : ConsoleCmdAbstract
    {
        public class Params
        {
            public string fileName;
            public int x, y, z; // Start position from where prefab is imported in direction North-East
            public int rotate; // 0 = unmodified, 1 = 90° right, 2 = 180°, 3 = 270° right

            // Only internally used:
            internal Vector3i size;
            internal int x2, y2, z2;
        }

        public override string[] GetCommands()
        {
            return new[] {"block-import"};
        }

        public override string GetDescription()
        {
            return "Imports an area of blocks including all container and ownership data.";
        }

        public override string GetHelp()
        {
            return "Imports an area previously exported with block-export, including all container and ownership data\r\n" +
                   "that was saved in the additional .te file next to the prefab .tts and .xml. The area is always loaded\r\n" +
                   "from the given position to the direction Noth-East, e.g. the position is the most eastern southern point." +
                   "Usage:\r\n" +
                   "  1. block-import <x> <y> <z>\r\n" +
                   "  2. block-import <x> <y> <z> <rotation>\r\n" +
                   "\r\n" +
                   "1. Imports the area without modifying it's rotation.\r\n" +
                   "2. Imports the area rotated: 0 = unmodified, 1 = 90° right, 2 = 180°, 3 = 270° right";
        }

        public override void Execute(List<string> paramz, CommandSenderInfo senderInfo)
        {
            try
            {
                Params p = ParseParams(paramz, senderInfo);
                // TODO: Either switch the following (TEST!) or pre-check if chunks are loaded
                LoadPrefab(p);
                LoadTileEntities(p);

                // Affected chunks
                var world = GameManager.Instance.World;
                HashSet<Chunk> affectedChunks = new HashSet<Chunk>();
                for (int x = p.x; x < p.x + p.size.x; x++)
                {
                    for (int z = p.z; z < p.z + p.size.z; z++)
                    {
                        Chunk chunk = (Chunk) world.GetChunkFromWorldPos(x, 0, z);
                        affectedChunks.Add(chunk);
                    }
                }

                Log.Debug("Reloading stability...");
                ScriptingMod.Managers.ChunkManager.ResetStability(affectedChunks);
                Log.Debug("Sending chunks to client...");
                ScriptingMod.Managers.ChunkManager.ReloadForClients(affectedChunks);

                SdtdConsole.Instance.Output($"Prefab {p.fileName} imported. Area placed at {p.x} {p.y} {p.z} with rotation {p.rotate}.");
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
                int.TryParse(paramz[1], out p.x) &&
                int.TryParse(paramz[2], out p.y) &&
                int.TryParse(paramz[3], out p.z);

            if (paramz.Count == 5)
                parseSuccess = parseSuccess && int.TryParse(paramz[4], out p.rotate);

            if (!parseSuccess)
                throw new FriendlyMessageException("At least one of the given coordinates or the rotation value is not valid.");

            return p;
        }

        private static void LoadPrefab(Params p)
        {
            Prefab prefab = new Prefab();
            if (!prefab.Load(p.fileName))
                throw new FriendlyMessageException($"Prefab {p.fileName} could not be loaded.");
            prefab.bCopyAirBlocks = true;
            // DO NOT USE!
            //prefab.bSleeperVolumes = true;

            for (int i = 0; i < p.rotate; i++)
                prefab.RotateY(false);

            prefab.CopyIntoLocal(GameManager.Instance.World.ChunkCache, new Vector3i(p.x, p.y, p.z), true, true);

            // Store for later
            p.size = prefab.size;
            p.x2 = p.x + p.size.x - 1;
            p.y2 = p.y + p.size.y - 1;
            p.z2 = p.z + p.size.z - 1;
            Log.Out($"Imported prefab into area {p.x} {p.y} {p.z} to {p.x2} {p.y2} {p.z2} from {p.fileName}.tts/.xml");
        }

        private static void LoadTileEntities(Params p)
        {
            var filePath = Utils.GetGameDir("Data/Prefabs/") + p.fileName + ".te";

            // Read all tile entities from file
            var world = GameManager.Instance.World;
            var tileEntities = new Dictionary<Vector3i, TileEntity>(); // posInWorld => TileEntity
            using (var reader = new BinaryReader(new FileStream(filePath, FileMode.Open)))
            {
                // See Assembly-CSharp::Chunk.read() -> search "tileentity.read"
                var tileEntitiyCount = reader.ReadInt32();                                // [Int32]   number of tile entities
                for (int i = 0; i < tileEntitiyCount; i++)
                {
                    var posInPrefab = NetworkUtils.ReadVector3i(reader);                  // [3xInt32] position relative to prefab
                    var posInWorld  = new Vector3i(p.x + posInPrefab.x, p.y + posInPrefab.y, p.z + posInPrefab.z);

                    // Must already be associated with a new chunk on read(..)
                    Chunk chunk = (Chunk)world.GetChunkFromWorldPos(posInWorld);
                    if (chunk == null)
                        throw new FriendlyMessageException("The area to export is far away. Chunk not loaded on that area.");

                    var tileEntityType = (TileEntityType)reader.ReadInt32();              // [Int32]   TileEntityType enum
                    TileEntity tileEntity = TileEntity.Instantiate(tileEntityType, chunk);
                    tileEntity.read(reader, TileEntity.StreamModeRead.Persistency);       // [dynamic] tile entity data depending on type
                    tileEntities.Add(posInWorld, tileEntity);

                    Log.Debug($"Read tile entity: {tileEntity}\r\n" +
                              $"posInPrefab: {posInPrefab.x} {posInPrefab.y} {posInPrefab.z}\r\n" +
                              $"posInWorld:  {posInWorld.x} {posInWorld.y} {posInWorld.z}");
                }
            }

            // Remove old and add new tile entities in the imported area
            for (int x = p.x; x < p.x + p.size.x; x++)
            {
                for (int z = p.z; z < p.z + p.size.z; z++)
                {
                    Chunk chunk = (Chunk)world.GetChunkFromWorldPos(x, 0, z);

                    for (int y = p.y; y < p.y + p.size.y; y++)
                    {
                        var posInChunk = World.toBlock(x, y, z);
                        var posInWorld = new Vector3i(x, y, z);

                        // Remove for this block
                        chunk.RemoveTileEntityAt<TileEntity>(world, posInChunk);
                        
                        // If existing, add our new one
                        if (tileEntities.TryGetValue(posInWorld, out TileEntity tileEntity))
                        {
                            tileEntity.localChunkPos = posInChunk; // adjust to new location
                            chunk.AddTileEntity(tileEntity);
                            Log.Debug($"Imported tile entity: {tileEntity}\r\n" +
                                      $"posInChunk:  {posInChunk.x} {posInChunk.y} {posInChunk.z}\r\n" +
                                      $"posInWorld:  {x} {y} {z}");
                        }
                    }
                }
            }

            Log.Out($"Imported tile entities into area {p.x} {p.y} {p.z} to {p.x2} {p.y2} {p.z2} from {p.fileName}.te");
        }

    }
}
