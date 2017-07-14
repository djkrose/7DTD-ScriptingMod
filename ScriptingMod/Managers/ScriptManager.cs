using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NLua.Exceptions;
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
            var fileName     = FileHelper.GetRelativePath(filePath, Api.CommandsFolder);
            var scriptEngine = ScriptEngine.GetInstance(Path.GetExtension(filePath));

            var action = new Action<List<string>, CommandSenderInfo>(delegate (List<string> paramsList, CommandSenderInfo senderInfo)
            {
                var oldDirectory = Directory.GetCurrentDirectory();
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

            var metadata         = scriptEngine.LoadMetadata(filePath);
            var commands         = metadata.GetValue("commands", "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var description      = metadata.GetValue("description", "");
            var help             = metadata.GetValue("help", null);
            int defaultPermision = metadata.GetValue("defaultPermission").ToInt() ?? 0;

            return new DynamicCommand(commands, action, description, help, defaultPermision);

        }

    }
}
