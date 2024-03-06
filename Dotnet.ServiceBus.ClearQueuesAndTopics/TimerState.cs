namespace Dotnet.ServiceBus.ClearQueuesAndTopics;

public class TimerState(int counter, string processorName, bool requestedStop)
{
    public int Counter { get; set; } = counter;

    public string ProcessorName { get; set; } = processorName;

    public bool RequestedStop { get; set; } = requestedStop;
}