using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using JetBrains.Annotations;
using ScriptingMod.Exceptions;
using ScriptingMod.Extensions;
using ScriptingMod.Tools;

namespace ScriptingMod
{
    [Flags]
    internal enum RepairEngineScans
    {
        WrongPowerItem     = 1,
        LockedBiomeRespawn = 2,
        DeathScreenLoop    = 4,
        Default            = WrongPowerItem,
        //All                = WrongPowerItem | LockedBiomeRespawn | DeathScreenLoop
    }

    internal class RepairEngine
    {
        private const int EntitiySearchRadius = 4; // entities are allowed to wander this many chunks away from their initial 5x5 chunk area

        /// <summary>
        /// Allows assigning a method that outputs status information to the console, e.g. SdtdConsole.Output
        /// </summary>
        public Action<string> ConsoleOutput;

        /// <summary>
        /// If set to true, problems will only be reported without fixing them
        /// </summary>
        public bool Simulate;

        /// <summary>
        /// If set, only the chunk containing this world position is scanned
        /// </summary>
        public Vector3i? WorldPos = null;

        /// <summary>
        /// Allows defining the problems to scan for, using binary flags (multiple possible)
        /// Default: RepairEngineScans.All
        /// </summary>
        public RepairEngineScans Scans = RepairEngineScans.Default;

        public int ScannedChunks;
        public int ProblemsFound;

        private string FoundOrRepaired => Simulate ? "Found" : "Repaired";

        /// <summary>
        /// For TileEntityPoweredTrigger objects this lists the TriggerTypes and which power item class are allowed together.
        /// Last updated: A16.2 b7
        /// Source: See TileEntityPoweredTrigger.CreatePowerItem()
        /// </summary>
        private static readonly Dictionary<PowerTrigger.TriggerTypes, Type> ValidTriggerTypes =
            new Dictionary<PowerTrigger.TriggerTypes, Type>
            {
                { PowerTrigger.TriggerTypes.Switch,        typeof(PowerTrigger) },
                { PowerTrigger.TriggerTypes.PressurePlate, typeof(PowerPressurePlate) },
                { PowerTrigger.TriggerTypes.TimerRelay,    typeof(PowerTimerRelay) },
                { PowerTrigger.TriggerTypes.Motion,        typeof(PowerTrigger) },
                { PowerTrigger.TriggerTypes.TripWire,      typeof(PowerTripWireRelay) }
            };

        private static readonly World World = GameManager.Instance.World;

        private static ulong _maxAllowedRespawnDelayCache;

        /// <summary>
        /// The cached maximum allowed respawn delay in world ticks for ChunkBiomeSpawnData entires.
        /// See: ChunkAreaBiomeSpawnData.SetRespawnLocked(..) and EntityPlayer.onSpawnStateChanged(..)
        /// </summary>
        private static ulong MaxAllowedRespawnDelay
        {
            get
            {
                if (_maxAllowedRespawnDelayCache == 0UL)
                {
                    _maxAllowedRespawnDelayCache = (ulong)(GamePrefs.GetInt(EnumGamePrefs.PlayerSafeZoneHours) * 1000);

                    foreach (var biome in World.Biomes.GetBiomeMap().Values)
                    {
                        if (biome == null)
                            continue;
                        var spawnEntityGroupList = BiomeSpawningClass.list[biome.m_sBiomeName];
                        if (spawnEntityGroupList == null)
                            continue;
                        foreach (var spawnEntityGroupData in spawnEntityGroupList.list)
                        {
                            if (spawnEntityGroupData == null)
                                continue;
                            if (_maxAllowedRespawnDelayCache < (ulong)spawnEntityGroupData.respawnDelayInWorldTime)
                                _maxAllowedRespawnDelayCache = (ulong)spawnEntityGroupData.respawnDelayInWorldTime;
                        }
                    }
                }
                return _maxAllowedRespawnDelayCache;
            }
        }

        public void Start()
        {
            // Scan chunks -> tile entities
            if (Scans.HasFlag(RepairEngineScans.WrongPowerItem) || Scans.HasFlag(RepairEngineScans.LockedBiomeRespawn))
            {
                //CollectEntityStubs();

                if (WorldPos == null)
                {
                    Output("Scanning all loaded chunks for corrupt data ...");
                    foreach (var chunk in World.ChunkCache.GetChunkArrayCopySync())
                    {
                        RepairChunk(chunk);
                    }
                }
                else
                {
                    var chunk = World.GetChunkFromWorldPos(WorldPos.Value) as Chunk;
                    if (chunk == null)
                        throw new FriendlyMessageException(Resources.ErrorAreaTooFarAway);

                    Output($"Scanning {chunk} for corrupt data ...");
                    RepairChunk(chunk);
                }
            }

            // Scan players
            if (Scans.HasFlag(RepairEngineScans.DeathScreenLoop))
            {
                Output("Scanning all players for corrupt data ...");
                // TODO
            }
        }

