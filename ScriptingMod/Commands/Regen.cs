using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using ScriptingMod.Exceptions;
using ScriptingMod.Extensions;
using ScriptingMod.Tools;

namespace ScriptingMod.Commands
{
    [UsedImplicitly]
    public class Regen : ConsoleCmdAbstract
    {
        /// <summary>
        /// Saves last position for each entity executing the command individually: entityId => position
        /// </summary>
        private static Dictionary<int, Vector3i> savedPos = new Dictionary<int, Vector3i>();

        public override string[] GetCommands()
        {
            return new[] { "dj-regen" };
        }

        public override string GetDescription()
        {
            return @"Regenerates a chunk or custom area based on the world seed.";
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
                1. Regenerates the chunk of the current player's position.
                2. Regenerates all chunks of the given area, extended to chunk borders.
                3. Saves the current player's position for usage 4.
                4. Regenerates all chunks of the area from the saved position to current players position, extended to chunk borders.
                ".Unindent();
        }

        public override void Execute(List<string> parameters, CommandSenderInfo senderInfo)
        {
            TelemetryTools.CollectEvent("command", "execute", GetCommands()[0]);
            try
            {
                var world = GameManager.Instance.World ?? throw new FriendlyMessageException(Resources.ErrorWorldNotReady);
                var chunkCache = world.ChunkCache ?? throw new FriendlyMessageException(Resources.ErrorChunkCacheNotReady);

                (var pos1, var pos2) = ParseParams(parameters, senderInfo);
                WorldTools.OrderAreaBounds(ref pos1, ref pos2);

                // Check if all needed chunks are in cache
                foreach (var chunkKey in GetChunksForArea(pos1, pos2, +1))
                    if (!chunkCache.ContainsChunkSync(chunkKey))
                        throw new FriendlyMessageException(Resources.ErrorAreaTooFarAway);

                ThreadManager.AddSingleTask(info => RegenerateChunks(pos1, pos2, senderInfo));
            }
            catch (Exception ex)
            {
                CommandTools.HandleCommandException(ex);
            }
        }

        /// <summary>
        /// Parses the given command parameters for the sender and returns the area to regenerate chunks for.
        /// </summary>
        /// <param name="parameters"></param>
        /// <param name="senderInfo"></param>
        /// <returns>The area from pos1 to pos2 for which chunks should be regenerated; y value should be ignored</returns>
        private static (Vector3i pos1, Vector3i pos2) ParseParams(List<string> parameters, CommandSenderInfo senderInfo)
        {
            Vector3i pos1, pos2;
            switch (parameters.Count)
            {
                case 0:
                    // one point is enough; area will extend to the chunk
                    pos1 = pos2 = senderInfo.GetRemoteClientInfo().GetEntityPlayer().GetServerPos().ToVector3i();
                    break;
                case 1:
                    var ci = senderInfo.GetRemoteClientInfo();
                    var currentPos = ci.GetEntityPlayer().GetServerPos().ToVector3i();
                    switch (parameters[0])
                    {
                        case "from":
                            savedPos[ci.entityId] = currentPos;
                            throw new FriendlyMessageException("Your current position was saved: " + savedPos[ci.entityId]);
                        case "to":
                            if (!savedPos.ContainsKey(ci.entityId))
                                throw new FriendlyMessageException("Please save start point of the area first. See help for details.");
                            pos1 = savedPos[ci.entityId];
                            pos2 = currentPos;
                            break;
                        default:
                            throw new FriendlyMessageException("Parameter unknown. See help for details.");
                    }
                    break;
                case 4:
                    pos1 = CommandTools.ParseXZ(parameters, 0);
                    pos2 = CommandTools.ParseXZ(parameters, 2);
                    break;
                default:
                    throw new FriendlyMessageException(Resources.ErrorParameerCountNotValid);
            }

            return (pos1, pos2);
        }

