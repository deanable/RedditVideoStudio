using Serilog.Core;
using Serilog.Events;
using System;

namespace RedditVideoStudio.UI.Logging
{
    /// <summary>
    /// A Serilog sink that delegates the emission of log events to another sink.
    /// This allows the log output target to be changed at runtime.
    /// </summary>
    public class DelegatingSink : ILogEventSink
    {
        private static ILogEventSink _sink = new NullSink();

        public void Emit(LogEvent logEvent)
        {
            _sink?.Emit(logEvent);
        }

        public static void SetSink(ILogEventSink sink)
        {
            _sink = sink;
        }

        /// <summary>
        /// A sink that does nothing, used as a default to prevent null reference exceptions.
        /// </summary>
        private class NullSink : ILogEventSink
        {
            public void Emit(LogEvent logEvent) { }
        }
    }
}
