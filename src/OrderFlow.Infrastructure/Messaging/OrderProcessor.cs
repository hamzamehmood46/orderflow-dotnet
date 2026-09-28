using Microsoft.EntityFrameworkCore;
using OrderFlow.Core.Domain;
using OrderFlow.Core.Messaging;
using OrderFlow.Infrastructure.Persistence;

namespace OrderFlow.Infrastructure.Messaging;

/// <summary>Handles a single OrderPlaced message. Safe to run more than once for the same order.</summary>
public class OrderProcessor(OrdersDbContext db, IPaymentGateway payments)
{
    public async Task ProcessAsync(Guid orderId, CancellationToken ct)
    {
        var order = await db.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null) return;

        // Idempotency: redelivery of an already-settled order is a no-op.
        if (order.Status != OrderStatus.Pending) return;

        order.RecordAttempt();
        await db.SaveChangesAsync(ct);

        await payments.ChargeAsync(order, ct);

        order.Complete();
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkFailedAsync(Guid orderId, string reason, CancellationToken ct)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null || order.Status != OrderStatus.Pending) return;

        order.Fail(reason);
        await db.SaveChangesAsync(ct);
    }
}
