using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using UnityEngine;

namespace ScriptingMod.ScriptEngines
{
    [UsedImplicitly(ImplicitUseTargetFlags.Members)]
    public class ScriptEventArgs
    {
        public ScriptEvent type;

        public ScriptEventArgs(ScriptEvent type)
        {
            this.type = type;
        }

        public override string ToString()
        {
            return "eventType=" + type;
        }
    }

    public class ChatMessageEventArgs : ScriptEventArgs
    {
        public ClientInfo clientInfo;
        public EnumGameMessages messageType;
        public string message;
        public string mainName;
        public bool localizeMain;
        public string secondaryName;
        public bool localizeSecondary;

        public bool isPropagationStopped = false;

        public ChatMessageEventArgs(ScriptEvent type) : base(type)
        {
        }

        public void stopPropagation()
        {
            isPropagationStopped = true;
        }

    }

    [UsedImplicitly(ImplicitUseTargetFlags.Members)]
    public class EntityDamagedEventArgs : ScriptEventArgs
    {
        public EntityAlive entity;
        public DamageResponse damageResponse;

        public EntityDamagedEventArgs(ScriptEvent type, EntityAlive entity, DamageResponse damageResponse) : base(type)
        {
            this.entity = entity;
            this.damageResponse = damageResponse;
        }

        public override string ToString()
        {
            return base.ToString() +
                ",entityId=" + entity.entityId +
                ",entityName=" + entity.EntityName +
                ",sourceEntityId=" + damageResponse.Source?.getEntityId() +
                ",damageType=" + damageResponse.Source?.GetName() +
                ",bodyPart=" + damageResponse.HitBodyPart +
                ",strength=" + damageResponse.Strength;
            //TODO
        }
    }

    public class EntityDiedEventArgs : EntityDamagedEventArgs
    {
        public EntityDiedEventArgs(ScriptEvent type, EntityAlive entity, DamageResponse damageResponse) : base(type, entity, damageResponse)
        {
        }
    }

    public class EacPlayerAuthenticatedEventArgs : ScriptEventArgs
    {
        public ClientInfo clientInfo;

        public EacPlayerAuthenticatedEventArgs(ScriptEvent type, ClientInfo clientInfo) : base(type)
        {
            this.clientInfo = clientInfo;
        }
    }

    public class EacPlayerKickedEventArgs : ScriptEventArgs
    {
        public ClientInfo clientInfo;
        public GameUtils.KickPlayerData kickPlayerData;

        public EacPlayerKickedEventArgs(ScriptEvent type, ClientInfo clientInfo, GameUtils.KickPlayerData kickPlayerData) : base(type)
        {
            this.clientInfo = clientInfo;
            this.kickPlayerData = kickPlayerData;
        }
    }

    public class ServerRegisteredEventArgs : ScriptEventArgs
    {
        public MasterServerAnnouncer masterServerAnnouncer;

        public ServerRegisteredEventArgs(ScriptEvent type, MasterServerAnnouncer masterServerAnnouncer) : base(type)
        {
            this.masterServerAnnouncer = masterServerAnnouncer;
        }
    }

    public class LogMessageReceivedEventArgs : ScriptEventArgs
    {
        public string condition;
        public string trace;
        public LogType logType;

        public LogMessageReceivedEventArgs(ScriptEvent type, string condition, string trace, LogType logType) : base(type)
        {
            this.condition = condition;
            this.trace = trace;
            this.logType = logType;
        }
    }

    public class EntityLoadedEventArgs : ScriptEventArgs
    {
        public Entity entity;

        public EntityLoadedEventArgs(ScriptEvent type, Entity entity) : base(type)
        {
            this.entity = entity;
        }
    }

    public class EntityUnloadedEventArgs : ScriptEventArgs
    {
        public Entity entity;
        public EnumRemoveEntityReason reason;

        public EntityUnloadedEventArgs(ScriptEvent type, Entity entity, EnumRemoveEntityReason reason) : base(type)
        {
            this.entity = entity;
            this.reason = reason;
        }
    }

    public class ChunkLoadedUnloadedEventArgs : ScriptEventArgs
    {
        public long chunkKey;

        public ChunkLoadedUnloadedEventArgs(ScriptEvent type, long chunkKey) : base(type)
        {
            this.chunkKey = chunkKey;
        }
    }

    public class GameStatsChangedEventArgs : ScriptEventArgs
    {
        public EnumGameStats gameState;
        public object newValue;

        public GameStatsChangedEventArgs(ScriptEvent type, EnumGameStats gameState, object newValue) : base(type)
        {
            this.gameState = gameState;
            this.newValue = newValue;
        }
    }

    public class PlayerLoginEventArgs : ScriptEventArgs
    {
        public ClientInfo clientInfo;
        public string compatibilityVersion;

        public PlayerLoginEventArgs(ScriptEvent type, ClientInfo clientInfo, string compatibilityVersion) : base(type)
        {
            this.clientInfo = clientInfo;
            this.compatibilityVersion = compatibilityVersion;
        }
    }


    public class PlayerSpawningEventArgs : ScriptEventArgs
    {
        public ClientInfo clientInfo;
        public int chunkViewDim;
        public PlayerProfile playerProfile;

        public PlayerSpawningEventArgs(ScriptEvent type, ClientInfo clientInfo, int chunkViewDim, PlayerProfile playerProfile) : base(type)
        {
            this.clientInfo = clientInfo;
            this.chunkViewDim = chunkViewDim;
            this.playerProfile = playerProfile;
        }
    }

    public class PlayerSpawnedInWorldEventArgs : ScriptEventArgs
    {
        public ClientInfo clientInfo;
        public RespawnType respawnReason;
        public Vector3i pos;

        public PlayerSpawnedInWorldEventArgs(ScriptEvent type, ClientInfo clientInfo, RespawnType respawnReason, Vector3i pos) : base(type)
        {
            this.clientInfo = clientInfo;
            this.respawnReason = respawnReason;
            this.pos = pos;
        }
    }

    public class PlayerDisconnectedEventArgs : ScriptEventArgs
    {
        public ClientInfo clientInfo;
        public bool shutdown;

        public PlayerDisconnectedEventArgs(ScriptEvent type, ClientInfo clientInfo, bool shutdown) : base(type)
        {
            this.clientInfo = clientInfo;
            this.shutdown = shutdown;
        }
    }

    public class PlayerSaveDataEventArgs : ScriptEventArgs
    {
        public ClientInfo clientInfo;
        public PlayerDataFile playerDataFile;

        public PlayerSaveDataEventArgs(ScriptEvent type, ClientInfo clientInfo, PlayerDataFile playerDataFile) : base(type)
        {
            this.clientInfo = clientInfo;
            this.playerDataFile = playerDataFile;
        }
    }

    public class ChunkMapCalculatedEventArgs : ScriptEventArgs
    {
        public Chunk chunk;

        public ChunkMapCalculatedEventArgs(ScriptEvent type, Chunk chunk) : base(type)
        {
            this.chunk = chunk;
        }
    }
}
