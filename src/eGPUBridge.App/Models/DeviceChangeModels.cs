namespace eGPUBridge.App.Models;

public enum DeviceChangeKind
{
    Arrived,
    Removed,
    DisplayConfigurationChanged
}

public sealed class DeviceChangeEvidence(
    DeviceChangeKind kind,
    Guid? interfaceClassGuid,
    string? interfacePath,
    DateTimeOffset observedAt) : EventArgs
{
    public DeviceChangeKind Kind { get; } = kind;

    public Guid? InterfaceClassGuid { get; } = interfaceClassGuid;

    public string? InterfacePath { get; } = interfacePath;

    public DateTimeOffset ObservedAt { get; } = observedAt;

    public string EventName => Kind switch
    {
        DeviceChangeKind.Arrived => "device.arrived",
        DeviceChangeKind.Removed => "device.removed",
        DeviceChangeKind.DisplayConfigurationChanged => "display.changed",
        _ => throw new ArgumentOutOfRangeException(nameof(Kind), Kind, null)
    };
}
