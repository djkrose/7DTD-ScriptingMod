using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using ScriptingMod.Exceptions;
using ScriptingMod.Tools;

namespace ScriptingMod.Extensions
{
    /// <summary>
    /// Class abstracts access to non-public fields, properties, methods, and types through reflection ...
    /// ... for INSTANCE members by adding public getters/setters as extension methods, and
    /// ... for STATIC members/types by adding static members/types to nested classes with same name
    /// 
    /// Hopefully we get extensions for static members and properties soon, then the second part would become much cleaner!
    /// </summary>
    internal static class NonPublic
    {

        #region Initialization of reflection accessors

        private static FieldInfo       fi_PowerTrigger_isTriggered;                            // PowerTrigger               -> protected bool isTriggered;
        private static FieldInfo       fi_PowerTrigger_isActive;                               // PowerTrigger               -> protected bool isActive;
        private static FieldInfo       fi_PowerTrigger_delayStartTime;                         // PowerTrigger               -> protected float delayStartTime;
        private static FieldInfo       fi_PowerTrigger_powerTime;                              // PowerTrigger               -> protected float powerTime;
        private static FieldInfo       fi_PowerConsumerToggle_isToggled;                       // PowerConsumerToggle        -> protected bool isToggled
        private static FieldInfo       fi_PowerRangedTrap_isLocked;                            // PowerRangedTrap            -> protected bool isLocked;
        private static FieldInfo       fi_PowerItem_hasChangesLocal;                           // PowerItem                  -> protected bool hasChangesLocal;
        private static FieldInfo       fi_TileEntityPowered_wireChildren;                      // TileEntityPowered          -> private List<Vector3i> list_1
        private static FieldInfo       fi_TileEntityPowered_wireParent;                        // TileEntityPowered          -> private Vector3i vector3i_1
        private static FieldInfo       fi_PowerManager_rootPowerItems;                         // PowerManager               -> private List<PowerItem> list_0;
        private static FieldInfo       fi_TileEntity_readVersion;                              // TileEntity                 -> protected int readVersion;
        private static FieldInfo       fi_TileEntity_nextHeatMapEvent;                         // TileEntity                 -> private ulong ulong_0;
        private static FieldInfo       fi_TileEntityPowered_bool_1;                            // TileEntityPowered          -> private bool bool_1;  (unknown purpose)
        private static FieldInfo       fi_TileEntityPowered_bool_3;                            // TileEntityPowered          -> private bool bool_3;  (unknown purpose)

        private static FieldInfo       fi_SdtdConsole_commandObjects;                          // SdtdConsole                -> private List<IConsoleCommand> list_0
        private static FieldInfo       fi_SdtdConsole_commandObjectPairs;                      // SdtdConsole                -> private List<SdtdConsole.Struct13> list_1
        private static FieldInfo       fi_SdtdConsole_commandObjectsReadOnly;                  // SdtdConsole                -> private ReadOnlyCollection<IConsoleCommand> readOnlyCollection_0;
        private static FieldInfo       fi_CommandObjectPair_CommandField;                      // SdtdConsole                -> Struct13 -> public string string_0;
        private static FieldInfo       fi_CommandObjectPair_CommandObjectField;                // SdtdConsole                -> Struct13 -> public IConsoleCommand CC;

        private static ConstructorInfo ci_CommandObjectPair_Constructor;                       // SdtdConsole                -> Struct13 -> public Struct13(string string_1, IConsoleCommand iconsoleCommand_1)

        private static MethodInfo      mi_ChunkProviderGenerateWorld_generateTerrain;          // ChunkProviderGenerateWorld -> protected virtual void generateTerrain(World _world, Chunk _chunk, System.Random _random)
        private static MethodInfo      mi_ChunkProviderGenerateWorld_DecorateChunkOverlapping; // ChunkProviderGenerateWorld -> private void DE(Chunk _param1) { World world = this.world; ...

        private static FieldInfo       fi_ChunkAreaBiomeSpawnData_dict;                        // ChunkAreaBiomeSpawnData    -> private Dictionary<string, ChunkAreaBiomeSpawnData.LK> FP

        private static FieldInfo       fi_EACServer_successDelegateField;                      // EACServer                  -> private AuthenticationSuccessfulCallbackDelegate authenticationSuccessfulCallbackDelegate_0;
        private static FieldInfo       fi_EACServer_kickDelegateField;                         // EACServer                  -> private KickPlayerDelegate kickPlayerDelegate_0;

