using MediatR;

public sealed class InvalidOrderException(string message) : Exception(message);

public sealed class CreateOrderHandler(IPricingClient pricing) : IRequestHandler<CreateOrderCommand, OrderResponse?>
{
    public async Task<OrderResponse?> Handle(CreateOrderCommand cmd, CancellationToken ct)
    {
        if (cmd.Quantity < 1)
            throw new InvalidOrderException("quantity must be >= 1");
        // CHECKPOINT (orders)
        var priced = await pricing.GetPriceAsync(cmd.ProductId, cmd.Quantity, cmd.Coupon, ct);
        if (priced is null)
            return null;
        var orderId = Guid.NewGuid().ToString("N")[..8];
        return new OrderResponse(orderId, cmd.ProductId, cmd.Quantity, cmd.Coupon,
            priced.UnitPrice, priced.Discount, priced.Total, priced.Cached);
    }
}
