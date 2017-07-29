using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ScriptingMod
{
    public class ObjectDumperOptions
    {
        public static ObjectDumperOptions Default = new ObjectDumperOptions();

        public bool WithFields { get; set; }

        public bool WithStatic { get; set; }

        public bool WithNonPublic { get; set; }

        public bool WithEnumerableMembers { get; set; }

        public bool IterateEnumerable { get; set; }

        public int MaxDepth { get; set; }

        public List<Type> DontDumpTypes { get; set; }

        public ObjectDumperOptions()
        {
            WithFields = true;
            WithStatic = false;
            WithNonPublic = false;
            WithEnumerableMembers = false;
            IterateEnumerable = true;
            MaxDepth = 4;
            DontDumpTypes = new List<Type>()
            {
                typeof(Vector3i),
                typeof(Vector3),
            };
        }
    }
}
