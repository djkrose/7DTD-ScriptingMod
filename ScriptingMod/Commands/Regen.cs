using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using JetBrains.Annotations;
using ScriptingMod.Exceptions;
using ScriptingMod.Extensions;
using ScriptingMod.Tools;
using ThreadInfo = ThreadManager.ThreadInfo;

namespace ScriptingMod.Commands
{
    /*
     * TODO [P2]: Allow regenerating unloaded chunks
     */
     
    [UsedImplicitly]
    public class Regen : ConsoleCmdAbstract
    {
        private enum Mode { Chunk, Area, SavePos, WalkOn, WalkOff }

        /// <summary>
        /// Saves last position for each entity executing the command individually: playerId => position
        /// </summary>
        private static Dictionary<int, Vector3i> savedPos = new Dictionary<int, Vector3i>();

        /// <summary>
        /// All entityId of players how have touch mode for regeneration on.
        /// </summary>
        private static Dictionary<int, ThreadInfo> walkModeThreads = new Dictionary<int, ThreadInfo>();

        public override string[] GetCommands()
        {
            return new[] { "dj-regen" };
        }

        public override string GetDescription()
        {
            return @"Regenerates a chunk or custom area.";
            
        }

        public override string GetHelp()
        {
            // ----------------------------------(max length: 120 char)----------------------------------------------------------------|
            return @"
                Regenerates chunks (16x16 blocks) from bedrock to sky. All player-built structures are removed and the area is recreated
                from the world seed as if it was visited for the first time. All content like prefabs, cars, vegetation, even traders
                are respawned. A given area is extended to chunk borders.
                Usage:
                    1. dj-regen
                    2. dj-regen <x1> <z1> <x2> <z2>
                    3. dj-regen from
                    4. dj-regen to
                    5. dj-regen walk
                1. Regenerates the chunk of the current player's position.
                2. Regenerates all chunks of the given area, extended to chunk borders.
                3. Saves the current player's position for usage 4.
                4. Regenerates all chunks of the area from the saved position to current players position, extended to chunk borders.
                5. Turns on/off permanent regeneration of all chunks that you walk (or better fly) through.
                ".Unindent();
        }

        public override void Execute(List<string> parameters, CommandSenderInfo senderInfo)
        {
            try
            {
                (var mode, var pos1, var pos2) = ParseParams(parameters, senderInfo);

                ClientInfo ci;
                switch (mode)
                {
                    case Mode.Chunk:
                    case Mode.Area:
                        WorldTools.OrderAreaBounds(ref pos1, ref pos2);
                        // Check if all needed chunks are in cache
                        foreach (var chunkKey in GetChunksForArea(pos1, pos2, +1))
                            if (!GameManager.Instance.World.ChunkCache.ContainsChunkSync(chunkKey))
                                throw new FriendlyMessageException(Resources.ErrorAreaTooFarAway);
                        ThreadManager.AddSingleTask(info => RegenerateChunksAsync(pos1, pos2, senderInfo));
                        break;

                    case Mode.SavePos:
                        ci = senderInfo.RemoteClientInfo;
                        savedPos[ci.entityId] = PlayerTools.GetPosition(ci);
                        SdtdConsole.Instance.Output("Your current position was saved: " + savedPos[ci.entityId]);
                        break;

                    case Mode.WalkOn:
                        ci = senderInfo.RemoteClientInfo;
                        walkModeThreads[ci.entityId] = ThreadManager.StartThread($"dj-regen walk [entityId: {ci.entityId}]",
                            t => RegenerateWalkAsync(senderInfo), ThreadPriority.Lowest);
                        Log.Out($"Player {ci.playerName} ({ci.playerId}) turned chunk generation in walk mode ON.");
                        SdtdConsole.Instance.Output("Chunk regeneration in walk mode turned ON.");
                        break;

                    case Mode.WalkOff:
                        ci = senderInfo.RemoteClientInfo;
                        var threadInfo = walkModeThreads[ci.entityId];
                        threadInfo.thread.Interrupt();
                        try
                        {
                            if (threadInfo.thread.IsAlive && !threadInfo.thread.Join(2000))
                                threadInfo.thread.Abort();
                        }
                        catch (Exception)
                        {
                             // join or abort fails if thread already ended; that's ok
                        }
                        walkModeThreads.Remove(ci.entityId);
                        Log.Out($"Player {ci.playerName} ({ci.playerId}) turned chunk generation in walk mode OFF.");
                        SdtdConsole.Instance.Output("Chunk regeneration in walk mode turned OFF.");
                        break;
                }
            }
            catch (Exception ex)
            {
                CommandTools.HandleCommandException(ex);
            }
        }

