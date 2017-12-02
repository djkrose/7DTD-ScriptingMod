namespace ScriptingMod
{
    public enum ScriptEvent
    {
        animalDamaged,
        animalDied,
        chatMessage,
        chunkLoaded,
        chunkMapCalculated,
        chunkUnloaded,
        eacPlayerAuthenticated,
        eacPlayerKicked,
        playerDamaged, // suggested by Guppycur, StompyNZ, Xyth
        entityLoaded,
        entityUnloaded,
        gameAwake,
        gameShutdown,
        gameStartDone,
        gameStatsChanged,
        logMessageReceived,
        playerDied,
        playerDisconnected,
        playerLogin,
        playerSaveData,
        playerSpawnedInWorld,
        playerSpawning,
        serverRegistered,
        zombieDamaged, // suggested by Guppycur, StompyNZ, Xyth
        zombieDied,
    }
}