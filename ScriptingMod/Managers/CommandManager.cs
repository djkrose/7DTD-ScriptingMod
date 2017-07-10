using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ScriptingMod.NativeCommands;
using ScriptingMod.ScriptEngines;

namespace ScriptingMod.Managers
{
    internal static class CommandManager
    {
        public static void LoadDynamicCommands()
        {
            Log.Out("Registering script commands ...");
            var scripts = Directory.GetFiles(Api.CommandsFolder, "*.*", SearchOption.AllDirectories)
                .Where(s => s.EndsWith(".lua", StringComparison.OrdinalIgnoreCase) ||
                            s.EndsWith(".js", StringComparison.OrdinalIgnoreCase));

            foreach (string script in scripts)
            {
                var filePath = script; // Needed prior C# 5.0 as closure

                var fileName = GetRelativePath(filePath, Api.CommandsFolder);
                var commands = new string[] { Path.GetFileNameWithoutExtension(filePath) };
                var description = $"Description for command {commands[0]}.";
                var help = $"This executes the script {fileName} using djkrose's Scripting Mod.";
                var defaultPermissionLevel = 0;

                var scriptEngine = ScriptEngine.GetInstance(Path.GetExtension(filePath));
                var action = new Action<List<string>, CommandSenderInfo>(delegate (List<string> paramsList, CommandSenderInfo senderInfo)
                {
                    scriptEngine.SetValue("params", paramsList.ToArray());
                    scriptEngine.SetValue("senderInfo", senderInfo);
                    scriptEngine.ExecuteFile(filePath);
                });

                var scriptCommand = new DynamicCommand(commands, action, description, help, defaultPermissionLevel);

                Log.Out($"Registered command(s) \"{string.Join(" ", commands)}\" with script \"{fileName}\".");

                // TODO: Register scriptCommand using nasty reflection hacks in 7DTD server
            }

            Log.Out("All scripts loaded and ready.");

        }

        /// <summary>
        /// Makes the given filePath relative to the given folder
        /// Source: https://stackoverflow.com/a/703292/785111
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="folder"></param>
        /// <returns></returns>
        private static string GetRelativePath(string filePath, string folder)
        {
            Uri pathUri = new Uri(filePath);
            // Folders must end in a slash
            if (!folder.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                folder += Path.DirectorySeparatorChar;
            }
            Uri folderUri = new Uri(folder);
            return Uri.UnescapeDataString(folderUri.MakeRelativeUri(pathUri).ToString().Replace('/', Path.DirectorySeparatorChar));
        }

    }
}
