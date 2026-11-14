using System.Runtime.InteropServices;
using Koy.Core.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Media.Audio;
using Windows.Win32.Media.KernelStreaming;
using Windows.Win32.System.Com;
using Windows.Win32.System.Com.StructuredStorage;
using Windows.Win32.UI.Shell.PropertiesSystem;

namespace Koy.Platform.Bluetooth;

/// <summary>
/// Bluetooth ses cihazına bağlan / bağlantıyı kes. Windows'ta genel bir API yok; belgelenmiş yol, cihazın ses uç
/// noktasının kernel streaming filtresine <c>KSPROPSETID_BtAudio</c> kümesindeki <c>KSPROPERTY_ONESHOT_RECONNECT</c> /
/// <c>KSPROPERTY_ONESHOT_DISCONNECT</c> özelliğini <c>IKsControl</c> ile göndermek. Başarılı dönüş yalnız sürücünün
/// isteği aldığını gösterir; sonuç bağlantı durumundan okunmalıdır (bunu <see cref="BluetoothService"/> yapar).
/// </summary>
internal static unsafe class BluetoothAudioKs
{
    public enum Operation : uint
    {
        Reconnect = 0,
        Disconnect = 1
    }

    // KSPROPSETID_BtAudio
    private static readonly Guid BtAudioSet = new("7FA06C40-B8F6-4C7E-8556-E8C33A12E54D");
    private static readonly Guid ClsidMMDeviceEnumerator = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    private static readonly Guid IidDeviceTopology = new("2A07407E-6497-4A18-9787-32F79BD0D98F");
    private static readonly Guid IidKsControl = new("28F54685-06FD-11D2-B27A-00A0C9223196");

    // PKEY_Device_ContainerId
    private static readonly PROPERTYKEY ContainerIdKey = new()
    {
        fmtid = new Guid("8C7ED206-3F8A-4827-B3AB-AE9E1FAEFC6C"),
        pid = 2
    };

    private const uint KsPropertyTypeGet = 0x1;

    // KSPROPERTY: Set (GUID), Id, Flags. Üretilen birleşim tipinin yerine aynı bellek düzeni.
    [StructLayout(LayoutKind.Sequential)]
    private struct KsPropertyBlock
    {
        public Guid Set;
        public uint Id;
        public uint Flags;
    }

    /// <summary>
    /// Kapsayıcıya (ContainerId) ait ses uç noktalarına isteği gönderir. En az bir uç nokta isteği kabul ettiyse true;
    /// uç nokta bulunamadıysa ya da hiçbiri kabul etmediyse false. İstisna fırlatmaz.
    /// </summary>
    public static bool TrySend(string containerId, Operation operation)
    {
        if (!Guid.TryParse(containerId, out var container))
            return false;

        var sent = false;
        object? enumeratorObj = null;
        try
        {
            var type = Type.GetTypeFromCLSID(ClsidMMDeviceEnumerator);
            if (type is null)
                return false;

            enumeratorObj = Activator.CreateInstance(type);
            if (enumeratorObj is not IMMDeviceEnumerator enumerator)
                return false;

            foreach (var flow in new[] { EDataFlow.eRender, EDataFlow.eCapture })
            {
                enumerator.EnumAudioEndpoints(flow, (DEVICE_STATE)0xF, out var devices);
                try
                {
                    devices.GetCount(out var count);
                    for (uint i = 0; i < count; i++)
                    {
                        devices.Item(i, out var device);
                        try
                        {
                            if (BelongsTo(device, container))
                                sent |= SendToEndpoint(device, operation);
                        }
                        finally
                        {
                            Marshal.ReleaseComObject(device);
                        }
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(devices);
                }
            }
        }
        catch (Exception ex)
        {
            CrashLog.Write("BluetoothAudioKs.TrySend", ex);
            return false;
        }
        finally
        {
            if (enumeratorObj is not null)
                Marshal.ReleaseComObject(enumeratorObj);
        }

        return sent;
    }

    private static bool BelongsTo(IMMDevice device, Guid container)
    {
        try
        {
            device.OpenPropertyStore(STGM.STGM_READ, out var store);
            try
            {
                var key = ContainerIdKey;
                PROPVARIANT value = default;
                store.GetValue(&key, out value);
                try
                {
                    Guid id = default;
                    return PInvoke.PropVariantToGUID(value, &id).Succeeded && id == container;
                }
                finally
                {
                    PInvoke.PropVariantClear(ref value);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(store);
            }
        }
        catch
        {
            return false;
        }
    }

    private static bool SendToEndpoint(IMMDevice device, Operation operation)
    {
        object? topologyObj = null;
        IConnector? connector = null;
        IConnector? peer = null;
        object? controlObj = null;
        try
        {
            var topologyIid = IidDeviceTopology;
            device.Activate(&topologyIid, CLSCTX.CLSCTX_ALL, null, out topologyObj);
            if (topologyObj is not IDeviceTopology topology)
                return false;

            topology.GetConnector(0, out connector);
            connector.GetConnectedTo(out peer);
            if (peer is not IPart part)
                return false;

            var controlIid = IidKsControl;
            part.Activate((uint)CLSCTX.CLSCTX_ALL, &controlIid, out var controlPtr);
            if (controlPtr is null)
                return false;

            controlObj = Marshal.GetObjectForIUnknown((nint)controlPtr);
            Marshal.Release((nint)controlPtr);
            if (controlObj is not IKsControl control)
                return false;

            var property = new KsPropertyBlock { Set = BtAudioSet, Id = (uint)operation, Flags = KsPropertyTypeGet };
            uint returned = 0;
            control.KsProperty((KSIDENTIFIER*)&property, (uint)sizeof(KsPropertyBlock), null, 0, out returned);
            return true;
        }
        catch
        {
            // Uç nokta bağlı olmayan cihazda topoloji açılamayabilir; bu uç nokta atlanır.
            return false;
        }
        finally
        {
            foreach (var com in new object?[] { controlObj, peer, connector, topologyObj })
            {
                if (com is not null && Marshal.IsComObject(com))
                    Marshal.ReleaseComObject(com);
            }
        }
    }
}