        //private struct EntityStubSpawnData : IEquatable<EntityStubSpawnData>
        //{
        //    public EnumSpawnerSource spawnerSource;
        //    public long chunkKey;
        //    public string groupName;
        //    public ulong worldTimeBorn;

        //    public bool Equals(EntityStubSpawnData other)
        //    {
        //        return spawnerSource == other.spawnerSource && chunkKey == other.chunkKey && string.Equals(groupName, other.groupName);
        //    }

        //    public override bool Equals(object obj)
        //    {
        //        if (ReferenceEquals(null, obj)) return false;
        //        return obj is EntityStubSpawnData && Equals((EntityStubSpawnData) obj);
        //    }

        //    public override int GetHashCode()
        //    {
        //        unchecked
        //        {
        //            var hashCode = (int) spawnerSource;
        //            hashCode = (hashCode * 397) ^ chunkKey.GetHashCode();
        //            hashCode = (hashCode * 397) ^ (groupName != null ? groupName.GetHashCode() : 0);
        //            return hashCode;
        //        }
        //    }

        //    public static bool operator ==(EntityStubSpawnData a, EntityStubSpawnData b)
        //    {
        //        return a.Equals(b);
        //    }

        //    public static bool operator !=(EntityStubSpawnData a, EntityStubSpawnData b)
        //    {
        //        return !a.Equals(b);
        //    }
        //}

        //private void CollectEntityStubs()
        //{
        //    _entityStubs = new List<EntityStubSpawnData>();
        //    foreach (var chunk in World.ChunkCache.GetChunkArrayCopySync())
        //    {
        //        foreach (var entityStub in chunk.GetEntityStubs().ToList())
        //        {
        //            if (entityStub.entityData.Length <= 0)
        //                continue;

        //            try
        //            {
        //                entityStub.entityData.Position = 0;
        //                var br = new BinaryReader(entityStub.entityData);
        //                var spawnerSource = (EnumSpawnerSource)br.ReadByte();
        //                if (spawnerSource != EnumSpawnerSource.Biome)
        //                    continue;

        //                _entityStubs.Add(new EntityStubSpawnData
        //                {
        //                    spawnerSource = spawnerSource,
        //                    groupName     = br.ReadString(),
        //                    chunkKey      = br.ReadInt64(),
        //                    worldTimeBorn = br.ReadUInt64()
        //                });
        //            }
        //            catch (Exception ex)
        //            {
        //                Log.Error($"Error loading entity data {entityStub.id} from entity stub of {chunk}: " + ex);
        //            }
        //        }
        //    }

        //    foreach (var entity in World.Entities.list.ToList())
        //    {
        //        _entityStubs.Add(new EntityStubSpawnData
        //        {
        //            spawnerSource = entity.GetSpawnerSource(),
        //            groupName = entity.GetSpawnerSourceEntityGroupName(),
        //            chunkKey = entity.GetSpawnerSourceChunkKey(),
        //            worldTimeBorn = entity.WorldTimeBorn
        //        });
        //    }

        //    _entityStubs = _entityStubs.Distinct().ToList();

        //    Log.Debug($"Collected {_entityStubs.Count} entity stubs from all loaded chunks.");
        //}

        /// <summary>
        /// Scans the given chunk object for broken power blocks and optionally fixes them
        /// </summary>
        /// <param name="chunk">The chunk object; must be loaded and ready</param>
        private void RepairChunk([NotNull] Chunk chunk)
        {
            if (Scans.HasFlag(RepairEngineScans.LockedBiomeRespawn))
                RepairChunkRespawn(chunk);

            foreach (var tileEntity in chunk.GetTileEntities().Values.ToList())
            {
                RepairTileEntity(tileEntity);
            }

            ScannedChunks++;
        }

