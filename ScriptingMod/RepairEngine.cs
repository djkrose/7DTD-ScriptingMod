using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using JetBrains.Annotations;
using ScriptingMod.Exceptions;
using ScriptingMod.Extensions;
using UnityEngine;

namespace ScriptingMod
{
    internal class RepairEngine
    {
        /// <summary>
        /// List of available repair tasks with a unique key and their description.
        /// </summary>
        public static readonly SortedDictionary<string, string> TasksDict = new SortedDictionary<string, string>
        {
            { "d", "Repair chunk density causing distorted textures and the error \"Failed setting triangles...\". (can take long)"},
            //{"f", "Remove endlessly falling blocks."},
            //{"l", "Fix death screen loop due to corrupt player files."},
            //{"m", "Bring all stuck minibikes to the surface."},
            {"p", "Fix corrupt power blocks and error \"NullReferenceException at TileEntityPoweredTrigger.write ...\"."},
            {"r", "Reset locked respawn of biome zombies and animals, especially after using settime, bc-remove, or dj-regen."},
            {"t", "Fix bugged trees that have zero HP without falling and give endless wood."},
        };

        public const string TasksDefault = "dprt";

        /// <summary>
        /// Entities are allowed to wander this many chunks away from their initial 5x5 chunk area
        /// </summary>
        private const int EntitiySearchRadius = 4;

        /// <summary>
        /// Delay to wait until first background timer execution, in ms
        /// </summary>
        private const int TimerDelay = 10000;

        /// <summary>
        /// If set to true, problems will only be reported without fixing them
        /// </summary>
        private readonly bool _simulate;

        /// <summary>
        /// Case-sensitive letters of tasks to scan for; can be empty but never null
        /// </summary>
        [NotNull]
        private readonly string _tasks;

        /// <summary>
        /// When set, output can be sent back to the console of the sender; if set to null, no console output is sent
        /// </summary>
        private readonly CommandSenderInfo? _senderInfo;

        private Stopwatch _stopwatch;

        /// <summary>
        /// Number of problems that this repair engine has found (and attempted fixing unless simulating) so far
        /// </summary>
        private int _problemsFound;
        private int _scannedChunks;

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

        private readonly World World = GameManager.Instance.World ?? throw new ApplicationException(Resources.ErrorWorldNotReady);

        private List<BlockChangeInfo> _blockChangeInfos = new List<BlockChangeInfo>();

        private static ulong _maxAllowedRespawnDelayCache;
        private static DateTime _lastLogMessageTriggeredScan = default(DateTime);
        private static System.Threading.Timer _backgroundTimer;
        private static object _syncLock = new object();

        public RepairEngine(string tasks, bool simulate, CommandSenderInfo? senderInfo = null)
        {
            _tasks = tasks;
            _simulate = simulate;
            _senderInfo = senderInfo;
        }

        /// <summary>
        /// The cached maximum allowed respawn delay in world ticks for ChunkBiomeSpawnData entires.
        /// See: ChunkAreaBiomeSpawnData.SetRespawnLocked(..) and EntityPlayer.onSpawnStateChanged(..)
        /// </summary>
        private ulong MaxAllowedRespawnDelay
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

        /// <summary>
        /// Activates automatic background repairs based on the settings in PersistentData.Instance.Repair*.
        /// Will NOT reset the counters so that it can be also used to continue background checks after restart.
        /// </summary>
        public static void AutoOn()
        {
            string tasks         = PersistentData.Instance.RepairTasks;
            bool   simulate      = PersistentData.Instance.RepairSimulate;
            int    timerInterval = PersistentData.Instance.RepairInterval; // seconds

            if (tasks.Contains("p"))
                Application.logMessageReceived += LogMessageReceived;

            _backgroundTimer = new System.Threading.Timer(delegate
            {
                Log.Debug("Automatic background repair timer started.");
                try
                {
                    var repairEngine = new RepairEngine(PersistentData.Instance.RepairTasks, PersistentData.Instance.RepairSimulate);
                    repairEngine.Start();

                    if (repairEngine._problemsFound >= 1)
                    {
                        PersistentData.Instance.RepairCounter += repairEngine._problemsFound;
                        PersistentData.Instance.Save();
                    }
                    Log.Debug("Automatic background repair timer ended.");
                }
                catch (Exception ex)
                {
                    Log.Error("Error while running repair in background: " + ex);
                    throw;
                }
            }, null, TimerDelay, timerInterval * 1000);

            SdtdConsole.Instance.LogAndOutput($"Automatic background {(simulate ? "scan (without repair)" : "repair")} for server problem{(tasks.Length == 1 ? "" : "s")} '{tasks}' every {timerInterval} seconds turned ON.");
            SdtdConsole.Instance.Output("To turn off, enter \"dj-repair /auto\" again.");
        }

