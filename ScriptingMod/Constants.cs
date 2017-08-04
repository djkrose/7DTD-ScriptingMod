using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ScriptingMod
{
    internal static class Constants
    {
        public const string ModNameFull             = "djkrose's Scripting Mod";
        public const string ModInfoFile             = "ModInfo.xml";

        public static readonly string ScriptingModFolder = Path.GetDirectoryName(Assembly.GetAssembly(typeof(Constants)).Location);
        public static readonly string ScriptsFolder      = Path.Combine(ScriptingModFolder, "scripts");
        public static readonly string PrefabsFolder      = Path.GetFullPath(Utils.GetGameDir(global::Constants.cDirPrefabs));

        //public static readonly string SaveGameFolder = GamePrefs.GetString(EnumGamePrefs.SaveGameFolder);
    }
}
