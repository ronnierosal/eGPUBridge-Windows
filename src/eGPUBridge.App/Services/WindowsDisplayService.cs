using System.ComponentModel;
using System.Runtime.InteropServices;
using eGPUBridge.App.Models;

namespace eGPUBridge.App.Services;

public sealed class WindowsDisplayService(AppLogger logger) : IDisplayService
{
    public DisplaySnapshot GetSnapshot()
    {
        var paths = QueryActivePaths();
        var displays = paths.Select(CreateDisplayTarget).ToArray();
        var adapters = EnumerateAdapters();
        var topology = DetermineTopology(paths, displays);
        var snapshot = new DisplaySnapshot(DateTimeOffset.UtcNow, topology, displays, adapters);

        logger.Info("display.snapshot", "Captured active Windows display configuration.", new
        {
            topology = topology.ToString(),
            displays,
            adapters
        });

        return snapshot;
    }

    public void ApplyTopology(DisplayTopology topology)
    {
        var topologyFlag = topology switch
        {
            DisplayTopology.Internal => NativeMethods.SdcTopologyInternal,
            DisplayTopology.External => NativeMethods.SdcTopologyExternal,
            DisplayTopology.Extend => NativeMethods.SdcTopologyExtend,
            DisplayTopology.Clone => NativeMethods.SdcTopologyClone,
            _ => throw new ArgumentOutOfRangeException(nameof(topology), topology, "A concrete topology is required.")
        };

        logger.Info("display.topology.requested", "Applying Windows display topology.", new
        {
            topology = topology.ToString()
        });

        var result = NativeMethods.SetDisplayConfig(0, 0, 0, 0, NativeMethods.SdcApply | topologyFlag);
        if (result != NativeMethods.ErrorSuccess)
        {
            var exception = new Win32Exception(result);
            logger.Error("display.topology.failed", "Windows rejected the requested display topology.", exception, new
            {
                topology = topology.ToString(),
                nativeError = result
            });
            throw exception;
        }

        logger.Info("display.topology.applied", "Windows accepted the requested display topology.", new
        {
            topology = topology.ToString()
        });
    }

    private static IReadOnlyList<NativeMethods.DisplayConfigPathInfo> QueryActivePaths()
    {
        var flags = NativeMethods.QdcOnlyActivePaths | NativeMethods.QdcVirtualModeAware;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var result = NativeMethods.GetDisplayConfigBufferSizes(flags, out var pathCount, out var modeCount);
            ThrowIfNativeError(result, "GetDisplayConfigBufferSizes");

            var paths = new NativeMethods.DisplayConfigPathInfo[pathCount];
            var modes = new NativeMethods.DisplayConfigModeInfo[modeCount];
            result = NativeMethods.QueryDisplayConfig(flags, ref pathCount, paths, ref modeCount, modes, 0);

            if (result == NativeMethods.ErrorInsufficientBuffer)
            {
                continue;
            }

            ThrowIfNativeError(result, "QueryDisplayConfig");
            return paths.Take((int)pathCount).ToArray();
        }

        throw new Win32Exception(NativeMethods.ErrorInsufficientBuffer, "The display configuration changed repeatedly while it was being read.");
    }

    private static DisplayTarget CreateDisplayTarget(NativeMethods.DisplayConfigPathInfo path)
    {
        var request = new NativeMethods.DisplayConfigTargetDeviceName
        {
            Header = new NativeMethods.DisplayConfigDeviceInfoHeader
            {
                Type = NativeMethods.DisplayConfigDeviceInfoGetTargetName,
                Size = (uint)Marshal.SizeOf<NativeMethods.DisplayConfigTargetDeviceName>(),
                AdapterId = path.TargetInfo.AdapterId,
                Id = path.TargetInfo.Id
            },
            MonitorFriendlyDeviceName = string.Empty,
            MonitorDevicePath = string.Empty
        };

        var result = NativeMethods.DisplayConfigGetDeviceInfo(ref request);
        var connection = DisplayConnectionClassifier.FromNativeValue(path.TargetInfo.OutputTechnology);
        var friendlyName = result == NativeMethods.ErrorSuccess
            ? request.MonitorFriendlyDeviceName?.Trim()
            : string.Empty;
        var devicePath = result == NativeMethods.ErrorSuccess
            ? request.MonitorDevicePath?.Trim()
            : string.Empty;

        return new DisplayTarget(
            string.IsNullOrWhiteSpace(friendlyName) ? $"Display target {path.TargetInfo.Id}" : friendlyName,
            devicePath ?? string.Empty,
            path.TargetInfo.AdapterId.ToString(),
            path.TargetInfo.Id,
            connection,
            DisplayConnectionClassifier.IsInternal(connection),
            path.TargetInfo.TargetAvailable);
    }

    private static IReadOnlyList<GpuAdapter> EnumerateAdapters()
    {
        var adapters = new List<GpuAdapter>();

        for (uint index = 0; ; index++)
        {
            var device = new NativeMethods.DisplayDevice
            {
                Cb = Marshal.SizeOf<NativeMethods.DisplayDevice>(),
                DeviceName = string.Empty,
                DeviceString = string.Empty,
                DeviceId = string.Empty,
                DeviceKey = string.Empty
            };

            if (!NativeMethods.EnumDisplayDevices(null, index, ref device, 0))
            {
                break;
            }

            var attached = (device.StateFlags & NativeMethods.DisplayDeviceAttachedToDesktop) != 0;
            var primary = (device.StateFlags & NativeMethods.DisplayDevicePrimaryDevice) != 0;
            var likelyExternal = attached && !primary;

            adapters.Add(new GpuAdapter(
                device.DeviceString?.Trim() ?? "Unknown adapter",
                device.DeviceName?.Trim() ?? string.Empty,
                device.DeviceId?.Trim() ?? string.Empty,
                primary,
                attached,
                likelyExternal));
        }

        return adapters;
    }

    private static DisplayTopology DetermineTopology(
        IReadOnlyList<NativeMethods.DisplayConfigPathInfo> paths,
        IReadOnlyList<DisplayTarget> displays)
    {
        if (displays.Count == 0)
        {
            return DisplayTopology.Unknown;
        }

        if (displays.Count == 1)
        {
            return displays[0].IsInternal ? DisplayTopology.Internal : DisplayTopology.External;
        }

        var firstSource = paths[0].SourceInfo;
        var allShareSource = paths.All(path =>
            path.SourceInfo.AdapterId.HighPart == firstSource.AdapterId.HighPart &&
            path.SourceInfo.AdapterId.LowPart == firstSource.AdapterId.LowPart &&
            path.SourceInfo.Id == firstSource.Id);

        return allShareSource ? DisplayTopology.Clone : DisplayTopology.Extend;
    }

    private static void ThrowIfNativeError(int result, string operation)
    {
        if (result != NativeMethods.ErrorSuccess)
        {
            throw new Win32Exception(result, $"{operation} failed.");
        }
    }
}

