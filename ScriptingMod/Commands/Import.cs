using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using JetBrains.Annotations;
using ScriptingMod.Exceptions;
using ScriptingMod.Extensions;
using ScriptingMod.Managers;
using Enumerable = UniLinq.Enumerable;

namespace ScriptingMod.Commands
{
    /*
     * TODO [P3]: Save prefabs in a subdirectory, but somehow allow also to load standard prefabs. (or use default file name prefix)
     * TODO [P3]: If a bed is overwritten during import, remove the player's home point
     * TODO [P3]: Also reload adjacent chunks to fix terrain height gaps
     * TODO [P3]: Enable/fix importing of spawners
     * TODO [P2]: Replace loot placeholder blocks according to loot.xml
     * TODO [P2]: Allow changing the directon into which the prefab is loaded (other north/east/up)
     */

    public class Import : ConsoleCmdAbstract
    {
        private static FieldInfo _wireChildrenField; // TileEntityPowered -> private List<Vector3i> ADD
        private static FieldInfo _wireParentField; // TileEntityPowered -> private Vector3i IDD

        static Import()
        {
            try
            {
                // Get references to private fields/methods/types by their signatures,
                // because the internal names change on every 7DTD release due to obfuscation.
                Log.Debug("Getting private field from TileEntityPowered: private List<Vector3i> ADD ...");
                _wireChildrenField = typeof(TileEntityPowered).GetFieldsByType(typeof(List<Vector3i>)).Single();

                Log.Debug("Getting private field from TileEntityPowered: private Vector3i IDD ...");
                _wireParentField = typeof(TileEntityPowered).GetFieldsByType(typeof(Vector3i)).Single();
            }
            catch (Exception ex)
            {
                Log.Error("Error while establishing references to 7DTD's \"private parts\". Your game version might not be compatible with this Scripting Mod version." + Environment.NewLine + ex);
                throw;
            }

            Log.Debug(typeof(Import) + " established reflection references.");
        }

        public override string[] GetCommands()
        {
            return new[] {"dj-import"};
        }

        public override string GetDescription()
        {
            return "Imports a prefab, optionally including all container content, sign texts, ownership, etc.";
        }

        public override string GetHelp()
        {
            // ----------------------------------(max length: 100 char)--------------------------------------------|
            return $@"
                Imports a prefab from the folder /Data/Prefabs into the world. With the optional parameter ""/all""
                additional block metadata like container content, sign texts, ownership, etc. is also restored. For
                this to work the prefab must be exported with dj-export and include a ""tile entity"" file ({Export.TileEntityFileExtension}).
                The prefab is placed facing north/east/up from the given position.
                Rotation can be 0 = unmodified, 1 = 90° right, 2 = 180°, 3 = 270° right.
                Usage:
                    1. dj-import [/all] <name>
                    2. dj-import [/all] <name> <rotation>
                    3. dj-import [/all] <name> <x> <y> <z>
                    4. dj-import [/all] <name> <x> <y> <z> <rotation>
                1. Imports the prefab at the current position.
                2. Imports the prefab at the current position with given rotation.
                3. Imports the prefab at the given position.
                4. Imports the prefab at the given position with the given rotation.
                ".Unindent();
        }

        public override void Execute(List<string> paramz, CommandSenderInfo senderInfo)
        {
            HashSet<Chunk> affectedChunks = null;
            try
            {
                (string prefabName, Vector3i pos1, int rotate, bool all) = ParseParams(paramz, senderInfo);

                // Will not do anything if chunks are not loaded; so no need to pre-check
                LoadPrefab(prefabName, pos1, rotate, out Vector3i pos2);
                affectedChunks = GetAffectedChunks(pos1, pos2);

                if (all)
                {
                    LoadTileEntities(prefabName, pos1, pos2, rotate);
                }

                SdtdConsole.Instance.Output($"Prefab {prefabName} placed{(all ? " with block metdata" : "")} at {pos1} with rotation {rotate}.");
            }
            catch (Exception ex)
            {
                CommandManager.HandleCommandException(ex);
            }

            // Error could have happened after prefab load, so we must reset/reload regardless
            try
            {
                if (affectedChunks != null)
                {
                    Managers.ChunkManager.ResetStability(affectedChunks);
                    Managers.ChunkManager.ReloadForClients(affectedChunks);
                }
            }
            catch (Exception ex)
            {
                CommandManager.HandleCommandException(ex);
            }
        }

