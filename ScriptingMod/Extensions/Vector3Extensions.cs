using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ScriptingMod.Extensions
{
    internal static class Vector3Extensions
    {
        public static Vector3i ToVector3i(this Vector3 pos)
        {
            // Do NOT use "new Vector3i(Vector3 v)", because it calculates incorrectly by just casting to int, which rounds UP on negative numbers.
            return new Vector3i((int) Math.Floor(pos.x), (int) Math.Floor(pos.y), (int) Math.Floor(pos.z));
        }
    }
}
