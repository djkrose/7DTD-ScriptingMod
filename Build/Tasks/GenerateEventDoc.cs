using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Build.MarkdownWikiGenerator;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Build.Tasks
{
    public class GenerateEventDoc : AppDomainIsolatedTask
    {
        [Required]
        public string OutputMarkup { get; set; }

        [Required]
        public string InputDll { get; set; }

        public override bool Execute()
        {
            try
            {
                Log.LogMessage($"Generating event docs for {InputDll} into {OutputMarkup} ...");
                var md = new MarkdownBuilder();
                var types = MarkdownGenerator.Load(InputDll).Where(t => t.Namespace.StartsWith("ScriptingMod.EventArgs"));
                foreach (MarkdownableType type in types)
                {
                    md.Append(type.ToString());
                }
                File.WriteAllText(OutputMarkup, md.ToString());
                Log.LogMessage($"Event docs in {OutputMarkup} generated.");
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex, true, true, null);
                return false;
            }
            return true;
        }

    }
}
