using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using ScriptingMod.Tools;
using UnityEngine;

namespace ScriptingMod.Commands
{
    [UsedImplicitly]
    public class Version : ConsoleCmdAbstract
    {

        public override string[] GetCommands()
        {
            return new string[] {"dj-version"};
        }

        public override string GetDescription()
        {
            return "Shows djkrose's Scripting Mod version information.";
        }

        public override string GetHelp()
        {
            return @"Tests if djkrose's Scripting Mod is correctly installed and prints out version information.";
        }

        public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
        {
            try
            {
                var modInfo = ModManager.GetMod(Constants.ModNameFull)?.ModInfo;
                if (modInfo == null)
                {
                    SdtdConsole.Instance.Output(Constants.ModNameFull);
                    SdtdConsole.Instance.Output("Could not load mod infos. Have you modified the ModInfo.xml?");
                }
                else
                {
                    // ReSharper disable once UnreachableCode
                    SdtdConsole.Instance.Output(Constants.ModNameFull + " - Version " + modInfo.Version.Value + (Log.IsDebug ? " DEBUG" : ""));
                    SdtdConsole.Instance.Output(modInfo.Description.Value);
                    if (!string.IsNullOrEmpty(modInfo.Website.Value))
                        SdtdConsole.Instance.Output("Website: " + modInfo.Website.Value);
                }

                SdtdConsole.Instance.Output("");

                SdtdConsole.Instance.Output("Operating System: " + Environment.OSVersion);
                SdtdConsole.Instance.Output("Unity version: " + Application.unityVersion);

                var displayName = Type.GetType("Mono.Runtime")?.GetMethod("GetDisplayName", BindingFlags.NonPublic | BindingFlags.Static);
                if (displayName != null)
                    SdtdConsole.Instance.Output("Mono display name: " + displayName.Invoke(null, null));

                var monoRuntimeVersion = Type.GetType("Mono.Runtime")?.Assembly.ImageRuntimeVersion;
                if (monoRuntimeVersion != null)
                    SdtdConsole.Instance.Output("Mono runtime version: " + monoRuntimeVersion);
            }
            catch (Exception ex)
            {
                CommandTools.HandleCommandException(ex);
            }
        }
    }
}
