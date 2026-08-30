using System.Runtime.InteropServices;
using eGPUBridge.App.Models;

namespace eGPUBridge.App.Services;

internal sealed class HardwareIdentityService(IEventLogger logger)
{
    private const string DisplaySetupClassGuid = "{4d36e968-e325-11ce-bfc1-08002be10318}";
    private static readonly Guid DisplayAdapterInterfaceClassGuid =
        new("5B45201D-F2F2-4F3B-85BB-30FF1F953599");

    internal HardwareIdentitySnapshot Capture(IEnumerable<NativeMethods.Luid> adapterLuids)
    {
        var errors = new List<string>();
        IReadOnlyList<PnpDeviceIdentity> deviceNodes;

        try
        {
            deviceNodes = EnumeratePresentDisplayDeviceNodes();
        }
        catch (Exception ex)
        {
            errors.Add(ex.Message);
            deviceNodes = Array.Empty<PnpDeviceIdentity>();
        }

        var adapterEvidence = new List<DisplayAdapterEvidence>();
        foreach (var adapterLuid in adapterLuids.DistinctBy(luid => luid.ToString()))
        {
            try
            {
                adapterEvidence.Add(ReadAdapterEvidence(adapterLuid));
            }
            catch (Exception ex)
            {
                errors.Add(ex.Message);
                adapterEvidence.Add(new DisplayAdapterEvidence(adapterLuid.ToString(), string.Empty));
            }
        }

        var snapshot = new HardwareIdentitySnapshot(
            DateTimeOffset.UtcNow,
            deviceNodes,
            DisplayAdapterCorrelator.Correlate(adapterEvidence, deviceNodes),
            errors);

        logger.Info("hardware.identity.snapshot", "Captured read-only Windows hardware identity evidence.", new
        {
            rawDeviceInstanceIds = snapshot.DeviceNodes.Select(node => node.DeviceInstanceId).ToArray(),
            rawDisplayAdapters = snapshot.DisplayAdapters.Select(adapter => new
            {
                adapter.AdapterLuid,
                adapter.AdapterDevicePath,
                adapter.DeviceInstanceId
            }).ToArray(),
            parsedDeviceNodes = snapshot.DeviceNodes,
            errors = snapshot.Errors
        });

        return snapshot;
    }

    private static IReadOnlyList<PnpDeviceIdentity> EnumeratePresentDisplayDeviceNodes()
    {
        var flags = NativeMethods.CmGetIdListFilterClass | NativeMethods.CmGetIdListFilterPresent;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var result = NativeMethods.CM_Get_Device_ID_List_Size(
                out var bufferLength,
                DisplaySetupClassGuid,
                flags);
            ThrowIfConfigurationManagerError(result, "CM_Get_Device_ID_List_Size");

            var buffer = new char[checked((int)Math.Max(bufferLength, 2u))];
            result = NativeMethods.CM_Get_Device_ID_List(
                DisplaySetupClassGuid,
                buffer,
                (uint)buffer.Length,
                flags);

            if (result == NativeMethods.CrBufferSmall)
            {
                continue;
            }

            ThrowIfConfigurationManagerError(result, "CM_Get_Device_ID_List");
            return HardwareIdentityParser.ParseMultiString(buffer)
                .Select(deviceInstanceId => new PnpDeviceIdentity(
                    deviceInstanceId,
                    EnumeratePresentDisplayAdapterInterfaces(deviceInstanceId),
                    HardwareIdentityParser.ParsePciIdentity(deviceInstanceId)))
                .ToArray();
        }

        throw new InvalidOperationException(
            "CM_Get_Device_ID_List returned CR_BUFFER_SMALL repeatedly while hardware identities were being read.");
    }

    private static IReadOnlyList<string> EnumeratePresentDisplayAdapterInterfaces(string deviceInstanceId)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var interfaceClassGuid = DisplayAdapterInterfaceClassGuid;
            var result = NativeMethods.CM_Get_Device_Interface_List_Size(
                out var bufferLength,
                ref interfaceClassGuid,
                deviceInstanceId,
                NativeMethods.CmGetDeviceInterfaceListPresent);
            ThrowIfConfigurationManagerError(result, "CM_Get_Device_Interface_List_Size");

            var buffer = new char[checked((int)Math.Max(bufferLength, 2u))];
            interfaceClassGuid = DisplayAdapterInterfaceClassGuid;
            result = NativeMethods.CM_Get_Device_Interface_List(
                ref interfaceClassGuid,
                deviceInstanceId,
                buffer,
                (uint)buffer.Length,
                NativeMethods.CmGetDeviceInterfaceListPresent);

            if (result == NativeMethods.CrBufferSmall)
            {
                continue;
            }

            ThrowIfConfigurationManagerError(result, "CM_Get_Device_Interface_List");
            return HardwareIdentityParser.ParseMultiString(buffer);
        }

        throw new InvalidOperationException(
            "CM_Get_Device_Interface_List returned CR_BUFFER_SMALL repeatedly while adapter interfaces were being read.");
    }

    private static DisplayAdapterEvidence ReadAdapterEvidence(NativeMethods.Luid adapterLuid)
    {
        var request = new NativeMethods.DisplayConfigAdapterName
        {
            Header = new NativeMethods.DisplayConfigDeviceInfoHeader
            {
                Type = NativeMethods.DisplayConfigDeviceInfoGetAdapterName,
                Size = (uint)Marshal.SizeOf<NativeMethods.DisplayConfigAdapterName>(),
                AdapterId = adapterLuid,
                Id = 0
            },
            AdapterDevicePath = string.Empty
        };

        var result = NativeMethods.DisplayConfigGetDeviceInfo(ref request);
        if (result != NativeMethods.ErrorSuccess)
        {
            throw new InvalidOperationException(
                $"DisplayConfigGetDeviceInfo failed for adapter LUID {adapterLuid} with error {result}.");
        }

        return new DisplayAdapterEvidence(
            adapterLuid.ToString(),
            request.AdapterDevicePath?.Trim() ?? string.Empty);
    }

    private static void ThrowIfConfigurationManagerError(uint result, string operation)
    {
        if (result != NativeMethods.CrSuccess)
        {
            throw new InvalidOperationException($"{operation} failed with CONFIGRET 0x{result:X8}.");
        }
    }
}
