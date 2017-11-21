// Example script to show how the chatMessage event works
// 
// @events chatMessage

dump(event);

console.info("******************* A message of type " + event.messageType + " from " + event.mainName + " was intercepted: " + event.message);

if (event.message.indexOf("shit") != -1) {
    console.warn("Someone said SHIT! Call the cops!!!");
    event.stopPropagation();
}
