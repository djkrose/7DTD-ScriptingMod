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

        public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
        {
            try
            {
                //float middleX = (float)(Screen.width / 2);
                //float middleY = (float)(Screen.height / 2);
                //for (int index = 0; index < GameManager.Instance.World.ChunkClusters.Count; ++index)
                //{
                //    ChunkCluster chunkCluster = GameManager.Instance.World.ChunkClusters[index];
                //    if (chunkCluster != null)
                //        chunkCluster.DebugOnGUI(middleX + (float)(100 * index), middleY, 6);
                //}
                //GameManager.Instance.World.m_ChunkManager.DebugOnGUI(middleX, middleY, 6);
                Log.Debug("Test done.");
            }
            catch (Exception ex)
            {
                CommandTools.HandleCommandException(ex);
            }
        }

    }
#endif

}
