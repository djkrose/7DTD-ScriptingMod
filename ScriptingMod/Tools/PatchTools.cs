using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Harmony;

namespace ScriptingMod.Tools
{
    internal static class PatchTools
    {
        public static void ApplyPatches()
        {
            //if (!PersistentData.Instance.PatchCorpseItemDupeExploit)
            //{
            //    Log.Debug("Skipping whole patching system because no patch is enabled.");
            //    return;
            //}

            Log.Debug("Applying patches ...");
            var harmony = HarmonyInstance.Create("com.github.djkrose.7DTD-ScriptingMod");
            
            // Will crash because of strange/obfuscated other types in the assembly:
            //harmony.PatchAll(Assembly.GetExecutingAssembly());

            // Copied from HarmonyInstance.PatchAll but added isClass and Namespace check to limit scan to relevant types
            Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsClass && t.Namespace == "ScriptingMod.Patches").Do(type =>
            {
                var parentMethodInfos = type.GetHarmonyMethods();
                if (parentMethodInfos != null && parentMethodInfos.Count > 0)
                {
                    var info = HarmonyMethod.Merge(parentMethodInfos);
                    var processor = new PatchProcessor(harmony, type, info);
                    processor.Patch();
                }
            });

            Log.Out("All enabled runtime patches were applied.");
        }
    }
}
