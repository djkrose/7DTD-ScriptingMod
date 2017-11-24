using System;
using System.IO;

namespace ScriptingMod.Tools
{
    internal static class FileTools
    {
        /// <summary>
        /// Makes the given filePath relative to the given folder
        /// Source: https://stackoverflow.com/a/703292/785111
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="folder"></param>
        /// <returns></returns>
        public static string GetRelativePath(string filePath, string folder)
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
