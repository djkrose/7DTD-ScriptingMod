using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Policy;
using System.Text;
using JetBrains.Annotations;
using ScriptingMod.Exceptions;
using ScriptingMod.Extensions;
using ScriptingMod.Tools;
using UnityEngine;

namespace ScriptingMod.Commands
{
    [UsedImplicitly]
    public class Repair : ConsoleCmdAbstract
    {
        private static DateTime lastAutomaticCheck = DateTime.Now;

        public override string[] GetCommands()
        {
            return new[] { "dj-repair" };
        }

        public override string GetDescription()
        {
            return "Repairs data corruption of various kinds.";
        }

        public override string GetHelp()
        {
            // ----------------------------------(max length: 120 char)----------------------------------------------------------------|
            return @"
                Scans for data corruption of various kinds and tries to repair it. Works only on loaded chunks with nearby players.
                Currently supported scans and fixes:
                 - Player stuck on login screen because of NullReferenceException at TileEntityPoweredTrigger.write
                 - Area cannot be exported due to corrupt power block state
                 - Biome spawn of zombies and animals halted in a chunk, especially after using settime, bc-remove, or dj-regen
                Usage:
                    1. dj-repair [/simulate]
                    2. dj-repair here [/simulate]
                    3. dj-repair <x> <z> [/simulate]
                    4. dj-repair auto [/simulate]
                1. Repairs all loaded chunks.
                2. Repairs the chunk where you are currently standing.
                3. Repairs the chunk that contains the given world coordinate.
                4. Turns periodic background scans with automatic repair on or off. State is kept between server restarts.
                Use optional parameter ""/simulate"" to just scan and report without actually repairing anything.
                ".Unindent();
        }

        public override void Execute(List<string> parameters, CommandSenderInfo senderInfo)
        {
            try
            {
                (var simulate, var worldPos) = ParseParams(parameters, senderInfo);

                var repairEngine = new RepairEngine();
                repairEngine.ConsoleOutput = SdtdConsole.Instance.Output;
                repairEngine.Simulate = simulate;
                repairEngine.WorldPos = worldPos;

                // Can only do specific scans when limited to a world pos
                if (repairEngine.WorldPos != null)
                    repairEngine.Scans = RepairEngineScans.LockedChunkRespawn | RepairEngineScans.WrongPowerItem;

                repairEngine.Start();

                var strChunks = $"chunk{(repairEngine.ScannedChunks != 1 ? "s" : "")}";
                var strProblems = $"problem{(repairEngine.ProblemsFound != 1 ? "s" : "")}";
                var msg = $"{(repairEngine.Simulate ? "Identified" : "Repaired")} {repairEngine.ProblemsFound} {strProblems} in {repairEngine.ScannedChunks} {strChunks}. [details in server log]";
                SdtdConsole.Instance.Output(msg);
                Log.Out(msg);
            }
            catch (Exception ex)
            {
                CommandTools.HandleCommandException(ex);
            }
        }

        private (bool simulate, Vector3i? worldPos) ParseParams(List<string> parameters, CommandSenderInfo senderInfo)
        {
            var simulate = parameters.Remove("/simulate");
            Vector3i? pos;

            switch (parameters.Count)
            {
                case 0:
                    pos = null;
                    break;
                case 1:
                    if (parameters[0] == "here")
                    {
                        pos = senderInfo.GetRemoteClientInfo().GetEntityPlayer().GetServerPos().ToVector3i();
                    }
                    else if (parameters[0] == "auto")
                    {
                        if (!PersistentData.Instance.RepairAuto)
                        {
                            // Turn on
                            Application.logMessageReceived += OnLogMessageReceived;

                            PersistentData.Instance.RepairAuto = true;
                            PersistentData.Instance.RepairAutoSimulate = simulate;
                            PersistentData.Instance.RepairCounter = 0;
                            PersistentData.Instance.Save();

                            var msg = $"Automatic{(simulate ? " simulated" : "" )} repair of corrupt data turned ON.";
                            Log.Out(msg);
                            throw new FriendlyMessageException(msg);
                        }
                        else
                        {
                            // Turn off
                            Application.logMessageReceived -= OnLogMessageReceived;

                            var counter = PersistentData.Instance.RepairCounter;

                            PersistentData.Instance.RepairAuto = false;
                            PersistentData.Instance.RepairCounter = 0;
                            PersistentData.Instance.Save();

                            var msg = $"Automatic {(simulate ? " simulated" : "" )} repair of corrupt data turned OFF." +
                                      $" {counter} problem{(counter == 1 ? " was" : "s were")} were identified since it was turned on.";
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

            return (simulate, pos);
        }

        public static void InitAuto()
        {
            if (PersistentData.Instance.RepairAuto)
            {
                Log.Out($"Automatic{(PersistentData.Instance.RepairAutoSimulate ? " simulated" : "" )} repair of corrupt data is still turned ON.");
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
            if (type == LogType.Exception 
                && condition == "NullReferenceException: Object reference not set to an instance of an object" 
                && stackTrace != null && stackTrace.StartsWith("TileEntityPoweredTrigger.write"))
            {
                ThreadManager.AddSingleTaskSafe(delegate
                {
                    // Doing it in background task so that NRE appears before our output in log
                    // and so that we can use ThreadManager.IsMainThread() to determie command or background mode
                    Log.Out("Detected NRE TileEntityPoweredTrigger.write. Starting integrity scan in background ...");
                    try
                    {
                        var repairEngine = new RepairEngine();
                        repairEngine.Simulate = PersistentData.Instance.RepairAutoSimulate;
                        repairEngine.Scans = RepairEngineScans.WrongPowerItem;
                        repairEngine.Start();

                        if (repairEngine.ProblemsFound >= 1)
                        {
                            PersistentData.Instance.RepairCounter += repairEngine.ProblemsFound;
                            PersistentData.Instance.Save();
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Error while running integrity scan in background: ", ex);
                        throw;
                    }
                });
            }
            else
            {
                Log.Debug($"Intercepted unknown log message:\r\ncondition={condition ?? "<null>"}\r\ntype={type}\r\nstackTrace={stackTrace ?? "<null>"}");
            }
        }

    }
}
