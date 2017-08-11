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
        /// <summary>
        /// For TileEntityPoweredTrigger objects this lists the TriggerTypes and which power item class are allowed together.
        /// Last updated: A16.2 b7
        /// Source: See TileEntityPoweredTrigger.CreatePowerItem()
        /// </summary>
        private static readonly Dictionary<PowerTrigger.TriggerTypes, Type> ValidTriggerTypes = 
            new Dictionary<PowerTrigger.TriggerTypes, Type>
        {
            // 
            { PowerTrigger.TriggerTypes.Switch,        typeof(PowerTrigger) },
            { PowerTrigger.TriggerTypes.PressurePlate, typeof(PowerPressurePlate) },
            { PowerTrigger.TriggerTypes.TimerRelay,    typeof(PowerTimerRelay) },
            { PowerTrigger.TriggerTypes.Motion,        typeof(PowerTrigger) },
            { PowerTrigger.TriggerTypes.TripWire,      typeof(PowerTripWireRelay) }
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

                Log.Warning($"Found broken power block at {tileEntity.ToWorldPos()} in {chunk}.");

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

            // Can't check what's not there. That's ok, some powered blocks (e.g. lamps) don't have a power item until connected.
            if (pi == null)
                return false;

            var piType = pi.GetType();

            var teTrigger = te as TileEntityPoweredTrigger;
            if (teTrigger != null)
            {
                // Trigger must be handled differently, because there multiple possible power items for one power item type,
                // and the PowerItemType determines the power item class only together with the TriggerType.

                // Power item should be of type PowerTrigger if this is a PoweredTrigger TE
                var piTrigger = pi as PowerTrigger;
                if (piTrigger == null)
                {
                    Log.Debug($"[{te.ToWorldPos()}] {teType} should have power item \"PowerTrigger\" or some descendant of it, but has power item \"{piType}\".");
                    return true;
                }

                // Trigger TE's sometimes have the default PowerItemType value, because the TriggerType determines the power item object
                if (te.PowerItemType != pi.PowerItemType && te.PowerItemType != PowerItem.PowerItemTypes.Consumer)
                {
                    Log.Debug($"[{te.ToWorldPos()}] {teType}.PowerItemType=\"{te.PowerItemType}\" doesn't match with {piType}.PowerItemType=\"{pi.PowerItemType}\" " +
                              $"and is also not the default \"{PowerItem.PowerItemTypes.Consumer}\".");
                    return true;
                }

                // TriggerType and actual power item type should be compatible
                var expectedClass = ValidTriggerTypes.GetValue(teTrigger.TriggerType);
                if (expectedClass == null)
                    Log.Warning($"Unknown enum value PowerTrigger.TriggerTypes.{teTrigger.TriggerType} found.");
                else if (piType != expectedClass)
                {
                    Log.Debug($"[{te.ToWorldPos()}] {teType}.TriggerType=\"{teTrigger.TriggerType}\" doesn't fit together with power item \"{piType}\". " +
                              $"A {expectedClass} was expected.");
                    return true;
                }

                // TE's TriggerType and PI's TriggerType should match
                if (teTrigger.TriggerType != piTrigger.TriggerType)
                {
                    Log.Debug($"[{te.ToWorldPos()}] {teType}.TriggerType=\"{teTrigger.TriggerType}\" doesn't match with {piType}.PowerItemType=\"{piTrigger.TriggerType}\".");
                    return true;
                }
            }
            else
            {
                // For all non-trigger tile entities the power item type must match with the actual object
                if (te.PowerItemType != pi.PowerItemType)
                {
                    Log.Debug($"[{te.ToWorldPos()}] {teType}.PowerItemType=\"{te.PowerItemType}\" doesn't match with {piType}.PowerItemType=\"{pi.PowerItemType}\".");
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

            // Prevent further errors on client updates; crucial when removing power item!
            tileEntity.SetDisableModifiedCheck(true);

            // Remove broken tile entity
            chunk.RemoveTileEntity(GameManager.Instance.World, tileEntity);

            // Remove power item
            var tePowered = tileEntity as TileEntityPowered;
            var powerItem = tePowered?.GetPowerItem();
            if (powerItem != null)
                PowerManager.Instance.RemovePowerNode(powerItem);

            // Create new tile entity
            var newTileEntity = TileEntity.Instantiate(tileEntity.GetTileEntityType(), chunk);
            newTileEntity.localChunkPos = tileEntity.localChunkPos;
            chunk.AddTileEntity(newTileEntity);

            // Recreate power item if necessary
            var newPowered = newTileEntity as TileEntityPowered;
            if (newPowered != null)
            {
                // Restore old PowerItemType and TriggerType values
                if (tePowered != null)
                    newPowered.PowerItemType = tePowered.PowerItemType;
                
                // fancy new C#7 syntax, isn't it? :)
                if (tileEntity is TileEntityPoweredTrigger teTrigger && newPowered is TileEntityPoweredTrigger newTrigger)
                    newTrigger.TriggerType = teTrigger.TriggerType;

                // Create power item according to PowerItemType and TriggerType
                newPowered.InitializePowerData();

                // TODO [P3]: Restore parent/childs and wires from old power item. Must also correctly set backlinks from parents/childs and update TE wiredata.
            }

            var newPowerItem = newPowered?.GetPowerItem();
            Log.Debug($"[{tileEntity.ToWorldPos()}] Replaced old {tileEntity.GetType()} with new {newTileEntity.GetType()}" +
                      $"{(newPowerItem != null ? " and new power item " + newPowerItem.GetType() : "")}.");
        }

    }
}