        private static (string prefabName, Vector3i pos1, int rotate, bool all)
            ParseParams(List<string> paramz, CommandSenderInfo senderInfo)
        {
            var all = false;

            // Parse /all parameter
            if (paramz.Contains("/all"))
            {
                // Make copy and remove from list to make subsequent parsing easier
                paramz = new List<string>(paramz);
                paramz.Remove("/all");
                all = true;
            }

            // Parse prefab name
            var prefabName = paramz[0];

            // Verify existence of prefab files
            const string tts = global::Constants.cExtPrefabs; // Cannot interpolate in string: https://youtrack.jetbrains.com/issue/RSRP-465524
            if (!File.Exists(Path.Combine(Constants.PrefabsFolder, prefabName + ".xml")) || 
                !File.Exists(Path.Combine(Constants.PrefabsFolder, prefabName + tts)))
                throw new FriendlyMessageException($"Could not find {prefabName}.xml/{tts} in {Constants.PrefabsFolder}.");

            // Verify existence and validity of tile entity file
            if (all)
            {
                var fileName = prefabName + Export.TileEntityFileExtension;
                var filePath = Path.Combine(Constants.PrefabsFolder, fileName);
                if (!File.Exists(filePath))
                    throw new FriendlyMessageException($"Could not find {fileName} in prefabs folder. This prefab does not have block metadata available, so you cannot use the /all option.");

                using (var reader = new BinaryReader(new FileStream(filePath, FileMode.Open)))
                {
                    VerifyTileEntityHeader(reader, fileName);
                }
            }

            // Parse coordinates
            Vector3i pos1;
            if (paramz.Count == 1 || paramz.Count == 2)
            {
                pos1 = PlayerManager.GetPosition(senderInfo);
            }
            else
            {
                try
                {
                    pos1 = new Vector3i(int.Parse(paramz[1]), int.Parse(paramz[2]), int.Parse(paramz[3]));
                }
                catch (Exception)
                {
                    throw new FriendlyMessageException("At least one of the given coordinates is not a valid integer.");
                }
            }

            // Parse rotation
            var rotate   = 0;
            if (paramz.Count == 2 || paramz.Count == 5)
            {
                rotate = paramz[paramz.Count - 1].ToInt()
                    ?? throw new FriendlyMessageException("Rotation value is not valid. Allowed values: 0, 1, 2, or 3");
            }

            return (prefabName, pos1, rotate, all);
        }

        [NotNull]
        private static HashSet<Chunk> GetAffectedChunks(Vector3i pos1, Vector3i pos2)
        {
            var world = GameManager.Instance.World;
            var affectedChunks = new HashSet<Chunk>();
            for (int x = pos1.x; x <= pos2.x; x++)
            {
                for (int z = pos1.z; z <= pos2.z; z++)
                {
                    var chunk = world.GetChunkFromWorldPos(x, 0, z) as Chunk;
                    if (chunk == null)
                        throw new FriendlyMessageException("Area to export is too far away. Chunk not loaded on that area.");

                    affectedChunks.Add(chunk);
                }
            }
            return affectedChunks;
        }

        private static void LoadPrefab(string prefabName, Vector3i pos1, int rotate, out Vector3i pos2)
        {
            Prefab prefab = new Prefab();
            if (!prefab.Load(prefabName))
                throw new FriendlyMessageException($"Prefab {prefabName} could not be loaded.");

            prefab.bCopyAirBlocks = true;
            prefab.bCopyAirBlocks = true;
            // DO NOT USE! Saving with this crashes the server.
            //prefab.bSleeperVolumes = true;

            for (int i = 0; i < rotate; i++)
                prefab.RotateY(false);

            prefab.CopyIntoLocal(GameManager.Instance.World.ChunkCache, new Vector3i(pos1.x, pos1.y, pos1.z), true, true);

            // Return for later
            pos2.x = pos1.x + prefab.size.x - 1;
            pos2.y = pos1.y + prefab.size.y - 1;
            pos2.z = pos1.z + prefab.size.z - 1;

            Log.Out($"Imported prefab {prefabName} into area {pos1} to {pos2}.");
        }

