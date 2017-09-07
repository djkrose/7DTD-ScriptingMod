using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using ScriptingMod.Tools;

namespace ScriptingMod.Commands
{
    internal delegate void DynamicCommandHandler(List<string> parameters, CommandSenderInfo senderInfo);

    /// <summary>
    /// Class used for all script commands; every command file gets one object
    /// </summary>
    public class DynamicCommand : ConsoleCmdAbstract
    {

        private string[] _commands;
        private DynamicCommandHandler _action;
        private string _description;
        private string _help;
        private int _defaultPermissionLevel;

        /// <summary>
        /// Prevent the server from creating a static command from this class.
        /// Will output: WRN Command class DynamicCommand does not contain a parameterless constructor, skipping
        /// </summary>
        [UsedImplicitly]
        private DynamicCommand()
        {
            // never called
        }

        /// <summary>
        /// Creates a dynamic command with the given parameters
        /// </summary>
        internal DynamicCommand(string[] commands, string description, string help, int defaultPermissionLevel, DynamicCommandHandler action)
        {
            _commands = commands;
            _action = action;
            _description = description;
            _help = help;
            _defaultPermissionLevel = defaultPermissionLevel;
        }

        [SuppressMessage("ReSharper", "ConvertToAutoProperty")]
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
                CommandTools.HandleCommandException(ex);
            }
        }
    }
}