        /// <summary>
        /// Parses the given command parameters for the sender and returns the action mode and area to regenerate chunks for.
        /// Also checks prerequisites and throws FriendlyMessageException on problems.
        /// </summary>
        /// <param name="parameters"></param>
        /// <param name="senderInfo"></param>
        /// <returns>The mode and area from pos1 to pos2 for which chunks should be regenerated; y value should be ignored.
        /// For Mode.Walk* and Mode.SavePos the pos1/2 is irrelevant</returns>
        /// <exception cref="FriendlyMessageException">On parse error or on wrong prerequisites (e.g. telnet user activates walk mode)</exception>
        private static (Mode mode, Vector3i pos1, Vector3i pos2) ParseParams(List<string> parameters, CommandSenderInfo senderInfo)
        {
            Vector3i pos1, pos2;
            Mode mode;

            switch (parameters.Count)
            {
                case 0:
                    pos1 = pos2 = PlayerTools.GetPosition(senderInfo);
                    mode = Mode.Chunk;
                    break;
                case 1:
                    var ci = PlayerTools.GetClientInfo(senderInfo);
                    switch (parameters[0])
                    {
                        case "from":
                            if (senderInfo.RemoteClientInfo == null)
                                throw new FriendlyMessageException(Resources.ErrorNotRemotePlayer);
                            pos1 = pos2 = Vector3i.zero;
                            mode = Mode.SavePos;
                            break;
                        case "to":
                            if (!savedPos.ContainsKey(ci.entityId))
                                throw new FriendlyMessageException("Please save start point of the area first. See help for details.");
                            pos1 = savedPos[ci.entityId];
                            pos2 = PlayerTools.GetPosition(ci);
                            mode = Mode.Area;
                            break;
                        case "walk":
                            pos1 = pos2 = Vector3i.zero;
                            mode = walkModeThreads.ContainsKey(ci.entityId) ? Mode.WalkOff : Mode.WalkOn;
                            break;
                        default:
                            throw new FriendlyMessageException("Parameter unknown. See help for details.");
                    }
                    break;
                case 4:
                    pos1 = CommandTools.ParseXZ(parameters, 0);
                    pos2 = CommandTools.ParseXZ(parameters, 2);
                    mode = Mode.Area;
                    break;
                default:
                    throw new FriendlyMessageException("Parameter count not valid. See help for details.");
            }

            return (mode, pos1, pos2);
        }

        /// <summary>
        /// Executes the main regenerate task for all chunks of the given area. This method should be called asynchronously.
        /// </summary>
        /// <param name="pos1">South-West corner of area; y is ignored</param>
        /// <param name="pos2">North-East corner of area; y is ignored</param>
        /// <param name="senderInfo">Info about the command sender; must EITHER have RemoteClientConnection or NetworkConnection object</param>
        private static void RegenerateChunksAsync(Vector3i pos1, Vector3i pos2, CommandSenderInfo senderInfo)
        {
            try
            {
                string msg;
                var chunksToRegenerate = GetChunksForArea(pos1, pos2);
                var chunksToReload = GetChunksForArea(pos1, pos2, +1);

                if (chunksToRegenerate.Count > 1)
                {
                    msg = $"Started regenerating {chunksToRegenerate.Count} chunk{(chunksToRegenerate.Count != 1 ? "s" : "")} in background ...";
                    Log.Out(msg);
                    SdtdConsole.Instance.OutputAsync(senderInfo, msg);
                }

                for (int i = 0; i < chunksToRegenerate.Count; i++)
                {
                    RegenerateChunk(chunksToRegenerate.ElementAt(i));
                    if (chunksToRegenerate.Count > 1)
                    {
                        msg = $"Regenerated chunk {i + 1}/{chunksToRegenerate.Count}.";
                        Log.Out(msg);
                        SdtdConsole.Instance.OutputAsync(senderInfo, msg);
                    }
                }

                // Calculate real pos1/2 to actual regenerated area dimensions
                var realPos1 = new Vector3i(World.toChunkXZ(pos1.x) * Constants.ChunkSize, 0, World.toChunkXZ(pos1.z) * Constants.ChunkSize);
                var realPos2 = new Vector3i(World.toChunkXZ(pos2.x) * Constants.ChunkSize + Constants.ChunkSize - 1, Constants.ChunkHeight, World.toChunkXZ(pos2.z) * Constants.ChunkSize + Constants.ChunkSize - 1);

                msg = $"Regenerated {chunksToRegenerate.Count} chunk{(chunksToRegenerate.Count != 1 ? "s" : "")} for area from {realPos1} to {realPos2}.";
                Log.Out(msg);
                SdtdConsole.Instance.OutputAsync(senderInfo, msg);

                // Reload chunks for clients, including neighbouring chunks for smooth terrain transition
                ChunkTools.ReloadForClients(chunksToReload);
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
                SdtdConsole.Instance.OutputAsync(senderInfo, string.Format(Resources.ErrorDuringCommand, ex.Message));
            }
        }

