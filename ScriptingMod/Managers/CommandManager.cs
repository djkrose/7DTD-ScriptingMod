using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ScriptingMod.NativeCommands;
using ScriptingMod.ScriptEngines;
using ScriptingMod.Extensions;

namespace ScriptingMod.Managers
{
    public class CommandManager
    {
        private static CommandManager _instance;
        public static CommandManager Instance => _instance ?? (_instance = new CommandManager());

        /// <summary>
        /// FieldInfo for shallow readonly copy of command objects; should always match SdtdConsole.WT.
        /// Reference to field: ReadOnlyCollection&lt;IConsoleCommand&gt; SdtdConsole.ET
        /// </summary>
        private static FieldInfo _commandObjectsReadOnlyField;

        /// <summary>
        /// Struct constructor: SdtdConsole.OL(string _param1, IConsoleCommand _param2)
        /// </summary>
        private static ConstructorInfo _commandObjectPairConstructor;

        /// <summary>
        /// Struct field: string SdtdConsole.OL.JT
        /// </summary>
        private static FieldInfo _commandObjectPair_CommandField;

        ///// <summary>
        ///// Struct field IConsoleCommand SdtdConsole.OL.TT
        ///// </summary>
        //private static FieldInfo _commandObjectPair_CommandObjectField;

        /// <summary>
        /// List of command objects.
        /// Reference to: List&lt;IConsoleCommand&gt; SdtdConsole.WT
        /// </summary>
        // ReSharper disable once PossibleNullReferenceException
        private List<IConsoleCommand> _commandObjects;

        /// <summary>
        /// List of pairs of (command name, command object), one for each command name.
        /// Reference to: List&lt;SdtdConsole.OL&gt; SdtdConsole.PT
        /// Must use type-unspecific IList here instead of List&lt;something&gt;
        /// </summary>
        private IList _commandObjectPairs;

        static CommandManager()
        {
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

            //_commandObjectPair_CommandObjectField = commandObjectPairType.GetField("TT");
            //if (_commandObjectPair_CommandObjectField == null)
            //    throw new TargetException("Could not find field through reflection: IConsoleCommand SdtdConsole.OL.TT");

            Log.Debug("Established references to 7DTD's \"private parts\" through reflection.");
        }

        public CommandManager()
        {
            // ReSharper disable PossibleNullReferenceException
            _commandObjects = (List<IConsoleCommand>)typeof(SdtdConsole)
                .GetField("WT", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(SdtdConsole.Instance);
            if (_commandObjects == null)
                throw new NullReferenceException("Received null value through reflection from SdtdConsole.WT");

            _commandObjectPairs = (IList)typeof(SdtdConsole)
                .GetField("PT", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(SdtdConsole.Instance);
            if (_commandObjectPairs == null)
                throw new NullReferenceException("Received null value through reflection from SdtdConsole.PT");
            // ReSharper restore PossibleNullReferenceException

            Log.Debug("Established references to private command lists through reflection.");
        }

        public void LoadDynamicCommands()
        {
            var scripts = Directory.GetFiles(Api.CommandsFolder, "*.*", SearchOption.AllDirectories)
                .Where(s => s.EndsWith(".lua", StringComparison.OrdinalIgnoreCase) ||
                            s.EndsWith(".js", StringComparison.OrdinalIgnoreCase));

            foreach (string script in scripts)
            {
                var filePath = script; // Needed prior C# 5.0 as closure
                var fileName = FileHelper.GetRelativePath(filePath, Api.CommandsFolder);

                Log.Debug($"Loading script \"{fileName}\" ...");

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
                AddCommand(commandObject);

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
        public void AddCommand(DynamicCommand commandObject)
        {
            var commands = commandObject.GetCommands();

            if (_commandObjects.Contains(commandObject))
                throw new ArgumentException($"The object for command(s) \"{commands.Join(" ")}\" is already registered and cannot be registered twice.");

            foreach (string command in commands)
            {
                if (string.IsNullOrEmpty(command))
                    continue;

                if (IsCommandRegistered(command))
                    throw new ArgumentException($"The command \"{command}\" is already registered elsewhere and cannot be registered twice.");

                object commandObjectPair = _commandObjectPairConstructor.Invoke(new object[] {command, commandObject});
                AddSortedCommandObjectPair(commandObjectPair);
            }

            _commandObjects.Add(commandObject);
        }

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
        /// </summary>
        /// <param name="item">An object of struct type SdtdConsole.OL</param>
        private void AddSortedCommandObjectPair(object item)
        {
            var index = Array.BinarySearch(_commandObjectPairs.Cast<object>().ToArray(), item, new CommandObjectPairComparer());
            if (index < 0) index = ~index;
            _commandObjectPairs.Insert(index, item);
            Log.Debug("Inserted new object at index " + index + " of " + _commandObjectPairs.Count);
        }

        public void SaveChanges()
        {
            // Update SdtdConsole.ET to be a readonly copy of SdtdConsole.WT
            Log.Debug("Updating readonly copy of command list ...");
            _commandObjectsReadOnlyField.SetValue(SdtdConsole.Instance, new ReadOnlyCollection<IConsoleCommand>(_commandObjects));

            Log.Debug("Saving changes to commands and permissions to disk ...");
            GameManager.Instance.adminTools.Save();
        }

        public bool IsCommandRegistered(string command)
        {
            return _commandObjectPairs.Cast<object>().Any(o =>
                command.Equals((string)_commandObjectPair_CommandField.GetValue(o), StringComparison.OrdinalIgnoreCase));
        }

    }
}
