using System;

namespace SimpleDiscordNet.Logging;

public sealed record LogMessage(DateTimeOffset Timestamp, LogLevel Level, string Message, Exception? Exception = null);
