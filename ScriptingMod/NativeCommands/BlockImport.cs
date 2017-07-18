using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ScriptingMod.NativeCommands
{
    // TODO [P3]: Allow omitting the x y z to import prefab at previous position. Previous position must be saved for this first!
    // TODO [P2]: Tests outstanding:
    // [ ] Check that overwritten land claim blocks disappear and the protection with them correctly
    // [ ] Check that copied new land claim blocks found the claimed areas correctly
    // [ ] Check if overwritten beds are unsetting player's home point correctly
    // [ ] Check if imported beds are stored as new home point
    public class BlockImport : ConsoleCmdAbstract
    {

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
                   "that was saved in the additional .te file next to the prefab .tts. The area is always loaded from\r\n" +
                   "the given position to the direction Noth-East, e.g. the position is the most eastern southern point.\r\n" +
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
                (string fileName, Vector3i pos1, int rotate) = ParseParams(paramz, senderInfo);

                // TODO: Either switch the following (TEST!) or pre-check if chunks are loaded
                LoadPrefab(fileName, pos1, rotate, out Vector3i pos2);

                HashSet<Chunk> affectedChunks = GetAffectedChunks(pos1, pos2);
                RemoveTileEntities(pos1, pos2);
                LoadTileEntities(fileName, pos1, pos2, rotate);

                ScriptingMod.Managers.ChunkManager.ResetStability(affectedChunks);
                ScriptingMod.Managers.ChunkManager.ReloadForClients(affectedChunks);

                SdtdConsole.Instance.Output($"Prefab {fileName} imported. Area placed at {pos1} with rotation {rotate}.");
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

        private static (string fileName, Vector3i pos1, int rotate) ParseParams(List<string> paramz, CommandSenderInfo senderInfo)
        {
            var fileName = paramz[0];
            var pos1     = Vector3i.zero;
            var rotate   = 0;

            bool parseSuccess =
                int.TryParse(paramz[1], out pos1.x) &&
                int.TryParse(paramz[2], out pos1.y) &&
                int.TryParse(paramz[3], out pos1.z);

            if (paramz.Count == 5)
                parseSuccess = parseSuccess && int.TryParse(paramz[4], out rotate);

            if (!parseSuccess)
                throw new FriendlyMessageException("At least one of the given coordinates or the rotation value is not valid.");

            return (fileName, pos1, rotate);
        }

        private HashSet<Chunk> GetAffectedChunks(Vector3i pos1, Vector3i pos2)
        {
            var world = GameManager.Instance.World;
            var affectedChunks = new HashSet<Chunk>();
            for (int x = pos1.x; x <= pos2.x; x++)
            {
                for (int z = pos1.z; z <= pos2.z; z++)
                {
                    var chunk = world.GetChunkFromWorldPos(x, 0, z) as Chunk;
                    if (chunk == null)
                        throw new FriendlyMessageException("The area to export is too far away. Chunk not loaded on that area.");

                    affectedChunks.Add(chunk);
                }
            }
            return affectedChunks;
        }

        private static void LoadPrefab(string fileName, Vector3i pos1, int rotate, out Vector3i pos2)
        {
            Prefab prefab = new Prefab();
            if (!prefab.Load(fileName))
                throw new FriendlyMessageException($"Prefab {fileName} could not be loaded.");
            prefab.bCopyAirBlocks = true;

            // DO NOT USE!
            //prefab.bSleeperVolumes = true;

            for (int i = 0; i < rotate; i++)
                prefab.RotateY(false);

            prefab.CopyIntoLocal(GameManager.Instance.World.ChunkCache, new Vector3i(pos1.x, pos1.y, pos1.z), true, true);

            // Return for later
            pos2.x = pos1.x + prefab.size.x - 1;
            pos2.y = pos1.y + prefab.size.y - 1;
            pos2.z = pos1.z + prefab.size.z - 1;

            Log.Out($"Imported prefab into area {pos1} to {pos2} from {fileName}.tts");
        }

        /// <summary>
        /// Reoves all tile entities from blocks in the given area defined by the two points.
        /// </summary>
        /// <param name="pos1">Point South/West/Down in the area, i.e. smallest numbers</param>
        /// <param name="pos2">Point North/East/Up in the area, i.e. highest numbers</param>
        private static void RemoveTileEntities(Vector3i pos1, Vector3i pos2)
        {
            var world = GameManager.Instance.World;
            var countRemoved = 0;

            for (int x = pos1.x; x <= pos2.x; x++)
            {
                for (int z = pos1.z; z <= pos2.z; z++)
                {
                    var chunk = world.GetChunkFromWorldPos(x, 0, z) as Chunk;
                    if (chunk == null)
                    {
                        Log.Warning($"Could not remove tile entities from blocks {x}, {pos1.y} to {pos2.y}, {z} because the chunk is not loaded.");
                        continue;
                    }

                    for (int y = pos1.y; y <= pos2.y; y++)
                    {
                        var posInChunk = World.toBlock(x, y, z);
                        if (chunk.GetTileEntity(posInChunk) == null)
                            continue;

                        countRemoved++;
                        chunk.RemoveTileEntityAt<TileEntity>(world, posInChunk);
                    }
                }
            }
            Log.Out($"Removed {countRemoved} tile entities in area {pos1} to {pos2}.");
        }

        private static void LoadTileEntities(string fileName, Vector3i pos1, Vector3i pos2, int rotate)
        {
            var filePath = Utils.GetGameDir("Data/Prefabs/") + fileName + ".te";
            var world    = GameManager.Instance.World;
            int tileEntitiyCount;

            // Read all tile entities from file
            using (var reader = new BinaryReader(new FileStream(filePath, FileMode.Open)))
            {
                // See Assembly-CSharp::Chunk.read() -> search "tileentity.read"
                tileEntitiyCount = reader.ReadInt32();                                      // [Int32]   number of tile entities
                for (int i = 0; i < tileEntitiyCount; i++)
                {
                    var posInPrefab = NetworkUtils.ReadVector3i(reader);                    // [3xInt32] position relative to prefab
                    posInPrefab     = RotatePosition(posInPrefab, pos2 - pos1, rotate);

                    var posInWorld  = new Vector3i(pos1.x + posInPrefab.x, pos1.y + posInPrefab.y, pos1.z + posInPrefab.z);
                    var posInChunk  = World.toBlock(posInWorld);

                    var chunk = world.GetChunkFromWorldPos(posInWorld) as Chunk;

                    var tileEntityType = (TileEntityType)reader.ReadInt32();                // [Int32]   TileEntityType enum
                    TileEntity tileEntity = TileEntity.Instantiate(tileEntityType, chunk ?? new Chunk()); // read(..) fails if chunk is null
                    tileEntity.read(reader, TileEntity.StreamModeRead.Persistency);         // [dynamic] tile entity data depending on type
                    tileEntity.localChunkPos = posInChunk; // adjust to new location

                    // Check cannot be done earlier, because we MUST do the file reads regardless
                    if (chunk == null)
                    {
                        Log.Warning($"Could not import tile entity for block {posInWorld} because the chunk is not loaded.");
                        continue;
                    }

                    chunk.AddTileEntity(tileEntity);
                    //Log.Debug($"Imported tile entity: {tileEntity}\r\n" +
                    //          $"posInPrefab: {posInPrefab}\r\n" +
                    //          $"posInWorld:  {posInWorld}\r\n" +
                    //          $"posInChunk:  {posInChunk}");
                }
            }

            Log.Out($"Imported {tileEntitiyCount} tile entities into area {pos1} to {pos2} from {fileName}.te");
        }

        /// <summary>
        /// Returns rotated coordinates of the given position within an area of area1 to area2.
        /// Only x and z are rotated, y stays the same.
        /// </summary>
        /// <param name="pos">The point in the area to rotate</param>
        /// <param name="area1">Most South/West point of the area, e.g. the smaller integers</param>
        /// <param name="area2">Most North/East point of the area, e.g. the higher integers</param>
        /// <param name="rotate">0 = unmodified, 1 = 90° right, 2 = 180°, 3 = 270° right</param>
        /// <returns></returns>
        private static Vector3i RotatePosition(Vector3i pos, Vector3i area1, Vector3i area2, int rotate)
        {
            var dx = pos.x - area1.x;
            var dz = pos.z - area1.z;

            switch (rotate)
            {
                case 0: // 0°
                    return pos;
                case 1: // 90° right
                    return new Vector3i(area1.x + dz, pos.y, area2.z - dx);
                //return new Vector3i(pos.z, pos.y, area2.z - pos.x);
                case 2: // 180°
                    return new Vector3i(area2.x - dx, pos.y, area2.z - dz);
                //return new Vector3i(area2.x - pos.x, pos.y, area2.z - pos.z);
                case 3: // 270°
                    return new Vector3i(area2.x - dz, pos.y, area1.z + dx);
                //return new Vector3i(area2.x - pos.z, pos.y, pos.x);
                default:
                    throw new ArgumentException("Rotation must be either 0, 1, 2, or 3", nameof(rotate));
            }
        }

        /// <summary>
        /// Returns rotated coordinates of the given position within an area of 0,0,0 to pos2.
        /// Only x and z are rotated, y stays the same.
        /// </summary>
        /// <param name="pos">The point in the area to rotate</param>
        /// <param name="pos2">Most North/East point of the area, e.g. the higher integers</param>
        /// <param name="rotate">0 = unmodified, 1 = 90° right, 2 = 180°, 3 = 270° right</param>
        /// <returns></returns>
        private static Vector3i RotatePosition(Vector3i pos, Vector3i pos2, int rotate)
        {
            switch (rotate)
            {
                case 0: // 0°
                    return pos;
                case 1: // 90° right
                    return new Vector3i(pos.z, pos.y, pos2.z - pos.x);
                case 2: // 180°
                    return new Vector3i(pos2.x - pos.x, pos.y, pos2.z - pos.z);
                case 3: // 270°
                    return new Vector3i(pos2.x - pos.z, pos.y, pos.x);
                default:
                    throw new ArgumentException("Rotation must be either 0, 1, 2, or 3", nameof(rotate));
            }
        }

    }
}
