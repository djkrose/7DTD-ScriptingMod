using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using ScriptingMod.Extensions;

namespace ScriptingMod.Commands
{

#if DEBUG
    public class Dump : ConsoleCmdAbstract
    {
        private static FieldInfo _powerItemsField;     // PowerManager -> private List<PowerItem> list_0;
        private static FieldInfo _powerSourcesField;   // PowerManager -> private List<PowerSource> list_1;
        private static FieldInfo _powerTriggersField;  // PowerManager -> private List<PowerTrigger> list_2;
        private static FieldInfo _powerItemsDictField; // PowerManager -> private Dictionary<Vector3i, PowerItem> dictionary_0

        static Dump()
        {
            try
            {
                // Get references to private fields/methods/types by their signatures,
                // because the internal names change on every 7DTD release due to obfuscation.
                _powerItemsField = typeof(PowerManager).GetFieldsByType(typeof(List<PowerItem>)).Single();
                _powerSourcesField = typeof(PowerManager).GetFieldsByType(typeof(List<PowerSource>)).Single();
                _powerTriggersField = typeof(PowerManager).GetFieldsByType(typeof(List<PowerTrigger>)).Single();
                _powerItemsDictField = typeof(PowerManager).GetFieldsByType(typeof(Dictionary<Vector3i, PowerItem>)).Single();
            }
            catch (Exception ex)
            {
                Log.Error("Error while establishing references to 7DTD's \"private parts\". Your game version might not be compatible with this Scripting Mod version." + Environment.NewLine + ex);
                throw;
            }

            Log.Debug(typeof(Import) + " established reflection references.");
        }

        public override string[] GetCommands()
        {
            return new [] { "dj-dump" };
        }

        public override string GetDescription()
        {
            return "Dumps some debug infos that is just needed in a development task.";
        }

        public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
        {
            Log.Debug("Current thread: " + Thread.CurrentThread.Name);
            PowerManager.Instance.LogPowerManager();
            Log.Debug(ObjectDumper.Dump(_powerItemsField.GetValue(PowerManager.Instance), "PowerItems", 3));
            Log.Debug(ObjectDumper.Dump(_powerSourcesField.GetValue(PowerManager.Instance), "PowerSources", 3));
            Log.Debug(ObjectDumper.Dump(_powerTriggersField.GetValue(PowerManager.Instance), "PowerTriggers", 3));
            Log.Debug(ObjectDumper.Dump(_powerItemsDictField.GetValue(PowerManager.Instance), "PowerItemsDict", 3));
            SdtdConsole.Instance.Output("All data dumped to log file.");
        }
    }
#endif

}
