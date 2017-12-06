// Example script to allow players teleporting their minibike to them.
// If you want to allow all players to use this command, you must set the permission accordingly like with any other command.
// Command must be entered in the console (F1) not the chat. Possibility to use chat is following shortly.
// Insipration by: Muratus Minibike Mod
//
// @commands           dj-minibike
// @description        Teleports the player's nearby minibike back to him/her.
// @help               Teleports the minibike that is shown as symbol in the compass and map back to the player's position.
//                     The minibike MUST be nearby, MUST show as symbol on the compass, and MUST exist in a loaded area.
//                     This command can't bring back minibikes that are despawned, fallen below bedrock, stolen, etc.

importAssembly('Assembly-CSharp');

// Using anonymous function so that we can use "return" for early exit
(function () {

    if (player == null) {
        // Happens when user is connected via telnet
        console.log("You must be logged in as player to execute this command.");
        return;
    }
    
    var found = false;
    var steamId = sender.RemoteClientInfo.playerId; // .ownerId could be someone else with Steam Family Sharing!

    var entities = GameManager.Instance.World.Entities.list;
    
    for (var i=0; i<entities.Count; i++) {
        var entity = entities[i];

        if (entity.GetType().Name == "EntityMinibike" && entity.IsOwner(steamId)) {
            // Found his minibike!
            entity.SetRotation(player.rotation);
            var pos = player.GetPosition();
            pos.y += 0.5;
            entity.SetPosition(pos);
            entity.Honk();
            console.log("Your minibike was teleported to you.");
            console.info(player.entityName + " teleported his minibike to him.");
            found = true;
            break;
        }
    }
    
    if (!found) {
        console.log("Could not find your minibike. Sorry!");
        console.info(player.entityName + "'s minibike could not be found for teleporting.");
    }
    
})();
