-- This script gets automatically registered in 7DTD as a new command. The tags (@) in the first
-- comment block define the command name and aliases, description, help text, etc. The metadata
-- must use single-line comment (--) and must appear before the first Lua statement at the
-- beginning. The tags @commands and @description are mandatory, all others are optional.
-- 
-- @commands           luatest lua
-- @defaultPermission  0
-- @description        Example command to demonstrate Lua scripting
-- @help               This command demonstrates scripting capabilities of djkrose's Scripting Mod.
--                     It also tests the correct functionality of the mod and shows version numbers.
--                     Usage:
--                       1. luatest
--                       2. luatest <firstname> <lastname>
--
--                     1. Test the Lua scripting functionality.
--                     2. Test the Lua scripting functionality with parameter handling.


-- Help text has ended here because the comment block is interrupted by a newline without comment.
-- 
-- Global variables:
--   params       string[]             Array of parameters to the command, e.g params[0], params[1]
--
-- Global functions:
--   print(text)                       Prints the text to console and log file
--   dump(variable[, maxDepth])        Dumps .Net objects in readable form into the log file.
--                                     maxDepth = How deep the structure is traversed; default: 4

if params.Length == 2 then
    print("Hello " .. params[0] .. " " .. params[1] .. ", nice to meet you! I am a Lua script.")
else
    print("Hello World! A Lua script is talking here.")
end

print("Version: " .. _VERSION)

-- This Lua module allows dumping Lua objects to the console, similar to dump() for .Net objects
-- Usage: inspect(variable)
package.path = "../Helpers/?.lua;" .. package.path
local inspect = require('inspect')
local someNumbers = {1, 3, 7, 11}
print(inspect(someNumbers))

-- You can import all standard .Net namespaces and use all public objects!
-- Let's use the .Net class System.IO.File to read this script file...
-- import("System.IO.File")
-- local content = File.ReadAllText("luatest.lua")
-- print("==== Here comes the content of luatest.lua ====")
-- print(content)

import("GameManager")
dump(GameManager)
