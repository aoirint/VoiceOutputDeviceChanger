using System;
using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;

namespace VoiceOutputDeviceChanger.Interop.WindowsAudio;

internal enum AudioClientShareMode
{
    Shared = 0,
    Exclusive = 1,
}

[Flags]
internal enum AudioClientStreamFlags : uint
{
    EventCallback = 0x00040000,
    SourceDefaultQuality = 0x08000000,
    AutoConvertPcm = 0x80000000,
}

[Flags]
internal enum AudioClientBufferFlags : uint
{
    None = 0,
    Silent = 0x00000002,
}

internal enum DataFlow
{
    Render = 0,
}

internal enum Role
{
    Multimedia = 1,
}

[Flags]
internal enum DeviceState : uint
{
    Active = 0x00000001,
}

[Flags]
internal enum ClassContext : uint
{
    InProcessServer = 0x1,
    InProcessHandler = 0x2,
    LocalServer = 0x4,
    All = InProcessServer | InProcessHandler | LocalServer,
}

[StructLayout(LayoutKind.Sequential)]
internal struct WaveFormat
{
    public ushort FormatTag;
    public ushort Channels;
    public uint SamplesPerSecond;
    public uint AverageBytesPerSecond;
    public ushort BlockAlign;
    public ushort BitsPerSample;
    public ushort ExtraSize;

