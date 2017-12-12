using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using Jint.Parser;
using ScriptingMod.Exceptions;
using ScriptingMod.Extensions;
using ScriptingMod.Patches;
using ScriptingMod.Tools;

namespace ScriptingMod.Commands
{
    [UsedImplicitly]
    public class LogEvents : ConsoleCmdAbstract
    {
        public override string[] GetCommands()
        {
            return new[] { "dj-log-events" };
        }

        public override string GetDescription()
        {
            return @"Enables or disables logging of events to the server console.";
        }

        public override string GetHelp()
        {
            string events = Enum.GetValues(typeof(ScriptEvent)).Cast<ScriptEvent>().Aggregate("", (s, e) => s + e + " ");
            // ----------------------------------(max length: 120 char)----------------------------------------------------------------|
            return (@"
                Allows managing event logging to the server console. By default no events are logged, but logging can be enabled or
                disbaled for each or all supported events individually. Log entries will contain additional event data in JSON format.
                Currently supported events: " + Environment.NewLine + events.Wrap(115).Indent(20) + @"
                Usage:
                    1. dj-log-events
                    2. dj-log-events <event(s)> </on|/off>
                    3. dj-log-events all </on|/off>
                1. Lists all currently logged events.
                2. Enables or disables logging for the given events. Example: dj-log-events playerDied playerLevelUp /on
                3. Enables or disables logging for ALL events. Example: dj-log-events all /off
                ").Unindent();
        }

        public override void Execute(List<string> parameters, CommandSenderInfo senderInfo)
        {
            TelemetryTools.CollectEvent("command", "execute", GetCommands()[0]);
            try
            {
                if (parameters.Count == 0)
                {
                    ListStatus();
                    return;
                }

                bool isModeOn = parameters.Remove("/on");
                bool isModeOff = parameters.Remove("/off");

                if (parameters.Count == 0)
                    throw new FriendlyMessageException("No event names given. See help.");
                if (isModeOn && isModeOff)
                    throw new FriendlyMessageException("Parameters /on and /off cannot be used simultaneously.");
                if (!isModeOn && !isModeOff)
                    throw new FriendlyMessageException("Neither parameter /on nor /off was specified. See help.");

                if (parameters.Contains("all"))
                {
                    if (parameters.Count > 1)
                        throw new FriendlyMessageException("When using the \"all\" keyword as event name you can't use other event names additionally.");
                    UpdateAllEvents(isModeOn);
                }
                else
                {
                    UpdateLogEvents(parameters, isModeOn);
                }
            }
            catch (Exception ex)
            {
                CommandTools.HandleCommandException(ex);
            }
        }

        private void UpdateAllEvents(bool isModeOn)
        {
            PersistentData.Instance.LogEvents = isModeOn 
                ? new HashSet<ScriptEvent>(Enum.GetValues(typeof(ScriptEvent)).Cast<ScriptEvent>()) 
                : new HashSet<ScriptEvent>();
            PersistentData.Instance.Save();
            PatchTools.ApplyPatches();
            SdtdConsole.Instance.Output($"Logging for all events {(isModeOn ? "enabled" : "disabled")}.");
        }

        private static void UpdateLogEvents(List<string> parameters, bool isModeOn)
        {
            // Parse parameters into valid and invalid events
            var validEvents = new List<ScriptEvent>();
            var invalidEventNames = new List<string>();
            foreach (var eventName in parameters)
            {
                if (EnumHelper.TryParse<ScriptEvent>(eventName, out var evt, true))
                    validEvents.Add(evt);
                else
                    invalidEventNames.Add(eventName);
            }

            if (invalidEventNames.Count > 0)
                throw new FriendlyMessageException($"The {(invalidEventNames.Count == 1 ? "event name is" : "following event names are")} invalid: " + invalidEventNames.Join(" "));

            // Add/remove valid events
            if (isModeOn)
            {
                PersistentData.Instance.LogEvents.UnionWith(validEvents);
                SdtdConsole.Instance.Output($"Logging for the given event{(validEvents.Count == 1 ? "" : "s")} was enabled.");
            }
            else
            {
                PersistentData.Instance.LogEvents.ExceptWith(validEvents);
                SdtdConsole.Instance.Output($"Logging for the given event{(validEvents.Count == 1 ? "" : "s")} was disabled.");
            }
            PersistentData.Instance.Save();
            PatchTools.ApplyPatches();
        }

        private void ListStatus()
        {
            if (PersistentData.Instance.LogEvents.Count == 0)
            {
                SdtdConsole.Instance.Output("Logging is not enabled for any event.");
                return;
            }
            string events = PersistentData.Instance.LogEvents.Aggregate("", (s, e) => s + Environment.NewLine + e);
            SdtdConsole.Instance.Output("Logging is enabled for these events: " + events);
        }
    }
}
