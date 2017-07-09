using System.Collections.Generic;
using System.IO;

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
            LuaEngine.Current.ExecuteFile(@"luatest.lua");
        }
    }
}
