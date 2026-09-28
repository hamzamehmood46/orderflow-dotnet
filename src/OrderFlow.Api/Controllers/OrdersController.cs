using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Api.Contracts;
using OrderFlow.Core.Domain;
using OrderFlow.Core.Messaging;
using OrderFlow.Infrastructure.Persistence;

namespace OrderFlow.Api.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController(OrdersDbContext db, IMessageBus bus) : ControllerBase
{
    /// <summary>
    /// Places an order. The Idempotency-Key header makes retries safe: repeating a request with the
    /// same key returns the original order instead of creating a duplicate.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create(
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromBody] CreateOrderRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Problem("The Idempotency-Key header is required.", statusCode: StatusCodes.Status400BadRequest);

        var existing = await FindByKeyAsync(idempotencyKey, ct);
        if (existing is not null) return Ok(OrderResponse.From(existing));

        var order = Order.Create(
            request.CustomerId,
            idempotencyKey,
            request.Lines.Select(l => new OrderLine { Sku = l.Sku, Quantity = l.Quantity, UnitPrice = l.UnitPrice }));

        db.Orders.Add(order);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Lost a race with a concurrent request carrying the same key.
            db.ChangeTracker.Clear();
            var winner = await FindByKeyAsync(idempotencyKey, ct);
            if (winner is not null) return Ok(OrderResponse.From(winner));
            throw;
        }

        await bus.PublishAsync(new OrderPlaced(order.Id), ct);

        return AcceptedAtAction(nameof(Get), new { id = order.Id }, OrderResponse.From(order));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == id, ct);
        return order is null ? NotFound() : Ok(OrderResponse.From(order));
    }

    private Task<Order?> FindByKeyAsync(string key, CancellationToken ct) =>
        db.Orders.AsNoTracking().Include(o => o.Lines).FirstOrDefaultAsync(o => o.IdempotencyKey == key, ct);
}

[ApiController]
[Route("api/dead-letters")]
public class DeadLettersController(IDeadLetterStore store) : ControllerBase
{
    /// <summary>Messages that exhausted their retries, for operator inspection.</summary>
    [HttpGet]
    public ActionResult<IReadOnlyList<DeadLetter>> List() => Ok(store.List());
}
