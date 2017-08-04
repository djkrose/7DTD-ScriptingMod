using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using Jint.Parser;
using ScriptingMod.Exceptions;
using ScriptingMod.Extensions;
using ScriptingMod.Managers;

namespace ScriptingMod.Commands
{
    [UsedImplicitly]
    public class CheckPower : ConsoleCmdAbstract
    {
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
                    countBroken = ScanChunkAt(worldPos.Value, isFixMode);
                    countChunks = 1;
                }
                else // scan all chunks
                {
                    (countChunks, countBroken) = ScanAllChunks(isFixMode);
                }

                var strChunks = $"chunk{(countChunks != 1 ? "s" : "")}";
                var strTripwires = $"tripwire{(countBroken != 1 ? "s" : "")}";
                var msg = isFixMode
                    ? ($"Found and fixed {countBroken} broken {strTripwires} in {countChunks} {strChunks}.")
                    : ($"Found {countBroken} broken {strTripwires} in {countChunks} {strChunks}."
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
            var chunks = GameManager.Instance.World.ChunkClusters[0].GetChunkArray();
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
            var tileEntities = chunk.GetTileEntities().Values.ToArray();

            foreach (var tileEntity in tileEntities)
            {
                if (!IsBrokenTileEntity(tileEntity))
                    continue;

                if (isFixMode)
                {
                    Log.Warning($"Tile entity {tileEntity} is broken. Recreating it ...");
                    RecreateTileEntity(chunk, tileEntity);
                    counter++;
                    Log.Out($"Fixed broken tile entity at position {tileEntity.ToWorldPos()}.");
                    SdtdConsole.Instance.Output($"Fixed broken power block at position {tileEntity.ToWorldPos()}.");
                }
                else
                {
                    Log.Warning($"Tile entity {tileEntity} is broken.");
                    counter++;
                    SdtdConsole.Instance.Output($"Found broken power block at position {tileEntity.ToWorldPos()}.");
                }
            }

            return counter;
        }

        /// <summary>
        /// Returns true if the given tile entity has an invalid PowerItem attached; false otherwise
        /// </summary>
        private static bool IsBrokenTileEntity([NotNull] TileEntity tileEntity)
        {
            var trigger = tileEntity as TileEntityPoweredTrigger;
            if (trigger != null)
            {
                var pi = trigger.GetPowerItem();

                // Find trigger that don't match it's power item
                if (trigger.TriggerType == PowerTrigger.TriggerTypes.Switch && !(pi is PowerTrigger))
                    return true;

                if (trigger.TriggerType == PowerTrigger.TriggerTypes.PressurePlate && !(pi is PowerPressurePlate))
                    return true;

                if (trigger.TriggerType == PowerTrigger.TriggerTypes.TimerRelay && !(pi is PowerTimerRelay))
                    return true;

                if (trigger.TriggerType == PowerTrigger.TriggerTypes.Motion && !(pi is PowerTrigger))
                    return true;

                if (trigger.TriggerType == PowerTrigger.TriggerTypes.TripWire && !(pi is PowerTripWireRelay))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Deletes the given tile entity from the given chunk and creates a new one based on the tile entity type
        /// </summary>
        private static void RecreateTileEntity([NotNull] Chunk chunk, [NotNull] TileEntity tileEntity)
        {
            // Save some important values
            var tileEntityType = tileEntity.GetTileEntityType();
            var localChunkPos = tileEntity.localChunkPos;

            // Remove broken tile entity and hopefully the power item with it
            chunk.RemoveTileEntity(GameManager.Instance.World, tileEntity);

            // Create new tile entity
            var newTileEntity = TileEntity.Instantiate(tileEntityType, chunk);
            newTileEntity.localChunkPos = localChunkPos;
            chunk.AddTileEntity(newTileEntity);
        }

    }
}
