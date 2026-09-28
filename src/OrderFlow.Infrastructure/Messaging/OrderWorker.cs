using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderFlow.Core.Messaging;

namespace OrderFlow.Infrastructure.Messaging;

public class WorkerOptions
{
    public const string Section = "Worker";

    /// <summary>Total delivery attempts before a message is dead-lettered.</summary>
    public int MaxAttempts { get; set; } = 4;

    /// <summary>Delay before the first retry; doubles on each subsequent attempt.</summary>
    public int BaseDelayMs { get; set; } = 250;
}

/// <summary>
/// Consumes OrderPlaced messages with exponential-backoff retries.
/// Messages that keep failing are marked Failed and parked in the dead-letter store.
/// </summary>
public class OrderWorker(
    ChannelMessageBus bus,
    IServiceScopeFactory scopes,
    IDeadLetterStore deadLetters,
    IOptions<WorkerOptions> options,
    ILogger<OrderWorker> logger) : BackgroundService
{
    private readonly WorkerOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in bus.Reader.ReadAllAsync(stoppingToken))
        {
            // One order at a time keeps the reference implementation simple;
            // scale out by running more consumers on a real broker.
            await HandleAsync(message, stoppingToken);
        }
    }

    internal async Task HandleAsync(OrderPlaced message, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= _options.MaxAttempts; attempt++)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<OrderProcessor>();
                await processor.ProcessAsync(message.OrderId, ct);
                logger.LogInformation("Order {OrderId} processed on attempt {Attempt}", message.OrderId, attempt);
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Order {OrderId} failed on attempt {Attempt}/{Max}",
                    message.OrderId, attempt, _options.MaxAttempts);

                if (attempt == _options.MaxAttempts)
                {
                    await DeadLetterAsync(message, ex.Message, attempt, ct);
                    return;
                }

                var delay = TimeSpan.FromMilliseconds(_options.BaseDelayMs * Math.Pow(2, attempt - 1));
                await Task.Delay(delay, ct);
            }
        }
    }

    private async Task DeadLetterAsync(OrderPlaced message, string reason, int attempts, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<OrderProcessor>();
        await processor.MarkFailedAsync(message.OrderId, reason, ct);
        deadLetters.Add(new DeadLetter(message.OrderId, reason, attempts, DateTime.UtcNow));
        logger.LogError("Order {OrderId} dead-lettered after {Attempts} attempts: {Reason}",
            message.OrderId, attempts, reason);
    }
}
