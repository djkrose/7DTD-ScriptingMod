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
        calcChunkColorsDone,
        savePlayerData,
        playerDisconnected
    }
}