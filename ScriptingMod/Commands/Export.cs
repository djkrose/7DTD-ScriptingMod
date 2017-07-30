using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using JetBrains.Annotations;
using ScriptingMod.Exceptions;
using ScriptingMod.Extensions;
using ScriptingMod.Managers;

namespace ScriptingMod.Commands
{
    /*
     * TODO [P3]: Copy trader entity and protected area correctly
     */

    public class Export : ConsoleCmdAbstract
    {
        internal const string TileEntityFileMarker    = "7DTD-TE";
        internal const string TileEntityFileExtension = ".te";
        internal const int    TileEntityFileVersion   = 4;

        private static Dictionary<int, Vector3i> savedPos = new Dictionary<int, Vector3i>(); // entityId => position

        public override string[] GetCommands()
        {
            return new[] {"dj-export"};
        }

        public override string GetDescription()
        {
            return "Exports a prefab including all container content, sign texts, ownership, etc.";
        }

        public override string GetHelp()
        {
            // ----------------------------------(max length: 100 char)--------------------------------------------|
            return $@"
                Exports an area as prefab into the /Data/Prefabs folder. Additional block metadata like container
                content, sign texts, ownership, etc. is stored in a separate ""tile entity"" file ({TileEntityFileExtension}).
                Usage:
                    1. dj-export <name> <x1> <y1> <z1> <x2> <y2> <z2>
                    2. dj-export
                    3. dj-export <name>
                1. Exports the prefab within the area from x1 y1 z1 to x2 y2 z2.
                2. Saves the current player's position for usage 3.
                3. Exports the prefab within the area from the saved position to current players position.
                ".Unindent();
        }

        public override void Execute(List<string> paramz, CommandSenderInfo senderInfo)
        {
            try
            {
                (string prefabName, Vector3i pos1, Vector3i pos2) = ParseParams(paramz, senderInfo);
                FixOrder(ref pos1, ref pos2);
                // Saving tile entities first, because that also checks if chunks are loaded
                SaveTileEntities(prefabName, pos1, pos2);
                SavePrefab(prefabName, pos1, pos2);

                SdtdConsole.Instance.Output($"Prefab {prefabName} with block metadata exported. Area mapped from {pos1} to {pos2}.");
            }
            catch (Exception ex)
            {
                CommandManager.HandleCommandException(ex);
            }
        }

        private static (string prefabName, Vector3i pos1, Vector3i pos2) ParseParams(List<string> paramz, CommandSenderInfo senderInfo)
        {
            if (paramz.Count == 0)
            {
                var ci = PlayerManager.GetClientInfo(senderInfo);
                savedPos[ci.entityId] = PlayerManager.GetPosition(ci);
                throw new FriendlyMessageException("Your current position was saved: " + savedPos[ci.entityId]);
            }

            var prefabName = paramz[0];
            Vector3i pos1, pos2;

            if (paramz.Count == 1)
            {
                var ci = PlayerManager.GetClientInfo(senderInfo);
                if (!savedPos.ContainsKey(ci.entityId))
                    throw new FriendlyMessageException("Please save start point of the area first. See help for details.");
                pos1 = savedPos[ci.entityId];
                pos2 = PlayerManager.GetPosition(ci);
            }
            else
            {
                try {
                    pos1 = new Vector3i(Int32.Parse(paramz[1]), Int32.Parse(paramz[2]), Int32.Parse(paramz[3]));
                    pos2 = new Vector3i(Int32.Parse(paramz[4]), Int32.Parse(paramz[5]), Int32.Parse(paramz[6]));
                } catch (Exception) {
                    throw new FriendlyMessageException("At least one of the given coordinates is not a valid integer.");
                }
            }

            return (prefabName, pos1, pos2);
        }

