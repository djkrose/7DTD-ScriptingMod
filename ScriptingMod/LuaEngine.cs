using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NLua;
using NLua.Exceptions;

namespace ScriptingMod
{
    internal class LuaEngine
    {
        private static LuaEngine luaEngine;
        public static LuaEngine Current => luaEngine ?? (luaEngine = new LuaEngine());

        private Lua engine;

        private LuaEngine()
        {
            engine = new Lua();
            engine.LoadCLRPackage();
            engine["print"] = new Action<object[]>(Print);
        }

        private void Print(params object[] values)
        {
            if (values == null || values.Length == 0)
                return;
            string output = values.Select(v => v.ToString()).Aggregate((s, s1) => s + s1);
            Log.Out(output);
        }

        public void Execute(string script)
        {
            try
            {
                Log.Debug("Starting inline LUA script ...");
                engine.DoString(script);
                Log.Debug("Inline LUA script ended.");
            }
            catch (LuaScriptException ex)
            {
                Log.Exception(ex);
                Log.Error("Inline LUA script failed.");
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
                Log.Error("Inline LUA script failed.");
            }
        }

        public void ExecuteFile(string fileName)
        {
            try
            {
                Log.Debug($"Starting LUA script {fileName} ...");
                // TODO: Let this also work when file has UTF-8 bom
                engine.DoFile(Path.Combine(Api.CommandsFolder, fileName));
                Log.Debug($"LUA script {fileName} ended.");
            }
            catch (LuaScriptException ex)
            {
                Log.Exception(ex);
                Log.Error($"LUA script {fileName} failed.");
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
                Log.Error($"LUA script {fileName} failed.");
            }
        }
    }
}
