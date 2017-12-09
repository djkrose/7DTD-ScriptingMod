using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;

namespace ScriptingMod.ScriptEngines
{
    public class ScriptEventArgs
    {
        public string eventType;
    }

    public class ChatMessageEventArgs : ScriptEventArgs
    {
        public string messageType;
        public string @from;
        public string message;
        [CanBeNull] // doesn't need to come from player: kill messages, server messages, etc
        public ClientInfo clientInfo;

        public bool isPropagationStopped = false;

        public void stopPropagation()
        {
            isPropagationStopped = true;
        }
    }

    public class EntityDamagedEventArgs : ScriptEventArgs
    {
        public Vector3i position;
        public int entityId;
        public string entityName;
        public int? sourceEntityId;
        public string sourceEntityName;
        public string damageType;
        public string hitBodyPart;
        public string hitDirection;
        public int damage;
        public float armorDamage;
        public string armorSlot;
        public string stunType;
        public float stunDuration;
        public bool critical;
        public bool fatal;
        public bool crippleLegs;
        public bool dismember;
        public bool turnIntoCrawler;
        public bool painHit;
    }

    public class EntityDiedEventArgs : EntityDamagedEventArgs
    {
    }

    public class EacPlayerAuthenticatedEventArgs : ScriptEventArgs
    {
        public ClientInfo clientInfo;
    }

    public class EacPlayerKickedEventArgs : ScriptEventArgs
    {
        public string reason;
        public ClientInfo clientInfo;
    }

    public class ServerRegisteredEventArgs : ScriptEventArgs
    {
        public Dictionary<string, string> gameInfos;
    }

    public class LogMessageReceivedEventArgs : ScriptEventArgs
    {
        public string logType;
        public string condition;
        public string trace;
    }

    public class EntityLoadedEventArgs : ScriptEventArgs
    {
        public string entityType;
        public int entityId;
        public string entityName;
        public Vector3i position;
    }

    public class EntityUnloadedEventArgs : ScriptEventArgs
    {
        public string reason;
        public string entityType;
        public int entityId;
        public string entityName;
        public Vector3i position;
    }

    public class ChunkLoadedUnloadedEventArgs : ScriptEventArgs
    {
        public long chunkKey;
        public Vector2xz chunkPos;
    }

    public class GameStatsChangedEventArgs : ScriptEventArgs
    {
        public string gameState;
        [CanBeNull]
        public object oldValue;
        [CanBeNull]
        public object newValue;
    }

    public class PlayerLoginEventArgs : ScriptEventArgs
    {
        public string compatibilityVersion;
        public ClientInfo clientInfo;
    }

    public class PlayerSpawningEventArgs : ScriptEventArgs
    {
        public PlayerProfile playerProfile;
        public ClientInfo clientInfo;
    }

    public class PlayerSpawnedInWorldEventArgs : ScriptEventArgs
    {
        public string reason;
        public Vector3i position;
        public ClientInfo clientInfo;
    }

    public class PlayerDisconnectedEventArgs : ScriptEventArgs
    {
        public bool shutdown;
        public ClientInfo clientInfo;
    }

    public class PlayerSaveDataEventArgs : ScriptEventArgs
    {
        public ClientInfo clientInfo;
        public string playerDataFile;
    }

    public class ChunkMapCalculatedEventArgs : ScriptEventArgs
    {
        public long chunkKey;
        public Vector2xz chunkPos;
    }

    public class PlayerLevelUpEventArgs : ScriptEventArgs
    {
        public int oldLevel;
        public int newLevel;
        public ClientInfo clientInfo;
    }

    public class PlayerExpGainedEventArgs : ScriptEventArgs
    {
        public int expGained;
        public int expToNextLevel;
        public bool levelUp;
        public ClientInfo clientInfo;
    }

    public class PlayerEnteredChunkEventArgs : ScriptEventArgs
    {
        public Vector2xz newChunk;
        public Vector2xz oldChunk;
        public Vector3i position;
        public ClientInfo clientInfo;
    }
}
