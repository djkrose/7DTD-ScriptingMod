using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using ScriptingMod.Exceptions;
using ScriptingMod.Extensions;
using ScriptingMod.Tools;

namespace ScriptingMod.Commands
{
    [UsedImplicitly]
    public class Repair : ConsoleCmdAbstract
    {

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

                var repairEngine = new RepairEngine(tasks, simulate);
                repairEngine.ConsoleOutput = SdtdConsole.Instance.Output;

                if (auto)
                {
                    if (!PersistentData.Instance.RepairAuto)
                        repairEngine.AutoOn();
                    else
                        repairEngine.AutoOff();
                }
                else
                { 
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
    }
}
