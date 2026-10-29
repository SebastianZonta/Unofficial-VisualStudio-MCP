namespace Pricing.Engine.Domain;

public static class PricingCatalog
{
    private static readonly Dictionary<string, decimal> Catalog = new(StringComparer.OrdinalIgnoreCase)
    {
        ["widget"] = 100m,
        ["gadget"] = 50m,
    };

    public static bool TryGetUnitPrice(string productId, out decimal unitPrice) =>
        Catalog.TryGetValue(productId, out unitPrice);
}
