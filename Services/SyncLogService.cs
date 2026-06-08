namespace DirectorComercialIA.Services;

public class SyncLogService
{
    private readonly List<LogEntry> _entries = new();
    private readonly object _lock = new();

    public IReadOnlyList<LogEntry> Entries
    {
        get
        {
            lock (_lock)
            {
                return _entries.ToList();
            }
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
        }
    }

    public void Log(string message, LogLevel level = LogLevel.Info)
    {
        lock (_lock)
        {
            _entries.Add(new LogEntry
            {
                Timestamp = DateTime.Now,
                Message = message,
                Level = level
            });
        }
    }

    public void LogInfo(string message) => Log(message, LogLevel.Info);
    public void LogWarning(string message) => Log(message, LogLevel.Warning);
    public void LogError(string message) => Log(message, LogLevel.Error);
}

public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public string Message { get; set; } = "";
    public LogLevel Level { get; set; }
}

public enum LogLevel
{
    Info,
    Warning,
    Error
}
