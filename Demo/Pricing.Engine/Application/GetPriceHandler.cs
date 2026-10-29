using MediatR;
using Pricing.Engine.Domain;
using Pricing.Engine.Infrastructure;

namespace Pricing.Engine.Application;

public class GetPriceHandler(PriceCache cache) : IRequestHandler<GetPriceQuery, PriceResult?>
{
    public Task<PriceResult?> Handle(GetPriceQuery request, CancellationToken cancellationToken)
    {
        if (request.Quantity < 1)
            throw new ValidationException("quantity must be >= 1");
        if (!PricingCatalog.TryGetUnitPrice(request.ProductId, out var unitPrice))
            return Task.FromResult<PriceResult?>(null);
        if (!PriceCalculator.TryGetCouponDiscount(request.Coupon, out var couponDiscount))
            throw new ValidationException($"unknown coupon '{request.Coupon}'");

        // CHECKPOINT (pricing)
        if (cache.TryGet(request.ProductId, out var cached))
            return Task.FromResult<PriceResult?>(cached with { Cached = true });

        var (discount, total) = PriceCalculator.Compute(unitPrice, request.Quantity, couponDiscount);
        var result = new PriceResult(unitPrice, discount, total, false);
        cache.Store(request.ProductId, result);
        return Task.FromResult<PriceResult?>(result);
    }
}
