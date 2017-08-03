using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ScriptingMod.Extensions;

namespace ScriptingMod.Managers
{
    internal static class ChunkManager
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
        /// <param name="chunks"></param>
        public static void ReloadForClients(ICollection<Chunk> chunks)
        {
            // Refresh clients chunks
            Log.Debug($"Forcing clients to reload {chunks.Count} chunks ...");

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

                var chunksLoaded = player.ChunkObserver.chunksLoaded;
                if (chunksLoaded == null)
                    continue; // player has no chunks loaded

                // TODO: need a lock here?
                foreach (var _chunkKey in chunksLoaded)
                {
                    if (chunks.All(c => c.Key != _chunkKey))
                        continue; // no affected chunks loaded

                    try
                    {
                        client.SendPackage(new NetPackageChunkRemove(_chunkKey));
                        if (reloadforclients.ContainsKey(client))
                            reloadforclients[client].Add(_chunkKey);
                        else
                            reloadforclients.Add(client, new List<long> { _chunkKey });
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Error forcing {client.playerName} to remove chunk {_chunkKey}:\r\n" + ex);
                    }
                }
                var countForcedReloads = reloadforclients.GetValue(client)?.Count ?? 0;
                if (countForcedReloads > 0)
                    Log.Out($"Forced {client.playerName} to remove {countForcedReloads} of {chunksLoaded.Count} chunks.");
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

                var chunksLoaded = player.ChunkObserver.chunksLoaded;
                if (chunksLoaded == null)
                    continue; // player has no chunks loaded

                var chunkCache = GameManager.Instance.World.ChunkClusters[0];
                if (chunkCache == null)
                    continue;

                var chunkKeys = chunkCache.GetChunkKeysCopySync();

                foreach (var chunkKey in reloadforclients[client])
                {
                    // TODO: verify if the above remove chunk takes them out of the EP.ChunkObserver.chunksLoaded dict
                    if (!chunkKeys.Contains(chunkKey) || !chunksLoaded.Contains(chunkKey))
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
                Log.Out($"Forced {client.playerName} to reload {reloadforclients[client].Count} of {chunksLoaded.Count} chunks.");
            }
        }

    }
}