        /// <summary>
        /// Continously regenerates all chunks that the player walks through until the user dies or logs out
        /// or until the thread is gracefully stopped with thread.Interrupt() from outside.
        /// </summary>
        /// <param name="senderInfo">The senderInfo of the player to observe; must have senderInfo.RemoteClientInfo.entityId set</param>
        private static void RegenerateWalkAsync(CommandSenderInfo senderInfo)
        {
            try
            {
                var world         = GameManager.Instance.World;
                var ci            = senderInfo.RemoteClientInfo;
                var lastChunkXZ   = Vector2xz.None;
                EntityPlayer entity;

                Log.Debug($"Asynchronous chunk regeneration in walk mode started for {ci.playerName} ({ci.playerId})");

                while ((entity = world.Players.dict.GetValue(ci.entityId)) != null && entity.IsAlive())
                {
                    var stopwatch = Stopwatch.StartNew();
                    var worldPos  = entity.GetBlockPosition();
                    var chunkXZ = ChunkTools.WorldPosToChunkXZ(worldPos);

                    if (lastChunkXZ != chunkXZ)
                    {
                        lastChunkXZ = chunkXZ;
                        ThreadManager.AddSingleTask(info => RegenerateChunksAsync(worldPos, worldPos, senderInfo));
                    }

                    // Try to do a check every half second even when regeneration takes some time of it
                    Thread.Sleep(Math.Max(0, 500 - (int)stopwatch.ElapsedMilliseconds));
                }

                Log.Out($"Asynchronous chunk regeneration in walk mode ended for {ci.playerName} ({ci.playerId})" +
                    (entity == null ? " because he/she is gone." :
                        (!entity.IsAlive() ? " because he/she died." : " for unknown reasons.")));
                SdtdConsole.Instance.OutputAsync(senderInfo, "Chunk regeneration in walk mode automatically turned off.");
                walkModeThreads.Remove(ci.entityId);
            }
            catch (ThreadInterruptedException)
            {
                // Execution gracefully stopped during sleep/wait. Good!
                // see: https://stackoverflow.com/a/1327377/785111
                Log.Debug($"Thread {Thread.CurrentThread.Name} was stopped gracefully.");
            }
            catch (ThreadAbortException)
            {
                // Thread forcefully aborted in the middle of execution. Not so good.
                Log.Warning($"Thread {Thread.CurrentThread.Name} was forcefully aborted.");
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
                SdtdConsole.Instance.OutputAsync(senderInfo, string.Format(Resources.ErrorDuringCommand, ex.Message));
            }
        }

