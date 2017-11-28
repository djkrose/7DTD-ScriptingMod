using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using ScriptingMod.Extensions;
using ScriptingMod.Tools;
using UnityEngine;

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

        public override void Execute(List<string> parameters, CommandSenderInfo senderInfo)
        {
            try
            {
                //var world = GameManager.Instance.World ?? throw new NullReferenceException(Resources.ErrorWorldNotReady);
                //var hashSetField = ReflectionTools.GetField(typeof(NetEntityDistribution), "hashSetList_0");
                //var hashSet = (HashSetList<NetEntityDistributionEntry>)hashSetField.GetValue(world.entityDistributer);
                //var hashMapField = ReflectionTools.GetField(typeof(NetEntityDistribution), "intHashMap_0");
                //var hashMap = (IntHashMap)hashMapField.GetValue(world.entityDistributer);
                //var entryPlayer = hashMap.lookup(senderInfo.RemoteClientInfo.entityId) as NetEntityDistributionEntry;
                //var entityPlayer = senderInfo.RemoteClientInfo.GetEntityPlayer();
                //var bagSlots = entityPlayer.bag.GetSlots();


                int min = 1;
                int max = 600;
                ItemValue itemValue;

                if (int.TryParse(parameters[0], out var itemId))
                {
                    itemValue = ItemClass.list[itemId] == null ? ItemValue.None : new ItemValue(itemId, min, max, true);
                }
                else
                {
                    if (!ItemClass.ItemNames.Contains(parameters[0]))
                    {
                        SdtdConsole.Instance.Output($"Unable to find item by id '{parameters[0]}'");
                        return;
                    }

                    itemValue = new ItemValue(ItemClass.GetItem(parameters[0]).type, min, max, true);
                }

                if (Equals(itemValue, ItemValue.None))
                {
                    SdtdConsole.Instance.Output($"Unable to find item by name '{parameters[0]}'");
                    return;
                }

                EntityPlayer entityPlayer = senderInfo.RemoteClientInfo.GetEntityPlayer();
                ItemStack itemStack = new ItemStack(itemValue, 1);
                entityPlayer.bag.SetSlot(30, itemStack);
                AIDirectorPlayerInventory inventory = AIDirectorPlayerInventory.FromEntity(entityPlayer);
                NetPackagePlayerInventory package = new NetPackagePlayerInventory(entityPlayer, inventory);
                senderInfo.RemoteClientInfo.SendPackage(package);
            }
            catch (Exception ex)
            {
                CommandTools.HandleCommandException(ex);
            }
        }

    }
#endif

}
