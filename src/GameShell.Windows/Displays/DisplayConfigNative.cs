using System.ComponentModel;
using System.Runtime.InteropServices;

namespace GameShell.Windows.Displays;

internal static class DisplayConfigNative
{
    public const uint DigcfPresent = 0x00000002;
    public const uint DigcfDeviceInterface = 0x00000010;
    public const uint CmLocateDevnodeNormal = 0;
    public const uint CmReenumerateSynch = 0x00000000;
    public const uint CmDisableUiNotOk = 0x00000002;
    public const int CrSuccess = 0;
    public const int CrAccessDenied = 0x00000033;
    public const int CrNoSuchDevnode = 0x0000000D;
    public const int CrBufferSmall = 0x0000001A;
    public static readonly Guid GuidDevinterfaceMonitor = new("e6f07b5f-ee97-4a90-b076-33f57bf4eaa7");
    public const uint QdcOnlyActivePaths = 0x00000002;
    public const uint QdcAllPaths = 0x00000001;
    public const uint QdcDatabaseCurrent = 0x00000004;
    public const uint SdcApply = 0x00000080;
    public const uint SdcValidate = 0x00000040;
    public const uint SdcUseSuppliedDisplayConfig = 0x00000020;
    public const uint SdcSaveToDatabase = 0x00000200;
    public const uint SdcAllowChanges = 0x00000400;
    public const uint DisplayConfigPathActive = 0x00000001;
    public const uint DisplayConfigPathModeIdxInvalid = 0xFFFFFFFF;
    public const int ErrorSuccess = 0;
    public const int ErrorInvalidParameter = 87;
    public const int ErrorInsufficientBuffer = 122;
    public const int CchDeviceName = 32;
    public const int CchFriendlyName = 64;
    public const int CchDevicePath = 128;
    public const uint EnumCurrentSettings = 0xFFFFFFFF;
    public const uint CdsUpdateRegistry = 0x00000001;
    public const int DispChangeSuccessful = 0;
    public const uint DmBitsPerPel = 0x00040000;
    public const uint DmPelsWidth = 0x00080000;
    public const uint DmPelsHeight = 0x00100000;
    public const uint DmDisplayFrequency = 0x00400000;

    [DllImport("user32.dll")]
    public static extern int GetDisplayConfigBufferSizes(
        uint flags,
        out uint numPathArrayElements,
        out uint numModeInfoArrayElements);

    [DllImport("user32.dll")]
    public static extern int QueryDisplayConfig(
        uint flags,
        ref uint numPathArrayElements,
        [Out] DisplayConfigPathInfo[] pathArray,
        ref uint numModeInfoArrayElements,
        [Out] DisplayConfigModeInfo[] modeInfoArray,
        IntPtr currentTopologyId);

