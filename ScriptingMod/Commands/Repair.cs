using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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
        private static DateTime _lastLogMessageTriggeredScan = default(DateTime);

        /// <summary>
        /// Sorted repair task letters that make up the RepairTasks.Default flag, e.g. "DLMPR".
        /// RepairTasks.Default doesn't HAVE to include ALL tasks but could exclude experimental tasks.
        /// </summary>
        private static readonly string DefaultTaskLetters = Enum.GetValues(typeof(RepairTasks))
            .Cast<RepairTasks>()
            .Where(task => RepairTasks.Default.HasFlag(task))
            .Select(task => task.GetAttributeOfType<RepairTaskAttribute>())
            .Where(attr => attr != null)
            .OrderBy(attr => attr.Letter)
            .Aggregate("", (str, attr) => str + attr.Letter);

        /// <summary>
        /// Dictionary of repair task letter => Task to quickly find the task for a letter
        /// </summary>
        private static readonly Dictionary<char, RepairTasks> TasksDict = Enum.GetValues(typeof(RepairTasks))
            .Cast<RepairTasks>()
            .Select(task => new { Attribute = task.GetAttributeOfType<RepairTaskAttribute>(), Task = task })
            .Where(o => o.Attribute != null)
            .ToDictionary(o => o.Attribute.Letter, o => o.Task);

        /// <summary>
        /// List of "repair task letter  =>  description" as multi-line string with indentation except for the first line
        /// </summary>
        private static readonly string TasksHelp = Enum.GetValues(typeof(RepairTasks))
            .Cast<RepairTasks>()
            .Select(task => task.GetAttributeOfType<RepairTaskAttribute>())
            .Where(attr => attr != null)
            .OrderBy(attr => attr.Letter)
            .Aggregate("", (str, attr) => str + $"                    {attr.Letter}  =>  {attr.Description}\r\n")
            .Trim();

        public override string[] GetCommands()
        {
            return new[] { "dj-repair" };
        }

        public override string GetDescription()
        {
            return "Repairs server problems of various kinds.";
        }

        public override string GetHelp()
        {
            // ----------------------------------(max length: 120 char)----------------------------------------------------------------|
            return $@"
                Scans for server problems of various kinds and tries to repair them. Currently supported scan & repair tasks:
                    {TasksHelp}
                Usage:
                    1. dj-repair [/sim] [/auto]
                    2. dj-repair <task letters> [/sim] [/auto]
                1. Performs all default repair tasks. Same as ""dj-repair {DefaultTaskLetters}"".
                2. Performs the repair tasks identified by their letter(s), for example ""dj-repair MR"" to repair only minibikes and respawn.
                Optional parameters:
                    /sim   Simulate scan and report results without actually repairing anything
                    /auto  Turn automatic repairing in background on or off. See logfile for ongoing repair results.
                ".Unindent();
        }

        public override void Execute(List<string> parameters, CommandSenderInfo senderInfo)
        {
            try
            {
                ParseParams(parameters, out var tasks, out bool simulate, out bool auto);

                if (auto)
                {
                    if (!PersistentData.Instance.RepairAuto)
                    {
                        // Turn automatic mode ON
                        PersistentData.Instance.RepairAuto     = true;
                        PersistentData.Instance.RepairTasks    = tasks;
                        PersistentData.Instance.RepairSimulate = simulate;
                        PersistentData.Instance.RepairCounter  = 0;
                        PersistentData.Instance.Save();

                        if (tasks.HasFlag(RepairTasks.CorruptPowerBlocks))
                            Application.logMessageReceived += OnLogMessageReceived;

                        // TODO: Initialize background scanning thread for other tasks

                        SdtdConsole.Instance.LogAndOutput($"Automatic background {(simulate ? "scan (without repair)" : "repair")} for server problem(s) {RepairEngine.GetTaskLetters(tasks)} turned ON.");
                        SdtdConsole.Instance.Output("To turn off, enter the same command again.");
                    }
                    else
                    {
                        // Turn automatic mode OFF
                        PersistentData.Instance.RepairAuto = false;
                        PersistentData.Instance.Save();

                        Application.logMessageReceived -= OnLogMessageReceived;

                        // TODO: Stop and dispose other background threads

                        SdtdConsole.Instance.LogAndOutput(
                            $"Automatic background {(PersistentData.Instance.RepairSimulate ? "scan (without repair)" : "repair")} for server problems " +
                            $"{RepairEngine.GetTaskLetters(PersistentData.Instance.RepairTasks)} turned OFF.");
                        SdtdConsole.Instance.LogAndOutput(
                            $"Report: {PersistentData.Instance.RepairCounter} problem{(PersistentData.Instance.RepairCounter == 1 ? " was" : "s were")} " +
                            $"{(PersistentData.Instance.RepairSimulate ? "identified" : "repaired")} since it was turned on.");
                    }
                }
                else
                { 
                    var repairEngine = new RepairEngine
                    {
                        ConsoleOutput = SdtdConsole.Instance.Output,
                        Simulate      = simulate,
                        Tasks         = tasks
                    };
                    repairEngine.Start();
                }
            }
            catch (Exception ex)
            {
                CommandTools.HandleCommandException(ex);
            }
        }

        private static void ParseParams(List<string> parameters, out RepairTasks tasks, out bool simulate, out bool auto)
        {
            simulate = parameters.Remove("/sim");
            auto     = parameters.Remove("/auto");

            switch (parameters.Count)
            {
                case 0:
                    tasks = RepairTasks.Default;
                    break;

                case 1:
                    tasks = RepairTasks.None;
                    string taskLetters = parameters[0].ToUpper().Trim();

                    foreach (char letter in taskLetters)
                    {
                        if (!TasksDict.ContainsKey(letter))
                            throw new FriendlyMessageException($"Did not recognize task letter '{letter}'. See help.");
                        tasks |= TasksDict[letter];
                    }
                    break;

                default:
                    throw new FriendlyMessageException("Wrong number of parameters. See help.");
            }
        }

        public static void InitAuto()
        {
            if (PersistentData.Instance.RepairAuto && PersistentData.Instance.RepairTasks.HasFlag(RepairTasks.CorruptPowerBlocks))
            {
                Log.Out($"Automatic {(PersistentData.Instance.RepairSimulate ? " scan (without repair)" : "repair")} of server problem(s) " +
                        $"{RepairEngine.GetTaskLetters(PersistentData.Instance.RepairTasks)} is still turned ON.");
                Application.logMessageReceived += OnLogMessageReceived;
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private static void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            // Only check once every 5 seconds
            if (_lastLogMessageTriggeredScan.AddSeconds(5) > DateTime.Now)
                return;

            _lastLogMessageTriggeredScan = DateTime.Now;

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
                        var repairEngine = new RepairEngine
                        {
                            Simulate = PersistentData.Instance.RepairSimulate,
                            Tasks = RepairTasks.CorruptPowerBlocks
                        };
                        repairEngine.Start();

                        if (repairEngine.ProblemsFound >= 1)
                        {
                            PersistentData.Instance.RepairCounter += repairEngine.ProblemsFound;
                            PersistentData.Instance.Save();
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Error while running integrity scan in background: " + ex);
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
