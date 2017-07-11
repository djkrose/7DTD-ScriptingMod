using System.Collections.Generic;
using System.IO;
using ScriptingMod.ScriptEngines;

namespace ScriptingMod.NativeCommands
{
    public class LuaTest : ConsoleCmdAbstract
    {
        public override string[] GetCommands()
        {
            return new string[] {"sm-luatest"};
        }

        public override string GetDescription()
        {
            return "Executes luatest.lua";
        }

        public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
        {
            LuaEngine.Instance.ExecuteFile(@"luatest.lua");
        }
    }
}
