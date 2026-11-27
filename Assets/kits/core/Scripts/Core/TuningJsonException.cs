using System;

namespace GameKit.Core
{
    public sealed class TuningJsonException : Exception
    {
        public TuningJsonException()
        {
        }

        public TuningJsonException(string message)
            : base(message)
        {
        }

        public TuningJsonException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public TuningJsonException(string sourceName, string item, string message, Exception innerException = null)
            : base(message, innerException)
        {
            SourceName = sourceName;
            Item = item;
        }

        public string SourceName { get; }

        public string Item { get; }
    }
}
