var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5201");
builder.Services.AddHttpClient("pricing", c => c.BaseAddress = new Uri("http://localhost:5202"));
var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "orders" }));

app.MapPost("/orders", async Task<IResult> (OrderRequest req, IHttpClientFactory httpFactory) =>
{
    var client = httpFactory.CreateClient("pricing");
    // CHECKPOINT (orders): break after this call to inspect the pricing payload for this orderId.
    var price = await client.PostAsJsonAsync("/price", new { req.ProductId, req.Quantity, req.Coupon });
    if (!price.IsSuccessStatusCode)
        return Results.Json(new { error = "pricing engine rejected the order" }, statusCode: 502);
    var priced = await price.Content.ReadFromJsonAsync<PriceResponse>();
    var orderId = Guid.NewGuid().ToString("N")[..8];
    return Results.Ok(new OrderResponse(orderId, req.ProductId, req.Quantity, req.Coupon,
        priced!.UnitPrice, priced.Discount, priced.Total, priced.Cached));
});

app.Run();

public record OrderRequest(string ProductId, int Quantity, string? Coupon);
public record PriceResponse(decimal UnitPrice, decimal Discount, decimal Total, bool Cached);
public record OrderResponse(string OrderId, string ProductId, int Quantity, string? Coupon,
    decimal UnitPrice, decimal Discount, decimal Total, bool Cached);
