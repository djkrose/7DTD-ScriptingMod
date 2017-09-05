using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using ScriptingMod.Exceptions;
using ScriptingMod.Extensions;
using ScriptingMod.Tools;

namespace ScriptingMod.Commands
{
    /*
     * TODO [P3]: Save prefabs in a subdirectory, but somehow allow also to load standard prefabs. (or use default file name prefix)
     * TODO [P3]: If a bed is overwritten during import, remove the player's home point
     * TODO [P3]: Also reload adjacent chunks to fix terrain height gaps
     * TODO [P3]: Enable/fix importing of spawners
     * TODO [P2]: Replace loot placeholder blocks according to loot.xml
     * TODO [P2]: Allow changing the directon into which the prefab is loaded (other north/east/up)
     * TODO [P2]: Allow importing vending machines and other things that have TraderData in the TileEntity. This requires that an entry is made into TraderInfo.traderInfoList[this.TraderID]
     * TODO [P2]: Register Land Claim Blocks correctly
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

        public override void Execute(List<string> parameters, CommandSenderInfo senderInfo)
        {
            HashSet<Chunk> affectedChunks = null;
            try
            {
                (string prefabName, Vector3i pos1, int rotate, bool all) = ParseParams(parameters, senderInfo);

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
                CommandTools.HandleCommandException(ex);
            }

            // Error could have happened after prefab load, so we must reset/reload regardless
            try
            {
                if (affectedChunks != null)
                {
                    Tools.ChunkTools.ResetStability(affectedChunks);
                    Tools.ChunkTools.ReloadForClients(affectedChunks.Select(c => c.Key).ToList());
                }
            }
            catch (Exception ex)
            {
                CommandTools.HandleCommandException(ex);
            }
        }

        private static (string prefabName, Vector3i pos1, int rotate, bool all)
            ParseParams(List<string> parameters, CommandSenderInfo senderInfo)
        {
            // Parse /all parameter
            var all = parameters.Remove("/all");

            if (parameters.Count == 0)
                throw new FriendlyMessageException(Resources.ErrorParameerCountNotValid);

            // Parse prefab name
            var prefabName = parameters[0];

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
            if (parameters.Count == 1 || parameters.Count == 2)
            {
                pos1 = senderInfo.GetRemoteClientInfo().GetEntityPlayer().GetServerPos().ToVector3i();
            }
            else if (parameters.Count == 4 || parameters.Count == 5)
            {
                pos1 = CommandTools.ParseXYZ(parameters, 1);
            }
            else
            {
                throw new FriendlyMessageException(Resources.ErrorParameerCountNotValid);
            }

            // Parse rotation
            var rotate   = 0;
            if (parameters.Count == 2 || parameters.Count == 5)
            {
                rotate = parameters[parameters.Count - 1].ToInt()
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
                        throw new FriendlyMessageException(Resources.ErrorAreaTooFarAway);

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
            int tileEntitiyCount;

            using (var reader = new BinaryReader(new FileStream(filePath, FileMode.Open)))
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
                // ReSharper disable once UnusedVariable
                var originalPos2 = NetworkUtils.ReadVector3i(reader);                       // [Vector3i] original area worldPos2
                var posDelta     = pos1 - originalPos1;

                // See Assembly-CSharp::Chunk.read() -> search "tileentity.read"
                tileEntitiyCount = reader.ReadInt32();                                      // [Int32]   number of tile entities
                for (int i = 0; i < tileEntitiyCount; i++)
                {
                    var posInPrefab = NetworkUtils.ReadVector3i(reader);                    // [3xInt32] position relative to prefab
                    posInPrefab     = RotatePosition(posInPrefab, pos2 - pos1, rotate);

                    var posInWorld  = pos1 + posInPrefab;
                    var posInChunk  = World.toBlock(posInWorld);
                    var chunk       = world.GetChunkFromWorldPos(posInWorld) as Chunk;
                    if (chunk == null)
                        throw new FriendlyMessageException(Resources.ErrorAreaTooFarAway);

                    var tileEntityType = (TileEntityType)reader.ReadByte();                 // [byte]    TileEntityType enum
                    var tileEntity = chunk.GetTileEntity(posInChunk);

                    // Create new tile entity if Prefab import didn't create it yet; it's only created for some blocks apparently
                    if (tileEntity == null)
                    {
                        tileEntity = TileEntity.Instantiate(tileEntityType, chunk);
                        tileEntity.localChunkPos = posInChunk;
                        chunk.AddTileEntity(tileEntity);
                        Log.Debug($"Tile entity at position {posInWorld} was not created by Prefab import, therefore a new one was created and added: {tileEntity.ToStringBetter()}");
                    }

                    // Make sure we are dealing with the correct type; you never know...
                    if (tileEntity.GetTileEntityType() != tileEntityType)
                        throw new ApplicationException($"Tile entity {tileEntity.GetType()} [{tileEntity.ToWorldPos()}] has wrong type {tileEntity.GetTileEntityType()} when it should have {tileEntityType}.");

                    LoadTileEntity(reader, tileEntity, posDelta);                             // [dynamic] tile entity data depending on type 

                    // Make sure localChunkPos was adjusted correctly during read
                    if (tileEntity.localChunkPos != posInChunk)
                        throw new ApplicationException($"Tile entity {tileEntity.GetType()} ({tileEntity.GetTileEntityType()} should have localChunkPos {posInChunk} but has {tileEntity.localChunkPos} instead.");

                    var tileEntityPowered = tileEntity as TileEntityPowered;
                    if (tileEntityPowered != null)                          
                    {
                        var powerItem = tileEntityPowered.GetPowerItem();
                        if (powerItem == null)
                            throw new ApplicationException("For imported TileEntityPowered no PowerItem was created during TileEntityPowered.read.");

                        LoadPowerItem(reader, powerItem, posDelta);
                    }
                }
            }

            Log.Out($"Imported {tileEntitiyCount} tile entities for prefab {prefabName} into area {pos1} to {pos2}.");
        }

        private static void LoadTileEntity(BinaryReader _br, [NotNull] TileEntity tileEntity, Vector3i posDelta)
        {
            // TileEntityPowered needs to be read manually because TileEntityPowered.read destroys wires
            // of the original position (where prefab originates) when it's loadedat new position.
            var tileEntityPowered = tileEntity as TileEntityPowered;
            if (tileEntityPowered != null)
            {
                LoadTileEntityPowered(_br, tileEntityPowered, posDelta);
            }
            else
            {
                tileEntity.read(_br, TileEntity.StreamModeRead.Persistency);

                // Adjust localChunkPos afterwards
                var localChunkPos = tileEntity.localChunkPos;
                localChunkPos = World.toBlock(tileEntity.GetChunk().ToWorldPos(localChunkPos) + posDelta);
                tileEntity.localChunkPos = localChunkPos;
            }

            Log.Debug($"Loaded tile entity {tileEntity.ToStringBetter()}.");
        }

        [SuppressMessage("ReSharper", "IsExpressionAlwaysTrue")]
        [SuppressMessage("ReSharper", "CanBeReplacedWithTryCastAndCheckForNull")]
        private static void LoadTileEntityPowered(BinaryReader _br, TileEntityPowered tileEntity, Vector3i posDelta)
        {
            const int expectedReadVersion = 3; // See CurrentSaveVersion.CurrentSaveVersion
            Vector3i noParent = new Vector3i(-9999, -9999, -9999);

            // Doing everything here that the TileEntityPowered classes do in read(..) methods, but modifying the locations of itself,
            // parents, and children in the process. Intentionally not using ELSE because of TileEntityPowered inheritence,
            // see: https://abload.de/img/tileentity-hierarchyanxo7.png

            if (tileEntity is TileEntity) // always
            {
                var te = (TileEntity)tileEntity;
                var readVersion = _br.ReadUInt16();
                if (readVersion != expectedReadVersion)
                {
                    Log.Error($"Could not load tile entity during import: te's readVersion is {readVersion}, but we expected version {expectedReadVersion} in the file.");
                    throw new FriendlyMessageException("The import file contains incompatible data; maybe it was written with a too new game version. Check if an update for the mod is available!");
                }

                te.SetReadVersion(readVersion);
                var localChunkPos = NetworkUtils.ReadVector3i(_br);
                localChunkPos = World.toBlock(te.GetChunk().ToWorldPos(localChunkPos) + posDelta); // Adjusting to new position
                te.localChunkPos = localChunkPos;
                // te.localChunkPos = World.toBlock(NetworkUtils.ReadVector3i(_br)); 
                te.entityId = _br.ReadInt32();
                //if (te.GetReadVersion() <= 1)
                //    return;
                te.SetNextHeatMapEvent(_br.ReadUInt64());
            }

            if (tileEntity is TileEntityPowered) // always
            {
                // ReSharper disable once RedundantCast
                var te = (TileEntityPowered)tileEntity;
                int num1 = _br.ReadInt32();
                te.IsPlayerPlaced = _br.ReadBoolean();
                te.PowerItemType = (PowerItem.PowerItemTypes)_br.ReadByte();
                te.InitializePowerData();
                te.SetBool1(true);
                int wireChildrenSize = _br.ReadByte();
                te.GetWireChildren().Clear(); // list_1
                for (int index = 0; index < wireChildrenSize; ++index)
                    te.GetWireChildren().Add(NetworkUtils.ReadVector3i(_br) + posDelta); // Adjusting to new position
                te.CheckForNewWires();
                var wireParent = NetworkUtils.ReadVector3i(_br);
                if (wireParent != noParent)
                    wireParent += posDelta;                                              // Adjusting to new position
                te.SetWireParent(wireParent);
                //if (_eStreamMode == TileEntity.StreamModeRead.FromServer)
                //    te.bool_2 = _br.ReadBoolean();
                te.SetBool3(true);
                te.MarkWireDirty();
                if (num1 <= 0)
                    return;
                //if (_eStreamMode == TileEntity.StreamModeRead.FromServer)
                //{
                //    bool flag = false;
                //    if (LocalPlayerUI.GetUIForPrimaryPlayer().windowManager.Contains(XUiC_PowerCameraWindowGroup.string_0))
                //    {
                //        XUiC_PowerCameraWindowGroup controller = (XUiC_PowerCameraWindowGroup)((XUiWindowGroup)LocalPlayerUI.GetUIForPrimaryPlayer().windowManager.GetWindow(XUiC_PowerCameraWindowGroup.string_0)).Controller;
                //        flag = te.IsUserAccessing() && controller != null && controller.TileEntity == te;
                //    }
                //    if (!flag)
                //    {
                //        te.CenteredPitch = _br.ReadSingle();
                //        te.CenteredYaw = _br.ReadSingle();
                //    }
                //    else
                //    {
                //        double num3 = (double)_br.ReadSingle();
                //        double num4 = (double)_br.ReadSingle();
                //    }
                //}
                //else
                //{
                    te.CenteredPitch = _br.ReadSingle();
                    te.CenteredYaw = _br.ReadSingle();
                //}
            }

            if (tileEntity is TileEntityPoweredBlock)
            {
                // Nothing to do here
            }

            if (tileEntity is TileEntityPoweredRangedTrap)
            {
                var te = (TileEntityPoweredRangedTrap) tileEntity;
                if (te.PowerItemType == PowerItem.PowerItemTypes.RangedTrap)
                    te.SetOwner(_br.ReadString());
            }

            if (tileEntity is TileEntityPoweredTrigger)
            {
                var te = (TileEntityPoweredTrigger) tileEntity;
                te.TriggerType = (PowerTrigger.TriggerTypes)_br.ReadByte();
                if (te.TriggerType == PowerTrigger.TriggerTypes.Motion)
                    te.SetOwner(_br.ReadString());
            }

            if (tileEntity is TileEntityPowerSource)
            {
                var te = (TileEntityPowerSource) tileEntity;
                if (te.ClientData == null)
                    te.ClientData = new TileEntityPowerSource.ClientPowerData();
            }

        }

        [SuppressMessage("ReSharper", "IsExpressionAlwaysTrue")]
        [SuppressMessage("ReSharper", "CanBeReplacedWithTryCastAndCheckForNull")]
        private static void LoadPowerItem(BinaryReader _br, [NotNull] PowerItem powerItem, Vector3i posDelta)
        {
            // Doing everything here that the PowerItem classes do in read(..) methods, but only for itself, not parents or childs.
            // Intentionally not using ELSE to because of PowerItem inheritence, see: https://abload.de/img/poweritem-hierarchyzvumv.png

            if (powerItem is PowerItem) // always
            {
                // ReSharper disable once RedundantCast
                var pi = (PowerItem) powerItem;

                // No need to set block data; it's already set from tile import
                //this.BlockID = _br.ReadUInt16();
                //this.SetValuesFromBlock();
                //this.Position = NetworkUtils.ReadVector3i(_br);

                if (_br.ReadBoolean()) // has parent
                {
                    // Do NOT use this, because it removes the connection from powerItems previous power item, which belongs to the old prefab location
                    //PowerManager.Instance.SetParent(this, PowerManager.Instance.GetPowerItemByWorldPos(NetworkUtils.ReadVector3i(_br)));

                    var parentPos = NetworkUtils.ReadVector3i(_br);
                    parentPos += posDelta;                                                      // Adjusting to new position
                    pi.Parent = PowerManager.Instance.GetPowerItemByWorldPos(parentPos);

                    if (pi.Parent != null) // Could happen when parent was not included in prefab area
                    {
                        // Add reciprocal connection back from parent
                        if (!pi.Parent.Children.Contains(pi))
                            pi.Parent.Children.Add(pi);

                        // When power item receives a parent, it is not a root power item anymore and must be removed from the list
                        var rootPowerItems = PowerManager.Instance.GetRootPowerItems();
                        if (rootPowerItems.Contains(pi))
                            rootPowerItems.Remove(pi);
                    }
                }
                pi.SendHasLocalChangesToRoot();

                // No need to read in children; the needed ones are added by tile import
                //int num = (int)_br.ReadByte();
                //this.Children.Clear();
                //for (int index = 0; index < num; ++index)
                //{
                //    PowerItem node = PowerItem.CreateItem((PowerItem.PowerItemTypes)_br.ReadByte());
                //    node.read(_br, _version);
                //    PowerManager.Instance.AddPowerNode(node, this);
                //}
            }

            if (powerItem is PowerConsumer)
            {
                // nothing to read
            }

            if (powerItem is PowerConsumerToggle)
            {
                var pi = (PowerConsumerToggle)powerItem;
                pi.SetIsToggled(_br.ReadBoolean());
            }

            if (powerItem is PowerElectricWireRelay)
            {
                // nothing to read
            }

            if (powerItem is PowerRangedTrap)
            {
                var pi = (PowerRangedTrap)powerItem;
                pi.SetIsLocked(_br.ReadBoolean());
                pi.SetSlots(GameUtils.ReadItemStack(_br));
                pi.TargetType = (PowerRangedTrap.TargetTypes)_br.ReadInt32();
            }

            if (powerItem is PowerTrigger)
            {
                var pi = (PowerTrigger)powerItem;
                pi.TriggerType = (PowerTrigger.TriggerTypes)_br.ReadByte();
                if (pi.TriggerType == PowerTrigger.TriggerTypes.Switch)
                    pi.SetIsTriggered(_br.ReadBoolean());
                else
                    pi.SetIsActive(_br.ReadBoolean());
                if (pi.TriggerType != PowerTrigger.TriggerTypes.Switch)
                {
                    pi.TriggerPowerDelay = (PowerTrigger.TriggerPowerDelayTypes)_br.ReadByte();
                    pi.TriggerPowerDuration = (PowerTrigger.TriggerPowerDurationTypes)_br.ReadByte();
                    pi.SetDelayStartTime(_br.ReadSingle());
                    pi.SetPowerTime(_br.ReadSingle());
                }
                if (pi.TriggerType != PowerTrigger.TriggerTypes.Motion)
                    return;
                pi.TargetType = (PowerTrigger.TargetTypes)_br.ReadInt32();
            }

            if (powerItem is PowerPressurePlate)
            {
                // nothing to read
            }

            if (powerItem is PowerTimerRelay)
            {
                var pi = (PowerTimerRelay)powerItem;
                pi.StartTime = _br.ReadByte();
                pi.EndTime = _br.ReadByte();
            }

            if (powerItem is PowerTripWireRelay)
            {
                // nothing to read
            }

            if (powerItem is PowerConsumerSingle)
            {
                // nothing to read
            }

            if (powerItem is PowerSource)
            {
                var pi = (PowerSource)powerItem;
                pi.LastCurrentPower = pi.CurrentPower = _br.ReadUInt16();
                pi.IsOn = _br.ReadBoolean();
                pi.SetSlots(GameUtils.ReadItemStack(_br));
                pi.SetHasChangesLocal(true);
            }

            if (powerItem is PowerBatteryBank)
            {
                // nothing to read
            }

            if (powerItem is PowerGenerator)
            {
                var pi = (PowerGenerator)powerItem;
                pi.CurrentFuel = _br.ReadUInt16();
            }

            if (powerItem is PowerSolarPanel)
            {
                // nothing to read
            }

            Log.Debug($"Loaded power item {powerItem.ToStringBetter()}.");
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
