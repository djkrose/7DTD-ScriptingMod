using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using ScriptingMod.Extensions;
using ScriptingMod.ScriptEngines;
using ScriptingMod.Tools;

namespace ScriptingMod
{

    /// <summary>
    /// Main API mod interface for 7DTD. All public objects deriving from ModApiAbstract are automatically loaded.
    /// All overridden methods are called withing a try-catch already; no need for adding top-level try-catch ourselves.
    /// </summary>
    [UsedImplicitly]
    public class Api : ModApiAbstract
    {
        public Api()
        {
            Log.Debug("Api constructor called.");
        }

        public override void GameAwake()
        {
            //Log.Debug("Api.GameAwake called.");
            Log.Out($"Initializing {Constants.ModNameFull} ...");
            NonPublic.Init();
            PersistentData.Load();
            PatchTools.ApplyPatches();
            CommandTools.InitEvents();
            CommandTools.InitScripts();
            CommandTools.InitScriptsMonitoring();
            RepairEngine.InitAuto();
            Log.Out($"Done initializing {Constants.ModNameFull}.");

            CommandTools.InvokeScriptEvents(new { type = ScriptEvents.gameAwake.ToString() });
        }

        public override void GameStartDone()
        {
            //Log.Debug("Api.GameStartDone called.");
            if (GamePrefs.GetBool(EnumGamePrefs.EACEnabled))
                EacTools.Init();
            CommandTools.InvokeScriptEvents(new { type = ScriptEvents.gameStartDone.ToString() });
        }

        public override void GameUpdate()
        {
            // Doing ANYTHING here has a big performance impact!
            //Log.Debug("Api.GameUpdate called.");
        }

        public override void GameShutdown()
        {
            //Log.Debug("Api.GameShutdown called.");
            CommandTools.InvokeScriptEvents(new { type = ScriptEvents.gameShutdown.ToString() });
        }

        public override void PlayerLogin(ClientInfo clientInfo, string compatibilityVersion)
        {
            //Log.Debug("Api.PlayerLogin called.");
            CommandTools.InvokeScriptEvents(new { type = ScriptEvents.playerLogin.ToString(), clientInfo, compatibilityVersion });
        }

        public override void PlayerSpawning(ClientInfo clientInfo, int chunkViewDim, PlayerProfile playerProfile)
        {
            //Log.Debug("Api.PlayerSpawning called.");
            CommandTools.InvokeScriptEvents(new { type = ScriptEvents.playerSpawning.ToString(), clientInfo, chunkViewDim, playerProfile });
        }

        public override void PlayerSpawnedInWorld(ClientInfo clientInfo, RespawnType respawnReason, Vector3i pos)
        {
            //Log.Debug("Api.PlayerSpawnedInWorld called.");
            CommandTools.InvokeScriptEvents(new { type = ScriptEvents.playerSpawnedInWorld.ToString(), clientInfo, respawnReason, pos });
        }

        public override void PlayerDisconnected(ClientInfo clientInfo, bool shutdown)
        {
            //Log.Debug("Api.PlayerDisconnected called.");
            CommandTools.InvokeScriptEvents(new { type = ScriptEvents.playerDisconnected.ToString(), clientInfo, shutdown });
        }

        public override void SavePlayerData(ClientInfo clientInfo, PlayerDataFile playerDataFile)
        {
            //Log.Debug("Api.SavePlayerData called.");
            CommandTools.InvokeScriptEvents(new { type = ScriptEvents.savePlayerData.ToString(), clientInfo, playerDataFile });
        }

        public override bool ChatMessage(ClientInfo clientInfo, EnumGameMessages messageType, string message, string mainName, bool localizeMain, string secondaryName, bool localizeSecondary)
        {
            //Log.Debug("Api.ChatMessage called.");
            var args = new ChatMessageEventArgs
            {
                type = ScriptEvents.chatMessage.ToString(),
                clientInfo = clientInfo,
                messageType = messageType,
                message = message,
                mainName = mainName,
                localizeMain = localizeMain,
                secondaryName = secondaryName,
                localizeSecondary = localizeSecondary
            };

            CommandTools.InvokeScriptEvents(args);

            return !args.isPropagationStopped;
        }

        public override void CalcChunkColorsDone(Chunk chunk)
        {
            //Log.Debug("Api.CalcChunkColorsDone called.");
            CommandTools.InvokeScriptEvents(new { type = ScriptEvents.calcChunkColorsDone.ToString(), chunk });
        }

    }
}