        /// <summary>
        /// Regenerates the chunk with the given chunkKey.
        /// New chunk is not yet sent to client.
        /// No console or Log output, only debug messages.
        /// </summary>
        /// <param name="chunkKey"></param>
        private static void RegenerateChunk(long chunkKey)
        {
            var world      = GameManager.Instance.World;
            var chunkCache = world.ChunkCache;
            var chunkXZ    = ChunkTools.ChunkKeyToChunkXZ(chunkKey);

            Log.Debug($"Starting regeneration of chunk {chunkXZ.x}, {chunkXZ.z} ...");

            var chunkProvider = chunkCache.ChunkProvider as ChunkProviderGenerateWorld;
            if (chunkProvider == null)
                throw new ApplicationException("Found unexpected chunk provider: " + (chunkCache.ChunkProvider?.GetType().ToString() ?? "null"));

            // See: ChunkProviderGenerateWorld.DoGenerateChunks()

            System.Random random = Utils.RandomFromSeedOnPos(chunkXZ.x, chunkXZ.z, world.Seed);
            Chunk newChunk = MemoryPools.PoolChunks.AllocSync(true);
            if (newChunk == null)
                throw new ApplicationException("Could not allocate new chunk from MemoryPool.");

            newChunk.X = chunkXZ.x;
            newChunk.Z = chunkXZ.z;
            Log.Debug("Generating terrain ...");
            chunkProvider.generateTerrain(world, newChunk, random);
            if (chunkProvider.IsDecorationsEnabled())
            {
                newChunk.NeedsDecoration = true;
                newChunk.NeedsLightCalculation = true;

                if (chunkProvider.GetDynamicPrefabDecorator() != null)
                {
                    Log.Debug("Adding prefab decorations ...");
                    chunkProvider.GetDynamicPrefabDecorator().DecorateChunk(world, newChunk, random);
                }
                if (chunkProvider.GetDynamicEntityDecorator() != null)
                {
                    Log.Debug("Adding entity decorations ...");
                    chunkProvider.GetDynamicEntityDecorator().DecorateChunk(world, newChunk, random);
                }
                if (chunkProvider.GetDynamicEntitySpawnerDecorator() != null)
                {
                    Log.Debug("Adding spawners ...");
                    chunkProvider.GetDynamicEntitySpawnerDecorator().DecorateChunk(world, newChunk, random);
                }
            }
            else
            {
                Log.Debug("Decorations are disabled.");
                newChunk.NeedsDecoration = false;
                newChunk.NeedsLightCalculation = false;
                newChunk.NeedsRegeneration = true;
            }
            
            // Replace old chunk with new chunk
            Log.Debug("Replacing current chunk with new chunk ...");
            if (chunkCache.ContainsChunkSync(chunkKey))
                chunkCache.RemoveChunkSync(chunkKey);
            if (!chunkCache.AddChunkSync(newChunk))
                throw new ApplicationException("Could not add newly generated chunk to cache.");

            // Add overlapping decorations and smoothen out terrain borders; needs this and all 8 neighbouring chunks in cache
            Log.Debug("Placing overlapping decorations and smoothing transitions to other chunks ...");
            chunkProvider.DecorateChunkOverlapping(newChunk);

            // Ensure it's being saved automatically before unload or shutdown
            newChunk.isModified = true;

            Log.Debug($"Done regenerating chunk {chunkXZ.x}, {chunkXZ.z}.");
        }

        /// <summary>
        /// Returns all chunks that are located (also partly) in the given area.
        /// </summary>
        /// <param name="pos1">South-West corner of area; y is ignored</param>
        /// <param name="pos2">North-East corner of area; y is ignored</param>
        /// <param name="expand">Allows expanding (or with negative value contracting) the area by the given number of chunks.
        /// For example with +1 it returns also neighbouring chunks.</param>
        /// <returns>Collection of chunk keys; chunks may or may not be loaded</returns>
        private static ICollection<long> GetChunksForArea(Vector3i pos1, Vector3i pos2, int expand = 0)
        {
            expand *= Constants.ChunkSize; // expand is now in blocks
            var chunkKeys = new List<long>();
            for (var x = pos1.x - expand; x <= pos2.x + expand; x += Constants.ChunkSize)
            {
                for (var z = pos1.z - expand; z <= pos2.z + expand; z += Constants.ChunkSize)
                {
                    chunkKeys.Add(WorldChunkCache.MakeChunkKey(World.toChunkXZ(x), World.toChunkXZ(z)));
                }
            }
            return chunkKeys;
        }

    }
}
