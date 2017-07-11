using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web;

namespace ScriptingMod
{
    internal static class ReflectionHelper
    {
        /// <summary>
        /// Sets the static field of the class defined by the given full class name from the given assembly
        /// </summary>
        /// <param name="assembly">The assembly type to look for the class in; can be retrieved from another public type for exambple</param>
        /// <param name="className">The fully qualified class name incl. namespace</param>
        /// <param name="fieldName">Field name of the static field to set</param>
        /// <param name="value">Value to set</param>
        public static void SetHiddenStaticField(Assembly assembly, string className, string fieldName, object value)
        {
            var classType = GetHiddenType(assembly, className);
            SetHiddenField(classType, null, fieldName, value);
        }

        /// <summary>
        /// Sets the static field of the class defined by the given class type
        /// </summary>
        /// <param name="classType">The class to look for the field in</param>
        /// <param name="fieldName">Field name of the static field to set</param>
        /// <param name="value">The value to the the field to</param>
        public static void SetHiddenStaticField(Type classType, string fieldName, object value)
        {
            SetHiddenField(classType, null, fieldName, value);
        }

        /// <summary>
        /// Sets the instance field of the given object to the given value
        /// </summary>
        /// <param name="classObj">The object to look for the field in</param>
        /// <param name="fieldName">Field name of the instance field to set</param>
        /// <param name="value">The value to the the field to</param>
        public static void SetHiddenInstanceField(object classObj, string fieldName, object value)
        {
            SetHiddenField(classObj.GetType(), classObj, fieldName, value);
        }

        /// <summary>
        /// Sets a private field (static or instance) in the given type and object to the given value.
        /// </summary>
        /// <param name="classType">The type of which classObj is</param>
        /// <param name="classObj">The object to set the instance field of, or null if this is a static field</param>
        /// <param name="fieldName">The full name of the field</param>
        /// <param name="value">The value to the the field to</param>
        private static void SetHiddenField(Type classType, object classObj, string fieldName, object value)
        {
            GetHiddenField(classType, fieldName).SetValue(classObj, value);
        }

        public static object GetHiddenStaticField(Assembly assembly, string className, string fieldName)
        {
            var classType = GetHiddenType(assembly, className);
            return GetHiddenField(classType, null, fieldName);
        }

        public static object GetHiddenStaticField(Type classType, string fieldName)
        {
            return GetHiddenField(classType, null, fieldName);
        }

        public static object GetHiddenInstanceField(object classObj, string fieldName)
        {
            return GetHiddenField(classObj.GetType(), classObj, fieldName);
        }

        private static object GetHiddenField(Type classType, object classObj, string fieldName)
        {
            return GetHiddenField(classType, fieldName).GetValue(classObj);
        }

        public static object GetHiddenEnum(Assembly assembly, string typeName, int value)
        {
            var enumType = GetHiddenType(assembly, typeName);
            return Enum.ToObject(enumType, value);
        }

        /// <summary>
        /// Calls the instance method with the given name from the classObj, passing in the parameters.
        /// The method must be UNIQUE, i.e. not overloaded, and must not be generic.
        /// </summary>
        /// <returns>Return value, otherwise null</returns>
        public static object CallHiddenInstanceMethod(object classObj, string methodName, object[] parameters = null)
        {
            MethodInfo method = GetHiddenMethod(classObj.GetType(), methodName);
            if (method == null)
                throw new TargetException($"Could not find instance method \"{methodName}\" in type \"{classObj.GetType().FullName}\".");
            return method.Invoke(classObj, parameters);
        }

        public static object CallHiddenStaticMethod(Type classType, string methodName, object[] parameters = null)
        {
            MethodInfo method = GetHiddenMethod(classType, methodName);
            if (method == null)
                throw new TargetException($"Could not find static method \"{methodName}\" in type \"{classType.FullName}\".");
            return method.Invoke(null, parameters);
        }

        public static object CallHiddenConstructor(Type classType, Type[] paramTypes, object[] paramValues = null)
        {
            ConstructorInfo ci = classType.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, paramTypes, null);
            return ci.Invoke(paramValues);
        }

        public static FieldInfo GetHiddenField(Type classType, string fieldName)
        {
            var flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;
            var instanceField = classType.GetField(fieldName, flags);
            if (instanceField == null)
                throw new TargetException($"Could not find hidden field {fieldName} in class {classType.FullName} of assembly {classType.Assembly.Location}.");
            return instanceField;
        }

        public static Type GetHiddenType(Assembly assembly, string typeName)
        {
            var type = assembly.GetType(typeName);
            if (type == null)
                throw new TargetException($"Could not find type {typeName} in assembly {assembly.Location}.");
            return type;
        }

        public static MethodInfo GetHiddenMethod(Type obj, string methodName)
        {
            // Static method
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
            return ((Type)obj).GetMethod(methodName, flags);
        }

    }
}