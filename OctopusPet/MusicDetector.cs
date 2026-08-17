using System;
using System.Runtime.InteropServices;

namespace OctopusPet;

/// <summary>
/// 通过 CoreAudio（MMDevice API）检测默认渲染设备的当前输出峰值音量（0~1）。
/// Windows 通常会在插入耳机时自动切换默认设备，因此扬声器和耳机都能被检测到。
/// </summary>
public sealed class MusicDetector : IDisposable
{
    private readonly IMMDeviceEnumerator? _enumerator;
    private readonly IMMDevice? _device;
    private readonly IAudioMeterInformation? _meter;
    private readonly bool _initFailed;

    public MusicDetector()
    {
        try
        {
            _enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            _enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eConsole, out var dev);
            _device = dev;

            // 记录默认设备名称，帮助诊断耳机问题
            string deviceName = GetDeviceName(dev);
            App.Log($"MusicDetector: default audio device = '{deviceName}'");

            var iid = typeof(IAudioMeterInformation).GUID;
            dev.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out var ptr);
            _meter = (IAudioMeterInformation)Marshal.GetObjectForIUnknown(ptr);
        }
        catch (Exception ex)
        {
            _initFailed = true;
            App.Log("MusicDetector init failed: " + ex.Message);
        }
    }

    private static string GetDeviceName(IMMDevice device)
    {
        try
        {
            device.OpenPropertyStore(0, out IntPtr storePtr);
            if (storePtr != IntPtr.Zero)
            {
                var store = (IPropertyStore)Marshal.GetObjectForIUnknown(storePtr);
                Marshal.ReleaseComObject(storePtr);

                var pKey = PKEY_Device_FriendlyName.Value;
                PropVariantInitClear.PropVariantInit(out var prop);
                store.GetValue(ref pKey, out prop);

                string name = "Unknown";
                if (prop.pwszVal != IntPtr.Zero)
                {
                    name = Marshal.PtrToStringUni(prop.pwszVal) ?? "Unknown";
                }

                PropVariantInitClear.PropVariantClear(ref prop);
                Marshal.ReleaseComObject(store);
                return name;
            }
        }
        catch { }
        return "Unknown";
    }

    private const uint CLSCTX_ALL = 0x17;

    /// <summary>当前输出峰值音量（0~1）；检测不可用或失败时返回 0。</summary>
    public float GetPeak()
    {
        if (_initFailed || _meter == null) return 0f;
        try
        {
            _meter.GetPeakValue(out float peak);
            return peak;
        }
        catch
        {
            return 0f;
        }
    }

    public void Dispose()
    {
        if (_meter != null) Marshal.ReleaseComObject(_meter);
        if (_device != null) Marshal.ReleaseComObject(_device);
        if (_enumerator != null) Marshal.ReleaseComObject(_enumerator);
    }
}

[ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
internal class MMDeviceEnumeratorComObject { }

[ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    int EnumAudioEndpoints(EDataFlow dataFlow, uint dwStateMask, out IMMDevice ppDevices);
    int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppEndpoint);
    int GetDevice(string pwstrId, out IMMDevice ppDevice);
    int RegisterEndpointNotificationCallback(IntPtr pClient);
    int UnregisterEndpointNotificationCallback(IntPtr pClient);
}

[ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    int Activate(ref Guid iid, uint dwClsCtx, IntPtr pActivationParams, out IntPtr ppInterface);
    int OpenPropertyStore(uint stgmAccess, out IntPtr ppProperties);
    int GetId(out IntPtr ppstrId);
    int GetState(out uint pdwState);
}

[ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioMeterInformation
{
    int GetPeakValue(out float pfPeak);
    int GetMeteringChannelCount(out uint pnChannelCount);
    int GetChannelsPeakValues(uint u32ChannelCount, [In, MarshalAs(UnmanagedType.LPArray)] float[] afPeakValues);
    int QueryHardwareSupport(out uint pdwHardwareSupportMask);
}

[ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPropertyStore
{
    int GetCount(out uint cProps);
    int GetAt(uint iProp, out PropertyKey pkey);
    int GetValue(ref PropertyKey key, out PropVariant pv);
    int SetValue(ref PropertyKey key, ref PropVariant propvar);
    int Commit();
}

internal enum EDataFlow { eRender = 0, eCapture, eAll }
internal enum ERole { eConsole = 0, eMultimedia, eCommunications }

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct PropertyKey
{
    public Guid fmtid;
    public uint pid;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct PropVariant
{
    public ushort vt;
    public ushort wReserved1;
    public ushort wReserved2;
    public ushort wReserved3;
    public IntPtr pwszVal;
}

internal static class PropVariantInitClear
{
    [DllImport("ole32.dll")]
    public static extern int PropVariantInit(out PropVariant pvar);

    [DllImport("ole32.dll")]
    public static extern int PropVariantClear(ref PropVariant pvar);
}

internal static class PKEY_Device_FriendlyName
{
    private static readonly Guid PKEY_Device_FriendlyName_Guid = new("A45C254E-DF1C-4EFD-8020-67D146A850E0");
    private const uint PKEY_Device_FriendlyName_Pid = 14;

    public static PropertyKey Value => new PropertyKey
    {
        fmtid = PKEY_Device_FriendlyName_Guid,
        pid = PKEY_Device_FriendlyName_Pid
    };
}
