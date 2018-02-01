// This script gets automatically registered in 7DTD as a new command. The tags (@) in the first
// comment block define the command name and aliases, description, help text, etc. The metadata
// must use single-line comment (//) and must appear before the first Lua statement at the
// beginning. Command scripts require at least the tags @commands and @description, all others
// are optional. See other example files for event-driven scripts.
// 
// @commands           dj-test-js
// @defaultPermission  0
// @description        Example command to demonstrate Lua scripting
// @help               This command demonstrates scripting capabilities of djkrose's Scripting Mod.
//                     Usage:
//                       1. dj-test-js
//                       2. dj-test-js <firstname> <lastname>
//
//                     1. Test the Lua scripting functionality.
//                     2. Test the Lua scripting functionality with parameter handling.


// Help text has ended here because the comment block is interrupted by a newline without comment.
// 
// Global variables:
//   global       special object       Global namespace similar to "window" in browsers. Can be omitted.
//   params       string[]             Array of parameters to the command, e.g params[0], params[1]
//   sender       CommandSenderInfo    Information about the client executed the command
//   player       EntityPlayer         Player object of the current player execuing the command
//
// Global functions:
//   importAssembly(assemblyName)      Imports all types of the given .Net assembly name (file name without .dll or .exe extension)
//   console.log(msg)                  Prints the text to the user's console
//   console.info/.warn/.error(msg)    Prints the text into the server log file
//   var foo = require(fileName[, passthrough])
//                                     Executes the external JavaScript file and returns the module.exports variable
//                                     passthrough = Set event, eventType, params, player, sender variables where applicable; default: false
//   dump(variable[, maxDepth])        Dumps .Net objects in readable form into the log file.
//                                     maxDepth = How deep the structure is traversed; default: 1

if (params.length === 2) {
    console.log("Hello " + params[0] + " " + params[1] + ", nice to meet you! I am a JavaScript.");
} else {
    console.log("Hello World! JavaScript is talking here.");
}

/*

// OTHER EXAMPLES

// You can access all standard .Net namespaces and all public objects!
// Let's use the .Net class System.IO.File to read this script file...
var script = System.IO.File.ReadAllText("dj-test-js.js");
console.log(script);

// You can dump contents of an object with dump(..) for example:
dump(System.Globalization.CultureInfo.CurrentCulture, 1);

// You can access the exposed GameManager.Instance object like so:
dump(GameManager.Instance, 1);

*/