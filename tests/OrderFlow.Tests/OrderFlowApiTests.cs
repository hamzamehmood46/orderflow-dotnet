using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using OrderFlow.Api.Contracts;
using OrderFlow.Core.Domain;
using OrderFlow.Core.Messaging;

namespace OrderFlow.Tests;

public sealed class OrderFlowFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"orderflow-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Orders", $"Data Source={_dbPath}");
        builder.UseSetting("Worker:BaseDelayMs", "5");
        builder.UseSetting("Worker:MaxAttempts", "4");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { File.Delete(_dbPath); } catch (IOException) { /* best effort */ }
    }
}

public class OrderFlowApiTests(OrderFlowFactory factory) : IClassFixture<OrderFlowFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static CreateOrderRequest Request(string sku) =>
        new("cust-1", [new OrderLineRequest(sku, 2, 19.99m)]);

    private async Task<HttpResponseMessage> PlaceAsync(string sku, string? key = null)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/orders") { Content = JsonContent.Create(Request(sku)) };
        message.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
        return await _client.SendAsync(message);
    }

    private async Task<OrderResponse> WaitForSettledAsync(Guid id)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            var order = await _client.GetFromJsonAsync<OrderResponse>($"/api/orders/{id}");
            if (order!.Status != OrderStatus.Pending) return order;
            await Task.Delay(20);
        }
        throw new TimeoutException($"Order {id} did not settle in time.");
    }

    [Fact]
    public async Task Placed_order_is_accepted_and_processed_asynchronously()
    {
        var response = await PlaceAsync("SKU-OK");
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.Equal(39.98m, created!.Total);

        var settled = await WaitForSettledAsync(created.Id);
        Assert.Equal(OrderStatus.Completed, settled.Status);
        Assert.Equal(1, settled.Attempts);
    }

    [Fact]
    public async Task Transient_failures_are_retried_until_they_succeed()
    {
        var created = await (await PlaceAsync("FLAKY")).Content.ReadFromJsonAsync<OrderResponse>();

        var settled = await WaitForSettledAsync(created!.Id);

        Assert.Equal(OrderStatus.Completed, settled.Status);
        Assert.Equal(3, settled.Attempts); // two failures, then success
    }

    [Fact]
    public async Task Poison_messages_are_dead_lettered_after_max_attempts()
    {
        var created = await (await PlaceAsync("POISON")).Content.ReadFromJsonAsync<OrderResponse>();

        var settled = await WaitForSettledAsync(created!.Id);
        Assert.Equal(OrderStatus.Failed, settled.Status);
        Assert.Equal(4, settled.Attempts);

        var deadLetters = await _client.GetFromJsonAsync<List<DeadLetter>>("/api/dead-letters");
        Assert.Contains(deadLetters!, d => d.OrderId == created.Id && d.Attempts == 4);
    }

    [Fact]
    public async Task Repeating_a_request_with_the_same_idempotency_key_does_not_duplicate_the_order()
    {
        var key = Guid.NewGuid().ToString();

        var first = await PlaceAsync("SKU-OK", key);
        var second = await PlaceAsync("SKU-OK", key);

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var a = await first.Content.ReadFromJsonAsync<OrderResponse>();
        var b = await second.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.Equal(a!.Id, b!.Id);
    }

    [Fact]
    public async Task Concurrent_requests_with_the_same_key_create_exactly_one_order()
    {
        var key = Guid.NewGuid().ToString();

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => PlaceAsync("SKU-OK", key)));
        var ids = new HashSet<Guid>();
        foreach (var r in responses)
        {
            Assert.True(r.IsSuccessStatusCode, $"Unexpected status {r.StatusCode}");
            ids.Add((await r.Content.ReadFromJsonAsync<OrderResponse>())!.Id);
        }

        Assert.Single(ids);
    }

    [Fact]
    public async Task Missing_idempotency_key_is_rejected()
    {
        var response = await _client.PostAsJsonAsync("/api/orders", Request("SKU-OK"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_order_is_rejected()
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(new CreateOrderRequest("cust-1", []))
        };
        message.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_order_returns_not_found()
    {
        var response = await _client.GetAsync($"/api/orders/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Health_endpoint_reports_healthy()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
