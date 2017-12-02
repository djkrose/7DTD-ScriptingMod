using System;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using ScriptingMod.Exceptions;
using ScriptingMod.Extensions;

namespace ScriptingMod.Tools
{
    internal static class ReflectionTools
    {
        private const BindingFlags DefaultFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        /// <summary>
        /// Use reflection to get field by its name
        /// </summary>
        /// <exception cref="ReflectionException">Thrown if no matching field could be found</exception>
        public static FieldInfo GetField(Type target, string name, BindingFlags flags = DefaultFlags)
        {
            return target.GetField(name, flags)
                   ?? throw new ReflectionException($"Couldn't find field with name {name} in {target}.");
        }

        /// <summary>
        /// Use reflection to get field by its type
        /// </summary>
        /// <param name="target"></param>
        /// <param name="fieldType">Type of field to get</param>
        /// <param name="index">Optional index of field if multiple would match; default: 0</param>
        /// <param name="flags"></param>
        /// <exception cref="ReflectionException">Thrown if no or more than one matching field was found</exception>
        public static FieldInfo GetField([NotNull] Type target, Type fieldType, int? index = null, BindingFlags flags = DefaultFlags)
        {
            var candidates = target.GetFields(flags).Where(f => f.FieldType == fieldType).ToList();
            if (index == null && candidates.Count > 1)
                throw new ReflectionException($"Found more than one possible field with type {fieldType} in {target}.");
            return candidates.ElementAtOrDefault(index ?? 0)
                   ?? throw new ReflectionException($"Couldn't find field{(index != null ? " #" + index : "")} with type {fieldType} in {target}.");
        }

        /// <summary>
        /// Use reflection to get event by its name
        /// </summary>
        /// <exception cref="ReflectionException">Thrown if no matching event could be found</exception>
        [Obsolete("Does not work on Linux!", true)]
        public static EventInfo GetEvent(Type target, string name, BindingFlags flags = DefaultFlags)
        {
            throw new NotSupportedException("Getting EventInfo via reflection is not supported because it seems not to work on Linux.");
        }

        /// <summary>
        /// Use reflection to get event by its type
        /// </summary>
        /// <param name="target"></param>
        /// <param name="eventType">Type of event to get</param>
        /// <param name="index">Optional index of event if multiple would match; default: 0</param>
        /// <param name="flags"></param>
        /// <exception cref="ReflectionException">Thrown if no or more than one matching event was found</exception>
        [Obsolete("Does not work on Linux!", true)]
        public static EventInfo GetEvent([NotNull] Type target, Type eventType, int? index = null, BindingFlags flags = DefaultFlags)
        {
            throw new NotSupportedException("Getting EventInfo via reflection is not supported because it seems not to work on Linux.");
        }

        /// <summary>
        /// Use reflection to get constructor with the given parameter types
        /// </summary>
        /// <param name="target"></param>
        /// <param name="paramTypes">Types of the contructor's parameters</param>
        /// <param name="flags"></param>
        /// <exception cref="ReflectionException">Thrown if no matching constructor could be found</exception>
        public static ConstructorInfo GetConstructor(Type target, Type[] paramTypes, BindingFlags flags = DefaultFlags)
        {
            return target.GetConstructor(flags, null, paramTypes, null)
                   ?? throw new ReflectionException($"Couldn't find constructor with parameters ({paramTypes.ToString().Join(", ")}) in {target}.");
        }

        /// <summary>
        /// Use reflection to get nested type that contains a field with the type
        /// </summary>
        /// <param name="target"></param>
        /// <param name="containingFieldType">One example field that the nested type should contain to match</param>
        /// <param name="index">Optional index of nested type if multiple would match; default: 0</param>
        /// <param name="flags"></param>
        /// <exception cref="ReflectionException">Thrown if no matching constructor could be found</exception>
        public static Type GetNestedType(Type target, Type containingFieldType, int? index = null, BindingFlags flags = DefaultFlags)
        {
            var candidates = target.GetNestedTypes(flags).Where(
                t => t.GetFields(DefaultFlags).Any(
                    f => f.FieldType == containingFieldType)).ToList();
            if (index == null && candidates.Count > 1)
                throw new ReflectionException($"Found more than one possible nested type containing field of {containingFieldType} in {target}.");
            return candidates.ElementAtOrDefault(index ?? 0)
                   ?? throw new ReflectionException($"Couldn't find nested type{(index != null ? " #" + index : "")} with field of type {containingFieldType} in {target}.");
        }

        /// <summary>
        /// Use reflection to get method by its name.
        /// </summary>
        /// <exception cref="ReflectionException">Thrown if no matching method could be found</exception>
        public static MethodInfo GetMethod(Type target, string name, BindingFlags flags = DefaultFlags)
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

        public static MethodInfo GetMethod(Type target, Type returnType, Type[] paramTypes, int? index = null, BindingFlags flags = DefaultFlags)
        {
            return GetMethod(target, returnType, paramTypes, null, index, flags);
        }

        public static MethodInfo GetAddMethod(Type target, Type returnType, Type[] paramTypes, int? index = null, BindingFlags flags = DefaultFlags)
        {
            return GetMethod(target, returnType, paramTypes, "add_", index, flags);
        }

        public static MethodInfo GetRemoveMethod(Type target, Type returnType, Type[] paramTypes, int? index = null, BindingFlags flags = DefaultFlags)
        {
            return GetMethod(target, returnType, paramTypes, "remove_", index, flags);
        }

        /// <summary>
        /// Use reflection to get method by its return type and parameter types
        /// </summary>
        /// <param name="target"></param>
        /// <param name="returnType">Return type that the method should have to matcm</param>
        /// <param name="paramTypes">Types of the method's parameters</param>
        /// <param name="startsWith">Filter method by the name it should start with, e.g. "add_", or null to ignore name; default: null</param>
        /// <param name="index">Optional index of event if multiple would match; default: 0</param>
        /// <param name="flags"></param>
        /// <exception cref="ReflectionException">Thrown if no matching method could be found</exception>
        private static MethodInfo GetMethod(Type target, Type returnType, Type[] paramTypes, string startsWith = null, int? index = null, BindingFlags flags = DefaultFlags)
        {
            var candidates = target.GetMethods(flags).Where((m) =>
            {
                if (startsWith != null && !m.Name.StartsWith(startsWith))
                    return false;
                if (m.ReturnType != returnType)
                    return false;
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
            if (index == null && candidates.Count > 1)
                throw new ReflectionException($"Found more than one method with return type {returnType} and parameter types ({paramTypes.ToString().Join(", ")}) in {target}.");
            return candidates.ElementAtOrDefault(index ?? 0)
                   ?? throw new ReflectionException($"Couldn't find method{(index != null ? " #" + index : "")} with return type {returnType} and parameter types ({paramTypes.ToString().Join(", ")}) in {target}.");
        }
    }
}