        private static void SavePrefab(string prefabName, Vector3i pos1, Vector3i pos2)
        {
            var size = new Vector3i(pos2.x - pos1.x + 1, pos2.y - pos1.y + 1, pos2.z - pos1.z + 1);
            var prefab = new Prefab(size);
            prefab.CopyFromWorld(GameManager.Instance.World, new Vector3i(pos1.x, pos1.y, pos1.z), new Vector3i(pos2.x, pos2.y, pos2.z));
            prefab.bCopyAirBlocks = true;
            // DO NOT SET THIS! It crashes the server!
            //prefab.bSleeperVolumes = true;
            prefab.filename = prefabName;
            prefab.addAllChildBlocks();
            prefab.Save(prefabName);
            Log.Out($"Exported prefab {prefabName} from area {pos1} to {pos2}.");
        }

        private static void SaveTileEntities(string prefabName, Vector3i pos1, Vector3i pos2)
        {
            Dictionary<Vector3i, TileEntity> tileEntities = CollectTileEntities(pos1, pos2);

            var filePath = Path.Combine(Constants.PrefabsFolder, prefabName + TileEntityFileExtension);

            // Save all tile entities
            using (var writer = new BinaryWriter(new FileStream(filePath, FileMode.Create)))
            {
                writer.Write(TileEntityFileMarker);                                   // [string]   constant "7DTD-TE"
                writer.Write(TileEntityFileVersion);                                  // [Int32]    file version number
                NetworkUtils.Write(writer, pos1);                                     // [Vector3i] original area worldPos1
                NetworkUtils.Write(writer, pos2);                                     // [Vector3i] original area worldPos2

                // see Assembly-CSharp::Chunk.write() -> search "tileentity.write"
                writer.Write(tileEntities.Count);                                     // [Int32]    number of tile entities
                foreach (var keyValue in tileEntities)
                {
                    var posInWorld = keyValue.Key;
                    var tileEntity = keyValue.Value;
                    var posInPrefab = new Vector3i(posInWorld.x - pos1.x, posInWorld.y - pos1.y, posInWorld.z - pos1.z);

                    NetworkUtils.Write(writer, posInPrefab);                          // [3xInt32]  position relative to prefab
                    writer.Write((int)tileEntity.GetTileEntityType());                // [Int32]    TileEntityType enum
                    tileEntity.write(writer, TileEntity.StreamModeWrite.Persistency); // [dynamic]  tile entity data depending on type
                    Log.Debug($"Wrote tile entity {tileEntity}.");

                    var tileEntityPowered = tileEntity as TileEntityPowered;
                    if (tileEntityPowered != null)
                    {
                        var powerItem = tileEntityPowered.GetPowerItem()
                            ?? PowerItem.CreateItem(tileEntityPowered.PowerItemType);
                        SavePowerItem(writer, powerItem);
                    }
                }
            }

            Log.Out($"Exported {tileEntities.Count} tile entities for prefab {prefabName} from area {pos1} to {pos2}.");
        }

        private static Dictionary<Vector3i, TileEntity> CollectTileEntities(Vector3i pos1, Vector3i pos2)
        {
            var world = GameManager.Instance.World;
            var tileEntities = new Dictionary<Vector3i, TileEntity>(); // posInWorld => TileEntity

            // Collect all tile entities in the area
            for (int x = pos1.x; x <= pos2.x; x++)
            {
                for (int z = pos1.z; z <= pos2.z; z++)
                {
                    var chunk = world.GetChunkFromWorldPos(x, 0, z) as Chunk;
                    if (chunk == null)
                        throw new FriendlyMessageException("Area to export is too far away. Chunk not loaded on that area.");

                    for (int y = pos1.y; y <= pos2.y; y++)
                    {
                        var posInChunk = World.toBlock(x, y, z);
                        var posInWorld = new Vector3i(x, y, z);
                        var tileEntity = chunk.GetTileEntity(posInChunk);

                        if (tileEntity == null)
                            continue; // no container/door/sign block

                        tileEntities.Add(posInWorld, tileEntity);
                    }
                }
            }

            return tileEntities;
        }

