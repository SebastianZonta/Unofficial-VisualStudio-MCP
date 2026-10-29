public record OrderRequest(string ProductId, int Quantity, string? Coupon);
public record PriceResponse(decimal UnitPrice, decimal Discount, decimal Total, bool Cached);
public record OrderResponse(string OrderId, string ProductId, int Quantity, string? Coupon,
    decimal UnitPrice, decimal Discount, decimal Total, bool Cached);
