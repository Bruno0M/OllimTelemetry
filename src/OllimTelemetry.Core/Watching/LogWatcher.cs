namespace OllimTelemetry.Core.Watching;

public sealed class LogWatcher : IDisposable
{
    private static readonly string WatchPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects");

    private FileSystemWatcher? _watcher;
    private Action<string>?    _onFileChanged;

    private readonly Dictionary<string, Timer> _debounceTimers = new();
    private readonly Lock                       _lock           = new();

    public void Start(Action<string> onFileChanged)
    {
        _onFileChanged = onFileChanged;

        Directory.CreateDirectory(WatchPath);

        _watcher = new FileSystemWatcher(WatchPath)
        {
            Filter                = "*.jsonl",
            IncludeSubdirectories = true,
            NotifyFilter          = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents   = true
        };

        _watcher.Changed += OnChanged;
        _watcher.Created += OnChanged;
    }

    public void Stop()
    {
        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Changed -= OnChanged;
            _watcher.Created -= OnChanged;
        }

        lock (_lock)
        {
            foreach (var timer in _debounceTimers.Values)
                timer.Dispose();
            _debounceTimers.Clear();
        }
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        var path = e.FullPath;

        lock (_lock)
        {
            if (_debounceTimers.TryGetValue(path, out var existing))
            {
                existing.Change(500, Timeout.Infinite);
                return;
            }

            var timer = new Timer(_ =>
            {
                lock (_lock) { _debounceTimers.Remove(path); }
                _onFileChanged?.Invoke(path);
            }, null, 500, Timeout.Infinite);

            _debounceTimers[path] = timer;
        }
    }

    public void Dispose()
    {
        Stop();
        _watcher?.Dispose();
    }
}