        /// <summary>
        /// Executes the main regenerate task for all chunks of the given area. This method should be called asynchronously.
        /// </summary>
        /// <param name="pos1">South-West corner of area; y is ignored</param>
        /// <param name="pos2">North-East corner of area; y is ignored</param>
        /// <param name="senderInfo">Info about the command sender; must contain valid senderInfo.NetworkConnection object</param>
        private void RegenerateChunks(Vector3i pos1, Vector3i pos2, CommandSenderInfo senderInfo)
        {
            try
            {
                var chunksToRegenerate = GetChunksForArea(pos1, pos2);
                var chunksToReload = GetChunksForArea(pos1, pos2, +1);

                SdtdConsole.Instance.LogAndOutputAsync(senderInfo, $"Started regenerating {chunksToRegenerate.Count} chunk{(chunksToRegenerate.Count != 1 ? "s" : "")} in background ...");

                for (int i=0; i<chunksToRegenerate.Count; i++)
                {
                    RegenerateChunk(chunksToRegenerate.ElementAt(i));
                    if (chunksToRegenerate.Count > 1)
                        SdtdConsole.Instance.OutputAsync(senderInfo, $"Regenerated chunk {i + 1}/{chunksToRegenerate.Count}.");
                }

                // Redefine pos1/2 to actual regenerated area dimensions
                var realPos1 = new Vector3i(World.toChunkXZ(pos1.x) * Constants.ChunkSize, 0, World.toChunkXZ(pos1.z) * Constants.ChunkSize);
                var realPos2 = new Vector3i(World.toChunkXZ(pos2.x) * Constants.ChunkSize + Constants.ChunkSize - 1, Constants.ChunkHeight, World.toChunkXZ(pos2.z) * Constants.ChunkSize + Constants.ChunkSize - 1);

                SdtdConsole.Instance.LogAndOutputAsync(senderInfo, $"Regenerated {chunksToRegenerate.Count} chunk{(chunksToRegenerate.Count != 1 ? "s" : "")} for area from {realPos1} to {realPos2}.");
                // Reload chunks for clients, including neighbouring chunks for smooth terrain transition
                ChunkTools.ReloadForClients(chunksToReload);
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
                SdtdConsole.Instance.OutputAsync(senderInfo, string.Format(Resources.ErrorDuringCommand, ex.Message));
            }
        }

        private static void RegenerateChunk(long chunkKey)
        {
            var world      = GameManager.Instance.World ?? throw new NullReferenceException(Resources.ErrorWorldNotReady);
            var chunkCache = world.ChunkCache ?? throw new NullReferenceException(Resources.ErrorChunkCacheNotReady);
            var chunkXZ    = ChunkTools.ChunkKeyToChunkXZ(chunkKey);

            Log.Debug($"Starting regeneration of chunk {chunkXZ.x}, {chunkXZ.z} ...");

            var chunkProvider = chunkCache.ChunkProvider as ChunkProviderGenerateWorld;
            if (chunkProvider == null)
                throw new ApplicationException("Found unexpected chunk provider: " + (chunkCache.ChunkProvider?.GetType().ToString() ?? "null"));

            // See: ChunkProviderGenerateWorld.DoGenerateChunks()

            var random = Utils.RandomFromSeedOnPos(chunkXZ.x, chunkXZ.z, world.Seed);
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
            lock (chunkCache.GetSyncRoot())
            {
                if (chunkCache.ContainsChunkSync(chunkKey))
                    chunkCache.RemoveChunkSync(chunkKey);
                if (!chunkCache.AddChunkSync(newChunk))
                    throw new ApplicationException("Could not add newly generated chunk to cache."); 
            }

            // Add overlapping decorations and smoothen out terrain borders; needs this and all 8 neighbouring chunks in cache
            Log.Debug("Placing overlapping decorations and smoothing transitions to other chunks ...");
            chunkProvider.DecorateChunkOverlapping(newChunk);

            // Ensure it's being saved automatically before unload or shutdown
            newChunk.isModified = true;

            Log.Debug($"Done regenerating chunk {chunkXZ.x}, {chunkXZ.z}.");
        }

        /// <summary>
        /// Returns all chunk keys that are located (also partly) in the given area.
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
