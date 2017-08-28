using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using JetBrains.Annotations;

namespace ScriptingMod
{
    [Serializable]
    public class PersistentData
    {
        private const string SaveFileName = "ScriptingModPeristentData.xml";

        private static PersistentData instance;
        public  static PersistentData Instance => instance ?? (instance = new PersistentData());

        [UsedImplicitly]
        public const int FileVersion = 3;
        public bool RepairSimulate;
        public RepairTasks RepairTasks;

        [Obsolete("Use RepairAuto instead. To be removed in version 0.10.")]
        [UsedImplicitly]
        public bool CheckPowerAuto;
        private bool _repairAuto;
        public bool RepairAuto
        {
            get { return _repairAuto || CheckPowerAuto; }
            set { CheckPowerAuto = _repairAuto = value; }
        }

        [Obsolete("Use RepairCounter instead. To be removed in version 0.10.")]
        [UsedImplicitly]
        public int CheckPowerCounter;
        private int _repairCounter;
        public int RepairCounter
        {
            get { return Math.Max(_repairCounter, CheckPowerCounter); }
            set { CheckPowerCounter = _repairCounter = value; }
        }

        public void Save()
        {
            lock (SaveFileName)
            {
                try
                {
                    var saveFilePath = Path.Combine(GameUtils.GetSaveGameDir(), SaveFileName);
                    using (var writer = new XmlTextWriter(new StreamWriter(saveFilePath)))
                    {
                        writer.Formatting = Formatting.Indented;
                        var serializer = new XmlSerializer(this.GetType());
                        serializer.Serialize(writer, this);
                        writer.Flush();
                        Log.Out($"Persistent data saved in {SaveFileName}.");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("Could not save persistent data: " + ex);
                } 
            }
        }

        public static void Load()
        {
            var saveFilePath = Path.Combine(GameUtils.GetSaveGameDir(), SaveFileName);
            lock (SaveFileName)
            {
                if (!File.Exists(saveFilePath))
                {
                    Log.Warning($"Could not find file {SaveFileName}. Assuming there is no saved data yet. Writing default file ...");
                    Instance.Save();
                    return;
                }

                try
                {
                    using (var reader = new StreamReader(saveFilePath))
                    {
                        var serializer = new XmlSerializer(typeof(PersistentData));
                        instance = serializer.Deserialize(reader) as PersistentData;
                    }
                    Log.Out($"Persistent data loaded from {SaveFileName}.");
                }
                catch (Exception ex)
                {
                    Log.Error("Could not load persistent data: " + ex);
                } 
            }
        }
    }
}