        /// <summary>
        /// Turn automatic background repairs off and log the results.
        /// No counters are reset.
        /// </summary>
        public static void AutoOff()
        {
            Application.logMessageReceived -= LogMessageReceived;
            _backgroundTimer?.Dispose();

            SdtdConsole.Instance.LogAndOutput($"Automatic background {(PersistentData.Instance.RepairSimulate ? "scan (without repair)" : "repair")}" +
                   $" for server problems {PersistentData.Instance.RepairTasks} turned OFF.");
            SdtdConsole.Instance.Output($"Report: {PersistentData.Instance.RepairCounter} problem{(PersistentData.Instance.RepairCounter == 1 ? " was" : "s were")} " +
                   $"{(PersistentData.Instance.RepairSimulate ? "identified" : "repaired")} since it was turned on.");
        }

        public static void InitAuto()
        {
            try
            {
                if (PersistentData.Instance.RepairAuto)
                    AutoOn();
            }
            catch (Exception ex)
            {
                Log.Error("Could not initialize automatic background repair: " + ex);
            }
        }

        private static void LogMessageReceived(string condition, string stackTrace, LogType type)
        {
            // Scan not more than every 5 seconds while log is spammed with error messages
            if (_lastLogMessageTriggeredScan.AddSeconds(5) > DateTime.Now)
                return;

            _lastLogMessageTriggeredScan = DateTime.Now;

            // Check if this is the exception we are looking for
            if (type == LogType.Exception
                && condition == "NullReferenceException: Object reference not set to an instance of an object"
                && stackTrace != null && stackTrace.StartsWith("TileEntityPoweredTrigger.write"))
            {
                // Doing it in background task so that NRE appears before our output in log
                ThreadManager.AddSingleTask(info => RepairPowerBlocks());
            }
            else
            {
                Log.Debug($"Intercepted unknown log message:\r\ncondition={condition ?? "<null>"}\r\ntype={type}\r\nstackTrace={stackTrace ?? "<null>"}");
            }
        }

