// You can attach a single script to multiple events just like that:
// @events zombieDied animalDied playerDied

(function () {

    // Check who killed the entity. If sourceEntityName is null the zombie died by itself, e.g. on spikes.
    if (event.sourceEntityName == null)
        return;

    var message;

    // In 'eventType' you will find the event name that triggered this script
    if (eventType == "zombieDied") {

        message = "[FF6666]" + event.sourceEntityName + "[FFFFFF] just killed a zombie with a clean hit to the " + event.hitBodyPart.toLowerCase() + ".";

    } else if (eventType == "animalDied") {

        message = "[FF6666]" + event.sourceEntityName + "[FFFFFF] just murdered an animal by " + event.damageType.toLowerCase() + ".";

    } else {

        // Let's tell everyone from which side the player was killed! Who is the backstabber!??
        var direction = ".";
        switch (event.hitDirection) {
            case "Explosion":
                direction = " with explosion.";
                break;
            case "None":
                direction = ".";
                break;
            default: // Back, Front, Left, Right
                direction = " from the " + event.hitDirection.toLowerCase() + ".";
        }
        message = "[FF6666]" + event.sourceEntityName + "[FFFFFF] just killed the player [FF6666]" + event.entityName + "[FFFFFF]" + direction;

    }

    // Send the message to chat
    importAssembly('Assembly-CSharp');
    GameManager.Instance.GameMessageServer(null, EnumGameMessages.Chat, message, "Server", false, "", false);

})();