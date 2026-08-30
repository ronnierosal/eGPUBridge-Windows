namespace eGPUBridge.App.Models;

public enum DeviceChangeKind
{
    Arrived,
    Removed,
    DisplayConfigurationChanged
}

public sealed record DeviceChangeEvidence(
    DeviceChangeKind Kind,
    Guid? InterfaceClassGuid,
    string? InterfacePath,
    DateTimeOffset ObservedAt) : EventArgs
{
    public string EventName => Kind switch
    {
        DeviceChangeKind.Arrived => "device.arrived",
        DeviceChangeKind.Removed => "device.removed",
        DeviceChangeKind.DisplayConfigurationChanged => "display.changed",
        _ => throw new ArgumentOutOfRangeException(nameof(Kind), Kind, null)
    };
}
