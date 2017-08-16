using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using JetBrains.Annotations;
using ScriptingMod.Extensions;
using ScriptingMod.Tools;
using UnityEngine;

namespace ScriptingMod.Commands
{

#if DEBUG
    [UsedImplicitly]
    public class Test : ConsoleCmdAbstract
    {

        public override string[] GetCommands()
        {
            return new [] { "dj-test" };
        }

        public override string GetDescription()
        {
            return "Internal tests for Scripting Mod";
        }

        public override void Execute(List<string> parameters, CommandSenderInfo senderInfo)
        {
            try
            {
                Application.logMessageReceived += delegate (string condition, string trace, LogType type)
                {
                    // Called for Unity log messages from MAIN thread
                    Log.Debug($"Application.logMessageReceived called.\r\ncondition={condition}\r\ntrace={trace}\r\ntype={type}");
                };

                Application.logMessageReceivedThreaded += delegate (string condition, string trace, LogType type)
                {
                    // Called for Unity log messages from ANY thread
                    Log.Debug($"Application.logMessageReceivedThreaded called.\r\ncondition={condition}\r\ntrace={trace}\r\ntype={type}");
                };

                GameManager.Instance.OnLocalPlayerChanged += delegate (EntityPlayerLocal local)
                {
                    Log.Debug($"GameManager.Instance.OnLocalPlayerChanged called.\r\nlocal={Dumper.Dump(local, 2)}");
                };

                GameManager.Instance.OnWorldChanged += delegate (World world)
                {
                    Log.Debug($"GameManager.Instance.OnWorldChanged called.\r\nworld={Dumper.Dump(world, 1)}");
                };

                GameManager.Instance.World.ChunkCache.OnChunkVisibleDelegates += delegate (long l, bool b)
                {
                    Log.Debug($"GameManager.Instance.World.ChunkCache.OnChunkVisibleDelegates called.\r\nl={l}, b={b}");
                };

                GameManager.Instance.World.ChunkCache.OnChunksFinishedLoadingDelegates += delegate
                {
                    Log.Debug($"GameManager.Instance.World.ChunkCache.OnChunksFinishedLoadingDelegates called.");
                };

                GameManager.Instance.World.ChunkCache.OnChunksFinishedDisplayingDelegates += delegate
                {
                    Log.Debug($"GameManager.Instance.World.ChunkCache.OnChunksFinishedDisplayingDelegates called.");
                };

                GameManager.Instance.World.ChunkClusters.ChunkClusterChangedDelegates += delegate (int idx)
                {
                    Log.Debug($"GameManager.Instance.World.ChunkClusters.ChunkClusterChangedDelegates called.\r\nidx={idx}");
                };

                GameManager.Instance.World.OnWorldChanged += delegate (string name)
                {
                    Log.Debug($"GameManager.Instance.World.OnWorldChanged called.\r\nname={name}");
                };

                GameManager.Instance.World.EntityLoadedDelegates += delegate (Entity entity)
                {
                    Log.Debug($"GameManager.Instance.World.OnWorldChanged called.\r\nentity={Dumper.Dump(entity, 1)}");
                };

                Log.Debug("Various listeners added.");
            }
            catch (Exception ex)
            {
                CommandTools.HandleCommandException(ex);
            }
        }

    }
#endif

}
