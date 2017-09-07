// @commands           js-sleepers
// @description        Counts and/or removes the sleeper volumes in the current chunk
// @help               Counts and/or removes all invisible sleeper volumes from chunk of the the current player's position.
//                     Usage:
//                       1. js-sleepers
//                       2. js-sleepers /remove
//
//                     1. Counts all sleeper volumes from current chunk
//                     2. Counts and removes all sleeper volumes from current chunk

importAssembly('Assembly-CSharp');
importAssembly('ScriptingMod');

// Using anonymous function so that we can use "return" for early exit
(function () {
    
    if (global.player == null) {
        console.log("You must be logged in as player to execute this command.");
        return;
    }
    
    /** @type {Vector3i} */
    var pos = global.player.GetBlockPosition();
    console.log("Loading chunk at position " + pos.x + ", " + pos.y + ", " + pos.z + " ...");
    
    /** @type {Chunk} */
    var chunk = GameManager.Instance.World.GetChunkFromWorldPos(pos);
    if (chunk == null) {
        console.log("Could not load chunk at this position. You are too far away.");
        return;
    }
    console.log("Processing chunk " + chunk.X + ", " + chunk.Z + " ...");

    /** @type {List<int>} */
    var spawners = chunk.GetSleeperVolumes();
    
    /** @type {int} */
    var count = spawners.Count;
    
    if (global.params.length > 0 && global.params[0] == "/remove") {
        spawners.Clear();
        console.log("Removed " + count + " sleeper volumes from chunk.");
    } else {
        console.log("Chunk has " + count + " sleeper volumes.");
    }
    
})();
