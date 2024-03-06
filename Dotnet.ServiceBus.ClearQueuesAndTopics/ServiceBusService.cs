using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SerilogTimings;

namespace Dotnet.ServiceBus.ClearQueuesAndTopics;

public class ServiceBusService : IAsyncDisposable
{
    private readonly IOptions<ServiceBusOptions> _serviceBusOptions;
    private readonly ILogger<ServiceBusService> _logger;
    private readonly ServiceBusClient _client;
    private static readonly Dictionary<string, int> Processors = [];

    public ServiceBusService(
        IOptions<ServiceBusOptions> serviceBusOptions,
        ILogger<ServiceBusService> logger)
    {
        _serviceBusOptions = serviceBusOptions;
        _logger = logger;
        var clientOptions = new ServiceBusClientOptions
        {
            TransportType = ServiceBusTransportType.AmqpWebSockets
        };
        _client = new ServiceBusClient(
            serviceBusOptions.Value.ConnectionString,
            clientOptions);
    }

#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
    public async ValueTask DisposeAsync()
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize
    {
        await _client.DisposeAsync();
    }

    public async Task ClearQueueAndDeadLetter(string queue)
    {
        var clearQueueTask = ClearQueue(queue);

        var clearDeadLetterQueueTask = ClearQueue(FormatDeadLetterPath(queue));

        await Task.WhenAll(
            clearQueueTask,
            clearDeadLetterQueueTask);

    }

    public async Task ClearTopicSubscriptionAndDeadLetter(string topic, string subscription)
    {
        var clearSubscriptionTask = ClearTopicSubscription(topic, subscription);

        var clearDeadLetterSubscriptionTask = ClearTopicSubscription(
            topic,
            FormatDeadLetterPath(subscription));

        await Task.WhenAll(
            clearSubscriptionTask,
            clearDeadLetterSubscriptionTask);
    }

    private async Task ClearQueue(string queue)
    {
        var processor = CreateProcessor(queue);

        try
        {
            await ReceiveUntilEmpty(processor);
        }
        finally
        {
            await processor.DisposeAsync();
        }
    }

    private async Task ClearTopicSubscription(string topic, string subscription)
    {
        var processor = CreateProcessor(topic, subscription);

        try
        {
            await ReceiveUntilEmpty(processor);
        }
        finally
        {
            await processor.DisposeAsync();
        }
    }

    private async Task ReceiveUntilEmpty(ServiceBusProcessor processor)
    {
        Processors.Add(processor.Identifier, 0);
        await processor.StartProcessingAsync();

        var timerState = new TimerState(counter: 0, processorName: processor.Identifier, requestedStop: false);
        using var timer = new Timer(
            callback: new TimerCallback(TimerTask!), // why can this be null
            state: timerState,
            dueTime: _serviceBusOptions.Value.PollingPeriodMilliseconds,
            period: _serviceBusOptions.Value.PollingPeriodMilliseconds);

        using var loggingOperation = Operation.Time("Clearing queue/topic using processor {Processor}", processor.Identifier);
        while (!timerState.RequestedStop)
        {
            await Task.Delay(_serviceBusOptions.Value.PollingPeriodMilliseconds);
            _logger.LogInformation(
                "Deleted {Count} messages from queue/topic using processor {Processor}, continuing...",
                timerState.Counter,
                processor.Identifier);
        }

        timer.Change(Timeout.Infinite, Timeout.Infinite); // Stop Timer
        await processor.StopProcessingAsync();
    }

    private ServiceBusProcessor CreateProcessor(string queue)
    {
        var processor = _client.CreateProcessor(
            queue,
            GetServiceBusProcessorOptions());

        processor.ProcessMessageAsync += MessageHandler;
        processor.ProcessErrorAsync += ErrorHandler;

        return processor;
    }

    private ServiceBusProcessor CreateProcessor(string topic, string subscription)
    {
        var processor = _client.CreateProcessor(
            topic,
            subscription,
            GetServiceBusProcessorOptions());

        processor.ProcessMessageAsync += MessageHandler;
        processor.ProcessErrorAsync += ErrorHandler;

        return processor;
    }

    private ServiceBusProcessorOptions GetServiceBusProcessorOptions()
    {
        return new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls = _serviceBusOptions.Value.MaxConcurrencyCalls,
            PrefetchCount = _serviceBusOptions.Value.PrefetchCount,
            ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete
        };
    }

    private static void TimerTask(object timerState)
    {
        if (timerState is not TimerState state)
        {
            throw new InvalidOperationException("Cannot cast object");
        }

        var count = Processors[state.ProcessorName];

        if (state.Counter == count)
        {
            state.RequestedStop = true;
        }

        state.Counter = count;
    }

    private async Task MessageHandler(ProcessMessageEventArgs args)
    {
        _logger.LogDebug("Received message with CorrelationId {CorrelationId}", args.Message.CorrelationId);
        Processors[args.Identifier]++;

        await Task.CompletedTask;
    }

    private async Task ErrorHandler(ProcessErrorEventArgs args)
    {
        _logger.LogWarning(args.Exception, "Exception on receiving message");

        await Task.CompletedTask;
    }

    public static string FormatDeadLetterPath(string entity)
    {
        return $"{entity}/$DeadLetterQueue";
    }
}
