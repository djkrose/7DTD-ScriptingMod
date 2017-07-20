using System;
using System.Linq;
using System.Collections.Generic;
using ScriptingMod.Managers;

namespace ScriptingMod.Commands
{
    /// <summary>
    /// Class used for all script commands; every command file gets one object
    /// </summary>
    public class DynamicCommand : ConsoleCmdAbstract
    {
        private string[] _commands;
        private Action<List<string>, CommandSenderInfo> _action;
        private string _description;
        private string _help;
        private int _defaultPermissionLevel;

        /// <summary>
        /// Prevent the server from creating a static command from this class.
        /// Will output: WRN Command class DynamicCommand does not contain a parameterless constructor, skipping
        /// </summary>
        private DynamicCommand()
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
        public DynamicCommand(string[] commands, Action<List<string>, CommandSenderInfo> action, string description, string help = null, int defaultPermissionLevel = 0)
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
            try
            {
                _action(_params, _senderInfo);

            }
            catch (Exception ex)
            {
                CommandManager.HandleCommandException(ex);
            }
        }
    }
}
