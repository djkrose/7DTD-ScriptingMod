using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using ScriptingMod.Extensions;

namespace ScriptingMod.Commands
{
    [UsedImplicitly]
    public class CheckTripWires : ConsoleCmdAbstract
    {
        public override string[] GetCommands()
        {
            return new[] { "dj-check-tripwires" };
        }

        public override string GetDescription()
        {
            return "Repairs corrupt tripwire block causing NullReferenceException spam.";
        }

        public override string GetHelp()
        {
            // ----------------------------------(max length: 100 char)--------------------------------------------|
            return $@"
                Scans for corrupt tripwires that cause the server to spam NullReferenceExceptions and lag when
                someone gets into visible range of it. Works only on currently LOADED chunks.
                Usage:
                    1. dj-check-tripwires
                    2. dj-check-tripwires /fix
                    3. dj-check-tripwires <x> <z> [/fix]
                1. Scans all loaded chunks for corrupt tripwires.
                2. Scans all loaded chunks for corrupt tripwires and fixes them.
                3. Scans (and optionally fixes) the chunk that contains the given world coordinate.
                ".Unindent();
        }

        public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
        {
            if (_params.Count > 3)
            {
                SdtdConsole.Instance.Output("Wrong number of parameters. See help.");
                return;
            }

            var isFixMode  = _params.Contains("/fix");
            int countBroken;
            int countChunks;

            if (_params.Count > 1)
            {
                Vector3i pos;
                try
                {
                    pos = new Vector3i(Int32.Parse(_params[0]), 0, Int32.Parse(_params[1]));
                }
                catch (Exception)
                {
                    SdtdConsole.Instance.Output("At least one of the given coordinates is not a valid integer.");
                    return;
                }

                var chunk = GameManager.Instance.World.GetChunkFromWorldPos(pos) as Chunk;
                if (chunk == null)
                {
                    SdtdConsole.Instance.Output($"Location {pos} is too far away. Chunk is not loaded.");
                    return;
                }
                SdtdConsole.Instance.Output($"Scanning chunk {chunk} for broken tripwires ...");
                countBroken = FindBrokenTripWires(chunk, isFixMode);
                countChunks = 1;
            }
            else // scan all chunks
            {
                SdtdConsole.Instance.Output("Scanning all loaded chunks for broken tripwires ...");
                var chunks = GameManager.Instance.World.ChunkClusters[0].GetChunkArray();
                countBroken = chunks.Sum(chunk => FindBrokenTripWires(chunk, isFixMode));
                countChunks = chunks.Count;
            }

            var strChunks = $"chunk{(countChunks != 1 ? "s" : "")}";
            var strTripwires = $"tripwire{(countBroken != 1 ? "s" : "")}";
            var msg = isFixMode
                ? ($"Found and fixed {countBroken} broken {strTripwires} in {countChunks} {strChunks}.")
                : ($"Found {countBroken} broken {strTripwires} in {countChunks} {strChunks}." 
                  + (countBroken > 0 ? $" Use option /fix to fix {(countBroken != 1 ? "them" : "it")}." : ""));

            SdtdConsole.Instance.Output(msg);
            Log.Out(msg);
        }

        private static int FindBrokenTripWires(Chunk chunk, bool fixThem)
        {
            int counter = 0;
            var tileEntities = chunk.GetTileEntities().Values.ToArray();
            //Log.Debug($"Searching for broken tripwires in chunk {chunk} with {tileEntities.Length} tile entities ...");

            foreach (var tileEntity in tileEntities)
            {
                if (!IsBrokenTripWire(tileEntity))
                    continue;

                if (fixThem)
                {
                    Log.Warning($"Tile entity {tileEntity} is broken. Recreating it ...");
                    RecreateTileEntity(chunk, tileEntity);
                    counter++;
                    Log.Out($"Fixed broken tripwire at position {tileEntity.ToWorldPos()}.");
                    SdtdConsole.Instance.Output($"Fixed broken tripwire at position {tileEntity.ToWorldPos()}.");
                }
                else
                {
                    Log.Warning($"Tile entity {tileEntity} is broken.");
                    counter++;
                    SdtdConsole.Instance.Output($"Found broken tripwire at position {tileEntity.ToWorldPos()}.");
                }
            }

            return counter;
        }

        private static bool IsBrokenTripWire(TileEntity tileEntity)
        {
            // Ignore tile entities that are not triggers
            var tileEntityPoweredTrigger = tileEntity as TileEntityPoweredTrigger;
            if (tileEntityPoweredTrigger == null)
                return false;

            // Ignore tile entities that are not trip wires
            if (tileEntityPoweredTrigger.TriggerType != PowerTrigger.TriggerTypes.TripWire)
                return false;

            return !(tileEntityPoweredTrigger.GetPowerItem() is PowerTripWireRelay);
        }


        private static void RecreateTileEntity(Chunk chunk, TileEntity tileEntity)
        {
            // Save some important values
            var tileEntityType = tileEntity.GetTileEntityType();
            var localChunkPos = tileEntity.localChunkPos;

            // Remove broken tile entity and hopefully the power item with it
            chunk.RemoveTileEntity(GameManager.Instance.World, tileEntity);

            // Create new tile entity
            var newTileEntity = TileEntity.Instantiate(tileEntityType, chunk);
            newTileEntity.localChunkPos = localChunkPos;
            chunk.AddTileEntity(newTileEntity);
        }

    }
}
