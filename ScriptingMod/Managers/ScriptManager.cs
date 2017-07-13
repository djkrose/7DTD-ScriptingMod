using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ScriptingMod.Extensions;
using ScriptingMod.NativeCommands;
using ScriptingMod.ScriptEngines;

namespace ScriptingMod.Managers
{
    internal static class ScriptManager
    {
        public static void LoadCommands()
        {
            var scripts = Directory.GetFiles(Api.CommandsFolder, "*.*", SearchOption.AllDirectories)
                .Where(s => s.EndsWith(".lua", StringComparison.OrdinalIgnoreCase) ||
                            s.EndsWith(".js", StringComparison.OrdinalIgnoreCase));

            foreach (string script in scripts)
            {
                var filePath = script; // Needed prior C# 5.0 as closure
                var fileName = FileHelper.GetRelativePath(filePath, Api.CommandsFolder);

                Log.Debug($"Loading script \"{fileName}\" ...");

                try
                {
                    var commandObject = CreateCommandObject(filePath);
                    CommandManager.AddCommand(commandObject);
                    Log.Out($"Registered command(s) \"{commandObject.GetCommands().Join(" ")}\" with script \"{fileName}\".");
                }
                catch (Exception ex)
                {
                    Log.Warning($"Could not load command script \"{fileName}\": {ex.Message}");
                    continue;
                }

            }

            Log.Out("All script commands added.");
        }

        private static DynamicCommand CreateCommandObject(string filePath)
        {
            var scriptEngine = ScriptEngine.GetInstance(Path.GetExtension(filePath));

            Dictionary<string, string> metadata = scriptEngine.LoadMetadata(filePath);

            var commands = metadata.GetValue("commands", "").Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);
            var description = metadata.GetValue("description", "");
            var help = metadata.GetValue("help", null);
            int defaultPermissionLevel;
            if (!int.TryParse(metadata.GetValue("defaultPermission"), out defaultPermissionLevel))
                defaultPermissionLevel = 0;

            var action = new Action<List<string>, CommandSenderInfo>(delegate (List<string> paramsList, CommandSenderInfo senderInfo)
            {
                var oldDirectory = Directory.GetCurrentDirectory();
                Directory.SetCurrentDirectory(Path.GetDirectoryName(filePath));
                scriptEngine.SetValue("params", paramsList.ToArray());
                scriptEngine.SetValue("senderInfo", senderInfo);

                try
                {
                    Log.Dump(paramsList.ToArray());
                    Log.Dump(senderInfo);
                    Log.Dump(scriptEngine);

                }
                catch (Exception ex)
                {
                    Log.Exception(ex);
                }

                scriptEngine.ExecuteFile(filePath);
                Directory.SetCurrentDirectory(oldDirectory);
            });

            return new DynamicCommand(commands, action, description, help, defaultPermissionLevel);

        }

    }
}
