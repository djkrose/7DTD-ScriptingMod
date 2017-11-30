using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using ScriptingMod.Exceptions;
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
                var ci = senderInfo.RemoteClientInfo ?? throw new FriendlyMessageException(Resources.ErrorNotRemotePlayer);
                var world = GameManager.Instance.World ?? throw new FriendlyMessageException(Resources.ErrorWorldNotReady);
                var player = senderInfo.RemoteClientInfo.GetEntityPlayer();
                var bounds = BoundsUtils.ExpandBounds(player.boundingBox, 2, 2, 2);
                var entities = world.GetEntitiesInBounds(typeof(EntityItem), bounds, new List<Entity>());
                int counter = 0;

                var tt = typeof(EntityItem);
                Log.Dump(tt);
                Log.Dump(tt.UnderlyingSystemType);

                foreach (var entity in entities)
                {
                    // Filter out derived types like EntityBackpack and EntiyLootContainer
                    if (entity.GetType() != typeof(EntityItem))
                        continue;

                    var entityItem = (EntityItem) entity;
                    Log.Out($"Player \"{ci.playerName}\" ({ci.playerId}) paid {entityItem.itemStack.count}x {entityItem.itemStack.itemValue.ItemClass.Name} of quality {entityItem.itemStack.itemValue.Quality}.");
                    world.RemoveEntity(entity.entityId, EnumRemoveEntityReason.Killed);
                    counter++;
                }

                if (counter == 0)
                {
                    SdtdConsole.Instance.Output("Could not find any dropped items nearby.");
                }
                else
                {
                    SdtdConsole.Instance.Output($"Removed {counter} items as payment.");
                }
            }
            catch (Exception ex)
            {
                CommandTools.HandleCommandException(ex);
            }
        }

    }
#endif

}
