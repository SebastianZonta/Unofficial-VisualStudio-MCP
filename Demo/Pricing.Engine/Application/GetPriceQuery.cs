using MediatR;

namespace Pricing.Engine.Application;

public record GetPriceQuery(string ProductId, int Quantity, string? Coupon) : IRequest<PriceResult?>;
