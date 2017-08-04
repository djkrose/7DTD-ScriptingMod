using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ScriptingMod.Extensions
{
    internal static class Vector3iExtensions
    {
        public static byte[] ToBytes(this Vector3i obj)
        {
            byte[] fakeBytes = new byte[3 * sizeof(int)];   // for Vector3i = x, y, z
            NetworkUtils.Write(new BinaryWriter(new MemoryStream(fakeBytes)), obj);
            return fakeBytes;
        }
    }
}