        private void RepairChunkRespawn([NotNull] Chunk chunk)
        {
            // Only area master chunks (every 5th chunk) control respawn with their chustom chunk data (ChunkBiomeSpawnData)
            if (!chunk.IsAreaMaster())
                return;

            // No ChunkBiomeSpawnData, no problem; that's the default
            var spawnData = chunk.GetChunkBiomeSpawnData();
            if (spawnData == null)
                return;

            Log.Debug($"Scanning area master {chunk} with spawn data: {spawnData}");
            foreach (var groupName in spawnData.GetEntityGroupNames().ToList())
            {
                ulong respawnLockedUntil = spawnData.GetRespawnLockedUntilWorldTime(groupName);
                int   registeredEntities = spawnData.GetEntitiesSpawned(groupName);

                // Check if respawn is locked for an unusual long time (more than 7 in-game days)
                if (respawnLockedUntil > World.worldTime + MaxAllowedRespawnDelay)
                {
                    ProblemsFound++;
                    spawnData.ClearRespawnLocked(groupName);
                    chunk.isModified = true;
                    Log.Warning($"{FoundOrRepaired} respawn of {groupName} locked for {respawnLockedUntil / 1000 / 24} game days in area master {chunk}.");
                    Output($"{FoundOrRepaired} respawn of {groupName} locked for incorrect duration in {chunk}.");
                }
                // Check if registered entities can be found online
                else if (registeredEntities > 0 && respawnLockedUntil == 0UL && AllChunksLoaded(chunk, EntitiySearchRadius))
                {
                    var spawnedEntities = CountSpawnedEntities(EnumSpawnerSource.Biome, chunk.Key, groupName);

                    if (registeredEntities > spawnedEntities)
                    {
                        ProblemsFound++;
                        SetEntitiesSpawned(spawnData, groupName, spawnedEntities);
                        chunk.isModified = true;
                        int lostEntities = registeredEntities - spawnedEntities;
                        Log.Warning($"{FoundOrRepaired} respawn of {groupName} locked because of {lostEntities} lost {(lostEntities == 1 ? "entity" : "entities")} in area master {chunk}.");
                        Output($"{FoundOrRepaired} respawn of {groupName} locked because of {lostEntities} lost {(lostEntities == 1 ? "zombies/animals" : "zombie/animal")} in {chunk}.");
                    }
                }
            }
        }

        private static void SetEntitiesSpawned(ChunkAreaBiomeSpawnData spawnData, string groupName, int entitiesSpawned)
        {
            int delta = entitiesSpawned - spawnData.GetEntitiesSpawned(groupName);
            for (int i = 0; i < delta; i++)
                spawnData.IncEntitiesSpawned(groupName);
            for (int i = 0; i > delta; i--)
                spawnData.DecEntitiesSpawned(groupName);
        }

        private void RepairTileEntity([NotNull] TileEntity tileEntity)
        {
            // Find/fix problems with TileEntityPowered objects and their PowerItems
            var powered = tileEntity as TileEntityPowered;
            if (powered != null && !IsValidTileEntityPowered(powered))
            {
                ProblemsFound++;

                if (!Simulate)
                    RecreateTileEntity(tileEntity);

                var msg = $"{FoundOrRepaired} broken power block at {tileEntity.ToWorldPos()} in {tileEntity.GetChunk()}.";
                Log.Warning(msg);
                Output(msg);
            }
        }

        private static int CountSpawnedEntities(EnumSpawnerSource spawnerSource, long spawnerSourceChunkKey, string spawnerSourceEntityGroupName)
        {
            return World.Entities.list.Count(
                e => e.GetSpawnerSource() == spawnerSource
                     && e.GetSpawnerSourceChunkKey() == spawnerSourceChunkKey
                     && e.GetSpawnerSourceEntityGroupName() == spawnerSourceEntityGroupName);
        }

        /// <summary>
        /// Returns true if all chunks that belong to the given area master chunk, extended by the given value, are currently loaded.
        /// </summary>
        /// <param name="areaMasterChunk">The area master chunk to use as basis</param>
        /// <param name="extendBy">Number of chunks to extend the check area in all directions, additionally to the 5x5 area master</param>
        private static bool AllChunksLoaded([NotNull] Chunk areaMasterChunk, int extendBy)
        {
            if (!areaMasterChunk.IsAreaMaster())
                throw new ArgumentException("Given chunk is not an area master chunk.", nameof(areaMasterChunk));

            for (int x = areaMasterChunk.X - extendBy; x < areaMasterChunk.X + Chunk.cAreaMasterSizeChunks + extendBy; x++)
                for (int z = areaMasterChunk.Z - extendBy; z < areaMasterChunk.Z + Chunk.cAreaMasterSizeChunks + extendBy; z++)
                    if (!World.ChunkCache.ContainsChunkSync(WorldChunkCache.MakeChunkKey(x, z, areaMasterChunk.ClrIdx)))
                        return false;
            return true;
        }

