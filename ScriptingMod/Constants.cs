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
        public const int    ChunkSize                    = 16;
        public const int    ChunkHeight                  = Byte.MaxValue;
        public const string ModName                      = "djkrose's Scripting Mod";
        public const string ModId                        = "ScriptingMod";

        public static readonly string ScriptingModFolder = Path.GetDirectoryName(Assembly.GetAssembly(typeof(Constants)).Location);
        public static readonly string ScriptsFolder      = Path.Combine(ScriptingModFolder, "scripts");

        // Using cached property to only execute this when needed, because the static initializer would fail in NUnit environment
        private static string _prefabsFolderCache;
        public static string PrefabsFolder => _prefabsFolderCache ?? (_prefabsFolderCache = Path.GetFullPath(Utils.GetGameDir(global::Constants.cDirPrefabs)));
    }
}
