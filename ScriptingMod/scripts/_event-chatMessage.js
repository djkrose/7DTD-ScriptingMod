// Example script to show how to filter chat messages.
//
// This script is disabled by default because its filename is prefixed with underscore (_).
// Remove the underscore to activate the script and test it. No server restart required.
//
// @events chatMessage

dump(event);

console.info("A message of type " + event.messageType + " from " + event.mainName + " was intercepted: " + event.message);

if (event.message != null && event.message.indexOf("shit") != -1) {
    console.warn("Someone said SHIT! Call the cops!!!");
    event.stopPropagation(); // Will drop the chat message
}
