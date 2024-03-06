using System.ComponentModel.DataAnnotations;

namespace Whds.Knapp.ServiceBus.Configuration;

public class TopicOptions
{
    [Required]
    public string? TopicName { get; set; }

    [Required]
    public string? SubscriptionName { get; set; }
}
