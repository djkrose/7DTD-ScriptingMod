using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;
using ScriptingMod.Commands;
using ScriptingMod.Exceptions;
using ScriptingMod.Extensions;

namespace ScriptingMod.Managers
{
    public static class CommandManager
    {
        private static FieldInfo _commandObjectsField;                // List<IConsoleCommand> SdtdConsole.TD
        private static FieldInfo _commandObjectPairsField;            // List<SdtdConsole.YU> SdtdConsole.OD
        // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
        private static Type      _commandObjectPairType;              // private struct YU (last in source)
        private static FieldInfo _commandObjectsReadOnlyField;        // ReadOnlyCollection<IConsoleCommand> SdtdConsole.ZD
        private static FieldInfo _commandObjectPair_CommandField;     // string SdtdConsole.YU.LD
        private static ConstructorInfo _commandObjectPairConstructor; // SdtdConsole.YU(string _param1, IConsoleCommand _param2)

        /// <summary>
        /// List of command objects.
        /// </summary>
        private static List<IConsoleCommand> _commandObjects => (List<IConsoleCommand>)_commandObjectsField.GetValue(SdtdConsole.Instance)
            ?? throw new NullReferenceException("Received null value through reflection from _commandObjects.");

        /// <summary>
        /// List of pairs of (command name, command object), one for each command name.
        /// Must use type-unspecific IList here instead of List&lt;something&gt;
        /// </summary>
        private static IList _commandObjectPairs => (IList)_commandObjectPairsField.GetValue(SdtdConsole.Instance)
            ?? throw new NullReferenceException("Received null value through reflection for _commandObjectPairs.");

        static CommandManager()
        {
            // Hard-coded reflection names are only valid for Alpha 16 b135!!!
            try
            {
                _commandObjectsField = typeof(SdtdConsole)
                    .GetField("TD", BindingFlags.NonPublic | BindingFlags.Instance);

                _commandObjectPairsField = typeof(SdtdConsole)
                    .GetField("OD", BindingFlags.NonPublic | BindingFlags.Instance);

                _commandObjectsReadOnlyField = typeof(SdtdConsole)
                    .GetField("ZD", BindingFlags.NonPublic | BindingFlags.Instance);
                if (_commandObjectsReadOnlyField == null)
                    throw new TargetException(
                        "Could not find field through reflection: _commandObjectsReadOnlyField");

                _commandObjectPairType = typeof(SdtdConsole)
                    .GetNestedType("YU", BindingFlags.NonPublic);
                // ReSharper disable once JoinNullCheckWithUsage
                if (_commandObjectPairType == null)
                    throw new TargetException("Could not find type through reflection: commandObjectPairType");

                _commandObjectPairConstructor = _commandObjectPairType.GetConstructor(
                    BindingFlags.Public | BindingFlags.Instance, null,
                    new[] {typeof(string), typeof(IConsoleCommand)}, null);
                if (_commandObjectPairConstructor == null)
                    throw new TargetException(
                        "Could not find constructor through reflection: _commandObjectPairConstructor");

                _commandObjectPair_CommandField = _commandObjectPairType.GetField("LD");
                if (_commandObjectPair_CommandField == null)
                    throw new TargetException("Could not find field through reflection: commandObjectPairType");
            }
            catch (Exception ex)
            {
                Log.Error("Error while establishing references to 7DTD's \"private parts\". Your game version might not be compatible with this Scripting Mod version." + Environment.NewLine + ex);
                throw;
            }

            Log.Debug("Established references to 7DTD's \"private parts\" through reflection.");
        }


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

            if (_commandObjects.Contains(commandObject))
                throw new ArgumentException($"The command object \"{commands.Join(" ")}\" already exists and cannot be registered twice.");

            foreach (string command in commands)
            {
                if (string.IsNullOrEmpty(command))
                    continue;

                if (CommandExists(command))
                    throw new ArgumentException($"The command \"{command}\" already exists and cannot be registered twice.");

                object commandObjectPair = _commandObjectPairConstructor.Invoke(new object[] {command, commandObject});
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
            var index = _commandObjects.BinarySearch(item, _commandObjectComparer);
            if (index < 0) index = ~index;
            _commandObjects.Insert(index, item);
            Log.Debug($"Inserted new command object at index {index} of {_commandObjects.Count-1}.");
        }

        private static CommandObjectPairComparer _commandObjectPairComparer = new CommandObjectPairComparer();

        private class CommandObjectPairComparer : IComparer
        {
            public int Compare(object o1, object o2)
            {
                string s1 = (string)_commandObjectPair_CommandField.GetValue(o1);
                string s2 = (string)_commandObjectPair_CommandField.GetValue(o2);
                return string.Compare(s1, s2, StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Inserts a new CommandObjectPair object in the list at the position sorted by the command name
        /// See: https://stackoverflow.com/a/12172412/785111
        /// </summary>
        /// <param name="item">An object of struct type SdtdConsole.OL</param>
        private static void AddSortedCommandObjectPair(object item)
        {
            var index = Array.BinarySearch(_commandObjectPairs.Cast<object>().ToArray(), item, _commandObjectPairComparer);
            if (index < 0) index = ~index;
            _commandObjectPairs.Insert(index, item);
            Log.Debug($"Inserted new command object pair at index {index} of {_commandObjectPairs.Count-1}.");
        }

        public static void SaveChanges()
        {
            // Update SdtdConsole.ET to be a readonly copy of SdtdConsole.WT
            Log.Debug("Updating readonly copy of command list ...");
            _commandObjectsReadOnlyField.SetValue(SdtdConsole.Instance, new ReadOnlyCollection<IConsoleCommand>(_commandObjects));

            Log.Dump(_commandObjectPairs);

            Log.Debug("Saving changes to commands and permissions to disk ...");
            GameManager.Instance.adminTools.Save();
        }

        public static bool CommandExists(string command)
        {
            return _commandObjectPairs.Cast<object>().Any(o =>
                command.Equals((string)_commandObjectPair_CommandField.GetValue(o), StringComparison.OrdinalIgnoreCase));
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
