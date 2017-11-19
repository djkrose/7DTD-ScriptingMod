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
    public class EacWhitelist : ConsoleCmdAbstract
    {
        public override string[] GetCommands()
        {
            return new[] { "dj-eac-whitelist" };
        }

        public override string GetDescription()
        {
            return @"Allows players to be exempt from EAC checks on an EAC enabled server.";
        }

        public override string GetHelp()
        {
            // ----------------------------------(max length: 120 char)----------------------------------------------------------------|
            return @"
                This allows adding, removing, and viewing players on the Easy Anti Cheat whitelist. Players who are on the whitelist do
                not need to have EAC enabled when connecting to this server, even when it is EAC-enabled. This allows selected players
                to have modified client, for example allowing admins to have special client-side mods installed.
                Usage:
                    1. dj-eac-whitelist
                    2. dj-eac-whitelist add <name / entity id / steam id>
                    3. dj-eac-whitelist remove <name / entity id / steam id>
                    4. dj-eac-whitelist clear
                1. Lists all players currently on the EAC whitelist.
                2. Adds the given player to the EAC whitelist.
                3. Removes the given player from the EAC whitelist.
                4. Removes all players from the EAC whitelist.
                ".Unindent();
        }

        public override void Execute(List<string> parameters, CommandSenderInfo senderInfo)
        {
            try
            {
                if (parameters.Count == 0)
                {
                    ListWhitelist();
                }
                else if (parameters.Count == 1)
                {
                    if (parameters[0] == "clear")
                    {
                        ClearWhitelist();
                    }
                    else
                    {
                        throw new FriendlyMessageException("Could not understand parameter. See help dj-eac-whitelist.");
                    }
                }
                else if (parameters.Count == 2)
                {
                    // ParseParamPartialNameOrId already sends error message when none or too many users were found
                    if (ConsoleHelper.ParseParamPartialNameOrId(parameters[1], out string steamId, out ClientInfo clientInfo, true) != 1)
                        return;

                    if (parameters[0] == "add")
                    {
                        AddToWhitelist(steamId);
                    }
                    else if (parameters[0] == "remove")
                    {
                        RemoveFromWhitelist(steamId);
                    }
                    else
                    {
                        throw new FriendlyMessageException("Could not understand first parameter. See help dj-eac-whitelist.");
                    }
                }
                else
                {
                    throw new FriendlyMessageException(Resources.ErrorParameerCountNotValid);
                }
            }
            catch (Exception ex)
            {
                CommandTools.HandleCommandException(ex);
            }
        }

        private void ListWhitelist()
        {
            if (PersistentData.Instance.EacWhitelist.Count == 0)
            {
                SdtdConsole.Instance.Output("The EAC whitelist is empty.");
                return;
            }
            PersistentData.Instance.EacWhitelist.Sort();
            SdtdConsole.Instance.Output("Players on the EAC whitelist:\r\n" + PersistentData.Instance.EacWhitelist.Join("\r\n"));
        }

        private void AddToWhitelist(string steamId)
        {
            lock (PersistentData.Instance.EacWhitelist)
            {
                if (PersistentData.Instance.EacWhitelist.Contains(steamId))
                {
                    SdtdConsole.Instance.Output("This player is already on the EAC whitelist.");
                    return;
                }

                PersistentData.Instance.EacWhitelist.Add(steamId);
                PersistentData.Instance.Save();
                if (!GamePrefs.GetBool(EnumGamePrefs.EACEnabled))
                    SdtdConsole.Instance.Output("Warning: EAC is not enabled on this server! Configuration won't have any effect.");
                SdtdConsole.Instance.LogAndOutput($"Player {steamId} was added to the EAC whitelist.");
                SdtdConsole.Instance.LogAndOutput("Note: The 7DTD client still does not allow connecting through the server list. The player must type (or modify) the IP and port (bottom right of server list) directly and then click connect.");
            }
        }

        private void RemoveFromWhitelist(string steamId)
        {
            lock (PersistentData.Instance.EacWhitelist)
            {
                if (!PersistentData.Instance.EacWhitelist.Contains(steamId))
                {
                    SdtdConsole.Instance.Output("This player is not on the EAC whitelist.");
                    return;
                }

                PersistentData.Instance.EacWhitelist.Remove(steamId);
                PersistentData.Instance.Save();
                SdtdConsole.Instance.LogAndOutput($"Player {steamId} was removed from the EAC whitelist.");
            }
        }

        private void ClearWhitelist()
        {
            lock (PersistentData.Instance.EacWhitelist)
            {
                PersistentData.Instance.EacWhitelist.Clear();
                PersistentData.Instance.Save();
                SdtdConsole.Instance.LogAndOutput("Removed all players from the EAC whitelist.");
            }
        }
    }
}