        private static FieldInfo       fi_EntityAlive_damageResponse;                          // EntityAlive                -> private DamageResponse damageResponse_0;

        private static MethodInfo      ev_MasterServerAnnouncer_add_ServerRegistered;          // MasterServerAnnouncer      -> private void add_Event_0(Action)

        private static FieldInfo       fi_NetPackagePlayerStats_entityId;                      // NetPackagePlayerStats      -> private int int_0;

        private static Type            t_JsonMapper_PropertyMetadata;                          // LitJson.JsonMapper         -> internal struct PropertyMetadata
        private static FieldInfo       fi_JsonMapper_typeProperties;                           // LitJson.JsonMapper         -> private static IDictionary<Type, IList<PropertyMetadata>> type_properties;
        private static FieldInfo       fi_JsonMapper_typePropertiesLock;                       // LitJson.JsonMapper         -> private static readonly object type_properties_lock = new object();

        private static FieldInfo       fi_PropertyMetadata_Info;                               // LitJson.PropertyMetadata   -> public MemberInfo Info;
        private static FieldInfo       fi_PropertyMetadata_IsField;                            // LitJson.PropertyMetadata   -> public bool IsField;
        private static FieldInfo       fi_PropertyMetadata_Type;                               // LitJson.PropertyMetadata   -> public Type Type;

