// This script changes the lifetime of dropped items
// @events entityLoaded

// console.log("Found entity of type: " + event.entity.GetType().ToString());
if (event.entity.GetType().ToString() === 'EntityItem') {
    event.entity.lifetime = 120; // 60 seconds is default
    // console.log("Changed lifetime of item " + event.entity.entityId + " to " + event.entity.lifetime + " seconds.");
}
