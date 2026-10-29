using MediatR;

public record CreateOrderCommand(string ProductId, int Quantity, string? Coupon) : IRequest<OrderResponse?>;
