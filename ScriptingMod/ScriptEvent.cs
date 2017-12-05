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
        entityLoaded,
        entityUnloaded,
        gameAwake,
        gameShutdown,
        gameStartDone,
        gameStatsChanged,
        logMessageReceived,
        playerDamaged, // suggested by Guppycur, StompyNZ, Xyth
        playerDied,
        playerDisconnected,
        playerExpGained,
        playerLevelUp,
        playerLogin,
        playerSaveData,
        playerSpawnedInWorld,
        playerSpawning,
        serverRegistered,
        zombieDamaged, // suggested by Guppycur, StompyNZ, Xyth
        zombieDied,
    }
}