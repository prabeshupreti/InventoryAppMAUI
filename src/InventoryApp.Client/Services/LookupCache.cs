using System.Collections.Concurrent;
using InventoryApp.Contracts.Common;

namespace InventoryApp.Client.Services;

/// <summary>
/// Short-lived in-memory cache for read-heavy reference data (categories, suppliers).
/// This is deliberately minimal: it removes repeat round trips on mobile networks without
/// turning the app into an offline-first system. Swapping the backing store for SQLite
/// would be enough to add real offline support later - nothing above this class would change.
/// </summary>
public sealed class LookupCache
{
    private readonly ConcurrentDictionary<string, (DateTime ExpiresAt, LookupList Value)> _entries = new();

    public async Task<LookupList> GetOrLoadAsync(string key, Func<Task<LookupList>> loader)
    {
        if (_entries.TryGetValue(key, out var entry) && entry.ExpiresAt > DateTime.UtcNow)
        {
            return entry.Value;
        }

        var value = await loader();
        _entries[key] = (DateTime.UtcNow.Add(ApiSettings.LookupCacheLifetime), value);
        return value;
    }

    public void Invalidate(string key) => _entries.TryRemove(key, out _);

    public void Clear() => _entries.Clear();
}
