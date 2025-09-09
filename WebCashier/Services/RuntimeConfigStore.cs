using System.Collections.Concurrent;
using System.Text.Json;

namespace WebCashier.Services
{
    public interface IRuntimeConfigStore
    {
        string? Get(string key);
        void Set(string key, string value);
        void SetRange(IDictionary<string, string?> values);
        IReadOnlyDictionary<string, string> GetAll();
    bool Remove(string key);
    }

    public class RuntimeConfigStore : IRuntimeConfigStore
    {
        private readonly ConcurrentDictionary<string, string> _values = new();
        private readonly string _persistPath;
        private readonly object _fileLock = new();
    public string PersistPath => _persistPath;

        public RuntimeConfigStore()
        {
            // Default to app base directory runtime-config.json
            _persistPath = Path.Combine(AppContext.BaseDirectory, "runtime-config.json");
            LoadFromDisk();
        }

        public RuntimeConfigStore(string persistPath)
        {
            _persistPath = persistPath;
            LoadFromDisk();
        }

        public string? Get(string key)
        {
            return _values.TryGetValue(key, out var val) ? val : null;
        }

        public void Set(string key, string value)
        {
            if (value is null) return;
            _values[key] = value;
            Persist();
        }

        public void SetRange(IDictionary<string, string?> values)
        {
            foreach (var kv in values)
            {
                if (!string.IsNullOrEmpty(kv.Key) && kv.Value != null)
                {
                    _values[kv.Key] = kv.Value;
                }
            }
            if (values.Count > 0) Persist();
        }

        public IReadOnlyDictionary<string, string> GetAll() => _values;

        public bool Remove(string key)
        {
            var removed = _values.TryRemove(key, out _);
            if (removed) Persist();
            return removed;
        }

        private void LoadFromDisk()
        {
            try
            {
                if (File.Exists(_persistPath))
                {
                    var json = File.ReadAllText(_persistPath);
                    var data = JsonSerializer.Deserialize<Dictionary<string,string>>(json) ?? new();
                    foreach (var kv in data) _values[kv.Key] = kv.Value;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RuntimeConfigStore] Failed loading persisted config: {ex.Message}");
            }
        }

        private void Persist()
        {
            try
            {
                lock (_fileLock)
                {
                    var dir = Path.GetDirectoryName(_persistPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    var json = JsonSerializer.Serialize(_values, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(_persistPath, json);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RuntimeConfigStore] Failed persisting config: {ex.Message}");
            }
        }
    }
}
