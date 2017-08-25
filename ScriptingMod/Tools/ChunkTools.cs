using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ScriptingMod.Extensions;

namespace ScriptingMod.Tools
{
    internal static class ChunkTools
    {
        /// <summary>
        /// Resets and recalculates stability for the given chunks.
        /// Based on StompiNZ's BadCompanySM:
        /// https://github.com/7days2mod/BadCompanySM/blob/master/BCManager/src/Functions/BCChunks.cs
        /// </summary>
        /// <param name="chunks"></param>
        public static void ResetStability(ICollection<Chunk> chunks)
        {
            Log.Debug($"Resetting stability for {chunks.Count} chunks ...");
            var _si = new StabilityInitializer(GameManager.Instance.World);
            foreach (var _chunk in chunks)
                _chunk.ResetStability();
            foreach (var _chunk in chunks)
            {
                _si.DistributeStability(_chunk);
                _chunk.NeedsRegeneration = true;
            }
            Log.Out($"Stability was reset for {chunks.Count} chunks.");
        }

        /// <summary>
        /// Forces clients that have the given chunks currently loaded to reload them.
        /// Based on StompiNZ's BadCompanySM:
        /// https://github.com/7days2mod/BadCompanySM/blob/master/BCManager/src/Functions/BCChunks.cs
        /// </summary>
        public static void ReloadForClients(ICollection<long> chunkKeys)
        {
            // Refresh clients chunks
            Log.Debug($"Forcing clients to reload {chunkKeys.Count} chunks ...");

            var clients          = ConnectionManager.Instance.GetClients();
            var entities         = GameManager.Instance.World.Entities.dict;
            var reloadforclients = new Dictionary<ClientInfo, List<long>>();

            // ------ Force chunk unload ------
            foreach (var client in clients)
            {
                if (!entities.ContainsKey(client.entityId))
                    continue; // client is not an entity

                var player = entities[client.entityId] as EntityPlayer;
                if (player == null)
                    continue; // entity is not a player

                var playersChunks = player.ChunkObserver.chunksLoaded?.ToList();
                if (playersChunks == null)
                    continue; // player has no chunks loaded

                foreach (var chunkKey in playersChunks)
                {
                    if (!chunkKeys.Contains(chunkKey))
                        continue; // this chunk doesn't need reloading

                    try
                    {
                        client.SendPackage(new NetPackageChunkRemove(chunkKey));
                        if (reloadforclients.ContainsKey(client))
                            reloadforclients[client].Add(chunkKey);
                        else
                            reloadforclients.Add(client, new List<long> { chunkKey });
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Error forcing {client.playerName} to remove chunk {chunkKey}:\r\n" + ex);
                    }
                }
                var countForcedReloads = reloadforclients.GetValue(client)?.Count ?? 0;
                if (countForcedReloads > 0)
                    Log.Out($"Forced {client.playerName} to remove {countForcedReloads} of {playersChunks.Count} chunks.");
            }

            // Delay to allow remove chunk packets to reach clients
            Thread.Sleep(50);

            // ------ Force chunk reload ------
            foreach (var client in reloadforclients.Keys)
            {
                if (!entities.ContainsKey(client.entityId))
                    continue; // client is not an entity

                var player = entities[client.entityId] as EntityPlayer;
                if (player == null)
                    continue; // entity is not a player

                var playersChunks = player.ChunkObserver.chunksLoaded;
                if (playersChunks == null)
                    continue; // player has no chunks loaded

                var chunkCache = GameManager.Instance.World.ChunkCache;
                if (chunkCache == null)
                    continue;

                var allCachedChunks = chunkCache.GetChunkKeysCopySync();

                foreach (var chunkKey in reloadforclients[client])
                {
                    // TODO: verify if the above remove chunk takes them out of the EP.ChunkObserver.chunksLoaded dict
                    if (!allCachedChunks.Contains(chunkKey) || !playersChunks.Contains(chunkKey))
                        continue;

                    var chunk = chunkCache.GetChunkSync(chunkKey);
                    if (chunk == null)
                        continue;

                    try
                    {
                        client.SendPackage(new NetPackageChunk(chunk));
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Error forcing {client.playerName} to reload chunk {chunkKey}:\r\n" + ex);
                    }
                }
                Log.Out($"Forced {client.playerName} to reload {reloadforclients[client].Count} of {playersChunks.Count} chunks.");
            }
        }

        public static Vector2xz WorldPosToChunkXZ(Vector3i worldPos)
        {
            return new Vector2xz(worldPos.x >> 4, worldPos.z >> 4);
        }

        public static Vector2xz ChunkKeyToChunkXZ(long chunkKey)
        {
            return new Vector2xz(WorldChunkCache.extractX(chunkKey), WorldChunkCache.extractZ(chunkKey));
        }

        public static Vector2xz ChunkXZToRegionXZ(Vector2xz chunkXZ)
        {
            return new Vector2xz((int)Math.Floor(chunkXZ.x / 32.0d), (int)Math.Floor(chunkXZ.z / 32.0d));
        }

        //public static long ChunkXZToChunkKey(Vector2xz chunkXZ, int clrIdx = 0)
        //{
        //    return WorldChunkCache.MakeChunkKey(chunkXZ.x, chunkXZ.z, clrIdx);
        //}
    }
}