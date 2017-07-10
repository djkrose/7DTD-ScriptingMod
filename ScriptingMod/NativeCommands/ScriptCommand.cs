using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ScriptingMod.NativeCommands
{
    /// <summary>
    /// Class used for all script commands; every command file gets one object
    /// </summary>
    public class ScriptCommand : ConsoleCmdAbstract
    {
        private readonly string[] _commands;
        private readonly Action<List<string>, CommandSenderInfo> _action;
        private readonly string _description;
        private readonly string _help;
        private readonly int _defaultPermissionLevel;

        /// <summary>
        /// Prevent the server from creating a static command from this class.
        /// Will output: WRN Command class ScriptCommand does not contain a parameterless constructor, skipping
        /// </summary>
        private ScriptCommand()
        {
            // never called
        }

        /// <summary>
        /// Creates a dynamic command with the given parameters
        /// </summary>
        /// <param name="commands"></param>
        /// <param name="action"></param>
        /// <param name="description"></param>
        /// <param name="help"></param>
        /// <param name="defaultPermissionLevel"></param>
        public ScriptCommand(string[] commands, Action<List<string>, CommandSenderInfo> action, string description, string help = null, int defaultPermissionLevel = 0)
        {
            _commands = commands;
            _action = action;
            _description = description;
            _help = help;
            _defaultPermissionLevel = defaultPermissionLevel;
        }

        public override int DefaultPermissionLevel
        {
            get
            {
                return _defaultPermissionLevel;
            }
        }

        public override string[] GetCommands()
        {
            return _commands;
        }

        public override string GetDescription()
        {
            return _description;
        }

        public override string GetHelp()
        {
            return _help;
        }

        public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
        {
            _action(_params, _senderInfo);
        }
    }
}
