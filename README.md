# djkrose's Scripting Mod
Adds scripting support and other useful functionality to 7 Days To Die dedicated server.

[![Download](https://abload.de/img/github-downloadm0ur7.png)](https://github.com/djkrose/7DTD-ScriptingMod/releases/latest)

## Scripting Examples

<table>
<tr></tr>

<tr>
<th>Lua</th>
<th>JavaScript</th>
</tr><tr><!-- start with gray backgrund -->

<td>
1. Create file <i>example.lua</i>

```lua
-- @commands    mylua
-- @description My first Lua command
-- @help        This example shows how easy it is
--              to add a new command in Lua script
print "Hello Lua World!"
```

2\. Drop file into *\\Mods\\ScriptingMod\\scripts* folder<br>
3\. Execute commands: `help mylua` and `mylua`<br>
![Example mylua](/Documentation/img/example-mylua.png?raw=true)<br>
4\. Profit!
</td>

<td>
1. Create file <i>example.js</i>

```javascript
// @commands    myjavascript myjs
// @description My first JavaScript command
// @help        This example shows how easy it is
//              to add a new command in JavaScript
console.log("Hello JavaScript World!");
```

2\. Drop file into *\\Mods\\ScriptingMod\\scripts* folder<br>
3\. Execute commands: `help myjs`and `myjs`<br>
![Example mylua](/Documentation/img/example-myjs.png?raw=true)<br>
4\. Profit!
</td>

</tr>
</table>

#### Why Scripting?

* Quickly and easily modify commands to your own preference; changes are live instantly without server restart
* Pick and choose commands from various online sources; write and share single command snippets with others
* Implement new commands from simple to complex individual features; no need for third-party bots
* Full OS access, full file access, full network access: use web requests (REST/JSON), any database, send emails, ...
* Two of the most flexible and easy to learn  programming languages with lots of resources online
* Full access to .Net Framework, all Unity objects, and all 7DTD  game data
* Full support of 7DTD command permission system: assign access levels to your commands just like any other

## Built-in Commands

<table>
<tr><td>dj-export</td><td>Exports a prefab including all container content, sign texts, ownership, etc.</td></tr>
<tr><td>dj-import</td><td>Imports a prefab, optionally including all container content, sign texts, ownership, etc.</td></tr>
<tr><td>dj-pos</td><td>Shows the current player's position in various units and formats.</td></tr>
<tr><td>dj-regen</td><td>Regenerates a chunk or custom area based on the world seed.</td></tr>
<tr><td>dj-repair</td><td>Repairs server problems of various kinds.</td></tr>
<tr><td>dj-eac-whitelist</td><td>Allows players to be exempt from EAC checks on an EAC-enabled server</td></tr>
<tr><td>dj-version</td><td>Shows djkrose's Scripting Mod version information.</td></tr>
</table>

## Compatibility

 * **Dedicated Server** of [7 Days to Die](http://store.steampowered.com/app/251570/7_Days_to_Die/). The mod is not meant to be used with the desktop client.
 * For specific version compatibility, see release notes.
 * Developed and tested on **Windows**. Linux may or may not work; reports are welcome!
 * **No dependencies**: No other mods or other software is required.
 * Successfully tested together with these great mods:
   * [Alloc's server fixes](https://7dtd.illy.bz/wiki/Server%20fixes)
   * [Coppis Additions](https://7daystodie.com/forums/showthread.php?44835-Coppi-MOD-New-features)
   * [StompiNZ's Bad Company Manager](https://7daystodie.com/forums/showthread.php?57569)

## Documentation
[coming later]

## Source Code
During this early phase of development the source code is not yet publicly available. The GitHub repository is just used for issue tracking and documentation. When the mod has reached stable status (version 1.0) the source code will be published and others are invited to contribute to it or use parts in their own mods.

## Road Map
The mod is still in early development and I want to add a lot more to it. Here are some ideas:

* Expose more game variables and functions to scripts through an easy ScriptingMod API with clear documentation
* Many more example scripts in Lua and JavaScript to get you started quicker
* Event system to execute scripts based on game events like player logged in, player killed, bloodmoon starting,  etc.
* Option to execute commands through chat messages rather than console commands, for instance /home or !home

This is just a rough outline; everything is subject to change depending on your feedback, on feasibility, on my real life commitments, and simply on my  pleasure to continue in any direction or at all.

## Contact
Feedback is welcome! If you have any suggestions, questions, or concerns please send me a message:
* [djkrose's Discord: #scriptingmod](https://discord.gg/y26jNDz)
* [djkrose in 7DTD Forum](https://7daystodie.com/forums/private.php?do=newpm&amp;u=46733)
 
Just remember: This software is provided free of charge, as is, without any guarantee or support. You are not *entitled* to support, nor is anyone but you liable for data loss. If it doesn't work, that is just bad luck. If data is lost, you should've made a backup.

## License
[![Creative Commons License](https://i.creativecommons.org/l/by-nc/4.0/88x31.png)](http://creativecommons.org/licenses/by-nc/4.0/) djkrose's Scripting Mod is licensed under a [CC BY-NC 4.0](http://creativecommons.org/licenses/by-nc/4.0/). That means, you are free to use the mod and the source code (once published) for non-commercial purposes; just leave the credit notes intact.

This mod is a private project and not affiliated with the official [7DTD game](http://store.steampowered.com/app/251570/7_Days_to_Die/) or with [The Fun Pimps](http://thefunpimps.com/) in any way.
