using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using A;

namespace ScriptingMod.NativeCommands
{
    public class BlockExport : ConsoleCmdAbstract
    {
        public class Params
        {
            public string fileName;
            public int x1, y1, z1, x2, y2, z2;
        }

        public override string[] GetCommands()
        {
            return new [] {"block-export"};
        }

        public override string GetDescription()
        {
            return "Exports exports an area of blocks including all data.";
        }

        public override void Execute(List<string> paramz, CommandSenderInfo senderInfo)
        {
            try
            {
                Params p = ParseParams(paramz, senderInfo);
                FixOrder(p);
                SavePrefab(p);
                SaveTileEntities(p);
                SdtdConsole.Instance.Output($"Prefab {p.fileName} exported. Area mapped from {p.x1} {p.y1} {p.z1} to {p.x2} {p.y2} {p.z2}");
            }
            catch (FriendlyMessageException ex)
            {
                SdtdConsole.Instance.Output(ex.Message);
            }
            catch (Exception ex)
            {
                SdtdConsole.Instance.Output("An error occured during command execution: " + ex.Message + " [details in server log]");
                Log.Exception(ex);
            }
        }

        private static Params ParseParams(List<string> paramz, CommandSenderInfo senderInfo)
        {
            var p = new Params();

            p.fileName = paramz[0];

            bool parseSuccess =
                int.TryParse(paramz[1], out p.x1) &&
                int.TryParse(paramz[2], out p.y1) &&
                int.TryParse(paramz[3], out p.z1) &&

                int.TryParse(paramz[4], out p.x2) &&
                int.TryParse(paramz[5], out p.y2) &&
                int.TryParse(paramz[6], out p.z2);

            if (!parseSuccess)
                throw new FriendlyMessageException("At least one of the given coordinates is not valid.");

            return p;
        }

        private static void SavePrefab(Params p)
        {
            Vector3i vectori2 = new Vector3i((p.x2 - p.x1) + 1, p.y2 - p.y1 + 1, (p.z2 - p.z1) + 1);
            Prefab pref = new Prefab(vectori2);
            pref.CopyFromWorld(GameManager.Instance.World, new Vector3i(p.x1, p.y1, p.z1), new Vector3i(p.x2, p.y2, p.z2));
            List<int> entities = new List<int>();
            pref.CopyFromWorldWithEntities(GameManager.Instance.World, new Vector3i(p.x1, p.y1, p.z1), new Vector3i(p.x2, p.y2, p.z2), entities);
            pref.bCopyAirBlocks = true;
            pref.filename = p.fileName;
            pref.addAllChildBlocks();
            pref.Save(p.fileName);
            Log.Debug($"Exported Prefab in area from {p.x1} {p.y1} {p.z1} to {p.x2} {p.y2} {p.z2} into {p.fileName}.tts/.xml");
        }

        private static void SaveTileEntities(Params p)
        {
            var world = GameManager.Instance.World;
            var filePath = Utils.GetGameDir("Data/Prefabs/") + p.fileName + ".te";

            try
            {
                using (var writer = new BinaryWriter(new FileStream(filePath, FileMode.Create)))
                {
                    for (int x = p.x1; x <= p.x2; x++)
                    {
                        for (int z = p.z1; z <= p.z2; z++)
                        {
                            Chunk chunk = (Chunk)world.GetChunkFromWorldPos(x, 0, z);
                            if (chunk == null)
                                throw new FriendlyMessageException("The area to export is far away. Chunk not loaded on that area.");

                            for (int y = p.y1; y <= p.y2; y++)
                            {
                                TileEntity tileEntity = world.GetTileEntity(chunk.ClrIdx, new Vector3i(x, y, z));
                                if (tileEntity == null)
                                    continue;
                                tileEntity.write(writer, TileEntity.StreamModeWrite.Persistency);
                            }
                        }
                    }
                }
                Log.Debug($"Exported tile entities in area from {p.x1} {p.y1} {p.z1} to {p.x2} {p.y2} {p.z2} into {p.fileName}.te");
            }
            catch (Exception)
            {
                // Try to delete incomplete tile entity export file
                try { File.Delete(filePath); }
                catch (Exception)
                {
                    // ignored
                }
                throw;
            }
        }

        /// <summary>
        /// Fix the order of xyz1 xyz2, so that the first is always smaller or equal to the second.
        /// </summary>
        private static void FixOrder(Params p)
        {
            if (p.x2 < p.x1)
            {
                int val = p.x1;
                p.x1 = p.x2;
                p.x2 = val;
            }

            if (p.y2 < p.y1)
            {
                int val = p.y1;
                p.y1 = p.y2;
                p.y2 = val;
            }

            if (p.z2 < p.z1)
            {
                int val = p.z1;
                p.z1 = p.z2;
                p.z2 = val;
            }
        }

    }
}
