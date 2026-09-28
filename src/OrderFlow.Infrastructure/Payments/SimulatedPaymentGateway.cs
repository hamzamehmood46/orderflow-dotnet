using System.Collections.Concurrent;
using OrderFlow.Core.Domain;
using OrderFlow.Core.Messaging;

namespace OrderFlow.Infrastructure.Payments;

/// <summary>
/// Stand-in for a real payment provider, with deterministic failure modes so retry,
/// idempotency and dead-letter behaviour can be demonstrated and tested:
///   SKU "FLAKY"  - fails transiently on the first two calls per order, then succeeds
///   SKU "POISON" - always fails
/// </summary>
public class SimulatedPaymentGateway : IPaymentGateway
{
    private readonly ConcurrentDictionary<Guid, int> _calls = new();

    public Task ChargeAsync(Order order, CancellationToken ct = default)
    {
        var call = _calls.AddOrUpdate(order.Id, 1, (_, n) => n + 1);

        if (order.Lines.Any(l => l.Sku.Equals("POISON", StringComparison.OrdinalIgnoreCase)))
            throw new TransientPaymentException("Payment provider rejected the charge.");

        if (order.Lines.Any(l => l.Sku.Equals("FLAKY", StringComparison.OrdinalIgnoreCase)) && call <= 2)
            throw new TransientPaymentException($"Payment provider timeout (call {call}).");

        return Task.CompletedTask;
    }
}
