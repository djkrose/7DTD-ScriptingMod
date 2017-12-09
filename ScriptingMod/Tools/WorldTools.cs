using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ScriptingMod.Tools
{
    internal static class WorldTools
    {
        /// <summary>
        /// Fix the order of xyz1 xyz2, so that the first is always smaller or equal to the second.
        /// </summary>
        public static void OrderAreaBounds(ref Vector3i pos1, ref Vector3i pos2)
        {
            if (pos2.x < pos1.x)
            {
                int val = pos1.x;
                pos1.x = pos2.x;
                pos2.x = val;
            }

            if (pos2.y < pos1.y)
            {
                int val = pos1.y;
                pos1.y = pos2.y;
                pos2.y = val;
            }

            if (pos2.z < pos1.z)
            {
                int val = pos1.z;
                pos1.z = pos2.z;
                pos2.z = val;
            }
        }
    }
}