        public static void Init()
        {
            try
            {
                fi_PowerTrigger_isTriggered                            = ReflectionTools.GetField(typeof(PowerTrigger), "isTriggered");
                fi_PowerTrigger_isActive                               = ReflectionTools.GetField(typeof(PowerTrigger), "isActive");
                fi_PowerTrigger_delayStartTime                         = ReflectionTools.GetField(typeof(PowerTrigger), "delayStartTime");
                fi_PowerTrigger_powerTime                              = ReflectionTools.GetField(typeof(PowerTrigger), "powerTime");
                fi_PowerConsumerToggle_isToggled                       = ReflectionTools.GetField(typeof(PowerConsumerToggle), "isToggled");
                fi_PowerRangedTrap_isLocked                            = ReflectionTools.GetField(typeof(PowerRangedTrap), "isLocked");
                fi_PowerItem_hasChangesLocal                           = ReflectionTools.GetField(typeof(PowerItem), "hasChangesLocal");
                fi_TileEntityPowered_wireChildren                      = ReflectionTools.GetField(typeof(TileEntityPowered), typeof(List<Vector3i>));
                fi_TileEntityPowered_wireParent                        = ReflectionTools.GetField(typeof(TileEntityPowered), typeof(Vector3i));
                fi_PowerManager_rootPowerItems                         = ReflectionTools.GetField(typeof(PowerManager), typeof(List<PowerItem>));
                fi_TileEntity_readVersion                              = ReflectionTools.GetField(typeof(TileEntity), "readVersion");
                fi_TileEntity_nextHeatMapEvent                         = ReflectionTools.GetField(typeof(TileEntity), typeof(ulong));
                fi_TileEntityPowered_bool_1                            = ReflectionTools.GetField(typeof(TileEntityPowered), typeof(bool), 2); // WARNING! Relying on member order here!
                fi_TileEntityPowered_bool_3                            = ReflectionTools.GetField(typeof(TileEntityPowered), typeof(bool), 4); // WARNING! Relying on member order here!
                fi_SdtdConsole_commandObjects                          = ReflectionTools.GetField(typeof(global::SdtdConsole), typeof(List<IConsoleCommand>));

                var t_StdtConsole_CommandObjectPair                    = ReflectionTools.GetNestedType(typeof(global::SdtdConsole), typeof(IConsoleCommand)); // struct Struct13, last in source, has IConsoleCommand field
                fi_SdtdConsole_commandObjectPairs                      = ReflectionTools.GetField(typeof(global::SdtdConsole), typeof(List<>).MakeGenericType(t_StdtConsole_CommandObjectPair));
                fi_SdtdConsole_commandObjectsReadOnly                  = ReflectionTools.GetField(typeof(global::SdtdConsole), typeof(ReadOnlyCollection<IConsoleCommand>));
                ci_CommandObjectPair_Constructor                       = ReflectionTools.GetConstructor(t_StdtConsole_CommandObjectPair, new[] { typeof(string), typeof(IConsoleCommand) });
                fi_CommandObjectPair_CommandField                      = ReflectionTools.GetField(t_StdtConsole_CommandObjectPair, typeof(string));
                fi_CommandObjectPair_CommandObjectField                = ReflectionTools.GetField(t_StdtConsole_CommandObjectPair, typeof(IConsoleCommand));

                mi_ChunkProviderGenerateWorld_generateTerrain          = ReflectionTools.GetMethod(typeof(ChunkProviderGenerateWorld), "generateTerrain");
                mi_ChunkProviderGenerateWorld_DecorateChunkOverlapping = ReflectionTools.GetMethod(typeof(ChunkProviderGenerateWorld), typeof(void), new [] {typeof(Chunk)}, 1); // WARNING! Relying on member order here!

                var t_ChunkAreaBiomeSpawnData_SpawnData                = ReflectionTools.GetNestedType(typeof(ChunkAreaBiomeSpawnData), typeof(ulong)); // private struct LK, has public ulong PP;
                fi_ChunkAreaBiomeSpawnData_dict                        = ReflectionTools.GetField(typeof(ChunkAreaBiomeSpawnData), typeof(Dictionary<,>).MakeGenericType(typeof(string), t_ChunkAreaBiomeSpawnData_SpawnData));

                fi_EACServer_successDelegateField                      = ReflectionTools.GetField(typeof(EACServer), typeof(AuthenticationSuccessfulCallbackDelegate));
                fi_EACServer_kickDelegateField                         = ReflectionTools.GetField(typeof(EACServer), typeof(KickPlayerDelegate));

                fi_EntityAlive_damageResponse                          = ReflectionTools.GetField(typeof(EntityAlive), typeof(DamageResponse));

                ev_MasterServerAnnouncer_add_ServerRegistered          = ReflectionTools.GetAddMethod(typeof(MasterServerAnnouncer), typeof(void), new [] { typeof(Action) });

                fi_NetPackagePlayerStats_entityId                      = ReflectionTools.GetField(typeof(NetPackagePlayerStats), typeof(int), 0); // WARNING! Relying on member order here!

                t_JsonMapper_PropertyMetadata                          = ReflectionTools.GetType(typeof(LitJson.JsonMapper).Assembly, "LitJson.PropertyMetadata");
                fi_JsonMapper_typeProperties                           = ReflectionTools.GetField(typeof(LitJson.JsonMapper), "type_properties");
                fi_JsonMapper_typePropertiesLock                       = ReflectionTools.GetField(typeof(LitJson.JsonMapper), "type_properties_lock");
                fi_PropertyMetadata_Info                               = ReflectionTools.GetField(t_JsonMapper_PropertyMetadata, "Info");
                fi_PropertyMetadata_IsField                            = ReflectionTools.GetField(t_JsonMapper_PropertyMetadata, "IsField");
                fi_PropertyMetadata_Type                               = ReflectionTools.GetField(t_JsonMapper_PropertyMetadata, "Type");

                Log.Out("Successfilly established reflection references.");
            }
            catch (Exception ex)
            {
                Log.Error("Error while establishing references to 7DTD's \"private parts\". Your game version might not be compatible with this Scripting Mod version." + Environment.NewLine + ex);
                throw;
            }
        }

        #endregion

        #region Reflected extensions to power-related types

        public static bool GetIsTriggered(this PowerTrigger obj)
        {
            return (bool)fi_PowerTrigger_isTriggered.GetValue(obj);
        }

        public static void SetIsTriggered(this PowerTrigger obj, bool value)
        {
            fi_PowerTrigger_isTriggered.SetValue(obj, value);
        }

        public static bool GetIsActive(this PowerTrigger obj)
        {
            return (bool)fi_PowerTrigger_isActive.GetValue(obj);
        }

        public static void SetIsActive(this PowerTrigger obj, bool value)
        {
            fi_PowerTrigger_isActive.SetValue(obj, value);
        }

        public static float GetDelayStartTime(this PowerTrigger obj)
        {
            return (float)fi_PowerTrigger_delayStartTime.GetValue(obj);
        }

        public static void SetDelayStartTime(this PowerTrigger obj, float value)
        {
            fi_PowerTrigger_delayStartTime.SetValue(obj, value);
        }

