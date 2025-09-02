using System.Collections.Concurrent;

namespace WebCashier.Services
{
    public interface IRuntimeConfigStore
    {
        string? Get(string key);
        void Set(string key, string value);
        void SetRange(IDictionary<string, string?> values);
        IReadOnlyDictionary<string, string> GetAll();
    }

    public class RuntimeConfigStore : IRuntimeConfigStore
    {
        private readonly ConcurrentDictionary<string, string> _values = new();

        public string? Get(string key)
        {
            return _values.TryGetValue(key, out var val) ? val : null;
        }

        public void Set(string key, string value)
        {
            if (value is null) return;
            _values[key] = value;
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
        }

        public IReadOnlyDictionary<string, string> GetAll() => _values;
    }
}
