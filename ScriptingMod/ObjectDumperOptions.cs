using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ScriptingMod
{
    public class ObjectDumperOptions
    {
        public static ObjectDumperOptions Default = new ObjectDumperOptions();

        public bool NoFields { get; set; }

        public bool NonPublic { get; set; }

        public int MaxDepth { get; set; }

        public ObjectDumperOptions()
        {
            NoFields = false;
            NonPublic = false;
            MaxDepth = 4;
        }
    }
}