        public static float GetPowerTime(this PowerTrigger obj)
        {
            return (float)fi_PowerTrigger_powerTime.GetValue(obj);
        }

        public static void SetPowerTime(this PowerTrigger obj, float value)
        {
            fi_PowerTrigger_powerTime.SetValue(obj, value);
        }

        public static bool GetIsToggled(this PowerConsumerToggle obj)
        {
            return (bool)fi_PowerConsumerToggle_isToggled.GetValue(obj);
        }

        public static void SetIsToggled(this PowerConsumerToggle obj, bool value)
        {
            fi_PowerConsumerToggle_isToggled.SetValue(obj, value);
        }

        public static bool GetIsLocked(this PowerRangedTrap obj)
        {
            return (bool)fi_PowerRangedTrap_isLocked.GetValue(obj);
        }

        public static void SetIsLocked(this PowerRangedTrap obj, bool value)
        {
            fi_PowerRangedTrap_isLocked.SetValue(obj, value);
        }

        public static void SetHasChangesLocal(this PowerItem obj, bool value)
        {
            fi_PowerItem_hasChangesLocal.SetValue(obj, value);
        }

        public static List<Vector3i> GetWireChildren(this TileEntityPowered obj)
        {
            return (List<Vector3i>)fi_TileEntityPowered_wireChildren.GetValue(obj);
        }

        public static void SetWireParent(this TileEntityPowered obj, Vector3i pos)
        {
            fi_TileEntityPowered_wireParent.SetValue(obj, pos);
        }

        public static List<PowerItem> GetRootPowerItems(this PowerManager obj)
        {
            return (List<PowerItem>)fi_PowerManager_rootPowerItems.GetValue(obj);
        }

        public static void SetReadVersion(this TileEntity obj, int readVersion)
        {
            fi_TileEntity_readVersion.SetValue(obj, readVersion);
        }

        public static void SetNextHeatMapEvent(this TileEntity obj, ulong nextHeatMapEvent)
        {
            fi_TileEntity_nextHeatMapEvent.SetValue(obj, nextHeatMapEvent);
        }

        public static void SetBool1(this TileEntityPowered obj, bool value)
        {
            fi_TileEntityPowered_bool_1.SetValue(obj, value);
        }

        public static void SetBool3(this TileEntityPowered obj, bool value)
        {
            fi_TileEntityPowered_bool_3.SetValue(obj, value);
        }

        #endregion

        #region Reflected extensions to console-related types

        /// <summary>
        /// List of command objects.
        /// </summary>
        public static List<IConsoleCommand> GetCommandObjects(this global::SdtdConsole obj)
        {
            return (List<IConsoleCommand>)fi_SdtdConsole_commandObjects.GetValue(obj);
        }

        public static NonPublic.SdtdConsole.CommandObjectPairList GetCommandObjectPairs(this global::SdtdConsole obj)
        {
            return new NonPublic.SdtdConsole.CommandObjectPairList((IList)fi_SdtdConsole_commandObjectPairs.GetValue(obj));
        }

        public static void SetCommandObjectsReadOnly(this global::SdtdConsole obj, ReadOnlyCollection<IConsoleCommand> command)
        {
            fi_SdtdConsole_commandObjectsReadOnly.SetValue(obj, command);
        }

        #endregion

        #region Reflected extensions to chunk-related types

        public static void generateTerrain(this ChunkProviderGenerateWorld target, World world, Chunk chunk, System.Random random)
        {
            mi_ChunkProviderGenerateWorld_generateTerrain.Invoke(target, new object[] {world, chunk, random});
        }

        public static void DecorateChunkOverlapping(this ChunkProviderGenerateWorld target, Chunk chunk)
        {
            mi_ChunkProviderGenerateWorld_DecorateChunkOverlapping.Invoke(target, new object[] {chunk});
        }

        public static IEnumerable<string> GetEntityGroupNames(this ChunkAreaBiomeSpawnData target)
        {
            return ((IDictionary)fi_ChunkAreaBiomeSpawnData_dict.GetValue(target)).Keys.Cast<string>();
        }

        #endregion

        #region Reflected extensions to EAC types

        public static AuthenticationSuccessfulCallbackDelegate GetSuccessDelegate(this EACServer target)
        {
            return (AuthenticationSuccessfulCallbackDelegate)fi_EACServer_successDelegateField.GetValue(target);
        }

