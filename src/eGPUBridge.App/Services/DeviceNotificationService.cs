using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using eGPUBridge.App.Models;

namespace eGPUBridge.App.Services;

public sealed class DeviceNotificationService : IDisposable
{
    private const int WmDisplayChange = 0x007E;
    private const int WmDeviceChange = 0x0219;
    private const int DbtDeviceArrival = 0x8000;
    private const int DbtDeviceRemoveComplete = 0x8004;
    private const int DbtDeviceTypeInterface = 0x00000005;
    private const uint DeviceNotifyWindowHandle = 0x00000000;

    private static readonly Guid DisplayAdapterInterfaceClassGuid =
        new("5B45201D-F2F2-4F3B-85BB-30FF1F953599");
    private static readonly Guid MonitorInterfaceClassGuid =
        new("E6F07B5F-EE97-4A90-B076-33F57BF4EAA7");

    private readonly HwndSource _source;
    private readonly List<nint> _registrations = [];
    private bool _disposed;

    public DeviceNotificationService(nint windowHandle)
    {
        _source = HwndSource.FromHwnd(windowHandle)
            ?? throw new InvalidOperationException("The eGPUBridge window handle is not available.");
        _source.AddHook(WindowMessageHook);

        try
        {
            _registrations.Add(RegisterInterface(windowHandle, DisplayAdapterInterfaceClassGuid));
            _registrations.Add(RegisterInterface(windowHandle, MonitorInterfaceClassGuid));
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public event EventHandler<DeviceChangeEvidence>? DeviceChanged;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _source.RemoveHook(WindowMessageHook);
        foreach (var registration in _registrations)
        {
            if (registration != 0)
            {
                UnregisterDeviceNotification(registration);
            }
        }

        _registrations.Clear();
    }

    public static DeviceChangeKind? ClassifyDeviceChange(nint wParam) => (int)wParam switch
    {
        DbtDeviceArrival => DeviceChangeKind.Arrived,
        DbtDeviceRemoveComplete => DeviceChangeKind.Removed,
        _ => null
    };

    private nint WindowMessageHook(
        nint hwnd,
        int message,
        nint wParam,
        nint lParam,
        ref bool handled)
    {
        if (message == WmDisplayChange)
        {
            DeviceChanged?.Invoke(this, new DeviceChangeEvidence(
                DeviceChangeKind.DisplayConfigurationChanged,
                null,
                null,
                DateTimeOffset.UtcNow));
            return 0;
        }

        if (message != WmDeviceChange || ClassifyDeviceChange(wParam) is not { } kind)
        {
            return 0;
        }

        var evidence = ReadDeviceInterfaceEvidence(kind, lParam);
        if (evidence is not null)
        {
            DeviceChanged?.Invoke(this, evidence);
        }

        return 0;
    }

    private static DeviceChangeEvidence? ReadDeviceInterfaceEvidence(DeviceChangeKind kind, nint messageData)
    {
        if (messageData == 0)
        {
            return new DeviceChangeEvidence(kind, null, null, DateTimeOffset.UtcNow);
        }

        var header = Marshal.PtrToStructure<DevBroadcastHeader>(messageData);
        if (header.DeviceType != DbtDeviceTypeInterface ||
            header.Size < Marshal.SizeOf<DevBroadcastDeviceInterface>())
        {
            return null;
        }

        var deviceInterface = Marshal.PtrToStructure<DevBroadcastDeviceInterface>(messageData);
        var pathPointer = nint.Add(messageData, Marshal.SizeOf<DevBroadcastDeviceInterface>());
        var path = Marshal.PtrToStringUni(pathPointer)?.TrimEnd('\0');
        return new DeviceChangeEvidence(
            kind,
            deviceInterface.ClassGuid,
            string.IsNullOrWhiteSpace(path) ? null : path,
            DateTimeOffset.UtcNow);
    }

    private static nint RegisterInterface(nint windowHandle, Guid interfaceClassGuid)
    {
        var filter = new DevBroadcastDeviceInterface
        {
            Size = Marshal.SizeOf<DevBroadcastDeviceInterface>(),
            DeviceType = DbtDeviceTypeInterface,
            ClassGuid = interfaceClassGuid
        };
        var filterPointer = Marshal.AllocHGlobal(filter.Size);
        try
        {
            Marshal.StructureToPtr(filter, filterPointer, false);
            var registration = RegisterDeviceNotification(windowHandle, filterPointer, DeviceNotifyWindowHandle);
            if (registration == 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "RegisterDeviceNotification failed.");
            }

            return registration;
        }
        finally
        {
            Marshal.FreeHGlobal(filterPointer);
        }
    }

    [DllImport("user32.dll", EntryPoint = "RegisterDeviceNotificationW", SetLastError = true)]
    private static extern nint RegisterDeviceNotification(
        nint recipient,
        nint notificationFilter,
        uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterDeviceNotification(nint handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct DevBroadcastHeader
    {
        internal int Size;
        internal int DeviceType;
        internal int Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DevBroadcastDeviceInterface
    {
        internal int Size;
        internal int DeviceType;
        internal int Reserved;
        internal Guid ClassGuid;
    }
}
