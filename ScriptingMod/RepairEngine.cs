using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using JetBrains.Annotations;
using ScriptingMod.Exceptions;
using ScriptingMod.Extensions;

namespace ScriptingMod
{
    [Flags]
    internal enum RepairEngineScans
    {
        WrongPowerItem = 1,
        LockedChunkRespawn = 2,
        DeathScreenLoop = 4,
        All = WrongPowerItem | LockedChunkRespawn | DeathScreenLoop
    }

    internal class RepairEngine
    {
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
        public RepairEngineScans Scans = RepairEngineScans.All;

        public int ScannedChunks;
        public int ProblemsFound;

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
            if (Scans.HasFlag(RepairEngineScans.WrongPowerItem) || Scans.HasFlag(RepairEngineScans.LockedChunkRespawn))
            {
                if (WorldPos == null)
                {
                    var chunks = World.ChunkCache.GetChunkArray().ToList();
                    Output("Scanning all loaded chunks for inconsistent data ...");
                    foreach (var chunk in chunks)
                    {
                        RepairChunk(chunk);
                    }
                }
                else
                {
                    var chunk = World.GetChunkFromWorldPos(WorldPos.Value) as Chunk;
                    if (chunk == null)
                        throw new FriendlyMessageException(Resources.ErrorAreaTooFarAway);

                    Output($"Scanning {chunk} for inconsistent data ...");
                    RepairChunk(chunk);
                }
            }

            // Scan players
            if (Scans.HasFlag(RepairEngineScans.DeathScreenLoop))
            {
                Output("Scanning all players for inconsistent data ...");
                // TODO
            }
        }

        /// <summary>
        /// Scans the given chunk object for broken power blocks and optionally fixes them
        /// </summary>
        /// <param name="chunk">The chunk object; must be loaded and ready</param>
        private void RepairChunk([NotNull] Chunk chunk)
        {
            if (Scans.HasFlag(RepairEngineScans.LockedChunkRespawn))
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

            Log.Debug($"Scanning area master chunk {chunk.X}, {chunk.Z} with spawn data: {spawnData}");
            foreach (var groupName in spawnData.GetEntityGroupNames())
            {
                // Fix respawn problems
                if (!IsValidSpawnData(spawnData, groupName))
                {
                    ProblemsFound++;

                    if (!Simulate)
                        spawnData.ClearRespawnLocked(groupName);

                    Vector3i from = chunk.ToWorldPos(Vector3i.zero);
                    Vector3i to = new Vector3i(from.x + Chunk.cAreaMasterSizeBlocks - 1, byte.MaxValue, from.y + Chunk.cAreaMasterSizeBlocks - 1);
                    string msg = $"{(Simulate ? "Found" : "Repaired")} locked respawn of {groupName} in area {from} to {to}.";
                    Log.Warning(msg);
                    Output(msg);
                }

            }
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

                var msg = $"{(Simulate ? "Found" : "Repaired")} broken power block at {tileEntity.ToWorldPos()} in {tileEntity.GetChunk()}.";
                Log.Warning(msg);
                Output(msg);
            }
        }

        /// <summary>
        /// Returns false if respawn appears to be locked for the given groupName, true otherwise
        /// </summary>
        private static bool IsValidSpawnData(ChunkAreaBiomeSpawnData spawnData, string groupName)
        {
            var respawnLockedUntil = spawnData.GetRespawnLockedUntilWorldTime(groupName);
            if (respawnLockedUntil > World.worldTime + MaxAllowedRespawnDelay)
            {
                Log.Debug($"Area master {spawnData.chunk} has respawning of {groupName} locked for {respawnLockedUntil / 1000} game hours, which is more than the maximum allowed {MaxAllowedRespawnDelay / 1000} game hours");
                return false;
            }

            if (respawnLockedUntil == 0UL)
            {
                int registeredEntites = spawnData.GetEntitiesSpawned(groupName);
                int spawnedEntities = World.Entities.list.Count(
                    e => e.GetSpawnerSource() == EnumSpawnerSource.Biome
                         && e.GetSpawnerSourceChunkKey() == spawnData.chunk.Key
                         && e.GetSpawnerSourceEntityGroupName() == groupName);

                if (registeredEntites > spawnedEntities)
                {
                    Log.Debug($"Area master {spawnData.chunk} has {registeredEntites} entites of {groupName} registered, but there are {spawnedEntities} in the world.");
                    return false;
                }
            }

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
