using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using Whds.Knapp.ServiceBus.Configuration;

namespace Dotnet.ServiceBus.ClearQueuesAndTopics;

public static class Program
{
    private const string KeyVaultPrefix = "KeyVaultPrefix";
    private const string ServiceBusSection = "ServiceBus";
    private const string TopicsSection = "Topics";
    private const string QueuesSection = "Queues";

    public static async Task Main()
    {
        var host = new HostBuilder()
            .ConfigureAppConfiguration(
            builder =>
            {
                builder
                    .AddJsonFile("appsettings.json");

                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Information()
                    .WriteTo.Console()
                    .CreateLogger();
            })
            .ConfigureServices((hostContext, services) =>
            {
                services.AddLogging(
                    builder => builder.AddSerilog(Log.Logger));

                var serviceBusOptions = new ServiceBusOptions();
                hostContext.Configuration
                    .GetSection(ServiceBusSection)
                    .Bind(serviceBusOptions);

                var keyVaultPrefix = hostContext.Configuration[KeyVaultPrefix];
                if (keyVaultPrefix != null)
                {
                    hostContext.Configuration
                       .GetSection($"{keyVaultPrefix}:{ServiceBusSection}")
                        .Bind(serviceBusOptions);
                }

                services
                    .AddOptions<List<TopicOptions>>()
                    .BindConfiguration(TopicsSection)
                    .ValidateDataAnnotations()
                    .ValidateOnStart();

                services
                    .AddOptions<QueueOptions>()
                    .BindConfiguration(QueuesSection)
                    .ValidateDataAnnotations()
                    .ValidateOnStart();

                services
                    .AddOptions<ServiceBusOptions>()
                    .BindConfiguration(ServiceBusSection)
                    .ValidateDataAnnotations()
                    .ValidateOnStart();

                services.AddSingleton<ServiceBusService>();

            }).Build();

        var queues = host.Services.GetRequiredService<IOptions<QueueOptions>>();
        var topics = host.Services.GetRequiredService<IOptions<List<TopicOptions>>>();
        var service = host.Services.GetRequiredService<ServiceBusService>();

        foreach (var queue in queues.Value.QueueNames ?? Enumerable.Empty<string>())
        {
            await service.ClearQueueAndDeadLetter(queue);
        }

        foreach (var topic in topics.Value ?? Enumerable.Empty<TopicOptions>())
        {
            await service.ClearQueueAndDeadLetter(topic.TopicName!); // check null dereference
        }
    }
}