        /// <summary>
        /// Returns false if the given tile entity has an invalid PowerItem attached; true otherwise
        /// </summary>
        public static bool IsValidTileEntityPowered([NotNull] TileEntityPowered te)
        {
            var teType = te.GetType();
            var pi = te.GetPowerItem();

            // Can't check what's not there. That's ok, some powered blocks (e.g. lamps) don't have a power item until connected.
            if (pi == null)
                return true;

            var piType = pi.GetType();

            var teTrigger = te as TileEntityPoweredTrigger;
            if (teTrigger != null)
            {
                // Trigger must be handled differently, because there are multiple possible power items for one TileEntityPoweredTriger,
                // and the PowerItemType is sometimes just (incorrectly) "PowerSource" when the TriggerType determines the *real* power type.

                // CHECK 1: Power item should be of type PowerTrigger if this is a TileEntityPoweredTrigger
                var piTrigger = pi as PowerTrigger;
                if (piTrigger == null)
                {
                    Log.Debug($"[{te.ToWorldPos()}] {teType} should have power item \"PowerTrigger\" or some descendant of it, but has power item \"{piType}\".");
                    return false;
                }

                // CHECK 2: PowerItemType should match the actual power item's object type, or be at least "PowerSource",
                // because TileEntityPoweredTrigger sometimes has the (incorrect) default PowerItemType "PowerSource" value
                // and only TriggerType is reliable. It "smells" but we have to accept it.
                if (te.PowerItemType != pi.PowerItemType && te.PowerItemType != PowerItem.PowerItemTypes.Consumer)
                {
                    Log.Debug($"[{te.ToWorldPos()}] {teType}.PowerItemType=\"{te.PowerItemType}\" doesn't match with {piType}.PowerItemType=\"{pi.PowerItemType}\" " +
                              $"and is also not the default \"{PowerItem.PowerItemTypes.Consumer}\".");
                    return false;
                }

                // CHECK 3: TriggerType and actual power item type should be compatible
                var expectedClass = ValidTriggerTypes.GetValue(teTrigger.TriggerType);
                if (expectedClass == null)
                    Log.Warning($"Unknown enum value PowerTrigger.TriggerTypes.{teTrigger.TriggerType} found.");
                else if (piType != expectedClass)
                {
                    Log.Debug($"[{te.ToWorldPos()}] {teType}.TriggerType=\"{teTrigger.TriggerType}\" doesn't fit together with power item \"{piType}\". " +
                              $"A {expectedClass} was expected.");
                    return false;
                }

                // CHECK 4: Tile entity's TriggerType and power items's TriggerType should match
                if (teTrigger.TriggerType != piTrigger.TriggerType)
                {
                    Log.Debug($"[{te.ToWorldPos()}] {teType}.TriggerType=\"{teTrigger.TriggerType}\" doesn't match with {piType}.PowerItemType=\"{piTrigger.TriggerType}\".");
                    return false;
                }
            }
            else
            {
                // CHECK 5: For all non-trigger tile entities, the power item type must match with the actual object
                if (te.PowerItemType != pi.PowerItemType)
                {
                    Log.Debug($"[{te.ToWorldPos()}] {teType}.PowerItemType=\"{te.PowerItemType}\" doesn't match with {piType}.PowerItemType=\"{pi.PowerItemType}\".");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Deletes the given tile entity from it's chunk and creates a new one based on the tile entity type
        /// </summary>
        private static void RecreateTileEntity([NotNull] TileEntity tileEntity)
        {
            var chunk = tileEntity.GetChunk();

            // Prevent further errors on client updates; crucial when removing power item!
            tileEntity.SetDisableModifiedCheck(true);

            // Remove broken tile entity
            chunk.RemoveTileEntity(World, tileEntity);

            // Remove power item
            var tePowered = tileEntity as TileEntityPowered;
            var powerItem = tePowered?.GetPowerItem();
            if (powerItem != null)
                PowerManager.Instance.RemovePowerNode(powerItem);

            // Create new tile entity
            var newTileEntity = TileEntity.Instantiate(tileEntity.GetTileEntityType(), chunk);
            newTileEntity.localChunkPos = tileEntity.localChunkPos;
            chunk.AddTileEntity(newTileEntity);

            // Recreate power item if necessary
            var newPowered = newTileEntity as TileEntityPowered;
            if (newPowered != null)
            {
                // Restore old PowerItemType and TriggerType values
                if (tePowered != null)
                    newPowered.PowerItemType = tePowered.PowerItemType;

                // fancy new C#7 syntax, isn't it? :)
                if (tileEntity is TileEntityPoweredTrigger teTrigger && newPowered is TileEntityPoweredTrigger newTrigger)
                    newTrigger.TriggerType = teTrigger.TriggerType;

                // Create power item according to PowerItemType and TriggerType
                newPowered.InitializePowerData();

                // Wires to the broken block are cut and not restored. We could try to reattach everything, but meh...
            }

            var newPowerItem = newPowered?.GetPowerItem();
            Log.Debug($"[{tileEntity.ToWorldPos()}] Replaced old {tileEntity.GetType()} with new {newTileEntity.GetType()}" +
                      $"{(newPowerItem != null ? " and new power item " + newPowerItem.GetType() : "")}.");
        }

        private void Output(string msg)
        {
            if (ConsoleOutput == null)
                return;
            ConsoleOutput(msg);
        }
    }
}
