using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using ScriptingMod.Exceptions;
using ScriptingMod.Extensions;
using ScriptingMod.Tools;

namespace ScriptingMod.Commands
{
    [UsedImplicitly]
    public class Export : ConsoleCmdAbstract
    {
        internal const string TileEntityFileMarker    = "7DTD-TE";
        internal const string TileEntityFileExtension = ".te";
        internal const int    TileEntityFileVersion   = 7;

        /// <summary>
        /// Saves last position for each entity executing the command individually: entityId => position
        /// </summary>
        private static Dictionary<int, Vector3i> savedPos = new Dictionary<int, Vector3i>();

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

        public override void Execute(List<string> parameters, CommandSenderInfo senderInfo)
        {
            TelemetryTools.CollectEvent("command", "execute", GetCommands()[0]);
            string prefabName = null;
            bool exportStarted = false;
            try
            {
                Vector3i pos1, pos2;
                (prefabName, pos1, pos2) = ParseParams(parameters, senderInfo);
                WorldTools.OrderAreaBounds(ref pos1, ref pos2);
                // Saving tile entities first, because that also checks if chunks are loaded
                exportStarted = true;
                SaveTileEntities(prefabName, pos1, pos2);
                SavePrefab(prefabName, pos1, pos2);

                SdtdConsole.Instance.Output($"Prefab {prefabName} with block metadata exported. Area mapped from {pos1} to {pos2}.");
            }
            catch (Exception ex)
            {
                // Clean up half-writen files. All or nothing.
                if (exportStarted && !string.IsNullOrEmpty(prefabName))
                {
                    try
                    {
                        var teFilePath = Path.Combine(Constants.PrefabsFolder, prefabName + TileEntityFileExtension);
                        if (File.Exists(teFilePath))
                            File.Delete(teFilePath);
                        var ttsFilePath = Path.Combine(Constants.PrefabsFolder, prefabName + global::Constants.cExtPrefabs);
                        if (File.Exists(ttsFilePath))
                            File.Delete(ttsFilePath);
                        var xmlFilePath = Path.Combine(Constants.PrefabsFolder, prefabName + ".xml");
                        if (File.Exists(xmlFilePath))
                            File.Delete(xmlFilePath);
                    }
                    catch (Exception)
                    {
                        Log.Warning("Exception thrown while cleaning up files because of another exception: " + ex);
                    }
                }

                CommandTools.HandleCommandException(ex);
            }
        }

        private static (string prefabName, Vector3i pos1, Vector3i pos2) ParseParams(List<string> parameters, CommandSenderInfo senderInfo)
        {
            if (parameters.Count == 0)
            {
                var ci = senderInfo.GetRemoteClientInfo();
                savedPos[ci.entityId] = ci.GetEntityPlayer().GetServerPos().ToVector3i();
                throw new FriendlyMessageException("Your current position was saved: " + savedPos[ci.entityId]);
            }

            var prefabName = parameters[0];

            // Sanatize prefabName to only include allowed characters
            if (!Regex.IsMatch(prefabName, @"^\w[\w.-]*$", RegexOptions.CultureInvariant))
                throw new FriendlyMessageException("The prefix name contains illegal characters. Please use only letters, numbers, dash, and underscore.");

            Vector3i pos1, pos2;

            if (parameters.Count == 1)
            {
                var ci = senderInfo.GetRemoteClientInfo();
                if (!savedPos.ContainsKey(ci.entityId))
                    throw new FriendlyMessageException("Please save start point of the area first. See help for details.");
                pos1 = savedPos[ci.entityId];
                pos2 = ci.GetEntityPlayer().GetServerPos().ToVector3i();
            }
            else if (parameters.Count == 7)
            {
                pos1 = CommandTools.ParseXYZ(parameters, 1);
                pos2 = CommandTools.ParseXYZ(parameters, 4);
            }
            else
            {
                throw new FriendlyMessageException(Resources.ErrorParameerCountNotValid);
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
                    var posInWorld  = keyValue.Key;
                    var tileEntity  = keyValue.Value;
                    var posInPrefab = posInWorld - pos1;

                    NetworkUtils.Write(writer, posInPrefab);                          // [3xInt32]  position relative to prefab
                    writer.Write((byte)tileEntity.GetTileEntityType());               // [byte]     TileEntityType enum
                    tileEntity.write(writer, TileEntity.StreamModeWrite.Persistency); // [dynamic]  tile entity data depending on type
                    Log.Debug($"Wrote tile entity {tileEntity}.");

                    var tileEntityPowered = tileEntity as TileEntityPowered;
                    if (tileEntityPowered != null)
                    {
                        if (!RepairEngine.IsValidTileEntityPowered(tileEntityPowered))
                            throw new FriendlyMessageException("The area contains a corrupt power block. Please fix it first with the \"dj-repair\" command.");

                        var powerItem = tileEntityPowered.GetPowerItem()
                            ?? PowerItem.CreateItem(tileEntityPowered.PowerItemType);
                        SavePowerItem(writer, powerItem);                             // [dynamic]  PowerItem data
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
                        throw new FriendlyMessageException(Resources.ErrorAreaTooFarAway);

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

        [SuppressMessage("ReSharper", "IsExpressionAlwaysTrue")]
        [SuppressMessage("ReSharper", "CanBeReplacedWithTryCastAndCheckForNull")]
        private static void SavePowerItem(BinaryWriter writer, [NotNull] PowerItem powerItem)
        {
            // Doing everything here that the PowerItem classes do in write(..) methods, but only for itself, not parents or childs.
            // Intentionally not using ELSE because of PowerItem inheritence, see: https://abload.de/img/poweritem-hierarchyzvumv.png

            if (powerItem is PowerItem)
            {
                // ReSharper disable once RedundantCast
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

            if (powerItem is PowerConsumer)
            {
                // nothing to write
            }

            if (powerItem is PowerConsumerToggle)
            {
                var pi = (PowerConsumerToggle)powerItem;
                writer.Write(pi.GetIsToggled());
            }

            if (powerItem is PowerElectricWireRelay)
            {
                // nothing to write
            }

            if (powerItem is PowerRangedTrap)
            {
                var pi = (PowerRangedTrap)powerItem;
                writer.Write(pi.GetIsLocked());
                GameUtils.WriteItemStack(writer, pi.Stacks);
                writer.Write((int)pi.TargetType);
            }

            if (powerItem is PowerTrigger)
            {
                var pi = (PowerTrigger)powerItem;
                writer.Write((byte)pi.TriggerType);
                // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
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

            if (powerItem is PowerPressurePlate)
            {
                // nothing to write
            }

            if (powerItem is PowerTimerRelay)
            {
                var pi = (PowerTimerRelay) powerItem;
                writer.Write(pi.StartTime);
                writer.Write(pi.EndTime);
            }

            if (powerItem is PowerTripWireRelay)
            {
                // nothing to write
            }

            if (powerItem is PowerConsumerSingle)
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

            Log.Debug($"Exported power item {powerItem.ToStringBetter()}.");
        }

    }
}