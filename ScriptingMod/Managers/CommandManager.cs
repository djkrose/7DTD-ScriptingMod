using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;
using JetBrains.Annotations;
using ScriptingMod.Commands;
using ScriptingMod.Exceptions;
using ScriptingMod.Extensions;

namespace ScriptingMod.Managers
{

    internal static class CommandManager
    {
        private static FieldInfo _commandObjectsField;                 // List<IConsoleCommand> SdtdConsole.TD
        private static FieldInfo _commandObjectPairsField;             // List<SdtdConsole.YU> SdtdConsole.OD
                                                                       // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
        private static Type      _commandObjectPairType;               // private struct YU (last in source)
        private static FieldInfo _commandObjectsReadOnlyField;         // ReadOnlyCollection<IConsoleCommand> SdtdConsole.ZD
        private static FieldInfo _commandObjectPair_CommandField;      // string SdtdConsole.YU.LD
        private static ConstructorInfo _commandObjectPair_Constructor; // SdtdConsole.YU(string _param1, IConsoleCommand _param2)

        /// <summary>
        /// List of command objects.
        /// </summary>
        [NotNull]
        private static List<IConsoleCommand> _commandObjects => (List<IConsoleCommand>)_commandObjectsField.GetValue(SdtdConsole.Instance)
            ?? throw new NullReferenceException("Received null value through reflection from _commandObjects.");

        /// <summary>
        /// List of pairs of (command name, command object), one for each command name.
        /// Must use type-unspecific IList here instead of List&lt;something&gt;
        /// </summary>
        [NotNull]
        private static IList _commandObjectPairs => (IList)_commandObjectPairsField.GetValue(SdtdConsole.Instance)
            ?? throw new NullReferenceException("Received null value through reflection for _commandObjectPairs.");

        static CommandManager()
        {
            try
            {
                // Get references to private fields/methods/types by their signatures,
                // because the internal names change on every 7DTD release due to obfuscation.

                // One way to do it:
                //_commandObjectsField = typeof(SdtdConsole).GetMemberByPattern(@"^System\.Collections\.Generic\.List`1\[IConsoleCommand\] [a-zA-Z0-9_]+$").First() as FieldInfo;
                //if (_commandObjectsField == null)
                //    throw new TargetException(
                //        "Could not find field through reflection: _commandObjectsField");
                //Log.Dump(_commandObjectsField);

                // Another way:
                Log.Debug("Getting private field from SdtdConsole: private List<IConsoleCommand> TD ...");
                _commandObjectsField = typeof(SdtdConsole)
                    .GetFieldsByType(typeof(List<IConsoleCommand>))
                    .Single();

                // Old way:
                //_commandObjectsField = typeof(SdtdConsole)
                //    .GetField("TD", BindingFlags.NonPublic | BindingFlags.Instance);

                // Example for generic types
                //Log.Debug("Getting private field from SdtdConsole: private List<SdtdConsole.YU> OD ...");
                //Type generic = typeof(List<>);
                //Log.Dump(generic);
                //Type constructed = generic.MakeGenericType(new Type[] {typeof(string)});
                //Log.Dump(constructed);

                Log.Debug($"Getting private nested struct YU from {typeof(SdtdConsole)} that contains field: public IConsoleCommand DD ...");
                _commandObjectPairType = typeof(SdtdConsole)
                    .GetNestedTypesByContainingField(typeof(IConsoleCommand))
                    .Single();

                Log.Debug($"Getting private field from {typeof(SdtdConsole)}: private List<SdtdConsole.YU> OD ...");
                _commandObjectPairsField = typeof(SdtdConsole)
                    .GetFieldsByType(typeof(List<>).MakeGenericType(_commandObjectPairType))
                    .Single();

                //_commandObjectPairsField = typeof(SdtdConsole)
                //    .GetField("OD", BindingFlags.NonPublic | BindingFlags.Instance);

                Log.Debug($"Getting private field from {typeof(SdtdConsole)}: private ReadOnlyCollection<IConsoleCommand> ZD ...");
                _commandObjectsReadOnlyField = typeof(SdtdConsole)
                    .GetFieldsByType(typeof(ReadOnlyCollection<IConsoleCommand>))
                    .Single();

                //_commandObjectPairType = typeof(SdtdConsole)
                //    .GetNestedType("YU", BindingFlags.NonPublic);
                //// ReSharper disable once JoinNullCheckWithUsage
                //if (_commandObjectPairType == null)
                //    throw new TargetException("Could not find type through reflection: commandObjectPairType");

                Log.Debug($"Getting constructor of {_commandObjectPairType} ...");
                _commandObjectPair_Constructor = _commandObjectPairType.GetConstructor(
                    BindingFlags.Public | BindingFlags.Instance, null,
                    new[] {typeof(string), typeof(IConsoleCommand)}, null);
                if (_commandObjectPair_Constructor == null)
                    throw new TargetException($"Could not find constructor of {_commandObjectPairType}.");

                Log.Debug($"Getting private field from {_commandObjectPairType}: public string LD ...");
                _commandObjectPair_CommandField = _commandObjectPairType
                    .GetFieldsByType(typeof(string))
                    .Single();
            }
            catch (Exception ex)
            {
                Log.Error("Error while establishing references to 7DTD's \"private parts\". Your game version might not be compatible with this Scripting Mod version." + Environment.NewLine + ex);
                throw;
            }

            Log.Debug(typeof(CommandManager) + " established reflection references.");
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

                object commandObjectPair = _commandObjectPair_Constructor.Invoke(new object[] {command, commandObject});
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
