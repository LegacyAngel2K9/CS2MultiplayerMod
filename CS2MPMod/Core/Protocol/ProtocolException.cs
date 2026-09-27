using System;

namespace CS2MPMod.Core.Protocol
{
    /// <summary>Raised when a payload cannot be decoded (truncated, unknown type, bad version).</summary>
    public sealed class ProtocolException : Exception
    {
        public ProtocolException(string message) : base(message) { }
        public ProtocolException(string message, Exception inner) : base(message, inner) { }
    }
}
