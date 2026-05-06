using System.Collections.Concurrent;
using DataDuck.Services;

namespace DataDuck.Desktop.Services;

public class InMemoryLocalStore : ILocalStore
{
    private readonly ConcurrentDictionary<string, string> _store = new();

    public string? Get(string key)
        => _store.TryGetValue(key, out var value) ? value : null;

    public void Set(string key, string value)
        => _store[key] = value;

    public void Remove(string key)
        => _store.TryRemove(key, out _);
}
