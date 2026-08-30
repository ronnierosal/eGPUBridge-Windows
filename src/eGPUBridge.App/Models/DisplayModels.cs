namespace eGPUBridge.App.Models;

public enum DisplayTopology
{
    Unknown,
    Internal,
    External,
    Extend,
    Clone
}

public enum DisplayConnectionKind
{
    Unknown,
    Hdmi,
    DisplayPort,
    EmbeddedDisplayPort,
    Internal,
    UsbDisplay,
    Other
}

public sealed record DisplayTarget(
    string Name,
    string DevicePath,
    string AdapterId,
    uint TargetId,
    DisplayConnectionKind Connection,
    bool IsInternal,
    bool IsAvailable);

public sealed record GpuAdapter(
    string Name,
    string DeviceName,
    string DeviceId,
    bool IsPrimary,
    bool IsAttachedToDesktop,
    bool LikelyExternal);

public sealed record DisplaySnapshot(
    DateTimeOffset CapturedAt,
    DisplayTopology CurrentTopology,
    IReadOnlyList<DisplayTarget> Displays,
    IReadOnlyList<GpuAdapter> Adapters);

public static class DisplayConnectionClassifier
{
    // DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY values from wingdi.h.
    public static DisplayConnectionKind FromNativeValue(uint value) => value switch
    {
        5 => DisplayConnectionKind.Hdmi,
        10 => DisplayConnectionKind.DisplayPort,
        11 => DisplayConnectionKind.EmbeddedDisplayPort,
        12 or 16 or 18 => DisplayConnectionKind.UsbDisplay,
        0x80000000 => DisplayConnectionKind.Internal,
        0xFFFFFFFF => DisplayConnectionKind.Other,
        _ => DisplayConnectionKind.Unknown
    };

    public static bool IsInternal(DisplayConnectionKind connection) =>
        connection is DisplayConnectionKind.Internal or DisplayConnectionKind.EmbeddedDisplayPort;
}

