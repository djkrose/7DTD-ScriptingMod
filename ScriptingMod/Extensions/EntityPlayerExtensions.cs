using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ScriptingMod.Extensions
{
    internal static class EntityPlayerExtensions
    {
        public static Vector3 GetServerPos(this EntityPlayer player)
        {
            return new Vector3(player.serverPos.x / 32f, player.serverPos.y / 32f, player.serverPos.z / 32f);
        }
    }
}
