namespace ScriptingMod
{
    public enum ScriptEvents
    {
        // Each member should have exactly ONE using, which is where the event handler is invoked

        chatMessage,
        chunkLoaded,
        chunkMapCalculated,
        chunkUnloaded,
        eacPlayerAuthenticated,
        eacPlayerKicked,
        entityLoaded,
        entityUnloaded,
        gameAwake,
        gameShutdown,
        gameStartDone,
        gameStatsChanged,
        logMessageReceived,
        playerDisconnected,
        playerLogin,
        playerSaveData,
        playerSpawnedInWorld,
        playerSpawning,
        serverRegistered,
    }
}