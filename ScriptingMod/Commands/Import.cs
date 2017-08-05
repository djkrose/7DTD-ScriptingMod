using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using ScriptingMod.Exceptions;
using ScriptingMod.Extensions;
using ScriptingMod.Managers;

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

    [UsedImplicitly]
    public class Import : ConsoleCmdAbstract
    {

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
            // Parse /all parameter
            var all = paramz.Remove("/all");

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
                        throw new FriendlyMessageException("Area to import is too far away. Chunk not loaded on that area.");

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

            prefab.RotateY(false, rotate);

            prefab.CopyIntoLocal(GameManager.Instance.World.ChunkCache, new Vector3i(pos1.x, pos1.y, pos1.z), true, true);

            // Return for later
            pos2.x = pos1.x + prefab.size.x - 1;
            pos2.y = pos1.y + prefab.size.y - 1;
            pos2.z = pos1.z + prefab.size.z - 1;

            Log.Out($"Imported prefab {prefabName} into area {pos1} to {pos2}.");
        }

        private static void LoadTileEntities(string prefabName, Vector3i pos1, Vector3i pos2, int rotate)
        {
            var filePath     = Path.Combine(Constants.PrefabsFolder, prefabName + Export.TileEntityFileExtension);
            var world        = GameManager.Instance.World;
            //var tileEntities = new Dictionary<Vector3i, TileEntity>(); // posInWorld => TileEntity
            int tileEntitiyCount;

            // Read all tile entities from file into dictionary using a fake reader inbetween to allow modifying read data
            var fakeReader = new FakeDataStream(new FileStream(filePath, FileMode.Open));
            using (var reader = new BinaryReader(fakeReader))
            {
                // File header
                var fileName = prefabName + Export.TileEntityFileExtension;
                var fileMarker = reader.ReadString();
                if (fileMarker != Export.TileEntityFileMarker)                              // [string]  constant "7DTD-TE"
                    throw new FriendlyMessageException($"File {fileName} is not a valid tile entity file for 7DTD.");

                var fileVersion = reader.ReadInt32();                                       // [Int32]   file version number
                if (fileVersion != Export.TileEntityFileVersion)
                    throw new FriendlyMessageException($"File format version of {fileName} is {fileVersion} but only {Export.TileEntityFileVersion} is supported.");

                var originalPos1 = NetworkUtils.ReadVector3i(reader);                       // [Vector3i] original area worldPos1
                var originalPos2 = NetworkUtils.ReadVector3i(reader);                       // [Vector3i] original area worldPos2
                var expectedSize = pos2 - pos1;
                var actualSize   = originalPos2 - originalPos1;
                var posDelta     = pos1 - originalPos1;

                if (actualSize != expectedSize)
                    throw new ApplicationException($"Dimensions of tile entitiy file ({actualSize}) does not match with expected dimensions ({expectedSize}).");

                // See Assembly-CSharp::Chunk.read() -> search "tileentity.read"
                tileEntitiyCount = reader.ReadInt32();                                      // [Int32]   number of tile entities
                for (int i = 0; i < tileEntitiyCount; i++)
                {
                    var posInPrefab = NetworkUtils.ReadVector3i(reader);                    // [3xInt32] position relative to prefab
                    posInPrefab     = RotatePosition(posInPrefab, pos2 - pos1, rotate);

                    var posInWorld  = new Vector3i(pos1.x + posInPrefab.x, pos1.y + posInPrefab.y, pos1.z + posInPrefab.z);
                    var posInChunk  = World.toBlock(posInWorld);
                    var chunk       = world.GetChunkFromWorldPos(posInWorld) as Chunk;
                    if (chunk == null)
                        throw new FriendlyMessageException("Area to import is too far away. Chunk not loaded on that area.");

#if DEBUG
                    // ReSharper disable UnusedVariable
                    var oldPosInWorld = posInWorld - posDelta;
                    var oldPosInChunk = World.toBlock(oldPosInWorld);
                    var oldChunk      = world.GetChunkFromWorldPos(oldPosInWorld) as Chunk;
                    var oldTileEntity = oldChunk?.GetTileEntity(oldPosInChunk);
                    // ReSharper restore UnusedVariable
#endif
                    var tileEntityType = (TileEntityType)reader.ReadInt32();                // [Int32]    TileEntityType enum

                    var tileEntity = chunk.GetTileEntity(posInChunk);

                    // Create new tile entity if Prefab import didn't create it yet.
                    if (tileEntity == null)
                    {
                        Log.Warning($"Could't find TileEntity at position {posInWorld}. It should've been created by Prefab.read(..). Now creating new one of type {tileEntityType} ...");
                        tileEntity = TileEntity.Instantiate(tileEntityType, chunk);
                        chunk.AddTileEntity(tileEntity);
                    }

                    // Ensure we are dealing with the correct type; you never know...
                    if (tileEntity.GetTileEntityType() != tileEntityType)
                        throw new ApplicationException($"Tile entity {tileEntity} has wrong type {tileEntity.GetTileEntityType()} whet it should have {tileEntityType}.");

                    // Instruct the stream to fake the localChunkPos during read to use the new posInChunk instead.
                    // This is necessary because the localChunkPos is used IMMEDIATEY during read to initialize all other sorts of data,
                    // like creating item entities for items in powered blocks (see TileEntityPowerSource.read).
                    // Second parameter "sizeof(ushort)" adds delay for reading file version in TileEntity.read;

                    //byte[] fakeBytes = new byte[3 * sizeof(int)];   // for Vector3i = x, y, z
                    //NetworkUtils.Write(new BinaryWriter(new MemoryStream(fakeBytes)), posInChunk);
                    //fakeReader.FakeRead(fakeBytes, sizeof(ushort)); 
                    fakeReader.FakeRead(posInChunk.ToBytes(), sizeof(ushort));

                    tileEntity.read(reader, TileEntity.StreamModeRead.Persistency);         // [dynamic] tile entity data depending on type
                    Log.Debug($"Loaded tile entity {tileEntity}.");

                    if (tileEntity.localChunkPos != posInChunk)
                        throw new ApplicationException($"Tile entity {tileEntity} should have localChunkPos {posInChunk} but has {tileEntity.localChunkPos} instead.");

                    //var tileEntityPowered = tileEntity as TileEntityPowered;
                    //if (tileEntityPowered != null)                                          // [bool] has power item
                    //{
                    //    AdjustWires(tileEntityPowered, posInPrefab, posDelta);

                    //    // TileEntityPowered.read has made SURE that the tile entity has a power item
                    //    var powerItem = tileEntityPowered.GetPowerItem();
                    //    LoadPowerItem(reader, powerItem, posDelta);
                    //}

                    //// Check cannot be done earlier, because we MUST do the file reads regardless in order to continue reading
                    //if (chunk == null)
                    //{
                    //    Log.Warning($"Could not import tile entity for block {posInWorld} because the chunk is not loaded.");
                    //    continue;
                    //}

                    //tileEntities.Add(posInWorld, tileEntity);
                }
            }

            //// Go through every block and replace tile entities
            //for (int x = pos1.x; x <= pos2.x; x++)
            //{
            //    for (int z = pos1.z; z <= pos2.z; z++)
            //    {
            //        var chunk = world.GetChunkFromWorldPos(x, 0, z) as Chunk;
            //        if (chunk == null)
            //        {
            //            Log.Warning($"Could not remove tile entities from blocks {x}, {pos1.y} to {pos2.y}, {z} because the chunk is not loaded.");
            //            continue;
            //        }

            //        for (int y = pos1.y; y <= pos2.y; y++)
            //        {
            //            // Remove default empty tile entity from prefab
            //            chunk.RemoveTileEntityAt<TileEntity>(world, World.toBlock(x, y, z));

            //            // Add previously loaded tile entity if we have one
            //            var tileEntity = tileEntities.GetValue(new Vector3i(x, y, z));
            //            if (tileEntity != null)
            //                chunk.AddTileEntity(tileEntity);
            //        }
            //    }
            //}

            Log.Out($"Imported {tileEntitiyCount} tile entities for prefab {prefabName} into area {pos1} to {pos2}.");
        }

        [SuppressMessage("ReSharper", "IsExpressionAlwaysTrue")]
        [SuppressMessage("ReSharper", "CanBeReplacedWithTryCastAndCheckForNull")]
        private static void LoadPowerItem(BinaryReader reader, [NotNull] PowerItem powerItem, Vector3i posDelta)
        {
            // Doing everything here that the PowerItem classes do in read(..) methods, but only for itself, not parents or childs.
            // Intentionally not using ELSE to because of PowerItem inheritence, see: https://abload.de/img/2017-07-3011_04_50-scwwpdu.png

            if (powerItem is PowerPressurePlate) // -> PowerTrigger
            {
                // nothing to write
            }
            if (powerItem is PowerTripWireRelay) // -> PowerTrigger
            {
                // nothing to write
            }
            if (powerItem is PowerTimerRelay) // -> PowerTrigger
            {
                var pi = (PowerTimerRelay)powerItem;
                pi.StartTime = reader.ReadByte();
                pi.EndTime = reader.ReadByte();
            }
            if (powerItem is PowerElectricWireRelay) // -> PowerConsumer
            {
                // nothing to write
            }
            if (powerItem is PowerTrigger) // -> PowerConsumer
            {
                var pi = (PowerTrigger)powerItem;
                pi.TriggerType = (PowerTrigger.TriggerTypes)reader.ReadByte();
                if (pi.TriggerType == PowerTrigger.TriggerTypes.Switch)
                    pi.SetIsTriggered(reader.ReadBoolean());
                else
                    pi.SetIsActive(reader.ReadBoolean());
                if (pi.TriggerType != PowerTrigger.TriggerTypes.Switch)
                {
                    pi.TriggerPowerDelay = (PowerTrigger.TriggerPowerDelayTypes)reader.ReadByte();
                    pi.TriggerPowerDuration = (PowerTrigger.TriggerPowerDurationTypes)reader.ReadByte();
                    pi.SetDelayStartTime(reader.ReadSingle());
                    pi.SetPowerTime(reader.ReadSingle());
                }
                if (pi.TriggerType != PowerTrigger.TriggerTypes.Motion)
                    return;
                pi.TargetType = (PowerTrigger.TargetTypes)reader.ReadInt32();
            }
            if (powerItem is PowerConsumerToggle)
            {
                var pi = (PowerConsumerToggle)powerItem;
                pi.SetIsToggled(reader.ReadBoolean());
            }
            if (powerItem is PowerRangedTrap)
            {
                var pi = (PowerRangedTrap)powerItem;
                pi.SetIsLocked(reader.ReadBoolean());
                pi.SetSlots(GameUtils.ReadItemStack(reader));
                pi.TargetType = (PowerRangedTrap.TargetTypes)reader.ReadInt32();
            }
            if (powerItem is PowerBatteryBank)
            {
                // nothing to write
            }
            if (powerItem is PowerGenerator)
            {
                var pi = (PowerGenerator)powerItem;
                pi.CurrentFuel = reader.ReadUInt16();
            }
            if (powerItem is PowerSolarPanel)
            {
                // nothing to write
            }
            if (powerItem is PowerConsumer)
            {
                // nothing to write
            }
            if (powerItem is PowerSource)
            {
                var pi = (PowerSource)powerItem;
                pi.LastCurrentPower = pi.CurrentPower = reader.ReadUInt16();
                pi.IsOn = reader.ReadBoolean();
                pi.SetSlots(GameUtils.ReadItemStack(reader));
                pi.SetHasChangesLocal(true);
            }
            if (powerItem is PowerConsumerSingle)
            {
                // nothing to write
            }
            if (powerItem is PowerItem)
            {
                var pi = (PowerItem)powerItem;

                // None of this is needed for import

                //pi.BlockID = reader.ReadUInt16();
                //pi.SetValuesFromBlock();
                //pi.Position = NetworkUtils.ReadVector3i(reader);

                if (reader.ReadBoolean())
                {
                    // adjust parent position to new position before finding parent power item
                    var parentPos = NetworkUtils.ReadVector3i(reader);
                    Log.Debug($"Changing parent power item on {powerItem.Position} from {parentPos} to {parentPos + posDelta}.");
                    parentPos = parentPos + posDelta;
                    PowerManager.Instance.SetParent(pi, PowerManager.Instance.GetPowerItemByWorldPos(parentPos));
                }

                //int num = (int)reader.ReadByte();
                //pi.Children.Clear();
                //for (int index = 0; index < num; ++index)
                //{
                //    PowerItem node = PowerItem.CreateItem((PowerItem.PowerItemTypes)reader.ReadByte());
                //    node.read(reader, _version);
                //    PowerManager.Instance.AddPowerNode(node, pi);
                //}
            }

            Log.Debug($"Read power item {powerItem.GetType()} at position {powerItem.Position}.");
        }

        private static void AdjustWires(TileEntityPowered tileEntityPowered, Vector3i posInPrefab, Vector3i posDelta)
        {
            // No need to adjust anything if TE stays at same position
            if (posDelta == Vector3i.zero)
                return;

            // Adjust child wires
            List<Vector3i> wireChildren = tileEntityPowered.GetWireChildren() ?? new List<Vector3i>();
            for (int i = 0; i < wireChildren.Count; i++)
            {
                Log.Debug($"Changing child wire on {tileEntityPowered} from {wireChildren[i]} to {wireChildren[i] + posDelta}");
                wireChildren[i] += posDelta;
            }

            // Adjust parent wire
            var parentWire = tileEntityPowered.GetParent();
            if (parentWire != new Vector3i(-9999, -9999, -9999))
            {
                Log.Debug($"Changing parent wire on {tileEntityPowered} from {parentWire} to {parentWire + posDelta}");
                tileEntityPowered.SetWireParent(parentWire + posDelta);
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
                    throw new ArgumentException(@"Rotation must be either 0, 1, 2, or 3", nameof(rotate));
            }
        }

    }
}
