using System;

namespace ScriptingMod.Exceptions
{
    /// <summary>
    /// Thrown in forseeable situations when the current operation should be canceled
    /// and the user should be given a message. The exception message can be directly
    /// passed to the user's console (or whereever), no further modification or stack
    /// trace must be shown.
    /// Typically used for input validation or when a command was used incorrecly.
    /// </summary>
    internal class FriendlyMessageException : ApplicationException
    {
        public FriendlyMessageException(string message) : base(message) { }
        public FriendlyMessageException(string message, Exception ex) : base(message, ex) { }
    }
}
