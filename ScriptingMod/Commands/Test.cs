using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using JetBrains.Annotations;
using ScriptingMod.Extensions;
using ScriptingMod.Managers;

namespace ScriptingMod.Commands
{

#if DEBUG
    [UsedImplicitly]
    public class Test : ConsoleCmdAbstract
    {

        public override string[] GetCommands()
        {
            return new [] { "dj-test" };
        }

        public override string GetDescription()
        {
            return "Internal tests for Scripting Mod";
        }

        public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
        {
            try
            {
                Log.Debug("Old tile entities: \r\n" + TileEntityListToString(Export.TileEntityPoweredList));
                Log.Debug("New tile entities: \r\n" + TileEntityListToString(Import.TileEntityPoweredList));
                Log.Debug("Old power items: \r\n" + PowerItemListToString(Export.PowerItemList));
                Log.Debug("New power items: \r\n" + PowerItemListToString(Import.PowerItemList));

                var dict = (Dictionary<Vector3i, PowerItem>) typeof(PowerManager).GetField("dictionary_0", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(PowerManager.Instance);
                Log.Debug("Power Item dictionary: \r\n" + dict.Select(kv => $"[{kv.Key}] " + PowerItemToString(kv.Value)).Join("\r\n"));

                Log.Debug("Power Item Roots: \r\n" + PowerItemListToString(PowerManager.Instance.GetRootPowerItems()));

                var sources = (List<PowerSource>) typeof(PowerManager).GetField("list_1", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(PowerManager.Instance);
                Log.Debug("Power Item Sources: \r\n" + PowerItemListToString(sources.Cast<PowerItem>().ToList()));

                var triggers = (List<PowerTrigger>) typeof(PowerManager).GetField("list_2", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(PowerManager.Instance);
                Log.Debug("Power Item Triggers: \r\n" + PowerItemListToString(triggers.Cast<PowerItem>().ToList()));

                Log.Debug("Power Manager Log: ");
                PowerManager.Instance.LogPowerManager();
                Log.Debug("All dumped.");
            }
            catch (Exception ex)
            {
                CommandManager.HandleCommandException(ex);
            }
        }

        private string TileEntityListToString(List<TileEntityPowered> list)
        {
            return list.Select(TileEntityToString).Join("\r\n");
        }

        private string TileEntityToString(TileEntityPowered te)
        {
            return $"{te.ToStringBetter()} Parent=({te.GetParent()}), Children=({te.GetWireChildren().Select(v => v.ToString()).Join("; ")})";
        }

        private string PowerItemListToString(List<PowerItem> list)
        {
            return list.Select(PowerItemToString).Join("\r\n");
        }

        private string PowerItemToString(PowerItem pi)
        {
            return $"{pi.ToStringBetter()} Parent=({pi.Parent?.Position}), Children=({pi.Children.Select(ipi => ipi.Position.ToString()).Join("; ")})";
        }

        public void Execute()
        {
            Execute(null, new CommandSenderInfo());
        }
    }
#endif

}
