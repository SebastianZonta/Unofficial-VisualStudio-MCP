using MediatR;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "orders" }));

        app.MapPost("/orders", async Task<IResult> (OrderRequest req, ISender sender, CancellationToken ct) =>
        {
            try
            {
                var order = await sender.Send(new CreateOrderCommand(req.ProductId, req.Quantity, req.Coupon), ct);
                if (order is null)
                    return Results.Json(new { error = "pricing engine rejected the order" }, statusCode: 502);
                return Results.Ok(order);
            }
            catch (InvalidOrderException ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 400);
            }
            catch (EngineErrorException ex)
            {
                return Results.Content(ex.Body, "application/json", statusCode: ex.StatusCode);
            }
            catch (HttpRequestException)
            {
                return Results.Json(new { error = "pricing engine rejected the order" }, statusCode: 502);
            }
        });
    }
}
