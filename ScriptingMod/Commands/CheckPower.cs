using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using JetBrains.Annotations;
using ScriptingMod.Exceptions;
using ScriptingMod.Extensions;
using ScriptingMod.Tools;
using UnityEngine;

namespace ScriptingMod.Commands
{
    [UsedImplicitly]
    public class CheckPower : ConsoleCmdAbstract
    {

        private static DateTime lastAutomaticCheck = DateTime.Now;

        /// <summary>
        /// For TileEntityPoweredTrigger objects this lists the TriggerTypes and which power item class are allowed together.
        /// Last updated: A16.2 b7
        /// Source: See TileEntityPoweredTrigger.CreatePowerItem()
        /// </summary>
        private static readonly Dictionary<PowerTrigger.TriggerTypes, Type> ValidTriggerTypes = 
            new Dictionary<PowerTrigger.TriggerTypes, Type>
        {
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
            // ----------------------------------(max length: 120 char)----------------------------------------------------------------|
            return @"
                Scans for corrupt power blocks that cause the server to spam this error message in the log files:
                    NullReferenceException: Object reference not set to an instance of an object
                    at TileEntityPoweredTrigger.write (System.IO.BinaryWriter _bw, StreamModeWrite _eStreamMode)
                    [...]
                Works only on currently LOADED chunks, that means a player must BE there and CAUSE the error to fix it.
                Usage:
                    1. dj-check-power [/fix]
                    2. dj-check-power here [/fix]
                    3. dj-check-power <x> <z> [/fix]
                    4. dj-check-power auto [/fix]
                1. Scans (and optionally fixes) all loaded chunks for corrupt power blocks.
                2. Scans (and optionally fixes) the chunk where you are currently standing.
                3. Scans (and optionally fixes) the chunk that contains the given world coordinate.
                4. Turns automatic fixing of broken power blocks on or off.
                Use optional parameter /fix to automatically repair errors.
                ".Unindent();
        }

        public override void Execute(List<string> parameters, CommandSenderInfo senderInfo)
        {
            try
            {
                (bool isFixMode, Vector3i? worldPos) = ParseParams(parameters, senderInfo);

                int scannedChunks;
                int brokenBlocks;

                if (worldPos == null)
                {
                    ScanAllChunks(isFixMode, out scannedChunks, out brokenBlocks);
                }
                else
                {
                    scannedChunks = 1;
                    brokenBlocks = ScanChunkAt(worldPos.Value, isFixMode);
                }

                var strChunks = $"chunk{(scannedChunks != 1 ? "s" : "")}";
                var strPowerBlocks = $"power block{(brokenBlocks != 1 ? "s" : "")}";
                var msg = isFixMode
                    ? ($"Found and fixed {brokenBlocks} broken {strPowerBlocks} in {scannedChunks} {strChunks}.")
                    : ($"Found {brokenBlocks} broken {strPowerBlocks} in {scannedChunks} {strChunks}."
                      + (brokenBlocks > 0 ? $" Use option /fix to fix {(brokenBlocks != 1 ? "them" : "it")}." : ""));

                SdtdConsole.Instance.Output(msg);
                Log.Out(msg);
            }
            catch (Exception ex)
            {
                CommandTools.HandleCommandException(ex);
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
                    if (parameters[0] == "here")
                    {
                        pos = PlayerTools.GetPosition(senderInfo);
                    }
                    else if (parameters[0] == "auto")
                    {
                        if (!PersistentData.Instance.CheckPowerAuto)
                        {
                            // Turn on
                            if (!isFixMode)
                                throw new FriendlyMessageException("Automatic mode doesn't make sense without fixing it. Option /fix is mandatory here.");

                            Application.logMessageReceived += OnLogMessageReceived;

                            PersistentData.Instance.CheckPowerAuto = true;
                            PersistentData.Instance.CheckPowerCounter = 0;
                            PersistentData.Instance.Save();

                            var msg = "Automatic fixing of broken power blocks turned ON.";
                            Log.Out(msg);
                            throw new FriendlyMessageException(msg);
                        }
                        else
                        {
                            // Turn off
                            Application.logMessageReceived -= OnLogMessageReceived;

                            var counter = PersistentData.Instance.CheckPowerCounter;

                            PersistentData.Instance.CheckPowerAuto = false;
                            PersistentData.Instance.CheckPowerCounter = 0;
                            PersistentData.Instance.Save();

                            var msg = "Automatic fixing of broken power blocks turned OFF." +
                                      $" {counter} power block{(counter == 1 ? " was" : "s were")} fixed since it was turned on.";
                            Log.Out(msg);
                            throw new FriendlyMessageException(msg);
                        }
                    }
                    else
                    {
                        throw new FriendlyMessageException("Wrong second parameter. See help.");
                    }
                    break;
                case 2:
                    pos = CommandTools.ParseXZ(parameters, 0);
                    break;
                default:
                    throw new FriendlyMessageException("Wrong number of parameters. See help.");
            }

            return (isFixMode, pos);
        }