        /// <summary>
        /// Starts a new engine to repair all loaded power blocks once based on the settings in PersistentData
        /// and saves results back into PersistentData. To be called in background thread.
        /// </summary>
        private static void RepairPowerBlocks()
        {
            try
            {
                Log.Out("Detected NRE TileEntityPoweredTrigger.write. Starting integrity scan in background ...");
                var repairEngine = new RepairEngine("p", PersistentData.Instance.RepairSimulate);
                repairEngine.Start();

                if (repairEngine._problemsFound >= 1)
                {
                    PersistentData.Instance.RepairCounter += repairEngine._problemsFound;
                    PersistentData.Instance.Save();
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error while running power block repair in background: " + ex);
            }
        }

        public void Start()
        {
            // Should not happen when parameters are parsed correctly
            if (_tasks == string.Empty)
                throw new ApplicationException("No repair tasks set.");

            if (!Monitor.TryEnter(_syncLock))
            {
                WarningAndOutput("Skipping repair task because another repair is already running.");
                return;
            }
            try
            {
                StartUnsynchronized();
            }
            finally
            {
                Monitor.Exit(_syncLock);
            }
        }

        private void StartUnsynchronized()
        {
            _stopwatch = new MicroStopwatch(true);
            LogAndOutput($"{(_simulate ? "Scan (without repair)" : "Repair")} for server problem(s) '{_tasks}' started.");

            // Scan/repair chunks
            if (_tasks.ContainsAnyChar("dprt"))
            {
                if (World.ChunkCache == null || World.ChunkCache.Count() == 0)
                {
                    Output("No chunks loaded. Skipping all chunk-related tasks.");
                }
                else
                {
                    Output("Scanning all loaded chunks ...");
                    foreach (var chunk in World.ChunkCache.GetChunkArrayCopySync())
                        RepairChunk(chunk);
                }
            }

            // Scan players
            //if (_tasks.HasFlag(RepairTasks.DeathScreenLoop))
            //{
            //    Output("Scanning all players ...");
            //    
            //}

            // Execute collected change info objects and distribute to clients in one go
            if (_blockChangeInfos.Count > 0)
            {
                Log.Debug($"Executing and distributing {_blockChangeInfos.Count} BlockChangeInfo objects ...");
                World.SetBlocksRPC(_blockChangeInfos);
                // Don't clear but replace list because list is used asynchronically in SetBlocksRPC(_blockChangeInfos) later
                _blockChangeInfos = new List<BlockChangeInfo>();
            }

            var msg = $"{(_simulate ? "Identified" : "Repaired")} {_problemsFound} problem{(_problemsFound != 1 ? "s" : "")}";
            if (_scannedChunks > 0)
                msg += $" in {_scannedChunks} chunk{(_scannedChunks != 1 ? "s" : "")}";
            msg += ".";
            Log.Out(msg);
            Output(msg + " [details in server log]");
            Log.Debug($"Repair engine done. Execution took {_stopwatch.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Scans the given chunk object and optionally fixes problems in it
        /// </summary>
        /// <param name="chunk">The chunk object; must be loaded and ready</param>
        private void RepairChunk([NotNull] Chunk chunk)
        {
            if (_tasks.Contains("r"))
                RepairChunkRespawn(chunk);

            // Scan tile entities
            if (_tasks.Contains("p"))
                foreach (var tileEntity in chunk.GetTileEntities().list.ToList())
                    RepairTileEntity(tileEntity);

            // Scan blocks
            if (_tasks.ContainsAnyChar("dt"))
            {
                // Performance improvement: Only task 't' requires block value with damage info
                var needsDamageValue = _tasks.Contains("t");

                var problemsDensity = 0;
                var problemsTree = 0;
                for (int x = 0; x < Constants.ChunkSize; ++x)
                {
                    for (int z = 0; z < Constants.ChunkSize; ++z)
                    {
                        for (int y = 0; y < Constants.ChunkHeight; ++y)
                        {
                            var blockValue = needsDamageValue ? chunk.GetBlock(x, y, z) : chunk.GetBlockNoDamage(x, y, z);
                            var pos = new Vector3i(x, y, z);

                            if (_tasks.Contains("d"))
                                problemsDensity += RepairBlockDensity(blockValue.Block, chunk, pos);

                            if (_tasks.Contains("t"))
                                problemsTree += RepairBlockTree(blockValue, chunk, pos);
                        }
                    }
                }

                if (problemsDensity > 0)
                {
                    WarningAndOutput($"{(_simulate ? "Found" : "Repaired")} {problemsDensity} density problem{(problemsDensity == 1 ? "" : "s")} in {chunk}.");
                    _problemsFound += problemsDensity;
                }

                if (problemsTree > 0)
                {
                    WarningAndOutput($"{(_simulate ? "Found" : "Repaired")} {problemsTree} buggged tree{(problemsTree == 1 ? "" : "s")} in {chunk}.");
                    _problemsFound += problemsTree;
                }
            }

            _scannedChunks++;
        }

        private int RepairBlockDensity(Block block, Chunk chunk, Vector3i posInChunk)
        {
            sbyte density = chunk.GetDensity(posInChunk.x, posInChunk.y, posInChunk.z);
            if (block.shape.IsTerrain())
            {
                if (density >= 0)
                {
                    Log.Debug($"Density is {density} but should be < 0 at {chunk.ToWorldPos(posInChunk)}.");
                    if (!_simulate)
                        chunk.SetDensity(posInChunk.x, posInChunk.y, posInChunk.z, -1);
                    return 1;
                }
            }
            else if (density < 0)
            {
                Log.Debug($"Density is {density} but should be >= 0 at {chunk.ToWorldPos(posInChunk)}.");
                if (!_simulate)
                    chunk.SetDensity(posInChunk.x, posInChunk.y, posInChunk.z, 1);
                return 1;
            }
            return 0;
        }

        private int RepairBlockTree(BlockValue blockValue, Chunk chunk, Vector3i posInChunk)
        {
            var block = blockValue.Block;
            // Bugged trees have more damage than max damage
            if (block is BlockModelTree && blockValue.damage >= block.MaxDamage)
            {
                Log.Debug($"Found tree with {blockValue.damage}/{block.MaxDamage} damage at {chunk.ToWorldPos(posInChunk)}.");
                if (!_simulate)
                {
                    // Remove all damage to tree and distribute changes to clients
                    blockValue.damage = 0;
                    _blockChangeInfos.Add(new BlockChangeInfo(chunk.ToWorldPos(posInChunk), blockValue, false, true));
                    //_world.SetBlockRPC(_clrIdx, _blockPos, BlockValue.Air);
                }
                return 1;
            }
            return 0;
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

            //Log.Debug($"Scanning area master {chunk} with spawn data: {spawnData}");
            foreach (var groupName in spawnData.GetEntityGroupNames().ToList())
            {
                RepairLongRespawnLock(spawnData, groupName);
                RepairLostEntities(spawnData, groupName);
            }
        }

        private void RepairLongRespawnLock([NotNull] ChunkAreaBiomeSpawnData spawnData, string groupName)
        {
            // Respawn is allowed to be locked for max 7 in-game days (MaxAllowedRespawnDelay)
            ulong respawnLockedUntil = spawnData.GetRespawnLockedUntilWorldTime(groupName);
            if (respawnLockedUntil <= World.worldTime + MaxAllowedRespawnDelay)
                return;

            // Problem: Respawn locked is too long; possibly due to modified worldtime with "settime"

            _problemsFound++;
            if (!_simulate)
            {
                spawnData.ClearRespawnLocked(groupName);
                spawnData.chunk.isModified = true;
            }
            WarningAndOutput($"{(_simulate ? "Found" : "Repaired")} respawn of {groupName} locked for {respawnLockedUntil / 1000 / 24} " +
                             $"game days in area master {spawnData.chunk}.");
        }

        private void RepairLostEntities([NotNull] ChunkAreaBiomeSpawnData spawnData, string groupName)
        {
            // No need to check if no entities are locked
            int registeredEntities = spawnData.GetEntitiesSpawned(groupName);
            if (registeredEntities <= 0)
                return;

            // Lock with timeout is handled by RepairLongRespawnLock(..)
            ulong respawnLockedUntil = spawnData.GetRespawnLockedUntilWorldTime(groupName);
            if (respawnLockedUntil > 0)
                return;

            // Less or equal entities are registered than online is normal;
            // less registered can occur when zombies from other chunks wander in
            var spawnedEntities = CountSpawnedEntities(EnumSpawnerSource.Biome, spawnData.chunk.Key, groupName);
            if (registeredEntities <= spawnedEntities)
                return;

            int lostEntities = registeredEntities - spawnedEntities;

            // Ignore missing zombies when it could come from surrounding chunks not loaded
            if (!AllChunksLoaded(spawnData.chunk, EntitiySearchRadius))
            {
                //Log.Debug($"Ignoring {lostEntities} lost {groupName} in area master {spawnData.chunk} " +
                //          $"because not all it's 5x5 chunks + {EntitiySearchRadius} chunks around them are loaded.");
                return;
            }

            // Problem: Zombies are registered in the chunk that cannot be found alive anywhere; they might have disappeared or wandered too far off

            _problemsFound++;
            if (!_simulate)
            {
                SetEntitiesSpawned(spawnData, groupName, spawnedEntities);
            }
            WarningAndOutput($"{(_simulate ? "Found" : "Repaired")} respawn of {groupName} locked because of {lostEntities} lost " +
                             $"{(lostEntities == 1 ? "entity" : "entities")} in area master {spawnData.chunk}.");
        }

        /// <summary>
        /// Find/fix problems with TileEntityPowered objects and their PowerItems,
        /// which may caus NRE at TileEntityPoweredTrigger.write and other problems
        /// </summary>
        /// <param name="tileEntity">Tile entity to repair; currently only TileEntityPowered type is scanned/repaired</param>
        private void RepairTileEntity([NotNull] TileEntity tileEntity)
        {
            if (tileEntity is TileEntityPowered powered && !IsValidTileEntityPowered(powered))
            {
                _problemsFound++;

                if (!_simulate)
                    RecreateTileEntity(tileEntity);

                LogAndOutput($"{(_simulate ? "Found" : "Repaired")} corrupt power block at {tileEntity.ToWorldPos()} in {tileEntity.GetChunk()}.");
            }
        }

        /// <summary>
        /// The the entitiesSpawned private variable in spawnData for the given groupName to the new value
        /// by repeatedly calling Inc.. or Dec.. methods. This is faster and less complicated than reflection
        /// </summary>
        /// <param name="spawnData">Class to modify</param>
        /// <param name="groupName">Spawn group name to change entry for</param>
        /// <param name="entitiesSpawned">New value</param>
        private static void SetEntitiesSpawned([NotNull] ChunkAreaBiomeSpawnData spawnData, string groupName, int entitiesSpawned)
        {
            int delta = entitiesSpawned - spawnData.GetEntitiesSpawned(groupName);
            for (int i = 0; i < delta; i++)
                spawnData.IncEntitiesSpawned(groupName);
            for (int i = 0; i > delta; i--)
                spawnData.DecEntitiesSpawned(groupName);
        }

        /// <summary>
        /// Returns the number of active entities in the world, filtered by the given spawner source, sourceChunkKey, and sourceEntityGroup
        /// </summary>
        /// <param name="spawnerSource"></param>
        /// <param name="spawnerSourceChunkKey"></param>
        /// <param name="spawnerSourceEntityGroupName"></param>
        /// <returns>The number of found entities; can be 0</returns>
        private int CountSpawnedEntities(EnumSpawnerSource spawnerSource, long spawnerSourceChunkKey, string spawnerSourceEntityGroupName)
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
        private bool AllChunksLoaded([NotNull] Chunk areaMasterChunk, int extendBy)
        {
            if (World.ChunkCache == null)
                return false;

            if (!areaMasterChunk.IsAreaMaster())
                throw new ArgumentException("Given chunk is not an area master chunk.", nameof(areaMasterChunk));

            lock (World.ChunkCache.GetSyncRoot())
            {
                for (int x = areaMasterChunk.X - extendBy; x < areaMasterChunk.X + Chunk.cAreaMasterSizeChunks + extendBy; x++)
                    for (int z = areaMasterChunk.Z - extendBy; z < areaMasterChunk.Z + Chunk.cAreaMasterSizeChunks + extendBy; z++)
                        if (!World.ChunkCache.ContainsChunkSync(WorldChunkCache.MakeChunkKey(x, z, areaMasterChunk.ClrIdx)))
                            return false;
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

            if (te is TileEntityPoweredTrigger teTrigger)
            {
                // Trigger must be handled differently, because there are multiple possible power items for one TileEntityPoweredTriger,
                // and the PowerItemType is sometimes just (incorrectly) "PowerSource" when the TriggerType determines the *real* power type.

                // CHECK 1: Power item should be of type PowerTrigger if this is a TileEntityPoweredTrigger
                if (!(pi is PowerTrigger piTrigger))
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
        private void RecreateTileEntity([NotNull] TileEntity tileEntity)
        {
            var chunk = tileEntity.GetChunk();

            // Prevent further errors on client updates; crucial when removing power item!
            tileEntity.SetDisableModifiedCheck(true);

            // Remove corrupt tile entity
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

                // Wires to the corrupt block are cut and not restored. We could try to reattach everything, but meh...
            }

            var newPowerItem = newPowered?.GetPowerItem();
            Log.Debug($"[{tileEntity.ToWorldPos()}] Replaced old {tileEntity.GetType()} with new {newTileEntity.GetType()}" +
                      $"{(newPowerItem != null ? " and new power item " + newPowerItem.GetType() : "")}.");
        }

        private void WarningAndOutput(string msg)
        {
            Log.Warning(msg);
            Output(msg);
        }

        private void LogAndOutput(string msg)
        {
            Log.Out(msg);
            Output(msg);
        }

        private void Output(string msg)
        {
            if (_senderInfo != null)
                SdtdConsole.Instance.OutputAsync(_senderInfo.Value, msg);
        }
    }
}
