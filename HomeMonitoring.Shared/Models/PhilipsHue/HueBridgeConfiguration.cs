namespace HomeMonitoring.Shared.Models.PhilipsHue;

public class HueBridgeConfiguration
{
    public int Id { get; set; }
    public required string BridgeId { get; set; }
    public required string IpAddress { get; set; }
    public required string ApiKey { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsEnabled { get; set; } = true;
}