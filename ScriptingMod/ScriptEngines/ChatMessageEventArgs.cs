using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;

namespace ScriptingMod.ScriptEngines
{
    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    public class ChatMessageEventArgs
    {
        // event name; MUST be property so that CommandTools.InvokeScriptEvents(..) works
        public string type { get; internal set; }

        public ClientInfo clientInfo;
        public EnumGameMessages messageType;
        public string message;
        public string mainName;
        public bool localizeMain;
        public string secondaryName;
        public bool localizeSecondary;

        public bool isPropagationStopped = false;

        public void stopPropagation()
        {
            isPropagationStopped = true;
        }

    }
}