        //private void ReadInt32(byte[] buffer, int offset, int value)
        //{
        //    Log.Debug($"Modifying tile entity read buffer. Returning {value} at position {Position - 4} ...");
        //    buffer[offset + 0] = (byte)value;
        //    buffer[offset + 1] = (byte)(value >> 8);
        //    buffer[offset + 2] = (byte)(value >> 16);
        //    buffer[offset + 3] = (byte)(value >> 24);
        //}

        // File format expected:
        // 0..1     [UInt16] tile entity format version
        // 2..5     [Int32]  TileEntity.localChunkPos.x
        // 6..9     [Int32]  TileEntity.localChunkPos.y
        // 10..13   [Int32]  TileEntity.localChunkPos.x


        private static void LoadTileEntities(string prefabName, Vector3i pos1, Vector3i pos2, int rotate)
        {
            var filePath     = Path.Combine(Constants.PrefabsFolder, prefabName + Export.TileEntityFileExtension);
            var world        = GameManager.Instance.World;
            var tileEntities = new Dictionary<Vector3i, TileEntity>(); // posInWorld => TileEntity
            int tileEntitiyCount;

            // Read all tile entities from file into dictionary using a fake reader inbetween to allow modifying read data
            var fakeReader = new FakeDataStream(new FileStream(filePath, FileMode.Open));
            using (var reader = new BinaryReader(fakeReader))
            {
                // TODO: Check if TileEntity.entityId needs to be changed/recreated on read to avoid duplicate entityId's
                // TODO: Adjust localpos during read

                VerifyTileEntityHeader(reader, prefabName + Export.TileEntityFileExtension);

                var originalPos1 = NetworkUtils.ReadVector3i(reader);                       // [Vector3i] original area worldPos1
                var originalPos2 = NetworkUtils.ReadVector3i(reader);                       // [Vector3i] original area worldPos2

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

                    // Instruct the stream to fake the localChunkPos during read to use the new posInChunk instead.
                    // This is necessary because the localChunkPos is used IMMEDIATEY during read to initialize all other sorts of data,
                    // like creating item entities for items in powered blocks (see TileEntityPowerSource.read).
                    byte[] fakeBytes = new byte[3 * sizeof(int)];   // for Vector3i = x, y, z
                    NetworkUtils.Write(new BinaryWriter(new MemoryStream(fakeBytes)), posInChunk);
                    fakeReader.FakeRead(fakeBytes, sizeof(ushort)); // delay for reading file version in TileEntity.read;

                    tileEntity.read(reader, TileEntity.StreamModeRead.Persistency);         // [dynamic] tile entity data depending on type

                    if (tileEntity.localChunkPos != posInChunk)
                        Log.Warning($"Tile entity {tileEntity} should have localChunkPos {posInChunk} but has {tileEntity.localChunkPos} instead!");

                    AdjustTileEntitiy(tileEntity, posInPrefab, pos1, originalPos1);

                    var tileEntityPowered = tileEntity as TileEntityPowered;
                    if (tileEntityPowered != null)                                          // [bool] has power item
                    {

                        var powerItemVersion = reader.ReadByte();
                        // TileEntityPowered.read creates and adds an empty PowerItem already
                        var powerItem = tileEntityPowered.GetPowerItem();

                        Log.Debug("Power Manager status BEFORE importing power item of tile entity " + tileEntity + ":");
                        new Dump().Execute(null, new CommandSenderInfo());

                        powerItem.read(reader, powerItemVersion);                           // [dynamic] power item

                        // Adjust position of power item in world
                        powerItem.Position = posInWorld;

                        // NOTES!
                        // Only parent is set in powerItem, not child
                        // Wire up all parents/childs
                        // does read() already overwrite some existing poweritem with the original world location?


                        // Not necessary for THIS poweritem, because TileEntity.read already attached it
                        //// Attach power item back to tile entity; this implicitly calls TileEntity.CreateWireDataFromPowerItem(),
                        //// which recreates all wire data, so we don't need to adjust wire data position manually! 
                        //powerItem.TileEntity = null;
                        //powerItem.AddTileEntity(tileEntityPowered);

                        Log.Debug("Power Manager status AFTER importing power item of tile entity " + tileEntity + ":");
                        new Dump().Execute(null, new CommandSenderInfo());
                    }

                    // Check cannot be done earlier, because we MUST do the file reads regardless in order to continue reading
                    if (chunk == null)
                    {
                        Log.Warning($"Could not import tile entity for block {posInWorld} because the chunk is not loaded.");
                        continue;
                    }

                    tileEntities.Add(posInWorld, tileEntity);
                }
            }