    public static WaveFormat CreateFloatStereo(int sampleRate)
    {
        const ushort channels = 2;
        const ushort bitsPerSample = 32;
        ushort blockAlign = (ushort)(channels * (bitsPerSample / 8));
        return new WaveFormat
        {
            FormatTag = 3,
            Channels = channels,
            SamplesPerSecond = checked((uint)sampleRate),
            AverageBytesPerSecond = checked((uint)(sampleRate * blockAlign)),
            BlockAlign = blockAlign,
            BitsPerSample = bitsPerSample,
            ExtraSize = 0,
        };
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct PropertyKey
{
    public Guid FormatId;
    public uint PropertyId;

    public PropertyKey(Guid formatId, uint propertyId)
    {
        FormatId = formatId;
        PropertyId = propertyId;
    }
}

[StructLayout(LayoutKind.Explicit)]
internal struct PropVariant
{
    [FieldOffset(0)]
    private readonly ushort _variantType;

    [FieldOffset(8)]
    private readonly IntPtr _pointerValue;

    public string GetString()
    {
        const ushort wideString = 31;
        return _variantType == wideString && _pointerValue != IntPtr.Zero
            ? Marshal.PtrToStringUni(_pointerValue) ?? string.Empty
            : string.Empty;
    }
}

[ComImport]
[Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
[ClassInterface(ClassInterfaceType.None)]
internal sealed class MmDeviceEnumeratorComObject
{
}

[ComImport]
[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMmDeviceEnumerator
{
    [PreserveSig]
    int EnumAudioEndpoints(DataFlow dataFlow, DeviceState stateMask, out IMmDeviceCollection devices);

    [PreserveSig]
    int GetDefaultAudioEndpoint(DataFlow dataFlow, Role role, out IMmDevice device);

    [PreserveSig]
    int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMmDevice device);

    [PreserveSig]
    int RegisterEndpointNotificationCallback(IntPtr client);

    [PreserveSig]
    int UnregisterEndpointNotificationCallback(IntPtr client);
}

[ComImport]
[Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMmDeviceCollection
{
    [PreserveSig]
    int GetCount(out uint count);

    [PreserveSig]
    int Item(uint index, out IMmDevice device);
}

[ComImport]
[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMmDevice
{
    [PreserveSig]
    int Activate(ref Guid interfaceId, ClassContext classContext, IntPtr activationParameters, [MarshalAs(UnmanagedType.IUnknown)] out object instance);

    [PreserveSig]
    int OpenPropertyStore(uint accessMode, out IPropertyStore properties);

    [PreserveSig]
    int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);

    [PreserveSig]
    int GetState(out DeviceState state);
}

[ComImport]
[Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPropertyStore
{
    [PreserveSig]
    int GetCount(out uint count);

    [PreserveSig]
    int GetAt(uint index, out PropertyKey key);

    [PreserveSig]
    int GetValue(ref PropertyKey key, out PropVariant value);

    [PreserveSig]
    int SetValue(ref PropertyKey key, ref PropVariant value);

    [PreserveSig]
    int Commit();
}

[ComImport]
[Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioClient
{
    [PreserveSig]
    int Initialize(AudioClientShareMode shareMode, AudioClientStreamFlags streamFlags, long bufferDuration, long periodicity, IntPtr format, IntPtr audioSessionGuid);

    [PreserveSig]
    int GetBufferSize(out uint bufferFrameCount);

    [PreserveSig]
    int GetStreamLatency(out long latency);

    [PreserveSig]
    int GetCurrentPadding(out uint currentPaddingFrames);

    [PreserveSig]
    int IsFormatSupported(AudioClientShareMode shareMode, IntPtr format, out IntPtr closestMatch);

    [PreserveSig]
    int GetMixFormat(out IntPtr deviceFormat);

    [PreserveSig]
    int GetDevicePeriod(out long defaultPeriod, out long minimumPeriod);

    [PreserveSig]
    int Start();

    [PreserveSig]
    int Stop();

    [PreserveSig]
    int Reset();

    [PreserveSig]
    int SetEventHandle(IntPtr eventHandle);

    [PreserveSig]
    int GetService(ref Guid interfaceId, [MarshalAs(UnmanagedType.IUnknown)] out object service);
}

[ComImport]
[Guid("F294ACFC-3146-4483-A7BF-ADDCA7C260E2")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioRenderClient
{
    [PreserveSig]
    int GetBuffer(uint requestedFrames, out IntPtr data);

    [PreserveSig]
    int ReleaseBuffer(uint writtenFrames, AudioClientBufferFlags flags);
}

internal static class CoreAudioNative
{
    public const int RpcChangedMode = unchecked((int)0x80010106);

    [DllImport("ole32.dll")]
    public static extern int CoInitializeEx(IntPtr reserved, uint coInitialize);

    [DllImport("ole32.dll")]
    public static extern void CoUninitialize();

    [DllImport("ole32.dll")]
    public static extern int PropVariantClear(ref PropVariant value);
}

internal readonly struct ComApartmentScope : IDisposable
{
    private readonly bool _ownsInitialization;

    private ComApartmentScope(bool ownsInitialization)
    {
        _ownsInitialization = ownsInitialization;
    }

    public static ComApartmentScope EnterMultithreaded()
    {
        const uint multithreaded = 0;
        int result = CoreAudioNative.CoInitializeEx(IntPtr.Zero, multithreaded);
        if (result < 0 && result != CoreAudioNative.RpcChangedMode)
        {
            Marshal.ThrowExceptionForHR(result);
        }

        return new ComApartmentScope(result == 0 || result == 1);
    }

    public void Dispose()
    {
        if (_ownsInitialization)
        {
            CoreAudioNative.CoUninitialize();
        }
    }
}

internal static class ComInterop
{
    public static void ThrowIfFailed(int result)
    {
        if (result < 0)
        {
            Marshal.ThrowExceptionForHR(result);
        }
    }

    public static void Release(object? instance)
    {
        if (instance is not null && Marshal.IsComObject(instance))
        {
            Marshal.FinalReleaseComObject(instance);
        }
    }

    public static void ReleaseAll(params object?[] instances)
    {
        Exception? firstFailure = null;
        foreach (object? instance in instances)
        {
            try
            {
                Release(instance);
            }
            catch (Exception exception)
            {
                firstFailure ??= exception;
            }
        }

        if (firstFailure is not null)
        {
            ExceptionDispatchInfo.Capture(firstFailure).Throw();
        }
    }
}
