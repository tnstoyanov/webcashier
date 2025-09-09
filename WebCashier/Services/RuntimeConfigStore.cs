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
            if (!IsDisabled()) LoadFromDisk();
        }

        public RuntimeConfigStore(string persistPath)
        {
            _persistPath = persistPath;
            if (!IsDisabled()) LoadFromDisk();
        }

        public string? Get(string key)
        {
            return _values.TryGetValue(key, out var val) ? val : null;
        }

        public void Set(string key, string value)
        {
            if (value is null) return;
            _values[key] = value;
            if (!IsDisabled()) Persist();
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
            if (values.Count > 0 && !IsDisabled()) Persist();
        }

        public IReadOnlyDictionary<string, string> GetAll() => _values;

        public bool Remove(string key)
        {
            var removed = _values.TryRemove(key, out _);
            if (removed && !IsDisabled()) Persist();
            return removed;
        }

        private void LoadFromDisk()
        {
            try
            {
                if (File.Exists(_persistPath))
                {
                    string json;
                    using (var fs = File.Open(_persistPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var sr = new StreamReader(fs))
                        json = sr.ReadToEnd();
                    var data = JsonSerializer.Deserialize<Dictionary<string,string>>(json) ?? new();
                    foreach (var kv in data)
                    {
                        if (!string.IsNullOrEmpty(kv.Key) && kv.Value != null)
                            _values[kv.Key] = kv.Value;
                    }
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
                if (IsDisabled()) return;
                lock (_fileLock)
                {
                    var dir = Path.GetDirectoryName(_persistPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    var snapshot = _values.ToDictionary(k => k.Key, v => v.Value);
                    var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
                    var tempFile = _persistPath + ".tmp";
                    File.WriteAllText(tempFile, json);
                    if (File.Exists(_persistPath))
                    {
                        try { File.Delete(_persistPath); } catch { }
                    }
                    File.Move(tempFile, _persistPath, true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RuntimeConfigStore] Failed persisting config: {ex.Message}");
            }
        }

        private static bool IsDisabled()
            => string.Equals(Environment.GetEnvironmentVariable("RUNTIME_CONFIG_DISABLE"), "true", StringComparison.OrdinalIgnoreCase);
    }
}
