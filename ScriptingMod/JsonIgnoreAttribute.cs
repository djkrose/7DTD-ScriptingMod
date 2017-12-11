using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LitJson;

namespace ScriptingMod
{
    /// <summary>
    /// Instructs the <see cref="JsonMapper"/> not to serialize the public field or public read/write property value.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public class JsonIgnoreAttribute : Attribute
    {
    }
}
