using System.Collections.Concurrent;
using System.Threading.Channels;
using OrderFlow.Core.Messaging;

namespace OrderFlow.Infrastructure.Messaging;

public class ChannelMessageBus : IMessageBus
{
    private readonly Channel<OrderPlaced> _channel = Channel.CreateUnbounded<OrderPlaced>(
        new UnboundedChannelOptions { SingleReader = true });

    public ChannelReader<OrderPlaced> Reader => _channel.Reader;

    public ValueTask PublishAsync(OrderPlaced message, CancellationToken ct = default) =>
        _channel.Writer.WriteAsync(message, ct);
}

public class InMemoryDeadLetterStore : IDeadLetterStore
{
    private readonly ConcurrentQueue<DeadLetter> _items = new();

    public void Add(DeadLetter deadLetter) => _items.Enqueue(deadLetter);

    public IReadOnlyList<DeadLetter> List() => _items.ToArray();
}