        public static void InitAutomaticCheck()
        {
            if (PersistentData.Instance.CheckPowerAuto)
            {
                Log.Out("Automatic fixing of broken power blocks ist still ON.");
                Application.logMessageReceived += OnLogMessageReceived;
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private static void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            // Only check once every 5 seconds
            if (lastAutomaticCheck.AddSeconds(5) > DateTime.Now)
                return;

            lastAutomaticCheck = DateTime.Now;

            // Check if this is the exception we are looking for
            if (type != LogType.Exception || stackTrace == null || !stackTrace.StartsWith("NullReferenceException: Object reference not set to an instance of an object\r\n  at TileEntityPoweredTrigger.write"))
            {
                Log.Debug("Detected NRE TileEntityPoweredTrigger.write. Starting scan for broken power blocks in background ...");
                ThreadManager.AddSingleTaskSafe(delegate
                {
                    // Doing it in background task so that NRE appears before our output in log
                    // and so that we can use ThreadManager.IsMainThread() to determie command or background mode
                    try
                    {
                        ScanAllChunks(true, out var _, out var brokenBlocks);
                        if (brokenBlocks >= 1)
                        {
                            PersistentData.Instance.CheckPowerCounter += brokenBlocks;
                            PersistentData.Instance.Save();
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Error while automatically fixing broken power block in background: ", ex);
                        throw;
                    }
                });
            }
        }

        /// <summary>
        /// Iterate over all currently loaded chunks and scans (and optionally fixes) them
        /// </summary>
        /// <param name="isFixMode">true if broken blocks should be fixed, false if just counted</param>
        /// <param name="scannedChunks">Returns the number of scanned chunks</param>
        /// <param name="brokenBlocks">Returns the number of broken blocks (that should be now fixed if isFixMode was true)</param>
        private static void ScanAllChunks(bool isFixMode, out int scannedChunks, out int brokenBlocks)
        {
            if (ThreadManager.IsMainThread()) // because this is also called async where Output is not available or needed
                SdtdConsole.Instance.Output("Scanning all loaded chunks for broken power blocks ...");
            var chunks = GameManager.Instance.World.ChunkCache.GetChunkArray().ToList();
            scannedChunks = chunks.Count;
            brokenBlocks = chunks.Sum(chunk => ScanChunk(chunk, isFixMode));
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
                throw new FriendlyMessageException(Resources.ErrorAreaTooFarAway);

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
            var counter = 0;
            var tileEntities = chunk.GetTileEntities().Values.ToList();

            foreach (var tileEntity in tileEntities)
            {
                var te = tileEntity as TileEntityPowered;
                if (te == null || !IsBrokenTileEntityPowered(te))
                    continue;

                var msg = $"{(isFixMode ? "Found and fixed" : "Found")} broken power block at {tileEntity.ToWorldPos()} in {chunk}.";

                if (isFixMode)
                {
                    RecreateTileEntity(tileEntity);
                    counter++;
                    Log.Warning(msg);
                    if (ThreadManager.IsMainThread()) // because this is also called async where Output is not available or needed
                        SdtdConsole.Instance.Output(msg);
                }
                else
                {
                    counter++;
                    Log.Warning(msg);
                    if (ThreadManager.IsMainThread()) // because this is also called async where Output is not available or needed
                        SdtdConsole.Instance.Output(msg);
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
                // Trigger must be handled differently, because there are multiple possible power items for one TileEntityPoweredTriger,
                // and the PowerItemType is sometimes just (incorrectly) "PowerSource" when the TriggerType determines the *real* power type.

                // CHECK 1: Power item should be of type PowerTrigger if this is a TileEntityPoweredTrigger
                var piTrigger = pi as PowerTrigger;
                if (piTrigger == null)
                {
                    Log.Debug($"[{te.ToWorldPos()}] {teType} should have power item \"PowerTrigger\" or some descendant of it, but has power item \"{piType}\".");
                    return true;
                }

                // CHECK 2: PowerItemType should match the actual power item's object type, or be at least "PowerSource",
                // because TileEntityPoweredTrigger sometimes has the (incorrect) default PowerItemType "PowerSource" value
                // and only TriggerType is reliable. It "smells" but we have to accept it.
                if (te.PowerItemType != pi.PowerItemType && te.PowerItemType != PowerItem.PowerItemTypes.Consumer)
                {
                    Log.Debug($"[{te.ToWorldPos()}] {teType}.PowerItemType=\"{te.PowerItemType}\" doesn't match with {piType}.PowerItemType=\"{pi.PowerItemType}\" " +
                              $"and is also not the default \"{PowerItem.PowerItemTypes.Consumer}\".");
                    return true;
                }

                // CHECK 3: TriggerType and actual power item type should be compatible
                var expectedClass = ValidTriggerTypes.GetValue(teTrigger.TriggerType);
                if (expectedClass == null)
                    Log.Warning($"Unknown enum value PowerTrigger.TriggerTypes.{teTrigger.TriggerType} found.");
                else if (piType != expectedClass)
                {
                    Log.Debug($"[{te.ToWorldPos()}] {teType}.TriggerType=\"{teTrigger.TriggerType}\" doesn't fit together with power item \"{piType}\". " +
                              $"A {expectedClass} was expected.");
                    return true;
                }

                // CHECK 4: Tile entity's TriggerType and power items's TriggerType should match
                if (teTrigger.TriggerType != piTrigger.TriggerType)
                {
                    Log.Debug($"[{te.ToWorldPos()}] {teType}.TriggerType=\"{teTrigger.TriggerType}\" doesn't match with {piType}.PowerItemType=\"{piTrigger.TriggerType}\".");
                    return true;
                }
            }
            else
            {
                // CHECK 5: For all non-trigger tile entities, the power item type must match with the actual object
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

                // Wires to the broken block are cut and not restored. We could try to reattach everything, but meh...
            }

            var newPowerItem = newPowered?.GetPowerItem();
            Log.Debug($"[{tileEntity.ToWorldPos()}] Replaced old {tileEntity.GetType()} with new {newTileEntity.GetType()}" +
                      $"{(newPowerItem != null ? " and new power item " + newPowerItem.GetType() : "")}.");
        }

    }
}
