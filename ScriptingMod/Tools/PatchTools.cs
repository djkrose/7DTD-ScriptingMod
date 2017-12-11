using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Text;
using Harmony;

namespace ScriptingMod.Tools
{
    internal static class PatchTools
    {
        /// <summary>
        /// Initializes or reinitializes all patches.
        /// WARNING: Can an will be called multiple times when settings changed!
        /// </summary>
        public static void ApplyPatches()
        {
            Log.Debug("Applying patches ...");
            var harmony = HarmonyInstance.Create("com.github.djkrose.7DTD-ScriptingMod");
            
            // Will crash because of strange/obfuscated other types in the assembly:
            //harmony.PatchAll(Assembly.GetExecutingAssembly());

            // See HarmonyInstance.PatchAll
            var patchTypes = Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsClass && t.Namespace == "ScriptingMod.Patches");
            foreach (var type in patchTypes)
            {
                var parentMethodInfos = type.GetHarmonyMethods();
                if (parentMethodInfos == null || parentMethodInfos.Count <= 0)
                    continue;

                var info = HarmonyMethod.Merge(parentMethodInfos);

                if (IsPatchedWithType(info, type))
                {
                    Log.Debug($"Patch {type.Name} is already applied.");
                    continue;
                }

                var processor = new PatchProcessor(harmony, type, info);
                processor.Patch();
            }

            Log.Out("All enabled runtime patches were applied.");
        }

        /// <summary>
        /// Checks the merged harmonyMethod info whether the original method is already patched with the given patch type.
        /// </summary>
        private static bool IsPatchedWithType(HarmonyMethod harmonyMethod, Type withType)
        {
            MethodInfo originalMethod = AccessTools.Method(harmonyMethod.originalType, harmonyMethod.methodName, harmonyMethod.parameter);
            Harmony.Patches patches = PatchProcessor.IsPatched(originalMethod);
            if (patches == null)
                return false;

            bool DeclaredInType(Patch p) => p.patch.DeclaringType == withType;

            if (patches.Prefixes.Any(DeclaredInType) || patches.Postfixes.Any(DeclaredInType) || patches.Transpilers.Any(DeclaredInType))
                return true;

            return false;
        }

    }
}
