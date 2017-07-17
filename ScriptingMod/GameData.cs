using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ScriptingMod
{
    [Obsolete("Don't really use this class! It's just for reference to see where to find things.")]
    public static class GameData
    {
        /// <summary>
        /// Dictionary of playrs; key = entityId, value = EntityPlayer object
        /// </summary>
        public static Dictionary<int, EntityPlayer> Players => GameManager.Instance.World.Players.dict;

    }
}
