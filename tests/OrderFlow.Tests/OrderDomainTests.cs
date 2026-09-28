using OrderFlow.Core.Domain;

namespace OrderFlow.Tests;

public class OrderDomainTests
{
    private static OrderLine Line(string sku = "SKU-1", int qty = 1, decimal price = 10m) =>
        new() { Sku = sku, Quantity = qty, UnitPrice = price };

    [Fact]
    public void Total_is_sum_of_quantity_times_unit_price()
    {
        var order = Order.Create("cust-1", "key-1", [Line(qty: 2, price: 10m), Line("SKU-2", 1, 5.50m)]);

        Assert.Equal(25.50m, order.Total);
    }

    [Fact]
    public void New_order_starts_pending_with_no_attempts()
    {
        var order = Order.Create("cust-1", "key-1", [Line()]);

        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(0, order.Attempts);
    }

    [Fact]
    public void Order_requires_at_least_one_line()
    {
        Assert.Throws<ArgumentException>(() => Order.Create("cust-1", "key-1", []));
    }

    [Theory]
    [InlineData("", "key")]
    [InlineData("cust", "")]
    [InlineData("  ", "key")]
    public void Order_requires_customer_and_idempotency_key(string customer, string key)
    {
        Assert.Throws<ArgumentException>(() => Order.Create(customer, key, [Line()]));
    }

    [Fact]
    public void Fail_records_reason_and_complete_clears_it()
    {
        var order = Order.Create("cust-1", "key-1", [Line()]);

        order.Fail("boom");
        Assert.Equal(OrderStatus.Failed, order.Status);
        Assert.Equal("boom", order.FailureReason);

        order.Complete();
        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.Null(order.FailureReason);
    }
}
