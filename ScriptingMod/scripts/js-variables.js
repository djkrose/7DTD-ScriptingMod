// @commands           js-variables
// @description        Example script to show how to access certain game objects

console.log("This command produces no output by default. See the source code to understand how to access game objects.");

// You can add "dump(variable);" after all variable assignments to see the variable content.
// Use "dump(variable, 2)" to go the object hierarchy 2 levels deep; use 3, 4, etc. to go deeper (lots of data!)

importAssembly('Assembly-CSharp');

var landClaimExpiryTime = GameStats.GetInt(EnumGameStats.LandClaimExpiryTime); // in days
var landClaimSize = GameStats.GetInt(EnumGameStats.LandClaimSize); // in blocks

/** @type {World} */
var world = GameManager.Instance.World;

/** @type {GameManager} */
var gm = GameManager.Instance;

/** @type {PersistentPlayerList} */
var persistentPlayerList = gm.GetPersistentPlayerList();

/** @type {Dictionary<Vector3i, PersistentPlayerData>} */
var landClaimBlocks = persistentPlayerList.m_lpBlockMap;

/** @type {int} */
var numerOfClaimBlocks = landClaimBlocks.Count;

/** @type {Enumerator} */
var enumerator = landClaimBlocks.GetEnumerator();
while (enumerator.MoveNext()) {
    /** @type {Vector3i} */
    var pos = enumerator.Current.Key;
    /** @type {PersistentPlayerData} */
    var playerData = enumerator.Current.Value;
    
    // playerData.PlayerId    -> steam id of LCB owner
    // playerData.LastLogin   -> last online time of LCB owner
    // ...
}

if (sender.RemoteClientInfo != null) {
    // Caller is an actual player in the game (and not a telnet connection)

    /**
     * Current player's steam id 
     * @type {string}
     */
    var steamId = sender.RemoteClientInfo.playerId; // don't use .ownerId! it is different when Steam Fanaily Sharing is active!
    console.log("Your steam id: " + steamId);

    /**
     * Current player's block position (integer values)
     * @type {Vector3i} 
     */
    var pos = player.GetBlockPosition();
    console.log("Your block position: " + pos.ToString());

    /**
     * Current player's exact position (float values)
     * @type {Vector3} 
     */
    var posf = player.GetPosition();
    console.log("Your exact position: " + posf.ToString());
    
    /** @type {PersistentPlayerData} */
    var playerData = persistentPlayerList.GetPlayerData(steamId)
    
    /** @type {List<Vector3i>} */
    var claimBlocks = playerData.LPBlocks;
    
}