using System.ComponentModel.DataAnnotations;
using OrderFlow.Core.Domain;

namespace OrderFlow.Api.Contracts;

public record OrderLineRequest(
    [Required, StringLength(64)] string Sku,
    [Range(1, 1000)] int Quantity,
    [Range(0.01, 1_000_000)] decimal UnitPrice);

public record CreateOrderRequest(
    [Required, StringLength(100)] string CustomerId,
    [Required, MinLength(1)] List<OrderLineRequest> Lines);

public record OrderResponse(
    Guid Id,
    string CustomerId,
    OrderStatus Status,
    decimal Total,
    int Attempts,
    string? FailureReason,
    DateTime CreatedAt)
{
    public static OrderResponse From(Order o) =>
        new(o.Id, o.CustomerId, o.Status, o.Total, o.Attempts, o.FailureReason, o.CreatedAt);
}

