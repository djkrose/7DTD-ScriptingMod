// Script is executed when the server was registered with Steam, which happens regardless
// whether the server is public or private. The variable 'event.gameInfos' contains all the
// game preferences.
//
// This script is disabled by default because its filename is prefixed with underscore (_).
// Remove the underscore to activate the script and test it. No server restart required.
//
// @events serverRegistered

console.info("Server just started and is ready for connections!");

// Let's this event object with all game preferences to console
dump(event, 2);