            // Go through every block and replace tile entities
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
                        // Remove default empty tile entity from prefab
                        chunk.RemoveTileEntityAt<TileEntity>(world, World.toBlock(x, y, z));

                        // Add previously loaded tile entity if we have one
                        var tileEntity = tileEntities.GetValue(new Vector3i(x, y, z));
                        if (tileEntity != null)
                            chunk.AddTileEntity(tileEntity);
                    }
                }
            }

            Log.Out($"Imported {tileEntitiyCount} tile entities for prefab {prefabName} into area {pos1} to {pos2}.");
        }

        private static void AdjustTileEntitiy(TileEntity tileEntity, Vector3i posInPrefab, Vector3i pos1, Vector3i originalPos1)
        {
            // No need to adjust anything if TE stays at same position
            if (pos1 == originalPos1)
                return;

            // Adjust wire endpoints from original worldPos to new worldPos
            var posDelta = pos1 - originalPos1;
            var tileEntityPowered = tileEntity as TileEntityPowered;
            if (tileEntityPowered != null)
            {
                // Adjust child wires
                List<Vector3i> wireChildren = (List<Vector3i>)_wireChildrenField.GetValue(tileEntityPowered) ?? new List<Vector3i>();
                for (int i = 0; i < wireChildren.Count; i++)
                {
                    Log.Debug($"Changing child wire on block {pos1 + posInPrefab} from {wireChildren[i]} to {wireChildren[i] + posDelta}");
                    wireChildren[i] += posDelta;
                }

                // Adjust parent wire
                var parentWire = (Vector3i) _wireParentField.GetValue(tileEntityPowered);
                if (parentWire != new Vector3i(-9999, -9999, -9999))
                {
                    Log.Debug($"Changing parent wire on block {pos1 + posInPrefab} from {parentWire} to {parentWire + posDelta}");
                    _wireParentField.SetValue(tileEntityPowered, parentWire + posDelta);
                }
            }
        }

        /// <summary>
        /// Reads and verifies the tile entity file's header. On errors, an exception is thrown.
        /// The readerposition is advanced in the file accordingly.
        /// </summary>
        /// <param name="reader">Already opened reader stream</param>
        /// <param name="fileName">File name of tile entity file without path, e.g. "mybase.te"</param>
        /// <exception cref="FriendlyMessageException">Thrown when the header is incompatible with the import function</exception>
        private static void VerifyTileEntityHeader(BinaryReader reader, string fileName)
        {
            var fileMarker = reader.ReadString();
            if (fileMarker != Export.TileEntityFileMarker)                           // [string]  constant "7DTD-TE"
                throw new FriendlyMessageException($"File {fileName} is not a valid tile entity file for 7DTD.");

            var fileVersion = reader.ReadInt32();                                       // [Int32]   file version number
            if (fileVersion != Export.TileEntityFileVersion)
                throw new FriendlyMessageException($"File format version of {fileName} is {fileVersion} but only {Export.TileEntityFileVersion} is supported.");
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
