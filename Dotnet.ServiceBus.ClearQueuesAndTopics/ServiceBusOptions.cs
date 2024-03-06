// Ignore Spelling: Prefetch

using System.ComponentModel.DataAnnotations;

namespace Dotnet.ServiceBus.ClearQueuesAndTopics;

public class ServiceBusOptions
{
    [Required]
    public string? ConnectionString { get; set; }

    [Required]
    public int ConnectionTimeoutSeconds { get; set; }

    [Required]
    public int DeleteBatchSize { get; set; }

    [Required]
    public int TimeToLiveHours { get; set; }

    /// <summary
    /// Number of items the Processor will prefetch into memory.  A higher number should result in quicket time to clearn down queue or topic.
    /// </summary>
    [Required]
    [Range(0, 200)]
    public int PrefetchCount { get; set; }

    /// <summary
    /// Number of max concurrent calls allowed to ServiceBus.  A higher number should result in quicket time to clearn down queue or topic.
    /// </summary>
    [Required]
    [Range(1, 200)]
    public int MaxConcurrencyCalls { get; set; }

    /// <summary
    /// Polling period to check if queue or topic is empty when clearing down.
    /// </summary>
    [Required]
    [Range(1000, 10000)]
    public int PollingPeriodMilliseconds { get; set; }
}