using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using ScriptingMod.Exceptions;
using ScriptingMod.Extensions;
using ScriptingMod.Managers;

namespace ScriptingMod.Commands
{
    [UsedImplicitly]
    public class CheckPower : ConsoleCmdAbstract
    {
        private static readonly Dictionary<PowerTrigger.TriggerTypes, Type> ValidTriggerTypes = new Dictionary<PowerTrigger.TriggerTypes, Type>()
        {
            // See TileEntityPoweredTrigger.CreatePowerItem()
            { PowerTrigger.TriggerTypes.Switch,        typeof(PowerTrigger) },
            { PowerTrigger.TriggerTypes.PressurePlate, typeof(PowerPressurePlate) },
            { PowerTrigger.TriggerTypes.TimerRelay,    typeof(PowerTimerRelay) },
            { PowerTrigger.TriggerTypes.Motion,        typeof(PowerTrigger) },
            { PowerTrigger.TriggerTypes.TripWire,      typeof(PowerTripWireRelay) }
        };

        private static readonly Dictionary<PowerItem.PowerItemTypes, Type> ValidPowerItemTypes = new Dictionary<PowerItem.PowerItemTypes, Type>()
        {
            // See TileEntity.Instantiate(..)
            { PowerItem.PowerItemTypes.Consumer,          typeof(PowerConsumer) },
            { PowerItem.PowerItemTypes.ConsumerToggle,    typeof(PowerConsumerToggle) },
            { PowerItem.PowerItemTypes.Trigger,           typeof(PowerTrigger) },
            { PowerItem.PowerItemTypes.Timer,             typeof(PowerTimerRelay) },
            { PowerItem.PowerItemTypes.Generator,         typeof(PowerGenerator) },
            { PowerItem.PowerItemTypes.SolarPanel,        typeof(PowerSolarPanel) },
            { PowerItem.PowerItemTypes.BatteryBank,       typeof(PowerBatteryBank) },
            { PowerItem.PowerItemTypes.RangedTrap,        typeof(PowerRangedTrap) },
            { PowerItem.PowerItemTypes.ElectricWireRelay, typeof(PowerElectricWireRelay) },
            { PowerItem.PowerItemTypes.TripWireRelay,     typeof(PowerTripWireRelay) },
            { PowerItem.PowerItemTypes.PressurePlate,     typeof(PowerPressurePlate) }
                                                              // PowerConsumerSingle is not yet used anywhere
        };

        public override string[] GetCommands()
        {
            return new[] { "dj-check-power" };
        }

        public override string GetDescription()
        {
            return "Repairs corrupt power blocks causing NullReferenceException spam.";
        }

        public override string GetHelp()
        {
            // ----------------------------------(max length: 100 char)--------------------------------------------|
            return @"
                Scans for corrupt power blocks that cause the server to spam this error message in the log files:
                    NullReferenceException: Object reference not set to an instance of an object
                    at TileEntityPoweredTrigger.write (System.IO.BinaryWriter _bw, StreamModeWrite _eStreamMode)
                    [...]
                Works only on currently LOADED chunks, that means you most GO there and CAUSE the error to fix it.
                Usage:
                    1. dj-check-power [/fix]
                    2. dj-check-power here [/fix]
                    3. dj-check-power <x> <z> [/fix]
                1. Scans (and optionally fixes) all loaded chunks for corrupt power blocks.
                2. Scans (and optionally fixes) the chunk where you are currently standing.
                3. Scans (and optionally fixes) the chunk that contains the given world coordinate.
                Use optional parameter /fix to automatically repair errors.
                ".Unindent();
        }

        public override void Execute(List<string> parameters, CommandSenderInfo senderInfo)
        {
            try
            {
                (bool isFixMode, Vector3i? worldPos) = ParseParams(parameters, senderInfo);

                int countBroken;
                int countChunks;

                if (worldPos != null)
                {
                    countChunks = 1;
                    countBroken = ScanChunkAt(worldPos.Value, isFixMode);
                }
                else // scan all chunks
                {
                    (countChunks, countBroken) = ScanAllChunks(isFixMode);
                }

                var strChunks = $"chunk{(countChunks != 1 ? "s" : "")}";
                var strPowerBlocks = $"power block{(countBroken != 1 ? "s" : "")}";
                var msg = isFixMode
                    ? ($"Found and fixed {countBroken} broken {strPowerBlocks} in {countChunks} {strChunks}.")
                    : ($"Found {countBroken} broken {strPowerBlocks} in {countChunks} {strChunks}."
                      + (countBroken > 0 ? $" Use option /fix to fix {(countBroken != 1 ? "them" : "it")}." : ""));

                SdtdConsole.Instance.Output(msg);
                Log.Out(msg);
            }
            catch (Exception ex)
            {
                CommandManager.HandleCommandException(ex);
            }
        }

