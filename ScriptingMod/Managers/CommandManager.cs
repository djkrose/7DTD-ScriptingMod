using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using ScriptingMod.Commands;
using ScriptingMod.Exceptions;
using ScriptingMod.Extensions;
using CommandObjectPair = ScriptingMod.Extensions.NonPublic.SdtdConsole.CommandObjectPair;

namespace ScriptingMod.Managers
{

    internal static class CommandManager
    {

        private static readonly CommandObjectPairComparer _commandObjectPairComparer = new CommandObjectPairComparer();

        /// <summary>
        /// Registers the given command object with it's command names into the Console.
        /// The command object or command names must not already exist in the console.
        /// To make all command changes persistent, SaveChanges() must be called afterwards.
        /// Adapted from: SdtdConsole.RegisterCommands
        /// </summary>
        /// <param name="commandObject"></param>
        public static void AddCommand(DynamicCommand commandObject)
        {
            if (commandObject == null)
                throw new ArgumentNullException(nameof(commandObject));

            var commands = commandObject.GetCommands();

            if (commands == null || commands.Length == 0 || commands.All(string.IsNullOrEmpty))
                throw new ArgumentException("No command name(s) defined.");

            if (SdtdConsole.Instance.GetCommandObjects().Contains(commandObject))
                throw new ArgumentException($"The command object \"{commands.Join(" ")}\" already exists and cannot be registered twice.");

            foreach (string command in commands)
            {
                if (string.IsNullOrEmpty(command))
                    continue;

                if (CommandExists(command))
                    throw new ArgumentException($"The command \"{command}\" already exists and cannot be registered twice.");

                var commandObjectPair = new NonPublic.SdtdConsole.CommandObjectPair(command, commandObject);
                AddSortedCommandObjectPair(commandObjectPair);
            }

            AddCommandObjectSorted(commandObject);
        }

        private static CommandObjectComparer _commandObjectComparer = new CommandObjectComparer();

        private class CommandObjectComparer : IComparer<IConsoleCommand>
        {
            [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
            public int Compare(IConsoleCommand o1, IConsoleCommand o2)
            {
                return string.Compare(o1.GetCommands()[0], o2.GetCommands()[0], StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Inserts a new CommandObject in the list at the position sorted by the first command name.
        /// See: https://stackoverflow.com/a/12172412/785111
        /// </summary>
        /// <param name="item"></param>
        private static void AddCommandObjectSorted(IConsoleCommand item)
        {
            var commandObjects = SdtdConsole.Instance.GetCommandObjects();
            var index = commandObjects.BinarySearch(item, _commandObjectComparer);
            if (index < 0) index = ~index;
            commandObjects.Insert(index, item);
            Log.Debug($"Inserted new command object at index {index} of {commandObjects.Count-1}.");
        }

        private class CommandObjectPairComparer : IComparer<CommandObjectPair>
        {
            public int Compare(CommandObjectPair o1, CommandObjectPair o2)
            {
                return string.Compare(o1.Command, o2.Command, StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Inserts a new CommandObjectPair object in the list at the position sorted by the command name
        /// See: https://stackoverflow.com/a/12172412/785111
        /// </summary>
        /// <param name="item">An object of struct type SdtdConsole.OL</param>
        private static void AddSortedCommandObjectPair(CommandObjectPair item)
        {
            var commandObjectPairs = SdtdConsole.Instance.GetCommandObjectPairs();
            var index = Array.BinarySearch(commandObjectPairs.ToArray(), item, _commandObjectPairComparer);
            if (index < 0) index = ~index;
            commandObjectPairs.Insert(index, item);
            Log.Debug($"Inserted new command object pair at index {index} of {commandObjectPairs.Count-1}.");
        }

        // TODO: Why is this never called???
        public static void SaveChanges()
        {
            Log.Debug("Updating readonly copy of command list ...");
            SdtdConsole.Instance.SetCommandObjectsReadOnly(new ReadOnlyCollection<IConsoleCommand>(SdtdConsole.Instance.GetCommandObjects()));
            Log.Debug("Saving changes to commands and permissions to disk ...");
            GameManager.Instance.adminTools.Save();
        }

        private static bool CommandExists(string command)
        {
            return SdtdConsole.Instance.GetCommandObjectPairs().Any(pair => command.Equals(pair.Command, StringComparison.OrdinalIgnoreCase));
        }

        public static void HandleCommandException(Exception ex)
        {
            if (ex is FriendlyMessageException)
            {
                SdtdConsole.Instance.Output(ex.Message);
                Log.Out(ex.Message);
            }
            else
            {
                SdtdConsole.Instance.Output("Error occured during command execution: " + ex.Message + " [details in server log]");
                Log.Exception(ex);
            }
        }

    }
}
