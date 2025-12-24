using System;

namespace SimpleDiscordNet.Logging;

public sealed class NativeLogger
{
    private readonly LogLevel _minLevel;
    private readonly Action<LogMessage>? _sink;
    public event EventHandler<LogMessage>? Logged;

    /// <summary>
    /// Creates a logger with the most verbose minimum level and no external sink.
    /// </summary>
    public NativeLogger() : this(LogLevel.Trace, null) { }

    /// <summary>
    /// Creates a logger with the given minimum level and optional sink callback.
    /// </summary>
    public NativeLogger(LogLevel minimumLevel, Action<LogMessage>? sink = null)
    {
        _minLevel = minimumLevel;
        _sink = sink;
    }

    /// <summary>
    /// Writes a log message if the level meets the configured minimum.
    /// </summary>
    public void Log(LogLevel level, string message, Exception? exception = null)
    {
        if (level < _minLevel) return;
        LogMessage msg = new(DateTimeOffset.UtcNow, level, message, exception);
        Logged?.Invoke(this, msg);
        try { _sink?.Invoke(msg); } catch { /* Swallow sink errors to prevent cascading failures */ }
    }
}
