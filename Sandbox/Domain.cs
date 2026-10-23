namespace Sandbox;

internal enum OrderStatus { Draft, Paid, Shipped }

internal readonly record struct Money(decimal Amount, string Currency);

internal sealed record Node(int Depth, string Label);

internal sealed class Address
{
    public string Street { get; init; } = "";
    public string City { get; init; } = "";
}

internal sealed class Customer
{
    public string Name { get; init; } = "";
    public Address Address { get; init; } = new();
}

internal sealed class Item
{
    public int Id;
    public string Sku = "";
    public int Qty;
    public Money Price;
}

internal sealed class Order
{
    public int Id { get; init; }
    public OrderStatus Status { get; set; }
    public Customer Customer { get; init; } = new();
    public List<Item> Items { get; } = new();
    public Dictionary<string, int> Tags { get; } = new();
    public string Note { get; init; } = "";
    public string? Coupon { get; init; }
    /// <summary>Stored at build time, so reading it runs no code beyond the getter.</summary>
    public decimal Total { get; private set; }

    /// <summary>Pure: reads only (no-side-effects mode may still judge it).</summary>
    public decimal ComputeSubtotal()
    {
        decimal sum = 0;
        foreach (var item in Items)
            sum += item.Price.Amount * item.Qty;
        return sum;
    }

    /// <summary>Id 1001, Paid, Montevideo customer, Qty = i % 5 + 1 at 1.25 UYU each
    /// (1500 items total 5625.00), a 2000-char note, two tags, no coupon.</summary>
    public static Order Sample(int itemCount)
    {
        var order = new Order
        {
            Id = 1001,
            Status = OrderStatus.Paid,
            Customer = new Customer { Name = "Ana", Address = new Address { Street = "18 de Julio 1234", City = "Montevideo" } },
            Note = string.Concat(Enumerable.Repeat("0123456789", 200)),
            Coupon = null,
        };
        for (var i = 1; i <= itemCount; i++)
            order.Items.Add(new Item { Id = i, Sku = $"SKU-{i:D4}", Qty = i % 5 + 1, Price = new Money(1.25m, "UYU") });
        order.Tags["priority"] = 1;
        order.Tags["gift"] = 0;
        order.Total = order.ComputeSubtotal();
        return order;
    }
}

/// <summary>State that only changes through side effects: Value must read 0 for the whole
/// inspect scenario, whatever the debugger is asked to evaluate.</summary>
internal sealed class Counter
{
    public int Value { get; private set; }

    public int Increment() => ++Value;

    /// <summary>A getter with a side effect: reading it bumps Value.</summary>
    public int NextTicket => ++Value;
}
