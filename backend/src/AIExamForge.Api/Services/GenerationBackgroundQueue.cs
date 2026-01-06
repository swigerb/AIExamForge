using System.Threading.Channels;

namespace AIExamForge.Api.Services;

public sealed class GenerationBackgroundQueue : IGenerationBackgroundQueue
{
    private readonly Channel<GenerationWorkItem> _channel;

    public GenerationBackgroundQueue()
    {
        _channel = Channel.CreateUnbounded<GenerationWorkItem>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
    }

    public ValueTask EnqueueAsync(GenerationWorkItem item, CancellationToken cancellationToken)
        => _channel.Writer.WriteAsync(item, cancellationToken);

    public ValueTask<GenerationWorkItem> DequeueAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAsync(cancellationToken);
}