        private (bool isFixMode, Vector3i? worldPos) ParseParams(List<string> parameters, CommandSenderInfo senderInfo)
        {
            var isFixMode = parameters.Remove("/fix");
            Vector3i? pos;

            switch (parameters.Count)
            {
                case 0:
                    pos = null;
                    break;
                case 1:
                    if (parameters[0] != "here")
                        throw new FriendlyMessageException("Wrong second parameter. See help.");
                    pos = PlayerManager.GetPosition(senderInfo);
                    break;
                case 2:
                    try
                    {
                        pos = new Vector3i(Int32.Parse(parameters[0]), 0, Int32.Parse(parameters[1]));
                    }
                    catch (Exception ex)
                    {
                        throw new FriendlyMessageException("At least one of the given coordinates is not a valid integer.", ex);
                    }
                    break;
                default:
                    throw new FriendlyMessageException("Wrong number of parameters. See help.");
            }

            return (isFixMode, pos);
        }

        /// <summary>
        /// Iterate over all currently loaded chunks and scans (and optionally fixes) them
        /// </summary>
        /// <param name="isFixMode">true if broken blocks should be fixed, false if just counted</param>
        /// <returns>
        /// countChunks = number of chunks processed;
        /// countBroken = number of broken power blocks found (and fixed)
        /// </returns>
        private static (int countChunks, int countBroken) ScanAllChunks(bool isFixMode)
        {
            SdtdConsole.Instance.Output("Scanning all loaded chunks for broken power blocks ...");
            var chunks = GameManager.Instance.World.ChunkClusters[0].GetChunkArray().ToList();
            var countBroken = chunks.Sum(chunk => ScanChunk(chunk, isFixMode));
            var countChunks = chunks.Count;
            return (countChunks, countBroken);
        }

        /// <summary>
        /// Finds and scans (and optionally fixes) the chunk at the given position
        /// </summary>
        /// <param name="worldPos">World position to find the the chunk by</param>
        /// <param name="isFixMode">true if broken blocks should be fixed, false if just counted</param>
        /// <returns></returns>
        private static int ScanChunkAt(Vector3i worldPos, bool isFixMode)
        {
            var chunk = GameManager.Instance.World.GetChunkFromWorldPos(worldPos) as Chunk;
            if (chunk == null)
                throw new FriendlyMessageException($"Location {worldPos} is too far away. Chunk is not loaded.");

            SdtdConsole.Instance.Output($"Scanning chunk {chunk} for broken power blocks ...");
            var countBroken = ScanChunk(chunk, isFixMode);
            return countBroken;
        }

        /// <summary>
        /// Scans the given chunk object for broken power blocks and optionally fixes them
        /// </summary>
        /// <param name="chunk">The chunk object; must be loaded and ready</param>
        /// <param name="isFixMode">true if broken blocks should be fixed, false if just counted</param>
        /// <returns>Number of broken power blocks found (and fixed)</returns>
        private static int ScanChunk([NotNull] Chunk chunk, bool isFixMode)
        {
            int counter = 0;
            var tileEntities = chunk.GetTileEntities().Values.ToList();

            foreach (var tileEntity in tileEntities)
            {
                var te = tileEntity as TileEntityPowered;
                if (te == null || !IsBrokenTileEntityPowered(te))
                    continue;

                Log.Warning($"Found broken power block at position {tileEntity.ToWorldPos()} in {chunk}.");

                if (isFixMode)
                {
                    RecreateTileEntity(tileEntity);
                    counter++;
                    SdtdConsole.Instance.Output($"Fixed broken power block at position {tileEntity.ToWorldPos()} in {chunk}.");
                }
                else
                {
                    counter++;
                    SdtdConsole.Instance.Output($"Found broken power block at position {tileEntity.ToWorldPos()} in {chunk}.");
                }
            }

            return counter;
        }

