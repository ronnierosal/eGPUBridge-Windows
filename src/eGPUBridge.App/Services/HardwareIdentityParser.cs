using System.Text.RegularExpressions;
using eGPUBridge.App.Models;

namespace eGPUBridge.App.Services;

public static class HardwareIdentityParser
{
    private static readonly Regex VendorPattern = CreatePciFieldPattern("VEN", 4);
    private static readonly Regex DevicePattern = CreatePciFieldPattern("DEV", 4);
    private static readonly Regex SubsystemPattern = CreatePciFieldPattern("SUBSYS", 8);
    private static readonly Regex RevisionPattern = CreatePciFieldPattern("REV", 2);

    public static PciHardwareIdentity? ParsePciIdentity(string? deviceInstanceId)
    {
        if (string.IsNullOrWhiteSpace(deviceInstanceId))
        {
            return null;
        }

        var vendorId = ReadField(VendorPattern, deviceInstanceId);
        var deviceId = ReadField(DevicePattern, deviceInstanceId);
        if (vendorId is null || deviceId is null)
        {
            return null;
        }

        return new PciHardwareIdentity(
            vendorId,
            deviceId,
            ReadField(SubsystemPattern, deviceInstanceId),
            ReadField(RevisionPattern, deviceInstanceId));
    }

    public static IReadOnlyList<string> ParseMultiString(ReadOnlySpan<char> buffer)
    {
        var values = new List<string>();
        var start = 0;

        for (var index = 0; index < buffer.Length; index++)
        {
            if (buffer[index] != '\0')
            {
                continue;
            }

            if (index == start)
            {
                break;
            }

            values.Add(buffer[start..index].ToString());
            start = index + 1;
        }

        return values;
    }

    private static Regex CreatePciFieldPattern(string field, int length) =>
        new($@"(?:^|[\\&]){field}_(?<value>[0-9A-F]{{{length}}})(?=&|\\|$)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static string? ReadField(Regex pattern, string value)
    {
        var match = pattern.Match(value);
        return match.Success ? match.Groups["value"].Value.ToUpperInvariant() : null;
    }
}

public static class DisplayAdapterCorrelator
{
    public static IReadOnlyList<DisplayAdapterIdentity> Correlate(
        IReadOnlyList<DisplayAdapterEvidence> adapters,
        IReadOnlyList<PnpDeviceIdentity> deviceNodes)
    {
        var nodesByInterfacePath = deviceNodes
            .SelectMany(node => node.InterfacePaths.Select(path => new { Path = path, Node = node }))
            .GroupBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Node, StringComparer.OrdinalIgnoreCase);

        return adapters.Select(adapter =>
        {
            var matched = nodesByInterfacePath.TryGetValue(adapter.AdapterDevicePath, out var deviceNode)
                ? deviceNode
                : null;

            return new DisplayAdapterIdentity(
                adapter.AdapterLuid,
                adapter.AdapterDevicePath,
                matched?.DeviceInstanceId,
                matched?.PciIdentity);
        }).ToArray();
    }
}
