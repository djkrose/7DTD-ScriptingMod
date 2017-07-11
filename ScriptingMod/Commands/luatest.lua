-- This script is automatically registered in 7DTD as a new command. The tags (@) in the first
-- comment block define the command name and aliases, description, help text, etc. The metadata must
-- use single-line comment (--) and must appear before the first LUA statement at the beginning.
-- The tags @commands and @description are mandatory, all others are optional.
-- 
-- @commands           luatest lua
-- @defaultPermission  0
-- @description        Example command to demonstrate LUA scripting
-- @help               This command demonstrates scripting capabilities of djkrose's Scripting Mod.
--                     It also tests the correct functionality of the mod and shows version numbers.
--                     Usage:
--                       1. luatest
--                       2. luatest <firstname> <lastname>
--
--                     1. Test the LUA scripting functionality.
--                     2. Test the LUA scripting functionality with parameter handling.


-- Help text has ended here, because the comment block is interrupted by a newline without comment.
-- 
-- Available global variables:
--   params[]

print("Hello World!")
