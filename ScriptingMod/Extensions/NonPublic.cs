using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using ScriptingMod.Exceptions;

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

        private const BindingFlags defaultFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static readonly FieldInfo       fi_PowerTrigger_isTriggered;             // PowerTrigger        -> protected bool isTriggered;
        private static readonly FieldInfo       fi_PowerTrigger_isActive;                // PowerTrigger        -> protected bool isActive;
        private static readonly FieldInfo       fi_PowerTrigger_delayStartTime;          // PowerTrigger        -> protected float delayStartTime;
        private static readonly FieldInfo       fi_PowerTrigger_powerTime;               // PowerTrigger        -> protected float powerTime;
        private static readonly FieldInfo       fi_PowerConsumerToggle_isToggled;        // PowerConsumerToggle -> protected bool isToggled
        private static readonly FieldInfo       fi_PowerRangedTrap_isLocked;             // PowerRangedTrap     -> protected bool isLocked;
        private static readonly FieldInfo       fi_PowerItem_hasChangesLocal;            // PowerItem           -> protected bool hasChangesLocal;
        private static readonly FieldInfo       fi_TileEntityPowered_wireChildren;       // TileEntityPowered   -> private List<Vector3i> list_1
        private static readonly FieldInfo       fi_TileEntityPowered_wireParent;         // TileEntityPowered   -> private Vector3i vector3i_1
        private static readonly FieldInfo       fi_PowerManager_rootPowerItems;          // PowerManager        -> private List<PowerItem> list_0;
        private static readonly FieldInfo       fi_TileEntity_readVersion;               // TileEntity          -> protected int readVersion;
        private static readonly FieldInfo       fi_TileEntity_nextHeatMapEvent;          // TileEntity          -> private ulong ulong_0;
        private static readonly FieldInfo       fi_TileEntityPowered_bool_1;             // TileEntityPowered   -> private bool bool_1;  (unknown purpose)
        private static readonly FieldInfo       fi_TileEntityPowered_bool_3;             // TileEntityPowered   -> private bool bool_3;  (unknown purpose)

        private static readonly FieldInfo       fi_SdtdConsole_commandObjects;           // SdtdConsole         -> private List<IConsoleCommand> list_0
        private static readonly FieldInfo       fi_SdtdConsole_commandObjectPairs;       // SdtdConsole         -> private List<SdtdConsole.Struct13> list_1
        private static readonly FieldInfo       fi_SdtdConsole_commandObjectsReadOnly;   // SdtdConsole         -> private ReadOnlyCollection<IConsoleCommand> readOnlyCollection_0;
        private static readonly FieldInfo       fi_CommandObjectPair_CommandField;       // SdtdConsole         -> Struct13 -> public string string_0;
        private static readonly ConstructorInfo ci_CommandObjectPair_Constructor;        // SdtdConsole         -> Struct13 -> public Struct13(string string_1, IConsoleCommand iconsoleCommand_1)

        private static readonly MethodInfo      mi_ChunkProviderGenerateWorld_generateTerrain;

        static NonPublic()
        {
            try
            {
                fi_PowerTrigger_isTriggered           = GetField(typeof(PowerTrigger), "isTriggered");
                fi_PowerTrigger_isActive              = GetField(typeof(PowerTrigger), "isActive");
                fi_PowerTrigger_delayStartTime        = GetField(typeof(PowerTrigger), "delayStartTime");
                fi_PowerTrigger_powerTime             = GetField(typeof(PowerTrigger), "powerTime");
                fi_PowerConsumerToggle_isToggled      = GetField(typeof(PowerConsumerToggle), "isToggled");
                fi_PowerRangedTrap_isLocked           = GetField(typeof(PowerRangedTrap), "isLocked");
                fi_PowerItem_hasChangesLocal          = GetField(typeof(PowerItem), "hasChangesLocal");
                fi_TileEntityPowered_wireChildren     = GetField(typeof(TileEntityPowered), typeof(List<Vector3i>));
                fi_TileEntityPowered_wireParent       = GetField(typeof(TileEntityPowered), typeof(Vector3i));
                fi_PowerManager_rootPowerItems        = GetField(typeof(PowerManager), typeof(List<PowerItem>));
                fi_TileEntity_readVersion             = GetField(typeof(TileEntity), "readVersion");
                fi_TileEntity_nextHeatMapEvent        = GetField(typeof(TileEntity), typeof(ulong));
                fi_TileEntityPowered_bool_1 = GetField(typeof(TileEntityPowered), typeof(bool), 2); // WARNING! Relying on field order here!
                fi_TileEntityPowered_bool_3 = GetField(typeof(TileEntityPowered), typeof(bool), 4); // WARNING! Relying on field order here!

                fi_SdtdConsole_commandObjects         = GetField(typeof(global::SdtdConsole), typeof(List<IConsoleCommand>));
                var t_StdtConsole_CommandObjectPair   = GetNestedType(typeof(global::SdtdConsole), typeof(IConsoleCommand)); // struct Struct13, last in source, has IConsoleCommand field
                fi_SdtdConsole_commandObjectPairs     = GetField(typeof(global::SdtdConsole), typeof(List<>).MakeGenericType(t_StdtConsole_CommandObjectPair));
                fi_SdtdConsole_commandObjectsReadOnly = GetField(typeof(global::SdtdConsole), typeof(ReadOnlyCollection<IConsoleCommand>));
                ci_CommandObjectPair_Constructor      = GetConstructor(t_StdtConsole_CommandObjectPair, new[] { typeof(string), typeof(IConsoleCommand) });
                fi_CommandObjectPair_CommandField     = GetField(t_StdtConsole_CommandObjectPair, typeof(string));

                mi_ChunkProviderGenerateWorld_generateTerrain = GetMethod(typeof(ChunkProviderGenerateWorld), "generateTerrain");

                Log.Debug("Successfilly established reflection references.");
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
            }

            public struct CommandObjectPair
            {
                public object Base { get; }

                public string Command => (string)fi_CommandObjectPair_CommandField.GetValue(Base);

                public CommandObjectPair(object baseObject)
                {
                    Base = baseObject;
                }

                public CommandObjectPair(string command, object commandObject)
                {
                    Base = ci_CommandObjectPair_Constructor.Invoke(new [] { command, commandObject });
                }
            }
        }
        #endregion

        #region Reflection access helpers

        /// <summary>
        /// Use reflection to get field by its name
        /// </summary>
        private static FieldInfo GetField(Type target, string name, BindingFlags flags = defaultFlags)
        {
            return target.GetField(name, flags)
                   ?? throw new ReflectionException($"Couldn't find field with name {name} in {target}.");
        }

        /// <summary>
        /// Use reflection to get field by its type
        /// </summary>
        private static FieldInfo GetField([NotNull] Type target, Type fieldType, int? index = null, BindingFlags flags = defaultFlags)
        {
            var candidates = target.GetFields(flags).Where(f => f.FieldType == fieldType).ToList();
            if (index == null && candidates.Count > 1)
                throw new ReflectionException($"Found more than one possible field with type {fieldType} in {target}.");
            return candidates.ElementAtOrDefault(index ?? 0)
                   ?? throw new ReflectionException($"Couldn't find field with type {fieldType} in {target}.");
        }

        /// <summary>
        /// Use reflection to get constructor with the given parameter types
        /// </summary>
        private static ConstructorInfo GetConstructor(Type target, Type[] paramTypes, BindingFlags flags = defaultFlags)
        {
            return target.GetConstructor(flags, null, paramTypes, null)
                   ?? throw new ReflectionException($"Couldn't find constructor with parameters ({paramTypes.ToString().Join(", ")}) in {target}.");
        }

        /// <summary>
        /// Use reflection to get nested type that contains a field with the type
        /// </summary>
        private static Type GetNestedType(Type target, Type containingFieldType, int? index = 0, BindingFlags flags = defaultFlags)
        {
            var candidates = target.GetNestedTypes(flags).Where(
                t => t.GetFields(defaultFlags).Any(
                    f => f.FieldType == containingFieldType)).ToList();
            if (index == null && candidates.Count > 1)
                throw new ReflectionException($"Found more than one possible nested type containing field of {containingFieldType} in {target}.");
            return candidates.ElementAtOrDefault(index ?? 0)
                   ?? throw new ReflectionException($"Couldn't find nested type with field of type {containingFieldType} in {target}.");
        }

        /// <summary>
        /// Use reflection to get method by its name.
        /// </summary>
        private static MethodInfo GetMethod(Type target, string name, BindingFlags flags = defaultFlags)
        {
            try
            {
                return target.GetMethod(name, flags)
                    ?? throw new ReflectionException($"Couldn't find method with name {name} in {target}.");
            }
            catch (AmbiguousMatchException ex)
            {
                throw new ReflectionException($"Found more than one possible methods with name {name} in {target}.", ex);
            }
        }

        /// <summary>
        /// Use reflection to get method by its return type and parameter types
        /// </summary>
        private static MethodInfo GetMethod(Type target, Type returnType, Type[] paramTypes, int? index = 0, BindingFlags bindingAttr = defaultFlags)
        {
            var candidates = target.GetMethods(bindingAttr).Where((m) =>
            {
                if (m.ReturnType != returnType) return false;
                var parameters = m.GetParameters();
                if ((paramTypes == null || paramTypes.Length == 0))
                    return parameters.Length == 0;
                if (parameters.Length != paramTypes.Length)
                    return false;
                for (int i = 0; i < paramTypes.Length; i++)
                {
                    if (parameters[i].ParameterType != paramTypes[i])
                        return false;
                }
                return true;
            }).ToList();
            if (candidates.Count > 1)
                throw new ReflectionException($"Found more than one method with return type {returnType} and parameter types ({paramTypes.ToString().Join(", ")}) in {target}.");
            return candidates.ElementAtOrDefault(index ?? 0)
                ?? throw new ReflectionException($"Couldn't find method with return type {returnType} and parameter types ({paramTypes.ToString().Join(", ")}) in {target}.");
        }

        #endregion

    }
}