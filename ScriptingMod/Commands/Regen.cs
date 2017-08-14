using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using JetBrains.Annotations;
using ScriptingMod.Exceptions;
using ScriptingMod.Extensions;
using ScriptingMod.Tools;
using UnityEngine;

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
            return @"Regenerates a chunk or custom area.";
        }

        public override string GetHelp()
        {
            // ----------------------------------(max length: 120 char)----------------------------------------------------------------|
            return $@"
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
            try
            {
                (Vector3i pos1, Vector3i pos2) = ParseParams(parameters, senderInfo);
                WorldTools.OrderAreaBounds(ref pos1, ref pos2);

                var chunks = GetChunksForArea(pos1, pos2);
                var count  = chunks.Count;
                SdtdConsole.Instance.OutputAndLog($"Starting regeneration of {count} chunk{(count != 1 ? "s" : "")} ...");
                foreach (var key in chunks)
                    RegenerateChunk(key);

                // Expand by one to cover neighbouring chunks for smooth terrain transition
                chunks = GetChunksForArea(pos1, pos2, +1);
                ChunkTools.ReloadForClients(chunks);

                // Reset pos1/2 to actual regenerated area dimensions
                pos1 = new Vector3i(World.toChunkXZ(pos1.x) * Constants.ChunkSize, 0, World.toChunkXZ(pos1.z) * Constants.ChunkSize);
                pos2 = new Vector3i(World.toChunkXZ(pos2.x) * Constants.ChunkSize + Constants.ChunkSize - 1, Constants.ChunkHeight,
                    World.toChunkXZ(pos2.z) * Constants.ChunkSize + Constants.ChunkSize - 1);

                SdtdConsole.Instance.OutputAndLog($"Regenerated {count} chunk{(count != 1 ? "s" : "")} for area from {pos1} to {pos2}.");
            }
            catch (Exception ex)
            {
                CommandTools.HandleCommandException(ex);
            }
        }

        /// <summary>
        /// Returns all chunks that are located (also partly) in the given area.
        /// </summary>
        /// <param name="worldPos1">South-West corner of area; y is ignored</param>
        /// <param name="worldPos2">North-East corner of area; y is ignored</param>
        /// <param name="expand">Allows expanding (or with negative value contracting) the area by the given number of chunks.
        /// For example with +1 it returns also neighbouring chunks.</param>
        /// <returns>Collection of chunk keys; chunks may or may not be loaded</returns>
        private static ICollection<long> GetChunksForArea(Vector3i worldPos1, Vector3i worldPos2, int expand = 0)
        {
            expand *= Constants.ChunkSize; // expand is now in blocks
            var chunkKeys = new List<long>();
            for (var x = worldPos1.x - expand; x <= worldPos2.x + expand; x += Constants.ChunkSize)
            {
                for (var z = worldPos1.z - expand; z <= worldPos2.z + expand; z += Constants.ChunkSize)
                {
                    chunkKeys.Add(WorldChunkCache.MakeChunkKey(World.toChunkXZ(x), World.toChunkXZ(z)));
                }
            }
            return chunkKeys;
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
            if (parameters.Count == 0)
            {
                // one point is enough; area will extend to the chunk
                pos1 = pos2 = PlayerTools.GetPosition(senderInfo);
            }
            else if (parameters.Count == 1)
            {
                var ci = PlayerTools.GetClientInfo(senderInfo);
                if (parameters[0] == "from")
                {
                    savedPos[ci.entityId] = PlayerTools.GetPosition(ci);
                    throw new FriendlyMessageException("Your current position was saved: " + savedPos[ci.entityId]);
                }
                else if (parameters[0] == "to")
                {
                    if (!savedPos.ContainsKey(ci.entityId))
                        throw new FriendlyMessageException("Please save start point of the area first. See help for details.");
                    pos1 = savedPos[ci.entityId];
                    pos2 = PlayerTools.GetPosition(ci);
                }
                else
                {
                    throw new FriendlyMessageException("Parameter unknown. See help for details.");
                }
            }
            else if (parameters.Count == 4)
            {
                pos1 = CommandTools.ParseXZ(parameters, 0);
                pos2 = CommandTools.ParseXZ(parameters, 2);
            }
            else
            {
                throw new FriendlyMessageException("Parameter count not valid. See help for details.");
            }

            return (pos1, pos2);
        }

        private static void RegenerateChunk(long chunkKey)
        {
            var chunkX     = WorldChunkCache.extractX(chunkKey);
            var chunkZ     = WorldChunkCache.extractZ(chunkKey);
            var world      = GameManager.Instance.World;
            var chunkCache = world.ChunkCache;

            Log.Debug($"Starting regeneration of chunk {chunkX}, {chunkZ} ...");

            var chunkProvider = chunkCache.ChunkProvider as ChunkProviderGenerateWorld;
            if (chunkProvider == null)
                throw new ApplicationException("Found unexpected chunk provider: " + (chunkCache.ChunkProvider?.GetType().ToString() ?? "null"));
            
            // See: ChunkProviderGenerateWorld.DoGenerateChunks()

            System.Random random = Utils.RandomFromSeedOnPos(chunkX, chunkZ, world.Seed);
            Chunk newChunk = MemoryPools.PoolChunks.AllocSync(true);
            if (newChunk == null)
                throw new ApplicationException("Could not allocate new chunk from MemoryPool.");

            try
            {
                newChunk.X = chunkX;
                newChunk.Z = chunkZ;
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

                Log.Debug("Replacing old chunk with new chunk ...");
                chunkCache.RemoveChunkSync(chunkKey);
                if (!chunkCache.AddChunkSync(newChunk))
                    throw new ApplicationException("Could not add newly generated chunk to cache.");
            }
            catch (Exception)
            {
                // If anything crashes until the new chunk is added, free it's memory
                MemoryPools.PoolChunks.FreeSync(newChunk);
                throw;
            }

            Log.Debug("Placing overlapping decorations and smoothing transitions to other chunks ...");
            chunkProvider.DecorateChunkOverlapping(newChunk);

            // Ensure it's being saved automatically before unload or shutdown
            newChunk.isModified = true;
            Log.Out($"Regenerated chunk {chunkX}, {chunkZ}.");
        }
    }
}