        /// <summary>
        /// Returns true if the given tile entity has an invalid PowerItem attached; false otherwise
        /// </summary>
        public static bool IsBrokenTileEntityPowered([NotNull] TileEntityPowered te)
        {
            var teType = te.GetType();

            var pi = te.GetPowerItem();
            if (pi == null)
                return false;

            var teTrigger = te as TileEntityPoweredTrigger;
            if (teTrigger != null)
            {
                var piTrigger = pi as PowerTrigger;
                // Power item should be of type PowerTrigger if this is a PoweredTrigger TE
                if (piTrigger == null)
                {
                    Log.Debug($"{teType} should have power item \"PowerTrigger\" or some descendant of it, but has power item \"{pi.GetType()}\".");
                    return true;
                }

                // Trigger TE's sometimes have the default PowerItemType value, because the TriggerType determines the power item object
                if (te.PowerItemType != pi.PowerItemType && te.PowerItemType != PowerItem.PowerItemTypes.Consumer)
                {
                    Log.Debug($"{teType}.PowerItemType=\"{te.PowerItemType}\" doesn't match with {pi.GetType()}.PowerItemType=\"{pi.PowerItemType}\" and is also not the default \"{PowerItem.PowerItemTypes.Consumer}\".");
                    return true;
                }

                // TriggerType and actual power item type should be compatible
                var expectedType = ValidTriggerTypes.GetValue(teTrigger.TriggerType);
                if (pi.GetType() != expectedType)
                {
                    Log.Debug($"{teType}.TriggerType=\"{teTrigger.TriggerType}\" doesn't fit together with power item \"{pi.GetType()}\". A {expectedType} was expected.");
                    return true;
                }

                // TE's TriggerType and PI's TriggerType should match
                if (teTrigger.TriggerType != piTrigger.TriggerType)
                {
                    Log.Debug($"{teType}.TriggerType=\"{teTrigger.TriggerType}\" doesn't match with {pi.GetType()}.PowerItemType=\"{piTrigger.TriggerType}\".");
                    return true;
                }
            }
            else
            {
                if (te.PowerItemType != pi.PowerItemType)
                {
                    Log.Debug($"{teType}.PowerItemType=\"{te.PowerItemType}\" doesn't match with {pi.GetType()}.PowerItemType=\"{pi.PowerItemType}\".");
                    return true;
                }

                // PowerItemType and actual power item object type should be compatible
                var expectedType = ValidPowerItemTypes.GetValue(te.PowerItemType);
                if (pi.GetType() != expectedType)
                {
                    Log.Debug($"{teType}.PowerItemType=\"{te.PowerItemType}\" doesn't fit together with power item \"{pi.GetType()}\". A {expectedType} was expected.");
                    return true;
                }
            }
            
            return false;
        }

        /// <summary>
        /// Deletes the given tile entity from the given chunk and creates a new one based on the tile entity type
        /// </summary>
        private static void RecreateTileEntity([NotNull] TileEntity tileEntity)
        {
            var chunk = tileEntity.GetChunk();

            // Remove broken tile entity and hopefully the power item with it
            chunk.RemoveTileEntity(GameManager.Instance.World, tileEntity);

            // Create new tile entity
            var newTileEntity = TileEntity.Instantiate(tileEntity.GetTileEntityType(), chunk);
            newTileEntity.localChunkPos = tileEntity.localChunkPos;
            chunk.AddTileEntity(newTileEntity);

            // Recreate power item if necessary
            var newPowered = newTileEntity as TileEntityPowered;
            if (newPowered != null)
            {
                // Restore old PowerItemType and TriggerType values
                // TODO: This is not ideal, because it would recreate inconsistent data where the type variables were wrong
                if (tileEntity is TileEntityPowered tePowered)
                    newPowered.PowerItemType = tePowered.PowerItemType;
                if (tileEntity is TileEntityPoweredTrigger teTrigger && newPowered is TileEntityPoweredTrigger newTrigger)
                    newTrigger.TriggerType = teTrigger.TriggerType;

                newPowered.InitializePowerData();
            }

            // Detailed logging
            var msg = $"Replaced old TileEntity {tileEntity} with new {newTileEntity}.";
            if (newPowered != null)
            {
                var pi = newPowered.GetPowerItem();
                msg += " It's powered and has " + (pi != null ? $"a PowerItem of type {pi.GetType()}." : "no PowerItem.");
            }
            Log.Out(msg);
        }

    }
}
