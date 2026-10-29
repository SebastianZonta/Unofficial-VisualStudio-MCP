using MediatR;
using Pricing.Engine.Application;

namespace Pricing.Engine.Api;

public static class PriceEndpoints
{
    public static void MapPriceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "pricing" }));

        app.MapPost("/price", async (GetPriceQuery query, ISender sender) =>
        {
            try
            {
                var result = await sender.Send(query);
                if (result is null)
                    return Results.NotFound(new { error = $"unknown product '{query.ProductId}'" });
                return Results.Ok(result);
            }
            catch (ValidationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });
    }
}
