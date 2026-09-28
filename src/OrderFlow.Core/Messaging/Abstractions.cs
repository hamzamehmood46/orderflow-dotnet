using OrderFlow.Core.Domain;

namespace OrderFlow.Core.Messaging;

/// <summary>Event raised once an order has been durably stored.</summary>
public record OrderPlaced(Guid OrderId);

/// <summary>A message that exhausted its retries and was parked for inspection.</summary>
public record DeadLetter(Guid OrderId, string Reason, int Attempts, DateTime At);

/// <summary>
/// Publishing abstraction. The default implementation is an in-process channel;
/// swap in Azure Service Bus (or RabbitMQ) without touching the API or handlers.
/// </summary>
public interface IMessageBus
{
    ValueTask PublishAsync(OrderPlaced message, CancellationToken ct = default);
}

public interface IDeadLetterStore
{
    void Add(DeadLetter deadLetter);
    IReadOnlyList<DeadLetter> List();
}

/// <summary>Downstream dependency that can fail transiently or permanently.</summary>
public interface IPaymentGateway
{
    Task ChargeAsync(Order order, CancellationToken ct = default);
}

public class TransientPaymentException(string message) : Exception(message);
