using System.Collections.Concurrent;
using Pricing.Engine.Application;

namespace Pricing.Engine.Infrastructure;

// Perf cache: warmed per product; later requests for the same product reuse the stored entry.
// Singleton by design: one warm cache shared by every request the engine serves.
public sealed class PriceCache
{
    private readonly ConcurrentDictionary<string, PriceResult> _entries = new(StringComparer.OrdinalIgnoreCase);

    public bool TryGet(string productId, out PriceResult result) =>
        _entries.TryGetValue(productId, out result!);

    public void Store(string productId, PriceResult result) => _entries[productId] = result;
}