    [DllImport("user32.dll")]
    public static extern int SetDisplayConfig(
        uint numPathArrayElements,
        [In] DisplayConfigPathInfo[] pathArray,
        uint numModeInfoArrayElements,
        [In] DisplayConfigModeInfo[] modeInfoArray,
        uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int DisplayConfigGetDeviceInfo(ref DisplayConfigSourceDeviceName requestPacket);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int DisplayConfigGetDeviceInfo(ref DisplayConfigTargetDeviceName requestPacket);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int DisplayConfigGetDeviceInfo(ref DisplayConfigTargetPreferredMode requestPacket);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool EnumDisplayDevices(
        string? lpDevice,
        uint iDevNum,
        ref DisplayDevice lpDisplayDevice,
        uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool EnumDisplaySettings(
        string? lpszDeviceName,
        uint iModeNum,
        ref DevMode lpDevMode);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int ChangeDisplaySettingsEx(
        string? lpszDeviceName,
        ref DevMode lpDevMode,
        IntPtr hwnd,
        uint dwflags,
        IntPtr lParam);

    [DllImport("setupapi.dll", SetLastError = true)]
    public static extern IntPtr SetupDiGetClassDevs(
        ref Guid classGuid,
        string? enumerator,
        IntPtr hwndParent,
        uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    public static extern bool SetupDiEnumDeviceInterfaces(
        IntPtr deviceInfoSet,
        IntPtr deviceInfoData,
        ref Guid interfaceClassGuid,
        uint memberIndex,
        IntPtr deviceInterfaceData);

    [DllImport("setupapi.dll", SetLastError = true)]
    public static extern bool SetupDiGetDeviceInterfaceDetail(
        IntPtr deviceInfoSet,
        IntPtr deviceInterfaceData,
        IntPtr deviceInterfaceDetailData,
        uint deviceInterfaceDetailDataSize,
        out uint requiredSize,
        IntPtr deviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true)]
    public static extern bool SetupDiGetDeviceInterfaceDetail(
        IntPtr deviceInfoSet,
        IntPtr deviceInterfaceData,
        IntPtr deviceInterfaceDetailData,
        uint deviceInterfaceDetailDataSize,
        out uint requiredSize,
        ref SetupDeviceInfoData deviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true)]
    public static extern bool SetupDiGetDeviceInstanceId(
        IntPtr deviceInfoSet,
        ref SetupDeviceInfoData deviceInfoData,
        [Out] char[] deviceInstanceId,
        uint deviceInstanceIdSize,
        out uint requiredSize);

    [DllImport("setupapi.dll", SetLastError = true)]
    public static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    public static extern int CM_Locate_DevNode(
        out uint devInst,
        string deviceInstanceId,
        uint flags);

    [DllImport("cfgmgr32.dll")]
    public static extern int CM_Reenumerate_DevNode(uint devInst, uint flags);

    [DllImport("cfgmgr32.dll")]
    public static extern int CM_Disable_DevNode(uint devInst, uint flags);

    [DllImport("cfgmgr32.dll")]
    public static extern int CM_Enable_DevNode(uint devInst, uint flags);

    [DllImport("cfgmgr32.dll")]
    public static extern int CM_Get_Parent(out uint parentDevInst, uint devInst, uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    public static extern int CM_Get_Device_ID(
        uint devInst,
        [Out] System.Text.StringBuilder buffer,
        uint bufferLength,
        uint flags);

    public static void ThrowIfWin32Error(int error, string operation)
    {
        if (error != ErrorSuccess)
        {
            throw new Win32Exception(error, $"{operation} failed with Win32 error {error}.");
        }
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct SetupDeviceInfoData
{
    public uint Size;
    public Guid ClassGuid;
    public uint DevInst;
    public IntPtr Reserved;

    public static SetupDeviceInfoData Create() => new()
    {
        Size = (uint)Marshal.SizeOf<SetupDeviceInfoData>()
    };
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct DevMode
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string DeviceName;

    public ushort SpecVersion;
    public ushort DriverVersion;
    public ushort Size;
    public ushort DriverExtra;
    public uint Fields;
    public int PositionX;
    public int PositionY;
    public int DisplayOrientation;
    public int DisplayFixedOutput;
    public short Color;
    public short Duplex;
    public short YResolution;
    public short TTOption;
    public short Collate;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string FormName;

    public ushort LogPixels;
    public uint BitsPerPel;
    public uint PelsWidth;
    public uint PelsHeight;
    public uint DisplayFlags;
    public uint DisplayFrequency;
    public uint ICMMethod;
    public uint ICMIntent;
    public uint MediaType;
    public uint DitherType;
    public uint Reserved1;
    public uint Reserved2;
    public uint PanningWidth;
    public uint PanningHeight;

    public static DevMode Create()
    {
        return new DevMode
        {
            DeviceName = string.Empty,
            FormName = string.Empty,
            Size = checked((ushort)Marshal.SizeOf<DevMode>())
        };
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct Luid
{
    public uint LowPart;
    public int HighPart;

    public readonly long ToInt64() => ((long)HighPart << 32) | LowPart;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigPathInfo
{
    public DisplayConfigPathSourceInfo SourceInfo;
    public DisplayConfigPathTargetInfo TargetInfo;
    public uint Flags;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigPathSourceInfo
{
    public Luid AdapterId;
    public uint Id;
    public uint ModeInfoIdx;
    public uint StatusFlags;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigPathTargetInfo
{
    public Luid AdapterId;
    public uint Id;
    public uint ModeInfoIdx;
    public DisplayConfigVideoOutputTechnology OutputTechnology;
    public DisplayConfigRotation Rotation;
    public DisplayConfigScaling Scaling;
    public DisplayConfigRational RefreshRate;
    public DisplayConfigScanLineOrdering ScanLineOrdering;
    public bool TargetAvailable;
    public uint StatusFlags;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigRational
{
    public uint Numerator;
    public uint Denominator;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigModeInfo
{
    public DisplayConfigModeInfoType InfoType;
    public uint Id;
    public Luid AdapterId;
    public DisplayConfigModeInfoUnion ModeInfo;
}

[StructLayout(LayoutKind.Explicit)]
internal struct DisplayConfigModeInfoUnion
{
    [FieldOffset(0)]
    public DisplayConfigTargetMode TargetMode;

    [FieldOffset(0)]
    public DisplayConfigSourceMode SourceMode;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigTargetMode
{
    public DisplayConfigVideoSignalInfo TargetVideoSignalInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigVideoSignalInfo
{
    public ulong PixelRate;
    public DisplayConfigRational HSyncFreq;
    public DisplayConfigRational VSyncFreq;
    public DisplayConfig2DRegion ActiveSize;
    public DisplayConfig2DRegion TotalSize;
    public uint VideoStandard;
    public DisplayConfigScanLineOrdering ScanLineOrdering;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfig2DRegion
{
    public uint Cx;
    public uint Cy;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigSourceMode
{
    public uint Width;
    public uint Height;
    public DisplayConfigPixelFormat PixelFormat;
    public PointL Position;
}

[StructLayout(LayoutKind.Sequential)]
internal struct PointL
{
    public int X;
    public int Y;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigDeviceInfoHeader
{
    public DisplayConfigDeviceInfoType Type;
    public uint Size;
    public Luid AdapterId;
    public uint Id;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct DisplayConfigSourceDeviceName
{
    public DisplayConfigDeviceInfoHeader Header;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = DisplayConfigNative.CchDeviceName)]
    public string ViewGdiDeviceName;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct DisplayConfigTargetDeviceName
{
    public DisplayConfigDeviceInfoHeader Header;
    public DisplayConfigTargetDeviceNameFlags Flags;
    public DisplayConfigVideoOutputTechnology OutputTechnology;
    public ushort EdidManufactureId;
    public ushort EdidProductCodeId;
    public uint ConnectorInstance;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = DisplayConfigNative.CchFriendlyName)]
    public string MonitorFriendlyDeviceName;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = DisplayConfigNative.CchDevicePath)]
    public string MonitorDevicePath;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigTargetPreferredMode
{
    public DisplayConfigDeviceInfoHeader Header;
    public uint Width;
    public uint Height;
    public DisplayConfigTargetMode TargetMode;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigTargetDeviceNameFlags
{
    public uint Value;

    public readonly bool FriendlyNameFromEdid => (Value & 0x1) != 0;
    public readonly bool FriendlyNameForced => (Value & 0x2) != 0;
    public readonly bool EdidIdsValid => (Value & 0x4) != 0;
}

internal enum DisplayConfigDeviceInfoType : uint
{
    GetSourceName = 1,
    GetTargetName = 2,
    GetTargetPreferredMode = 3
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct DisplayDevice
{
    public uint Cb;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string DeviceName;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string DeviceString;

    public DisplayDeviceStateFlags StateFlags;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string DeviceId;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string DeviceKey;
}

[Flags]
internal enum DisplayDeviceStateFlags : uint
{
    AttachedToDesktop = 0x00000001,
    MultiDriver = 0x00000002,
    PrimaryDevice = 0x00000004,
    MirroringDriver = 0x00000008,
    VgaCompatible = 0x00000010,
    Removable = 0x00000020,
    ModesPruned = 0x08000000,
    Remote = 0x04000000,
    Disconnect = 0x02000000
}

internal enum DisplayConfigModeInfoType : uint
{
    Source = 1,
    Target = 2,
    DesktopImage = 3
}

internal enum DisplayConfigVideoOutputTechnology : uint
{
    Other = 0xFFFFFFFF,
    Hd15 = 0,
    Svideo = 1,
    CompositeVideo = 2,
    ComponentVideo = 3,
    Dvi = 4,
    Hdmi = 5,
    Lvds = 6,
    DJpn = 8,
    Sdi = 9,
    DisplayPortExternal = 10,
    DisplayPortEmbedded = 11,
    UdiExternal = 12,
    UdiEmbedded = 13,
    Sdtvdongle = 14,
    Miracast = 15,
    IndirectWired = 16,
    Internal = 0x80000000,
    ForceUint32 = 0xFFFFFFFF
}

internal enum DisplayConfigRotation : uint
{
    Identity = 1,
    Rotate90 = 2,
    Rotate180 = 3,
    Rotate270 = 4,
    ForceUint32 = 0xFFFFFFFF
}

internal enum DisplayConfigScaling : uint
{
    Identity = 1,
    Centered = 2,
    Stretched = 3,
    AspectRatioCenteredMax = 4,
    Custom = 5,
    Preferred = 128,
    ForceUint32 = 0xFFFFFFFF
}

internal enum DisplayConfigScanLineOrdering : uint
{
    Unspecified = 0,
    Progressive = 1,
    Interlaced = 2,
    InterlacedUpperFieldFirst = Interlaced,
    InterlacedLowerFieldFirst = 3,
    ForceUint32 = 0xFFFFFFFF
}

internal enum DisplayConfigPixelFormat : uint
{
    BitsPerPixel8 = 1,
    BitsPerPixel16 = 2,
    BitsPerPixel24 = 3,
    BitsPerPixel32 = 4,
    NonGdi = 5,
    ForceUint32 = 0xffffffff
}
