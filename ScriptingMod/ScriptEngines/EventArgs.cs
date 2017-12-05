using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using LitJson;
using ScriptingMod.Tools;
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

        public virtual string ToJson()
        {
            return JsonMapper.ToJson(new { eventType = type.ToString() });
        }
    }

    public class ChatMessageEventArgs : ScriptEventArgs
    {
        [CanBeNull] // doesn't need to come from player: kill messages, server messages, etc
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

        public override string ToJson()
        {
            return JsonMapper.ToJson(new
            {
                eventType = type.ToString(),
                messageType = messageType.ToString(),
                from = mainName,
                message,
                clientInfo,
            });
        }
    }

    public class EntityDamagedEventArgs : ScriptEventArgs
    {
        public EntityAlive entity;
        public DamageResponse damageResponse;

        public EntityDamagedEventArgs(ScriptEvent type, EntityAlive entity, DamageResponse damageResponse) : base(type)
        {
            this.entity = entity;
            this.damageResponse = damageResponse;
        }

        public override string ToJson()
        {
            var sourceEntity = GameManager.Instance.World?.GetEntity(damageResponse.Source?.getEntityId() ?? -1) as EntityAlive;
            return JsonMapper.ToJson(new
            {
                eventType = type.ToString(),
                position = entity.GetBlockPosition(),
                entityId = entity.entityId,
                entityName = entity.EntityName,
                sourceEntityId = sourceEntity?.entityId,
                sourceEntityName = sourceEntity?.EntityName,
                damageType = damageResponse.Source?.GetName().ToString(),
                bodyPart = damageResponse.HitBodyPart.ToString(),
                hitPoints = damageResponse.Strength,
                critical = damageResponse.Critical,
                fatal = damageResponse.Fatal,
            });
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

        public override string ToJson()
        {
            return JsonMapper.ToJson(new
            {
                eventType = type.ToString(),
                clientInfo,
            });
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

        public override string ToJson()
        {
            return JsonMapper.ToJson(new
            {
                eventType = type.ToString(),
                reason = kickPlayerData.ToString(),
                clientInfo,
            });
        }
    }

    public class ServerRegisteredEventArgs : ScriptEventArgs
    {
        public MasterServerAnnouncer masterServerAnnouncer;

        public ServerRegisteredEventArgs(ScriptEvent type, MasterServerAnnouncer masterServerAnnouncer) : base(type)
        {
            this.masterServerAnnouncer = masterServerAnnouncer;
        }

        public override string ToJson()
        {
            if (masterServerAnnouncer?.LocalGameInfo == null)
                return base.ToJson();

            return JsonMapper.ToJson(new
            {
                eventType = type.ToString(),
                gameInfos = masterServerAnnouncer.LocalGameInfo.GetDisplayValues() // dictionary of all relevant game infos
            });
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

        public override string ToJson()
        {
            return JsonMapper.ToJson(new
            {
                eventType = type.ToString(),
                logType = logType.ToString(),
                condition = condition.TrimEnd(),
                trace = trace.TrimEnd(),
            });
        }
    }

    public class EntityLoadedEventArgs : ScriptEventArgs
    {
        public Entity entity;

        public EntityLoadedEventArgs(ScriptEvent type, Entity entity) : base(type)
        {
            this.entity = entity;
        }

        public override string ToJson()
        {
            var entityAlive = entity as EntityAlive;
            return JsonMapper.ToJson(new
            {
                eventType = type.ToString(),
                entityType = entity.GetType().ToString(),
                entityId = entity.entityId,
                entityName = entityAlive?.EntityName,
                position = entity.GetBlockPosition(),
            });
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

        public override string ToJson()
        {
            var entityAlive = entity as EntityAlive;
            return JsonMapper.ToJson(new
            {
                eventType = type.ToString(),
                reason = reason.ToString(),
                entityType = entity.GetType().ToString(),
                entityId = entity.entityId,
                entityName = entityAlive?.EntityName,
                position = entity.GetBlockPosition(),
            });
        }
    }

    public class ChunkLoadedUnloadedEventArgs : ScriptEventArgs
    {
        public long chunkKey;

        public ChunkLoadedUnloadedEventArgs(ScriptEvent type, long chunkKey) : base(type)
        {
            this.chunkKey = chunkKey;
        }

        public override string ToJson()
        {
            return JsonMapper.ToJson(new
            {
                eventType = type.ToString(),
                chunkKey = chunkKey,
                chunkPos = ChunkTools.ChunkKeyToChunkXZ(chunkKey)
            });
        }
    }

    public class GameStatsChangedEventArgs : ScriptEventArgs
    {
        public EnumGameStats gameState;
        [CanBeNull]
        public object newValue;

        public GameStatsChangedEventArgs(ScriptEvent type, EnumGameStats gameState, object newValue) : base(type)
        {
            this.gameState = gameState;
            this.newValue = newValue;
        }

        public override string ToJson()
        {
            var jsonSupported = (newValue == null || newValue is int || newValue is long || newValue is float || newValue is double || newValue is bool);

            return JsonMapper.ToJson(new
            {
                eventType = type.ToString(),
                gameState = gameState.ToString(),
                newValue = jsonSupported ? newValue : newValue.ToString()
            });
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

        public override string ToJson()
        {
            return JsonMapper.ToJson(new
            {
                eventType = type.ToString(),
                clientInfo,
                compatibilityVersion,
            });
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

        public override string ToJson()
        {
            return JsonMapper.ToJson(new
            {
                eventType = type.ToString(),
                clientInfo,
                playerProfile
            });
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

        public override string ToJson()
        {
            return JsonMapper.ToJson(new
            {
                eventType = type.ToString(),
                reason = respawnReason.ToString(),
                position = pos,
                clientInfo,
            });
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

        public override string ToJson()
        {
            return JsonMapper.ToJson(new
            {
                eventType = type.ToString(),
                clientInfo,
            });
        }
    }

    public class PlayerSaveDataEventArgs : ScriptEventArgs
    {
        public ClientInfo clientInfo;
        public PlayerDataFile playerDataFile;

        private static string _playerDataDir;
        private static string PlayerDataDir
        {
            get
            {
                if (_playerDataDir == null)
                    _playerDataDir = GameUtils.GetPlayerDataDir().Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                return _playerDataDir;
            }
        }

        public PlayerSaveDataEventArgs(ScriptEvent type, ClientInfo clientInfo, PlayerDataFile playerDataFile) : base(type)
        {
            this.clientInfo = clientInfo;
            this.playerDataFile = playerDataFile;
        }

        public override string ToJson()
        {
            return JsonMapper.ToJson(new
            {
                eventType = type.ToString(),
                playerDataFile = PlayerDataDir + clientInfo.playerId + "." + PlayerDataFile.EXT,
                clientInfo,
            });
        }
    }

    public class ChunkMapCalculatedEventArgs : ScriptEventArgs
    {
        public Chunk chunk;

        public ChunkMapCalculatedEventArgs(ScriptEvent type, Chunk chunk) : base(type)
        {
            this.chunk = chunk;
        }

        public override string ToJson()
        {
            return JsonMapper.ToJson(new
            {
                eventType = type.ToString(),
                chunkKey = chunk.Key,
                chunkPos = ChunkTools.ChunkKeyToChunkXZ(chunk.Key),
            });
        }
    }

    public class PlayerLevelUpEventArgs : ScriptEventArgs
    {
        public EntityPlayer player;
        public int oldLevel;
        public int newLevel;

        public PlayerLevelUpEventArgs(ScriptEvent type, EntityPlayer player, int oldLevel, int newLevel) : base(type)
        {
            this.player = player;
            this.oldLevel = oldLevel;
            this.newLevel = newLevel;
        }

        public override string ToJson()
        {
            var clientInfo = ConnectionManager.Instance?.GetClientInfoForEntityId(player.entityId);

            return JsonMapper.ToJson(new
            {
                eventType = type.ToString(),
                oldLevel,
                newLevel,
                clientInfo,
            });
        }
    }

    public class PlayerExpGainedEventArgs : ScriptEventArgs
    {
        public EntityPlayer player;
        public int expGained;
        public bool levelUp;

        public PlayerExpGainedEventArgs(ScriptEvent type, EntityPlayer player, int expGained, bool levelUp) : base(type)
        {
            this.player = player;
            this.expGained = expGained;
            this.levelUp = levelUp;
        }

        public override string ToJson()
        {
            var clientInfo = ConnectionManager.Instance?.GetClientInfoForEntityId(player.entityId);

            return JsonMapper.ToJson(new
            {
                eventType = type.ToString(),
                expGained,
                expToNextLevel = player.ExpToNextLevel,
                levelUp,
                clientInfo,
            });
        }
    }
}
