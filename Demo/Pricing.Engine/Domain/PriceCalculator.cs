namespace Pricing.Engine.Domain;

public static class PriceCalculator
{
    private static readonly Dictionary<string, decimal> Coupons = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SAVE20"] = 0.20m,
        ["SAVE10"] = 0.10m,
        ["WELCOME5"] = 0.05m,
    };

    public static bool TryGetCouponDiscount(string? coupon, out decimal discount)
    {
        if (string.IsNullOrEmpty(coupon))
        {
            discount = 0m;
            return true;
        }
        return Coupons.TryGetValue(coupon, out discount);
    }

    public static (decimal Discount, decimal Total) Compute(decimal unitPrice, int quantity, decimal couponDiscount)
    {
        var qtyDiscount = quantity >= 10 ? 0.10m : 0m;
        var discount = Math.Min(qtyDiscount + couponDiscount, 0.30m);
        var total = Math.Round(unitPrice * quantity * (1 - discount) * 1.08m, 2);
        return (discount, total);
    }
}
