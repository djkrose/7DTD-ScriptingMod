// This script changes the lifetime of dropped items.
// Additionally to what is shown in JSON logs, the 'event' object contains the full 'entity' object.
// @events entityLoaded

importAssembly('Assembly-CSharp');

console.log("Found entity of type: " + event.entityType);
if (event.entityType === 'EntityItem') {

    event.entity.lifetime = 120; // 60 seconds is default
    console.log("Changed lifetime of item " + event.entity.entityId + " to " + event.entity.lifetime + " seconds.");
}
