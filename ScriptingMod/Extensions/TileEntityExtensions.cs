using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;

namespace ScriptingMod.Extensions
{
    internal static class TileEntityExtensions
    {
        public static string ToStringBetter([CanBeNull] this TileEntity te)
        {
            if (te == null)
                return "TileEntity (null)";
            return $"{te.GetType()} ({te.GetTileEntityType()}) [{te.ToWorldPos()}]";
        }
    }
}
