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
    [UsedImplicitly(ImplicitUseTargetFlags.Members)]
    public class PersistentData
    {
        private static PersistentData instance;
        public  static PersistentData Instance => instance ?? (instance = new PersistentData());

        private const string SaveFileName = "ScriptingModPeristentData.xml";
        private bool isSaveOnShutdown = false;

        #region Persistent values to be saved

        public bool RepairAuto;
        public string RepairTasks;
        public bool RepairSimulate;
        public int RepairInterval; // seconds
        public int RepairCounter;
        public bool PatchCorpseItemDupeExploit;

        [NotNull]
        public List<string> EacWhitelist = new List<string>();

#if DEBUG
        [Serializable]
        [UsedImplicitly(ImplicitUseTargetFlags.Members)]
        public class InvokedEvent
        {
            [XmlAttribute]
            public string EventName;
            public string FirstCall;
            public List<string> LastCalls;
        }

        // Tracks which events get invoked at all to find "dead" events.
        // Using List of tuple instead of Dictionary, because Dictionary is not serializable.
        // event name => unstructured log information about first call and eventArgs
        [NotNull]
        public List<InvokedEvent> InvokedEvents = new List<InvokedEvent>();
#endif

        #endregion

        ~PersistentData()
        {
            if (isSaveOnShutdown)
                Save();
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

        public void SaveOnShutdown()
        {
            isSaveOnShutdown = true;
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
