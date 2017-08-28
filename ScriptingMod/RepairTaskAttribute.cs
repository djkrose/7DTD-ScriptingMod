using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ScriptingMod
{
    [AttributeUsage(AttributeTargets.Field)]
    internal class RepairTaskAttribute : Attribute
    {
        private char _letter;
        private string _description;

        public RepairTaskAttribute(char letter, string description)
        {
            this._letter = letter;
            this._description = description;
        }

        public char Letter => _letter;

        public string Description => _description;
    }
}