        private static void SavePowerItem(BinaryWriter writer, [NotNull] PowerItem powerItem)
        {
            // Doing everything here that the PowerItem classes do in write(..) methods, but only for itself, not parents or childs.
            // Intentionally not using ELSE to because of PowerItem inheritence, see: https://abload.de/img/2017-07-3011_04_50-scwwpdu.png

            if (powerItem is PowerPressurePlate) // -> PowerTrigger
            {
                // nothing to write
            }
            if (powerItem is PowerTripWireRelay) // -> PowerTrigger
            {
                // nothing to write
            }
            if (powerItem is PowerTimerRelay) // -> PowerTrigger
            {
                var pi = (PowerTimerRelay) powerItem;
                writer.Write(pi.StartTime);
                writer.Write(pi.EndTime);
            }
            if (powerItem is PowerElectricWireRelay) // -> PowerConsumer
            {
                // nothing to write
            }
            if (powerItem is PowerTrigger) // -> PowerConsumer
            {
                var pi = (PowerTrigger)powerItem;
                writer.Write((byte)pi.TriggerType);
                if (pi.TriggerType == PowerTrigger.TriggerTypes.Switch)
                    writer.Write(pi.GetIsTriggered());
                else
                    writer.Write(pi.GetIsActive());
                if (pi.TriggerType != PowerTrigger.TriggerTypes.Switch)
                {
                    writer.Write((byte)pi.TriggerPowerDelay);
                    writer.Write((byte)pi.TriggerPowerDuration);
                    writer.Write(pi.GetDelayStartTime());
                    writer.Write(pi.GetPowerTime());
                }
                if (pi.TriggerType != PowerTrigger.TriggerTypes.Motion)
                    return;
                writer.Write((int)pi.TargetType);
            }
            if (powerItem is PowerConsumerToggle)
            {
                var pi = (PowerConsumerToggle)powerItem;
                writer.Write(pi.GetIsToggled());
            }
            if (powerItem is PowerRangedTrap)
            {
                var pi = (PowerRangedTrap)powerItem;
                writer.Write(pi.GetIsLocked());
                GameUtils.WriteItemStack(writer, pi.Stacks);
                writer.Write((int)pi.TargetType);
            }
            if (powerItem is PowerBatteryBank)
            {
                // nothing to write
            }
            if (powerItem is PowerGenerator)
            {
                var pi = (PowerGenerator)powerItem;
                writer.Write(pi.CurrentFuel);
            }
            if (powerItem is PowerSolarPanel)
            {
                // nothing to write
            }
            if (powerItem is PowerConsumer)
            {
                // nothing to write
            }
            if (powerItem is PowerSource)
            {
                var pi = (PowerSource)powerItem;
                writer.Write(pi.CurrentPower);
                writer.Write(pi.IsOn);
                GameUtils.WriteItemStack(writer, pi.Stacks);
            }
            if (powerItem is PowerConsumerSingle)
            {
                // nothing to write
            }
            if (powerItem is PowerItem)
            {
                var pi = (PowerItem)powerItem;

                // None of this is needed for import

                //writer.Write(pi.BlockID);
                //NetworkUtils.Write(writer, pi.Position);
                writer.Write(pi.Parent != null);
                if (pi.Parent != null)
                    NetworkUtils.Write(writer, pi.Parent.Position);
                //writer.Write((byte)pi.Children.Count);
                //for (int index = 0; index < pi.Children.Count; ++index)
                //{
                //    writer.Write((byte)pi.Children[index].PowerItemType);
                //    pi.Children[index].write(writer);
                //}
            }

            Log.Debug($"Wrote power item {powerItem.GetType()} at position {powerItem.Position}.");
        }

        /// <summary>
        /// Fix the order of xyz1 xyz2, so that the first is always smaller or equal to the second.
        /// </summary>
        private static void FixOrder(ref Vector3i pos1, ref Vector3i pos2)
        {
            if (pos2.x < pos1.x)
            {
                int val = pos1.x;
                pos1.x = pos2.x;
                pos2.x = val;
            }

            if (pos2.y < pos1.y)
            {
                int val = pos1.y;
                pos1.y = pos2.y;
                pos2.y = val;
            }

            if (pos2.z < pos1.z)
            {
                int val = pos1.z;
                pos1.z = pos2.z;
                pos2.z = val;
            }
        }
    }
}