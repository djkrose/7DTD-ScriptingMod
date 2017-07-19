// This script gets automatically registered in 7DTD as a new command. The tags (@) in the first
// comment block define the command name and aliases, description, help text, etc. The metadata
// must use single-line comment (//) and must appear before the first Lua statement at the
// beginning. The tags @commands and @description are mandatory, all others are optional.
// 
// @commands           js-test js
// @defaultPermission  0
// @description        Example command to demonstrate Lua scripting
// @help               This command demonstrates scripting capabilities of djkrose's Scripting Mod.
//                     Usage:
//                       1. js-test
//                       2. js-test <firstname> <lastname>
//
//                     1. Test the Lua scripting functionality.
//                     2. Test the Lua scripting functionality with parameter handling.


// Help text has ended here because the comment block is interrupted by a newline without comment.
// 
// Global variables:
//   params       string[]             Array of parameters to the command, e.g params[0], params[1]
//   GameManager  GameManager.Instance The main access point for Unity game data
//
// Global functions:
//   console.log                       Prints the text to the user's console
//   console.info/.warn/.error         Prints the text into the server log file
//   dump(variable[, maxDepth])        Dumps .Net objects in readable form into the log file.
//                                     maxDepth = How deep the structure is traversed; default: 4

if (params.length == 2) {
    console.log("Hello " + params[0] + " " + params[1] + ", nice to meet you! I am a JavaScript.");
} else {
    console.log("Hello World! JavaScript is talking here.");
}

/*

// OTHER EXAMPLES

// You can access all standard .Net namespaces and all public objects!
// Let's use the .Net class System.IO.File to read this script file...
var script = System.IO.File.ReadAllText("js-test.js");
console.log(script);

// You can dump contents of an object with dump(..) for example:
dump(System.Globalization.CultureInfo.CurrentCulture, 1);

// You can access the exposed GameManager.Instance object like so:
dump(GameManager.Instance, 1);

*/