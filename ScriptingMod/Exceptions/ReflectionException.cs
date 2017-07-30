using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ScriptingMod.Exceptions
{
    internal class ReflectionException : ApplicationException
    {
        public Type TargetType { get; set; }

        public ReflectionException(Type targetType, Exception innerEx) :
            base($"Error while establishing references to {targetType}. Your game version might not be compatible with this Scripting Mod version.", innerEx)
        {
            TargetType = targetType;
        }

    }
}
