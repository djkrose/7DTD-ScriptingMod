using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using JetBrains.Annotations;

namespace ScriptingMod.Extensions
{
    internal static class TypeExtensions
    {

        [Obsolete("Unused at the moment, but may be needed later")]
        public static IEnumerable<MethodInfo> GetMethodsBySig(this Type type, Type returnType, Type[] parameterTypes,
            BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
        {
            return type.GetMethods(bindingAttr).Where((m) =>
            {
                if (m.ReturnType != returnType) return false;
                var parameters = m.GetParameters();
                if ((parameterTypes == null || parameterTypes.Length == 0))
                    return parameters.Length == 0;
                if (parameters.Length != parameterTypes.Length)
                    return false;
                for (int i = 0; i < parameterTypes.Length; i++)
                {
                    if (parameters[i].ParameterType != parameterTypes[i])
                        return false;
                }
                return true;
            });
        }

        [NotNull]
        public static IEnumerable<FieldInfo> GetFieldsByType(this Type type, Type fieldType,
            BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
        {
            //Log.Debug($"Scanning {type} for fields of type {fieldType} ...");
            return type.GetFields(bindingAttr).Where(f => f.FieldType == fieldType);
        }

        [NotNull]
        public static IEnumerable<Type> GetNestedTypesByContainingField(this Type type, Type containingFieldType,
            BindingFlags bindingAttr = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
        {
            //Log.Debug($"Scanning {type} for nested types that contain field of type {containingFieldType} ...");
            return type.GetNestedTypes(bindingAttr).Where(t => t.GetFieldsByType(typeof(IConsoleCommand)).Any());
        }


    }
}
