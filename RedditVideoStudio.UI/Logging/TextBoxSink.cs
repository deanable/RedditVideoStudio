using Serilog.Core;
using Serilog.Events;
using System;
using System.IO;
using System.Windows.Controls;
using System.Windows.Threading;

namespace RedditVideoStudio.UI.Logging
{
    /// <summary>
    /// A Serilog sink that writes log events to a WPF TextBox control.
    /// It ensures that UI updates are performed on the correct thread using the Dispatcher.
    /// </summary>
    public class TextBoxSink : ILogEventSink
    {
        private readonly TextBox _textBox;
        private readonly Dispatcher _dispatcher;
        private readonly IFormatProvider? _formatProvider;

        public TextBoxSink(TextBox textBox, Dispatcher dispatcher, IFormatProvider? formatProvider = null)
        {
            _textBox = textBox;
            _dispatcher = dispatcher;
            _formatProvider = formatProvider;
        }

        public void Emit(LogEvent logEvent)
        {
            if (logEvent == null) throw new ArgumentNullException(nameof(logEvent));

            // MODIFIED: Build a formatted string instead of just rendering the message.
            var writer = new StringWriter();
            logEvent.RenderMessage(writer, _formatProvider);

            var formattedMessage = $"[{logEvent.Timestamp:HH:mm:ss} {logEvent.Level.ToString().ToUpperInvariant()}] {writer.ToString()}";

            _dispatcher.InvokeAsync(() =>
            {
                _textBox.AppendText(formattedMessage + Environment.NewLine);
                _textBox.ScrollToEnd();
            });
        }
    }
}