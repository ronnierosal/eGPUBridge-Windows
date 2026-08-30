namespace eGPUBridge.App.Models;

public sealed record PciHardwareIdentity(
    string VendorId,
    string DeviceId,
    string? SubsystemId,
    string? RevisionId);

public sealed record PnpDeviceIdentity(
    string DeviceInstanceId,
    IReadOnlyList<string> InterfacePaths,
    PciHardwareIdentity? PciIdentity);

public sealed record DisplayAdapterEvidence(
    string AdapterLuid,
    string AdapterDevicePath);

public sealed record DisplayAdapterIdentity(
    string AdapterLuid,
    string AdapterDevicePath,
    string? DeviceInstanceId,
    PciHardwareIdentity? PciIdentity);

public sealed record HardwareIdentitySnapshot(
    DateTimeOffset CapturedAt,
    IReadOnlyList<PnpDeviceIdentity> DeviceNodes,
    IReadOnlyList<DisplayAdapterIdentity> DisplayAdapters,
    IReadOnlyList<string> Errors);
