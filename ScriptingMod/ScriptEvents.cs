namespace ScriptingMod
{
    public enum ScriptEvents
    {
        // Each member should have exactly ONE using, which is where the event handler is invoked

        playerSpawnedInWorld,
        chatMessage,
        logMessageReceived,
        gameShutdown,
        gameStartDone,
        playerLogin,
        playerSpawning,
        gameAwake,
        chunkMapCalculated,
        savePlayerData,
        playerDisconnected,
        localPlayerChanged,
        gameManagerWorldChanged,
        chunkClusterChanged,
        chunksFinishedLoading,
        chunksFinishedDisplaying,
        worldChanged,
        entityLoaded,
        eacPlayerKicked,
        eacPlayerAuthenticated,
        entityUnloaded,
        gameStatsChanged,
        steamApplicationQuit,
        steamPlayerConnected,
        steamPlayerDisconnected,
        steamServerInitialized,
        steamConnectedToServer,
        steamDestroy,
        steamDisconnectedFromServer,
        steamFailedToConnect,
        chunkLoaded,
        chunkUnloaded
    }
}