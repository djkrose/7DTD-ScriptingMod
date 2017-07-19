using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using ScriptingMod.Commands;
using ScriptingMod.Extensions;
using ScriptingMod.ScriptEngines;

namespace ScriptingMod.Managers
{
    internal static class ScriptManager
    {
        public static void LoadCommands()
        {
            var scripts = Directory.GetFiles(Constants.ScriptsFolder, "*.*", SearchOption.AllDirectories)
                .Where(s => s.EndsWith(".lua", StringComparison.OrdinalIgnoreCase) ||
                            s.EndsWith(".js", StringComparison.OrdinalIgnoreCase));

            foreach (string script in scripts)
            {
                var filePath = script; // Needed prior C# 5.0 as closure
                var fileName = FileHelper.GetRelativePath(filePath, Constants.ScriptsFolder);

                Log.Debug($"Loading script \"{fileName}\" ...");

                try
                {
                    var commandObject = CreateCommandObject(filePath);
                    if (commandObject == null)
                    {
                        Log.Debug($"Script file {fileName} is ignored becaus it does not contain a command name definition.");
                        continue;
                    }

                    CommandManager.AddCommand(commandObject);
                    Log.Out($"Registered command(s) \"{commandObject.GetCommands().Join(" ")}\" in script \"{fileName}\".");
                }
                catch (Exception ex)
                {
                    Log.Warning($"Could not load command script \"{fileName}\": {ex.Message}");
                    Log.Debug(ex.ToString());
                    continue;
                }

            }

            Log.Out("All script commands added.");
        }

        /// <summary>
        /// Parses the file as command script and tries to create a command object from it.
        /// </summary>
        /// <param name="filePath">Full path of the file to parse.</param>
        /// <returns>The new command object, or null if the script has no command name in metadata and therefore is not a command script.</returns>
        [CanBeNull]
        private static DynamicCommand CreateCommandObject(string filePath)
        {
            var fileName     = FileHelper.GetRelativePath(filePath, Constants.ScriptsFolder);
            var scriptEngine = ScriptEngine.GetInstance(Path.GetExtension(filePath));

            var metadata         = scriptEngine.LoadMetadata(filePath);
            // ReSharper disable once PossibleNullReferenceException
            var commands         = metadata.GetValue("commands", "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var description      = metadata.GetValue("description", "");
            var help             = metadata.GetValue("help", null);
            int defaultPermision = metadata.GetValue("defaultPermission").ToInt() ?? 0;

            // Skip files that have no command name defined and therefore are not commands but helper scripts.
            if (commands.Length == 0)
                return null;

            var action = new Action<List<string>, CommandSenderInfo>(delegate (List<string> paramsList, CommandSenderInfo senderInfo)
            {
                var oldDirectory = Directory.GetCurrentDirectory();
                // ReSharper disable once AssignNullToNotNullAttribute
                Directory.SetCurrentDirectory(Path.GetDirectoryName(filePath));

                scriptEngine.SetValue("params", paramsList.ToArray());

                try
                {
                    scriptEngine.ExecuteFile(filePath);
                }
                // LuaScriptException is already handled in LuaEngine
                catch (Exception ex)
                {
                    SdtdConsole.Instance.Output($"Script {fileName} failed: " + ex.GetType().FullName + ": " + ex.Message + " [details in server log]");
                    Log.Error($"Script {fileName} failed: " + ex);
                }

                Directory.SetCurrentDirectory(oldDirectory);
            });

            return new DynamicCommand(commands, action, description, help, defaultPermision);

        }

    }
}
