using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using ScriptingMod.Exceptions;
using ScriptingMod.Extensions;
using ScriptingMod.Patches;
using ScriptingMod.Tools;

namespace ScriptingMod.Commands
{
    [UsedImplicitly]
    public class Patch : ConsoleCmdAbstract
    {
        public override string[] GetCommands()
        {
            return new[] { "dj-patch" };
        }

        public override string GetDescription()
        {
            return @"Enables or disables runtime server patches.";
        }

        public override string GetHelp()
        {
            // ----------------------------------(max length: 120 char)----------------------------------------------------------------|
            return @"
                Applies runtime patches to the server code in memory without modifying any server files. The on/off status of the patch
                is remembered and automatically reapplied after server restart. The applied patches is listed in server log upon restart.
                Currently supported patches:
                    corpse-dupe   =>  Patches the zombie corpse item dupe exploit listed in the known issues for A16.x
                Usage:
                    1. dj-patch
                    2. dj-patch <patch-name> on
                    3. dj-patch <patch-name> off
                3. Lists the current status (on/off) of all patches.
                2. Enable the named patch.
                3. Disable the named patch.
                Example:
                    dj-patch corpse-dupe on                Patch the zombie corpse item dupe exploit
                ".Unindent();
        }

        public override void Execute(List<string> parameters, CommandSenderInfo senderInfo)
        {
            try
            {
                if (parameters.Count == 0)
                {
                    SdtdConsole.Instance.Output($"Patch for {CorpseDupePatch.PatchName} is {(PersistentData.Instance.PatchCorpseItemDupeExploit ? "ENABLED" : "DISABLED")}.");
                    return;
                }

                if (parameters.Count == 1 || parameters.Count > 2)
                    throw new FriendlyMessageException(Resources.ErrorParameerCountNotValid);

                var patchName = parameters[0];
                string mode = parameters[1];

                if (mode != "on" && mode != "off")
                    throw new FriendlyMessageException($"Wrong second parameter \"{parameters[1]}\". See help.");

                switch (patchName)
                {
                    case "corpse-dupe":
                        if (mode == "on")
                        {
                            if (PersistentData.Instance.PatchCorpseItemDupeExploit)
                                throw new FriendlyMessageException($"Patch for {CorpseDupePatch.PatchName} is already enabled.");
                            PersistentData.Instance.PatchCorpseItemDupeExploit = true;
                            PatchTools.ApplyPatches();
                            PersistentData.Instance.Save(); // save after patching in case something crashes
                            SdtdConsole.Instance.Output($"Patch for {CorpseDupePatch.PatchName} enabled.");
                        }
                        else if (mode == "off")
                        {
                            if (!PersistentData.Instance.PatchCorpseItemDupeExploit)
                                throw new FriendlyMessageException($"Patch for {CorpseDupePatch.PatchName} is already disabled.");
                            PersistentData.Instance.PatchCorpseItemDupeExploit = false;
                            PersistentData.Instance.Save();
                            SdtdConsole.Instance.Output($"Patch for {CorpseDupePatch.PatchName} disabled.");
                        }
                        break;
                    default:
                        throw new FriendlyMessageException($"Unknown patch name \"{patchName}\". See help.");
                }
            }
            catch (Exception ex)
            {
                CommandTools.HandleCommandException(ex);
            }
        }
    }
}
