namespace OrderFlow.Core.Domain;

public enum OrderStatus
{
    Pending,
    Completed,
    Failed
}

public class OrderLine
{
    public int Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class Order
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string CustomerId { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public List<OrderLine> Lines { get; private set; } = new();
    public OrderStatus Status { get; private set; } = OrderStatus.Pending;
    public int Attempts { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    public decimal Total => Lines.Sum(l => l.Quantity * l.UnitPrice);

    private Order() { }

    public static Order Create(string customerId, string idempotencyKey, IEnumerable<OrderLine> lines)
    {
        if (string.IsNullOrWhiteSpace(customerId)) throw new ArgumentException("Customer is required.", nameof(customerId));
        if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new ArgumentException("Idempotency key is required.", nameof(idempotencyKey));

        var order = new Order { CustomerId = customerId, IdempotencyKey = idempotencyKey, Lines = lines.ToList() };
        if (order.Lines.Count == 0) throw new ArgumentException("An order needs at least one line.", nameof(lines));
        return order;
    }

    public void RecordAttempt() => Attempts++;

    public void Complete()
    {
        Status = OrderStatus.Completed;
        FailureReason = null;
    }

    public void Fail(string reason)
    {
        Status = OrderStatus.Failed;
        FailureReason = reason;
    }
}
