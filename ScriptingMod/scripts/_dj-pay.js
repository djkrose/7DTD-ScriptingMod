// Example script to use as basis for implementing an item payment system.
// Extend functionality by either adding code here to give the player something when he pays
// a certain item (another item, perform a teleport, add XPs, etc.) or evaluate the console
// log message from an external monitoring application like Botman or RAT.
// - Idea by Trekkan
//
// This script also demonstrates some advanced features:
// - Create objects from generic types (see ListOfEntity) for C#'s new List<Entity>
// - Using types as variables directly (GetEntitiesInBounds(EntityItem, ...)) for C#'s typeof(EntityTiem)
// - Using an anonymous function wrapper to be able to use return for premature exit
// - Comparing types using GetType().Name == "xxx"
// 
// @commands       dj-pay
// @description    Removes dropped items directly next to the player and allows to pay with them
// @help           This command removes all dropped items within 2 blocks of the player and logs the removed items to
//                 the server console. This allows server managers to implement payment functionality to pay for special
//                 items or services with dropped items.

importAssembly('Assembly-CSharp');

(function () {

    if (player == null) {
        // Happens when user is connected via telnet
        console.log("You must be logged in as player to execute this command.");
        return;
    }

    // Create empty generic target list; equivalent of C#: new List<Entity>
    var ListOfEntity = System.Collections.Generic.List(Entity);
    var entities = new ListOfEntity();

    var ci = sender.RemoteClientInfo;
    var world = GameManager.Instance.World;
    var bounds = BoundsUtils.ExpandBounds(player.boundingBox, 2, 2, 2);
    var counter = 0;

    // Find entities nearby the player
    entities = world.GetEntitiesInBounds(EntityItem, bounds, entities);

    // Check all found entities; remove and log if they are items
    for (var i = 0; i < entities.Count; i++) {
        var entity = entities[i];
        if (entity.GetType().Name == "EntityItem") {
            console.info("Player \"" + ci.playerName + "\" (" + ci.playerId + ") paid " +
                entity.itemStack.count + "x " + entity.itemStack.itemValue.ItemClass.Name + " of quality " + entity.itemStack.itemValue.Quality + ".");
            world.RemoveEntity(entity.entityId, EnumRemoveEntityReason.Killed);
            counter++;
        }
    }

    // Let the user know what happened
    if (counter == 0) {
        console.log("Could not find any dropped items nearby.");
    } else {
        console.log("Removed " + counter + " items as payment.");
    }

})();
