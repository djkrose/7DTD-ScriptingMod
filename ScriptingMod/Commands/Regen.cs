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
        private enum Mode { Region, Chunk, Custom, All }

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
            // ----------------------------------(max length: 100 char)--------------------------------------------|
            return $@"
                Regenerates a chunk, a region, or an area of custom dimensions. All player-built structures are
                removed and the area is recreated from the world seed as if it was visited for the first time.
                Usage:
                    1. dj-regen region
                    2. dj-regen region <regionX> <regionZ>
                    3. dj-regen chunk
                    4. dj-regen chunk <chunkX> <chunkY>
                    5. dj-regen <x1> <z1> <x2> <z2>
                    6. dj-regen <x1> <y1> <z1> <x1> <y1> <z1>
                    7. dj-regen all
                1. Regenerates the current player's region (512x512 blocks) from bedrock to sky.
                2. Regenerates the given region, which is the same as deleting the region file. <regionX> and
                   <regionY> are the same numbers as in the region file name. Use ""dj-pos"" to get current region.
                   Example: ""dj-regen region 4 -8"" deletes and regenerates the region file r.4.-8.7rg.
                3. Regenerates the current player's chunk (16x16 blocks) from bedrock to sky.
                4. Regenerates the given chunk, identified by chunk coordinates as can be seen in F3 debug screen
                   or with ""dj-pos"" command.
                5. Regenerates the given custom area from bedrock to sky.
                6. Regenerates the given custom area defined by the two 3-dimensional coordinates.
                7. Regenerates the entire world, but keeps all player data.
                ".Unindent();
        }
        public override void Execute(List<string> parameters, CommandSenderInfo senderInfo)
        {
            try
            {
                (Mode mode, Vector3i pos1, Vector3i pos2) = ParseParams(parameters, senderInfo);
                if (mode == Mode.Chunk)
                {
                    RegenerateChunk(pos1.x, pos1.z);
                }
            }
            catch (Exception ex)
            {
                CommandTools.HandleCommandException(ex);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="parameters"></param>
        /// <param name="senderInfo"></param>
        /// <returns>A tuple with these values:
        /// - mode: The size mode for regeneration, affects how pos1 and po2 is interpreted:
        ///         Region: pos1      contains regionX/regionZ, y values and pos2 are ignored
        ///         Chunk:  pos1      contains chunkX/chunkZ, y values and pos2 are ignored
        ///         Area:   pos1/pos2 are worldPos coordinates
        ///         All:    pos1/pos2 are ignored
        /// </returns>
        private (Mode mode, Vector3i pos1, Vector3i pos2) ParseParams(List<string> parameters, CommandSenderInfo senderInfo)
        {
            if (parameters.Count == 0)
                throw new FriendlyMessageException("This command needs parameter(s). See \"help dj-regen\" for details.");

            Mode mode;
            Vector3i pos1 = default(Vector3i);
            Vector3i pos2 = default(Vector3i);
            switch (parameters[0].ToLowerInvariant())
            {
                case "region":
                    mode = Mode.Region;
                    throw new NotImplementedException();
                    break;
                case "chunk":
                    mode = Mode.Chunk;
                    if (parameters.Count == 1)
                    {
                        // Use player's position to get current chunk
                        var worldPos = PlayerTools.GetPosition(senderInfo);
                        pos1.x = World.toChunkXZ(worldPos.x);
                        pos1.z = World.toChunkXZ(worldPos.z);
                    }
                    else if (parameters.Count == 3)
                    {
                        // Parse given chunkX/Z
                        pos1 = CommandTools.ParseXZ(parameters, 1);

                        // Check that chunk coordinates are within bounds of the world
                        GameManager.Instance.World.ChunkCache.ChunkProvider.GetWorldExtent(out Vector3 minSize, out Vector3 maxSize);
                        var maxViewDistance = 200;
                        if (pos1.x < World.toChunkXZ((int) Math.Floor(minSize.x) - maxViewDistance) ||
                            pos1.x > World.toChunkXZ((int) Math.Floor(maxSize.x) + maxViewDistance) ||
                            pos1.z < World.toChunkXZ((int) Math.Floor(minSize.z) - maxViewDistance) ||
                            pos1.z > World.toChunkXZ((int) Math.Floor(maxSize.z) + maxViewDistance))
                        {
                            throw new FriendlyMessageException("Chunk coordinates are out of bounds. These are not world coordinates!");
                        }
                    }
                    else
                    {
                        throw new FriendlyMessageException("Wrong number of parameters for \"chunk\" mode.");
                    }
                    break;
                case "all":
                    mode = Mode.All;
                    throw new NotImplementedException();
                    break;
                default:
                    mode = Mode.Custom;
                    throw new NotImplementedException();
                    break;
            }

            return (mode, pos1, pos2);
        }


        private void RegenerateChunk(int chunkX, int chunkZ)
        {
            var world = GameManager.Instance.World;
            var chunkCache = world.ChunkCache;

            Chunk oldChunk = world.GetChunkSync(chunkX, chunkZ) as Chunk;
            if (oldChunk == null)
                throw new FriendlyMessageException($"Chunk [{chunkX}, {chunkZ}] is not loaded. Move closer!");

            var pos1 = oldChunk.ToWorldPos(Vector3i.zero);
            var pos2 = oldChunk.ToWorldPos(new Vector3i(ChunkTools.ChunkSize - 1, ChunkTools.ChunkHeight - 1, ChunkTools.ChunkSize - 1));

            Log.Debug($"Starting regeneration of chunk [{chunkX}, {chunkZ}], which covers area [{pos1}] to [{pos2}] ...");

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

                    Log.Debug("Adding prefab decorations ...");
                    if (chunkProvider.GetDynamicPrefabDecorator() != null)
                        chunkProvider.GetDynamicPrefabDecorator().DecorateChunk(world, newChunk, random);
                    Log.Debug("Adding entity decorations ...");
                    if (chunkProvider.GetDynamicEntityDecorator() != null)
                        chunkProvider.GetDynamicEntityDecorator().DecorateChunk(world, newChunk, random);
                    Log.Debug("Adding spawners ...");
                    if (chunkProvider.GetDynamicEntitySpawnerDecorator() != null)
                        chunkProvider.GetDynamicEntitySpawnerDecorator().DecorateChunk(world, newChunk, random);
                }
                else
                {
                    Log.Debug("Decorations are disabled.");
                    newChunk.NeedsDecoration = false;
                    newChunk.NeedsLightCalculation = false;
                    newChunk.NeedsRegeneration = true;
                }

                Log.Debug("Replacing old chunk with new chunk ...");
                chunkCache.RemoveChunk(oldChunk);
                if (!chunkCache.AddChunkSync(newChunk))
                    throw new ApplicationException("Could not add newly generated chunk to cache.");

                Log.Debug("Doing something with the adjacent chunks ...");
                // TODO: make reflection future-proof
                typeof(ChunkProviderGenerateWorld).GetMethod("DE", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(chunkProvider, new object[] {newChunk});

                Log.Debug("Reloading affected chunks for clients ...");
                //var affectedChunks = new List<Chunk>();

                var affectedChunks = new Chunk[3*3];
                chunkCache.GetNeighborChunks(newChunk, affectedChunks); // fills slot 0-7
                affectedChunks[8] = newChunk;

                newChunk.isModified = true;


                //affectedChunks.Add(newChunk);
                //affectedChunks.Add((Chunk)world.GetChunkSync(chunkX - 1, chunkZ - 1));
                //affectedChunks.Add((Chunk)world.GetChunkSync(chunkX - 1, chunkZ));
                //affectedChunks.Add((Chunk)world.GetChunkSync(chunkX - 1, chunkZ + 1));
                //affectedChunks.Add((Chunk)world.GetChunkSync(chunkX, chunkZ - 1));
                //affectedChunks.Add((Chunk)world.GetChunkSync(chunkX, chunkZ + 1));
                //affectedChunks.Add((Chunk)world.GetChunkSync(chunkX + 1, chunkZ -1));
                //affectedChunks.Add((Chunk)world.GetChunkSync(chunkX + 1, chunkZ));
                //affectedChunks.Add((Chunk)world.GetChunkSync(chunkX + 1, chunkZ + 1));
                ChunkTools.ReloadForClients(affectedChunks);

                Log.Out($"{newChunk} was regenerated.");
                SdtdConsole.Instance.Output("All done.");
            }
            catch (Exception)
            {
                MemoryPools.PoolChunks.FreeSync(newChunk);
                throw;
            }

            //if (chunk != null && chunkCache != null)
            //{
            //    if (chunkCache.AddChunkSync(chunk, false))
            //        this.DE(chunk); // Regenerates overlapping decorations with neighbor chunks
            //    else
            //        MemoryPools.PoolChunks.FreeSync(chunk);
            //}
        }
    }
}
