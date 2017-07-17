using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ScriptingMod.Managers
{
    public static class ChunkManager
    {

        public static void ResetStability(IEnumerable<Chunk> chunks)
        {
            var _si = new StabilityInitializer(GameManager.Instance.World);
            foreach (var _chunk in chunks)
                _chunk.ResetStability();
            foreach (var _chunk in chunks)
            {
                _si.DistributeStability(_chunk);
                _chunk.NeedsRegeneration = true;
            }
        }

        // Gratefully copied from StompiNZ's BadCompanySM:
        // https://github.com/7days2mod/BadCompanySM/blob/master/BCManager/src/Functions/BCChunks.cs
        public static void ReloadForClients(IEnumerable<Chunk> chunks)
        {
            // Refresh clients chunks
            var clients = ConnectionManager.Instance.GetClients();
            var reloadforclients = new Dictionary<ClientInfo, List<long>>();
            foreach (var client in clients)
            {
                if (GameManager.Instance.World.Entities.dict.ContainsKey(client.entityId))
                {
                    var EP = GameManager.Instance.World.Entities.dict[client.entityId] as EntityPlayer;

                    if (EP != null)
                    {
                        // TODO: need a lock here?
                        var chunksLoaded = EP.ChunkObserver.chunksLoaded;
                        foreach (var _chunkKey in chunksLoaded)
                        {
                            if (chunks.Any(c => c.Key == _chunkKey))
                            {
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
                                    Log.Out("Error removing chunk " + _chunkKey + " for " + client.playerName + ":\r\n" + ex);
                                }
                            }
                        }
                        Log.Out("Reloading " + reloadforclients[client].Count + "/" +
                                EP.ChunkObserver.chunksLoaded.Count + " chunks for " + client.playerName);
                    }
                }
            }

            // Delay to allow remove chunk packets to reach clients
            Thread.Sleep(50);

            foreach (var client in reloadforclients.Keys)
            {
                if (reloadforclients[client] != null)
                {
                    foreach (var _chunkKey in reloadforclients[client])
                    {
                        var chunkCache = GameManager.Instance.World.ChunkClusters[0];
                        if (chunkCache != null)
                        {
                            var chunkKeys = chunkCache.GetChunkKeysCopySync();
                            var EP = GameManager.Instance.World.Entities.dict[client.entityId] as EntityPlayer;

                            // TODO: verify if the above remove chunk takes them out of the EP.ChunkObserver.chunksLoaded dict
                            if (chunkKeys.Contains(_chunkKey) && EP != null && EP.ChunkObserver.chunksLoaded.Contains(_chunkKey))
                            {
                                var c = chunkCache.GetChunkSync(_chunkKey);
                                if (c != null)
                                {
                                    try
                                    {
                                        client.SendPackage(new NetPackageChunk(c));
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.Out("Error reloading chunk " + _chunkKey + " for " + client.playerName + ":\r\n" + ex);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

    }
}