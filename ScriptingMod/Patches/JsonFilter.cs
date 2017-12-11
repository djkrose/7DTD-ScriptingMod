using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Harmony;
using JetBrains.Annotations;
using LitJson;
using ScriptingMod.Extensions;

namespace ScriptingMod.Patches
{
    [HarmonyPatch(typeof(JsonMapper), "AddTypeProperties")]
    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    public static class JsonFilter
    {

        public static bool Prepare()
        {
            Log.Out($"Injecting patch {nameof(JsonFilter)} ...");
            return true;
        }

        // This is a copy of the original JsonMapper.AddTypeProperties method and replaces it,
        // but modified to skip all fields and properties that are marked with [JsonIgnore].
        public static bool Prefix(Type type)
        {
            IDictionary typeProperties = NonPublic.JsonMapper.GetTypeProperties();
            if (!typeProperties.Contains(type))
            {
                IList list = NonPublic.JsonMapper.CreatePropertyMetadataList();
                foreach (PropertyInfo propertyInfo in type.GetProperties())
                {
                    // ------------- patch by djkrose --------------------------
                    if (propertyInfo.GetCustomAttributes(typeof(JsonIgnoreAttribute), false).Length != 0)
                        continue;
                    // ---------------------------------------------------------

                    if (propertyInfo.Name != "Item")
                    {
                        list.Add(NonPublic.JsonMapper.CreatePropertyMetadata(propertyInfo, false, null));
                    }
                }
                foreach (FieldInfo info in type.GetFields())
                {
                    // ------------- patch by djkrose --------------------------
                    if (info.GetCustomAttributes(typeof(JsonIgnoreAttribute), false).Length != 0)
                        continue;
                    // ---------------------------------------------------------

                    list.Add(NonPublic.JsonMapper.CreatePropertyMetadata(info, true, null));
                }
                lock (NonPublic.JsonMapper.GetTypePropertiesLock())
                {
                    try
                    {
                        typeProperties.Add(type, list);
                    }
                    catch (ArgumentException)
                    {
                    }
                }
            }
            return false;
        }

    }
}
