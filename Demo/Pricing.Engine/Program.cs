using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5202");
var app = builder.Build();

// Catalog: the engine is the single owner of price math.
var catalog = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
{
    ["widget"] = 100m,
    ["gadget"] = 50m,
};

// Perf cache: keyed by product only. Warms on first request for a product and
// serves later requests for the same product from the stored entry.
var cache = new ConcurrentDictionary<string, PriceResponse>(StringComparer.OrdinalIgnoreCase);

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "pricing" }));

app.MapPost("/price", (PriceRequest req) =>
{
    if (!catalog.TryGetValue(req.ProductId, out var unitPrice))
        return Results.NotFound(new { error = $"unknown product '{req.ProductId}'" });

    // CHECKPOINT (pricing): break here to inspect the cache lookup for this flow.
    if (cache.TryGetValue(req.ProductId, out var cached))
        return Results.Ok(cached with { Cached = true });

    var qtyDiscount = req.Quantity >= 10 ? 0.10m : 0m;
    var couponDiscount = string.Equals(req.Coupon, "SAVE20", StringComparison.OrdinalIgnoreCase) ? 0.20m : 0m;
    var discount = Math.Min(qtyDiscount + couponDiscount, 0.30m);
    var total = Math.Round(unitPrice * req.Quantity * (1 - discount) * 1.08m, 2);
    var response = new PriceResponse(unitPrice, discount, total, false);
    cache[req.ProductId] = response;
    return Results.Ok(response);
});

app.Run();

public record PriceRequest(string ProductId, int Quantity, string? Coupon);
public record PriceResponse(decimal UnitPrice, decimal Discount, decimal Total, bool Cached);
