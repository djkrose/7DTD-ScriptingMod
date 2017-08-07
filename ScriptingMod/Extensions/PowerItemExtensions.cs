using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;

namespace ScriptingMod.Extensions
{
    internal static class PowerItemExtensions
    {
        public static string ToStringBetter([CanBeNull] this PowerItem pi)
        {
            if (pi == null)
                return "PowerItem (null)";

            return $"{pi.GetType()} ({pi.PowerItemType}) [{pi.Position}]";
        }
    }
}
