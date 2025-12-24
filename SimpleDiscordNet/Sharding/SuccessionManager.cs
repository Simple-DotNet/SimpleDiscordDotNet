using SimpleDiscordNet.Logging;
using SimpleDiscordNet.Sharding.Models;

namespace SimpleDiscordNet.Sharding;

/// <summary>
/// Manages ordered succession list for coordinator failover.
/// Workers are ordered by position, with position 1 being the current coordinator.
/// Example: var manager = new SuccessionManager(logger); manager.AddWorker("worker-1", "http://...", false);
/// </summary>
internal sealed class SuccessionManager
{
    private readonly List<SuccessionEntry> _succession = [];
    private readonly object _lock = new();
    private readonly NativeLogger _logger;

    public SuccessionManager(NativeLogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Adds a worker to the succession list.
    /// Example: manager.AddWorker("worker-1", "http://192.168.1.100:8080", false);
    /// </summary>
    public int AddWorker(string processId, string url, bool isOriginalCoordinator)
    {
        lock (_lock)
        {
            // Remove if already exists
            _succession.RemoveAll(e => e.ProcessId == processId);

            // Add at end with next position
            int position = _succession.Count + 1;
            SuccessionEntry entry = new(position, processId, url, isOriginalCoordinator);
            _succession.Add(entry);

            _logger.Log(LogLevel.Information, $"Succession updated: {processId} added at position {position}");
            return position;
        }
    }

    /// <summary>
    /// Removes a worker from the succession list and renumbers positions.
    /// Example: manager.RemoveWorker("worker-1");
    /// </summary>
    public void RemoveWorker(string processId)
    {
        lock (_lock)
        {
            int removed = _succession.RemoveAll(e => e.ProcessId == processId);
            if (removed > 0)
            {
                // Renumber positions
                for (int i = 0; i < _succession.Count; i++)
                {
                    _succession[i] = _succession[i] with { Position = i + 1 };
                }
                _logger.Log(LogLevel.Information, $"Succession updated: {processId} removed");
            }
        }
    }

    /// <summary>
    /// Gets the next coordinator in line (position 2, since position 1 is current coordinator).
    /// Example: var nextCoordinator = manager.GetNextCoordinator();
    /// </summary>
    public SuccessionEntry? GetNextCoordinator()
    {
        lock (_lock)
        {
            // Position 1 is current coordinator, position 2 is next
            return _succession.FirstOrDefault(e => e.Position == 2);
        }
    }

    /// <summary>
    /// Gets the position of a worker in the succession list (1-based).
    /// Example: int pos = manager.GetPosition("worker-1"); // 1 = coordinator, 2 = next
    /// </summary>
    public int GetPosition(string processId)
    {
        lock (_lock)
        {
            var entry = _succession.FirstOrDefault(e => e.ProcessId == processId);
            return entry?.Position ?? -1;
        }
    }

    /// <summary>
    /// Gets all succession entries in order.
    /// Example: var entries = manager.GetAll();
    /// </summary>
    public List<SuccessionEntry> GetAll()
    {
        lock (_lock)
        {
            return _succession.ToList();
        }
    }

    /// <summary>
    /// Gets the total number of workers in succession.
    /// Example: int count = manager.Count;
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _succession.Count;
            }
        }
    }

    /// <summary>
    /// Clears all succession entries.
    /// Example: manager.Clear();
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _succession.Clear();
            _logger.Log(LogLevel.Information, "Succession cleared");
        }
    }

    /// <summary>
    /// Loads succession from a list (used during coordinator resumption).
    /// Example: manager.LoadFrom(handoffData.SuccessionOrder);
    /// </summary>
    public void LoadFrom(List<SuccessionEntry> entries)
    {
        lock (_lock)
        {
            _succession.Clear();
            _succession.AddRange(entries.OrderBy(e => e.Position));
            _logger.Log(LogLevel.Information, $"Succession loaded with {entries.Count} entries");
        }
    }

    /// <summary>
    /// Gets a succession update message for broadcasting to workers.
    /// Example: var update = manager.CreateUpdate();
    /// </summary>
    public SuccessionUpdate CreateUpdate()
    {
        lock (_lock)
        {
            return new SuccessionUpdate(
                Succession: GetAll(),
                RemovedNode: null,
                AddedNode: null,
                Timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            );
        }
    }
}