        public static KickPlayerDelegate GetKickDelegate(this EACServer target)
        {
            return (KickPlayerDelegate) fi_EACServer_kickDelegateField.GetValue(target);
        }

        public static void SetSuccessDelegate(this EACServer target, AuthenticationSuccessfulCallbackDelegate successDelegate)
        {
            fi_EACServer_successDelegateField.SetValue(target, successDelegate);
        }

        public static void SetKickDelegate(this EACServer target, KickPlayerDelegate kickDelegate)
        {
            fi_EACServer_kickDelegateField.SetValue(target, kickDelegate);
        }

        #endregion

        #region Reflected extensions for entity-related types

        public static DamageResponse GetDamageResponse(this EntityAlive target)
        {
            return (DamageResponse) fi_EntityAlive_damageResponse.GetValue(target);
        }

        #endregion

        #region Reflected extensions for Steam types

        /// <summary>
        /// Adds the given action to the private MasterServerAnnouncer event "onServerRegistered",
        /// which is called after the game was registered in Steam master servers,
        /// see MasterServerAnnouncer.RegisterGame
        /// </summary>
        /// <param name="target"></param>
        /// <param name="onServerRegistered"></param>
        public static void AddEventServerRegistered(this MasterServerAnnouncer target, Action onServerRegistered)
        {
            ev_MasterServerAnnouncer_add_ServerRegistered.Invoke(target, new object[] {onServerRegistered});
        }

        #endregion

        #region Reflected extionsions for NetPackage types

        public static int GetEntityId(this NetPackagePlayerStats target)
        {
            return (int)fi_NetPackagePlayerStats_entityId.GetValue(target);
        }

        #endregion

        #region Accessors for static members and types

        public static class SdtdConsole
        {
            public class CommandObjectPairList : IEnumerable<CommandObjectPair>
            {
                private IList _baseList;

                public int Count => _baseList.Count;

                public CommandObjectPairList(IList baseList)
                {
                    _baseList = baseList;
                }

                public IEnumerator<CommandObjectPair> GetEnumerator()
                {
                    return _baseList.Cast<object>().Select(o => new CommandObjectPair(o)).GetEnumerator();
                }

                IEnumerator IEnumerable.GetEnumerator()
                {
                    return GetEnumerator();
                }

                public void Insert(int index, CommandObjectPair obj)
                {
                    _baseList.Insert(index, obj.Base);
                }

                public CommandObjectPair ElementAt(int index)
                {
                    return new CommandObjectPair(_baseList[index]);
                }

                public void RemoveAt(int index)
                {
                    _baseList.RemoveAt(index);
                }
            }

            public struct CommandObjectPair
            {
                public object Base { get; }

                public string Command => (string) NonPublic.fi_CommandObjectPair_CommandField.GetValue(Base);
                public IConsoleCommand CommandObject => (IConsoleCommand) NonPublic.fi_CommandObjectPair_CommandObjectField.GetValue(Base);

                public CommandObjectPair(object baseObject)
                {
                    Base = baseObject;
                }

                public CommandObjectPair(string command, object commandObject)
                {
                    Base = NonPublic.ci_CommandObjectPair_Constructor.Invoke(new [] { command, commandObject });
                }
            }
        }

        public static class JsonMapper
        {
            public static object GetTypePropertiesLock()
            {
                return fi_JsonMapper_typePropertiesLock.GetValue(null);
            }

            public static IDictionary GetTypeProperties()
            {
                return (IDictionary) fi_JsonMapper_typeProperties.GetValue(null);
            }

            public static IList CreatePropertyMetadataList()
            {
                Type listType = typeof(List<>);
                Type listOfPropertyMetadataType = listType.MakeGenericType(t_JsonMapper_PropertyMetadata);
                return (IList)Activator.CreateInstance(listOfPropertyMetadataType);
            }

            public static object CreatePropertyMetadata(MemberInfo info, bool isField, Type type)
            {
                var propertyMetadata = Activator.CreateInstance(t_JsonMapper_PropertyMetadata);
                fi_PropertyMetadata_Info.SetValue(propertyMetadata, info);
                fi_PropertyMetadata_IsField.SetValue(propertyMetadata, isField);
                fi_PropertyMetadata_Type.SetValue(propertyMetadata, type);
                return propertyMetadata;
            }
        }

        #endregion

    }
}