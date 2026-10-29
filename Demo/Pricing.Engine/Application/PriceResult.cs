namespace Pricing.Engine.Application;

public record PriceResult(decimal UnitPrice, decimal Discount, decimal Total, bool Cached);
