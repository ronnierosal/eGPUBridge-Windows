using System.Runtime.InteropServices;

namespace eGPUBridge.App.Services;

internal static class NativeMethods
{
    internal const uint QdcOnlyActivePaths = 0x00000002;
    internal const uint QdcVirtualModeAware = 0x00000010;
    internal const uint DisplayConfigDeviceInfoGetTargetName = 2;
    internal const uint DisplayConfigDeviceInfoGetAdapterName = 4;

    internal const uint CmGetIdListFilterPresent = 0x00000100;
    internal const uint CmGetIdListFilterClass = 0x00000200;
    internal const uint CmGetDeviceInterfaceListPresent = 0x00000000;

    internal const uint SdcTopologyInternal = 0x00000001;
    internal const uint SdcTopologyClone = 0x00000002;
    internal const uint SdcTopologyExtend = 0x00000004;
    internal const uint SdcTopologyExternal = 0x00000008;
    internal const uint SdcApply = 0x00000080;

    internal const uint DisplayDeviceAttachedToDesktop = 0x00000001;
    internal const uint DisplayDevicePrimaryDevice = 0x00000004;

    internal const int ErrorSuccess = 0;
    internal const int ErrorInsufficientBuffer = 122;
    internal const uint CrSuccess = 0x00000000;
    internal const uint CrBufferSmall = 0x0000001A;

    [DllImport("user32.dll")]
    internal static extern int GetDisplayConfigBufferSizes(
        uint flags,
        out uint numPathArrayElements,
        out uint numModeInfoArrayElements);

    [DllImport("user32.dll")]
    internal static extern int QueryDisplayConfig(
        uint flags,
        ref uint numPathArrayElements,
        [Out] DisplayConfigPathInfo[] pathInfoArray,
        ref uint numModeInfoArrayElements,
        [Out] DisplayConfigModeInfo[] modeInfoArray,
        nint currentTopologyId);

    [DllImport("user32.dll")]
    internal static extern int DisplayConfigGetDeviceInfo(ref DisplayConfigTargetDeviceName requestPacket);

    [DllImport("user32.dll")]
    internal static extern int DisplayConfigGetDeviceInfo(ref DisplayConfigAdapterName requestPacket);

    [DllImport("CfgMgr32.dll", EntryPoint = "CM_Get_Device_ID_List_SizeW", ExactSpelling = true, CharSet = CharSet.Unicode)]
    internal static extern uint CM_Get_Device_ID_List_Size(
        out uint bufferLength,
        string? filter,
        uint flags);

    [DllImport("CfgMgr32.dll", EntryPoint = "CM_Get_Device_ID_ListW", ExactSpelling = true, CharSet = CharSet.Unicode)]
    internal static extern uint CM_Get_Device_ID_List(
        string? filter,
        [Out] char[] buffer,
        uint bufferLength,
        uint flags);

    [DllImport("CfgMgr32.dll", EntryPoint = "CM_Get_Device_Interface_List_SizeW", ExactSpelling = true, CharSet = CharSet.Unicode)]
    internal static extern uint CM_Get_Device_Interface_List_Size(
        out uint bufferLength,
        ref Guid interfaceClassGuid,
        string? deviceInstanceId,
        uint flags);

    [DllImport("CfgMgr32.dll", EntryPoint = "CM_Get_Device_Interface_ListW", ExactSpelling = true, CharSet = CharSet.Unicode)]
    internal static extern uint CM_Get_Device_Interface_List(
        ref Guid interfaceClassGuid,
        string? deviceInstanceId,
        [Out] char[] buffer,
        uint bufferLength,
        uint flags);

    [DllImport("user32.dll")]
    internal static extern int SetDisplayConfig(
        uint numPathArrayElements,
        nint pathArray,
        uint numModeInfoArrayElements,
        nint modeInfoArray,
        uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumDisplayDevices(
        string? lpDevice,
        uint iDevNum,
        ref DisplayDevice lpDisplayDevice,
        uint dwFlags);

    [StructLayout(LayoutKind.Sequential)]
    internal struct Luid
    {
        internal uint LowPart;
        internal int HighPart;

        public override readonly string ToString() => $"{HighPart:x8}:{LowPart:x8}";
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DisplayConfigPathSourceInfo
    {
        internal Luid AdapterId;
        internal uint Id;
        internal uint ModeInfoIdx;
        internal uint StatusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DisplayConfigRational
    {
        internal uint Numerator;
        internal uint Denominator;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DisplayConfigPathTargetInfo
    {
        internal Luid AdapterId;
        internal uint Id;
        internal uint ModeInfoIdx;
        internal uint OutputTechnology;
        internal uint Rotation;
        internal uint Scaling;
        internal DisplayConfigRational RefreshRate;
        internal uint ScanLineOrdering;

        [MarshalAs(UnmanagedType.Bool)]
        internal bool TargetAvailable;

        internal uint StatusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DisplayConfigPathInfo
    {
        internal DisplayConfigPathSourceInfo SourceInfo;
        internal DisplayConfigPathTargetInfo TargetInfo;
        internal uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DisplayConfig2DRegion
    {
        internal uint Cx;
        internal uint Cy;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PointL
    {
        internal int X;
        internal int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RectL
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DisplayConfigVideoSignalInfo
    {
        internal ulong PixelRate;
        internal DisplayConfigRational HSyncFreq;
        internal DisplayConfigRational VSyncFreq;
        internal DisplayConfig2DRegion ActiveSize;
        internal DisplayConfig2DRegion TotalSize;
        internal uint VideoStandard;
        internal uint ScanLineOrdering;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DisplayConfigTargetMode
    {
        internal DisplayConfigVideoSignalInfo TargetVideoSignalInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DisplayConfigSourceMode
    {
        internal uint Width;
        internal uint Height;
        internal uint PixelFormat;
        internal PointL Position;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DisplayConfigDesktopImageInfo
    {
        internal PointL PathSourceSize;
        internal RectL DesktopImageRegion;
        internal RectL DesktopImageClip;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct DisplayConfigModeInfo
    {
        [FieldOffset(0)] internal uint InfoType;
        [FieldOffset(4)] internal uint Id;
        [FieldOffset(8)] internal Luid AdapterId;
        [FieldOffset(16)] internal DisplayConfigTargetMode TargetMode;
        [FieldOffset(16)] internal DisplayConfigSourceMode SourceMode;
        [FieldOffset(16)] internal DisplayConfigDesktopImageInfo DesktopImageInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DisplayConfigDeviceInfoHeader
    {
        internal uint Type;
        internal uint Size;
        internal Luid AdapterId;
        internal uint Id;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DisplayConfigTargetDeviceName
    {
        internal DisplayConfigDeviceInfoHeader Header;
        internal uint Flags;
        internal uint OutputTechnology;
        internal ushort EdidManufactureId;
        internal ushort EdidProductCodeId;
        internal uint ConnectorInstance;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        internal string MonitorFriendlyDeviceName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        internal string MonitorDevicePath;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DisplayConfigAdapterName
    {
        internal DisplayConfigDeviceInfoHeader Header;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        internal string AdapterDevicePath;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DisplayDevice
    {
        internal int Cb;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        internal string DeviceName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        internal string DeviceString;

        internal uint StateFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        internal string DeviceId;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        internal string DeviceKey;
    }
}

