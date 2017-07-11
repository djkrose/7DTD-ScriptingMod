using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ScriptingMod.NativeCommands;
using ScriptingMod.ScriptEngines;
using ScriptingMod.Extensions;

namespace ScriptingMod.Managers
{
    public static class CommandManager
    {
        private static FieldInfo _commandObjectsField;                // List<IConsoleCommand> SdtdConsole.WT
        private static FieldInfo _commandObjectPairsField;            // List<SdtdConsole.OL> SdtdConsole.PT
        private static FieldInfo _commandObjectsReadOnlyField;        // ReadOnlyCollection<IConsoleCommand> SdtdConsole.ET
        private static FieldInfo _commandObjectPair_CommandField;     // string SdtdConsole.OL.JT
        private static ConstructorInfo _commandObjectPairConstructor; // SdtdConsole.OL(string _param1, IConsoleCommand _param2)

        /// <summary>
        /// List of command objects.
        /// Reference to: List&lt;IConsoleCommand&gt; SdtdConsole.WT
        /// </summary>
        private static List<IConsoleCommand> _commandObjects => (List<IConsoleCommand>)_commandObjectsField.GetValue(SdtdConsole.Instance)
            ?? throw new NullReferenceException("Received null value through reflection from SdtdConsole.WT");

        /// <summary>
        /// List of pairs of (command name, command object), one for each command name.
        /// Reference to: List&lt;SdtdConsole.OL&gt; SdtdConsole.PT
        /// Must use type-unspecific IList here instead of List&lt;something&gt;
        /// </summary>
        private static IList _commandObjectPairs => (IList)_commandObjectPairsField.GetValue(SdtdConsole.Instance)
            ?? throw new NullReferenceException("Received null value through reflection from SdtdConsole.PT");

        static CommandManager()
        {
            _commandObjectsField = typeof(SdtdConsole)
                .GetField("WT", BindingFlags.NonPublic | BindingFlags.Instance);

            _commandObjectPairsField = typeof(SdtdConsole)
                .GetField("PT", BindingFlags.NonPublic | BindingFlags.Instance);

            _commandObjectsReadOnlyField = typeof(SdtdConsole)
                .GetField("ET", BindingFlags.NonPublic | BindingFlags.Instance);
            if (_commandObjectsReadOnlyField == null)
                throw new TargetException("Could not find field through reflection: ReadOnlyCollection<IConsoleCommand> SdtdConsole.ET");

            var commandObjectPairType = typeof(SdtdConsole)
                .GetNestedType("OL", BindingFlags.NonPublic);
            if (commandObjectPairType == null)
                throw new TargetException("Could not find type through reflection: struct SdtdConsole.OL");

            _commandObjectPairConstructor = commandObjectPairType.GetConstructor(
                BindingFlags.Public | BindingFlags.Instance, null,
                new[] { typeof(string), typeof(IConsoleCommand) }, null);
            if (_commandObjectPairConstructor == null)
                throw new TargetException("Could not find constructor through reflection: struct SdtdConsole.OL(string _param1, IConsoleCommand _param2)");

            _commandObjectPair_CommandField = commandObjectPairType.GetField("JT");
            if (_commandObjectPair_CommandField == null)
                throw new TargetException("Could not find field through reflection: string SdtdConsole.OL.JT");

            Log.Debug("Established references to 7DTD's \"private parts\" through reflection.");
        }

        public static void LoadDynamicCommands()
        {
            var scripts = Directory.GetFiles(Api.CommandsFolder, "*.*", SearchOption.AllDirectories)
                .Where(s => s.EndsWith(".lua", StringComparison.OrdinalIgnoreCase) ||
                            s.EndsWith(".js", StringComparison.OrdinalIgnoreCase));

            foreach (string script in scripts)
            {
                var filePath = script; // Needed prior C# 5.0 as closure
                var fileName = FileHelper.GetRelativePath(filePath, Api.CommandsFolder);

                Log.Debug($"Loading script \"{fileName}\" ...");

                // TODO: Load stuff from script metadata
                var commands = new string[] { Path.GetFileNameWithoutExtension(filePath) };
                var description = $"Description for command {commands[0]}.";
                var help = $"This executes the script {fileName} using djkrose's Scripting Mod.";
                var defaultPermissionLevel = 0;

                var scriptEngine = ScriptEngine.GetInstance(Path.GetExtension(filePath));
                var action = new Action<List<string>, CommandSenderInfo>(delegate (List<string> paramsList, CommandSenderInfo senderInfo)
                {
                    scriptEngine.SetValue("params", paramsList.ToArray());
                    scriptEngine.SetValue("senderInfo", senderInfo);
                    scriptEngine.ExecuteFile(filePath);
                });

                var commandObject = new DynamicCommand(commands, action, description, help, defaultPermissionLevel);
                try
                {
                    AddCommand(commandObject);
                }
                catch (Exception ex)
                {
                    Log.Warning($"Could not register command script \"{fileName}\": {ex.Message}");
                    continue;
                }

                Log.Out($"Registered command(s) \"{commands.Join(" ")}\" with script \"{fileName}\".");
            }

            Log.Out("All script commands added.");
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
            var commands = commandObject.GetCommands();

            if (_commandObjects.Contains(commandObject))
                throw new ArgumentException($"The object for command(s) \"{commands.Join(" ")}\" is already registered and cannot be registered twice.");

            foreach (string command in commands)
            {
                if (string.IsNullOrEmpty(command))
                    continue;

                if (CommandExists(command))
                    throw new ArgumentException($"The command \"{command}\" is already registered elsewhere and cannot be registered twice.");

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

    }
}
