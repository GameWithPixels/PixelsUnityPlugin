using System;
using System.Collections.Generic;
using System.Linq;
using Systemic.Pixels.Unity.BluetoothLE.Internal;

namespace Systemic.Pixels.Unity.BluetoothLE
{
    public enum BluetoothStatus
    {
        Disabled,
        Enabled,
    }

    // Must match C++ enum Pixels::CoreBluetoothLE::ConnectionEvent and Objective-C PXBlePeripheralConnectionEvent
    public enum ConnectionEvent
    {
        // Raised at the beginning of the connect sequence, will be followed either by Connected or FailedToConnect
        Connecting,

        // Raised once the peripheral is connected, at which point service discovery is triggered
        Connected,

        // Raised when the peripheral fails to connect, the reason of failure is also given
        FailedToConnect,

        // Raised after a Connected event, once the required services have been discovered
        Ready,

        // Raised at the beginning of a user initiated disconnect
        Disconnecting,

        // Raised when the peripheral is disconnected, the reason for the connection loss is also given
        Disconnected,
    }

    // Must match C++ enum Pixels::CoreBluetoothLE::ConnectionEventReason and Objective-C PXBlePeripheralConnectionEventReason
    public enum ConnectionEventReason
    {
        // The disconnect happened for an unknown reason
        Unknown = -1,

        // The disconnect was initiated by user
        Success = 0,

        // Connection attempt canceled by user
        Canceled,

        // Peripheral does not have all required services
        NotSupported,

        // Peripheral didn't responded in time
        Timeout,

        // Peripheral was disconnected while in "auto connect" mode
        LinkLoss,

        // The local device Bluetooth adapter is off
        AdpaterOff,

        // Disconnection was initiated by peripheral
        Peripheral,
    }

    // Must match C++ enum Pixels::CoreBluetoothLE::BleRequestStatus
    public enum RequestStatus
    {
        Success,
        Error, // Generic error
        InProgress,
        Canceled,
        Disconnected,
        InvalidPeripheral,
        InvalidCall,
        InvalidParameters,
        NotSupported,
        ProtocolError,
        AccessDenied,
        AdpaterOff,
        Timeout,
    }

    public delegate void NativeBluetoothEventHandler(BluetoothStatus status);
    public delegate void NativePeripheralConnectionEventHandler(ConnectionEvent connectionEvent, ConnectionEventReason reason);
    public delegate void NativePeripheralCreatedHandler(PeripheralHandle peripheralHandle);
    public delegate void NativeRequestResultHandler(RequestStatus status);
    public delegate void NativeValueRequestResultHandler<T>(T value, RequestStatus status);
    public delegate void NativeValueChangedHandler(byte[] data, RequestStatus status);

    [Flags]
    public enum CharacteristicProperties : ulong
    {
        None = 0,
        Broadcast = 0x001, // Characteristic is broadcastable
        Read = 0x002, // Characteristic is readable
        WriteWithoutResponse = 0x004, // Characteristic can be written without response
        Write = 0x008, // Characteristic can be written
        Notify = 0x010, // Characteristic supports notification
        Indicate = 0x020, // Characteristic supports indication
        SignedWrite = 0x040, // // Characteristic supports write with signature
        ExtendedProperties = 0x080, // Characteristic has extended properties
        NotifyEncryptionRequired = 0x100,
        IndicateEncryptionRequired = 0x200,
    }

    public class NativeInterface
    {
        public const int MinMtu = 23;
        public const int MaxMtu = 517;

        static INativeInterfaceImpl _impl =
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            new Internal.Windows.WinRTNativeInterfaceImpl();
#elif UNITY_EDITOR_OSX || UNITY_IOS || UNITY_STANDALONE_OSX
            new Internal.Apple.AppleNativeInterfaceImpl();
#elif UNITY_ANDROID
            new Internal.Android.AndroidNativeInterfaceImpl();
#else
            null;
#endif

        public static bool Initialize(NativeBluetoothEventHandler onBluetoothEvent)
        {
            if (onBluetoothEvent == null) throw new ArgumentNullException(nameof(onBluetoothEvent));

            return _impl.Initialize(onBluetoothEvent);
        }

        public static void Shutdown()
        {
            _impl.Shutdown();
        }

        /// <summary>
        /// A new scan replaces any on-going scan
        /// </summary>
        /// <param name="requiredServices">can be null</param>
        public static bool StartScan(IEnumerable<Guid> requiredServices, Action<ScannedPeripheral> onScannedPeripheral)
        {
            if (onScannedPeripheral == null) throw new ArgumentNullException(nameof(onScannedPeripheral));

            SanityCheck();

            return _impl.StartScan(ToString(requiredServices), onScannedPeripheral);
        }

        public static void StopScan()
        {
            _impl.StopScan();
        }

        PeripheralHandle CreatePeripheral(ulong bluetoothAddress, NativePeripheralConnectionEventHandler onConnectionEventChanged)
        {
            if (bluetoothAddress == 0) throw new ArgumentException("Empty bluetooth address", nameof(bluetoothAddress));
            if (onConnectionEventChanged == null) throw new ArgumentNullException(nameof(onConnectionEventChanged));

            SanityCheck();

            return _impl.CreatePeripheral(bluetoothAddress, onConnectionEventChanged);
        }

        public static PeripheralHandle CreatePeripheral(ScannedPeripheral scannedPeripheral, NativePeripheralConnectionEventHandler onConnectionEvent)
        {
            if (scannedPeripheral == null) throw new ArgumentNullException(nameof(scannedPeripheral));
            if (!((IScannedPeripheral)scannedPeripheral).IsValid) throw new ArgumentException("Invalid ScannedPeripheral", nameof(scannedPeripheral));
            if (onConnectionEvent == null) throw new ArgumentNullException(nameof(onConnectionEvent));

            SanityCheck();

            return _impl.CreatePeripheral(scannedPeripheral, onConnectionEvent);
        }

        public static void ReleasePeripheral(PeripheralHandle peripheral)
        {
            SanityCheck();

            if (peripheral.IsValid)
            {
                _impl.ReleasePeripheral(peripheral);
            }
        }

        public static void ConnectPeripheral(PeripheralHandle peripheral, IEnumerable<Guid> requiredServices, bool autoConnect, NativeRequestResultHandler onResult)
        {
            if (!peripheral.IsValid) throw new ArgumentException("Invalid PeripheralHandle", nameof(peripheral));
            if (onResult == null) throw new ArgumentNullException(nameof(onResult));

            SanityCheck();

            _impl.ConnectPeripheral(peripheral, ToString(requiredServices), autoConnect, onResult);
        }

        public static void DisconnectPeripheral(PeripheralHandle peripheral, NativeRequestResultHandler onResult)
        {
            if (!peripheral.IsValid) throw new ArgumentException("Invalid PeripheralHandle", nameof(peripheral));
            if (onResult == null) throw new ArgumentNullException(nameof(onResult));

            SanityCheck();

            _impl.DisconnectPeripheral(peripheral, onResult);
        }

        public static string GetPeripheralName(PeripheralHandle peripheral)
        {
            if (!peripheral.IsValid) throw new ArgumentException("Invalid PeripheralHandle", nameof(peripheral));

            SanityCheck();

            return _impl.GetPeripheralName(peripheral);
        }

        public static int GetPeripheralMtu(PeripheralHandle peripheral)
        {
            if (!peripheral.IsValid) throw new ArgumentException("Invalid PeripheralHandle", nameof(peripheral));

            SanityCheck();

            return _impl.GetPeripheralMtu(peripheral);
        }

        // See MinMTU and MaxMTU, supported on Android only
        public static void RequestPeripheralMtu(PeripheralHandle peripheral, int mtu, NativeValueRequestResultHandler<int> onMtuResult)
        {
            if (!peripheral.IsValid) throw new ArgumentException("Invalid PeripheralHandle", nameof(peripheral));
            if ((mtu < MinMtu) || (mtu > MaxMtu)) throw new ArgumentException($"MTU must be between {MinMtu} and {MaxMtu}", nameof(mtu));
            if (onMtuResult == null) throw new ArgumentNullException(nameof(onMtuResult));

            SanityCheck();

            _impl.RequestPeripheralMtu(peripheral, mtu, onMtuResult);
        }

        // See MinMTU and MaxMTU, supported on Apple and Android
        public static void ReadPeripheralRssi(PeripheralHandle peripheral, NativeValueRequestResultHandler<int> onRssiRead)
        {
            if (!peripheral.IsValid) throw new ArgumentException("Invalid PeripheralHandle", nameof(peripheral));
            if (onRssiRead == null) throw new ArgumentNullException(nameof(onRssiRead));

            SanityCheck();

            _impl.ReadPeripheralRssi(peripheral, onRssiRead);
        }

        public static Guid[] GetPeripheralDiscoveredServices(PeripheralHandle peripheral)
        {
            if (!peripheral.IsValid) throw new ArgumentException("Invalid PeripheralHandle", nameof(peripheral));

            return ToGuidArray(_impl.GetPeripheralDiscoveredServices(peripheral));
        }

        public static Guid[] GetPeripheralServiceCharacteristics(PeripheralHandle peripheral, Guid service)
        {
            if (!peripheral.IsValid) throw new ArgumentException("Invalid PeripheralHandle", nameof(peripheral));
            if (service == Guid.Empty) throw new ArgumentException("Empty service UUID", nameof(service));

            return ToGuidArray(_impl.GetPeripheralServiceCharacteristics(peripheral, service.ToString()));
        }

        public static CharacteristicProperties GetCharacteristicProperties(PeripheralHandle peripheral, Guid service, Guid characteristic, uint instanceIndex)
        {
            if (!peripheral.IsValid) throw new ArgumentException("Invalid PeripheralHandle", nameof(peripheral));
            if (service == Guid.Empty) throw new ArgumentException("Empty service UUID", nameof(service));
            if (characteristic == Guid.Empty) throw new ArgumentException("Empty characteristic UUID", nameof(characteristic));

            SanityCheck();

            return _impl.GetCharacteristicProperties(peripheral, service.ToString(), characteristic.ToString(), instanceIndex);
        }

        public static void ReadCharacteristic(PeripheralHandle peripheral, Guid service, Guid characteristic, uint instanceIndex, NativeValueChangedHandler onValueChanged, NativeRequestResultHandler onResult)
        {
            if (!peripheral.IsValid) throw new ArgumentException("Invalid PeripheralHandle", nameof(peripheral));
            if (service == Guid.Empty) throw new ArgumentException("Empty service UUID", nameof(service));
            if (characteristic == Guid.Empty) throw new ArgumentException("Empty characteristic UUID", nameof(characteristic));
            if (onValueChanged == null) throw new ArgumentNullException(nameof(onValueChanged));
            if (onResult == null) throw new ArgumentNullException(nameof(onResult));

            SanityCheck();

            _impl.ReadCharacteristic(peripheral, service.ToString(), characteristic.ToString(), instanceIndex, onValueChanged, onResult);
        }

        public static void WriteCharacteristic(PeripheralHandle peripheral, Guid service, Guid characteristic, uint instanceIndex, byte[] data, bool withResponse, NativeRequestResultHandler onResult)
        {
            if (!peripheral.IsValid) throw new ArgumentException("Invalid PeripheralHandle", nameof(peripheral));
            if (service == Guid.Empty) throw new ArgumentException("Empty service UUID", nameof(service));
            if (characteristic == Guid.Empty) throw new ArgumentException("Empty characteristic UUID", nameof(characteristic));
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Length == 0) throw new ArgumentException("Empty data set", nameof(data));
            if (onResult == null) throw new ArgumentNullException(nameof(onResult));

            SanityCheck();

            _impl.WriteCharacteristic(peripheral, service.ToString(), characteristic.ToString(), instanceIndex, data, withResponse, onResult);
        }

        public static void SubscribeCharacteristic(PeripheralHandle peripheral, Guid service, Guid characteristic, uint instanceIndex, NativeValueChangedHandler onValueChanged, NativeRequestResultHandler onResult)
        {
            if (!peripheral.IsValid) throw new ArgumentException("Invalid PeripheralHandle", nameof(peripheral));
            if (service == Guid.Empty) throw new ArgumentException("Empty service UUID", nameof(service));
            if (characteristic == Guid.Empty) throw new ArgumentException("Empty characteristic UUID", nameof(characteristic));
            if (onValueChanged == null) throw new ArgumentNullException(nameof(onValueChanged));
            if (onResult == null) throw new ArgumentNullException(nameof(onResult));

            SanityCheck();

            _impl.SubscribeCharacteristic(peripheral, service.ToString(), characteristic.ToString(), instanceIndex, onValueChanged, onResult);
        }

        public static void UnsubscribeCharacteristic(PeripheralHandle peripheral, Guid service, Guid characteristic, uint instanceIndex, NativeRequestResultHandler onResult)
        {
            if (!peripheral.IsValid) throw new ArgumentException("Invalid PeripheralHandle", nameof(peripheral));
            if (service == Guid.Empty) throw new ArgumentException("Empty service UUID", nameof(service));
            if (characteristic == Guid.Empty) throw new ArgumentException("Empty characteristic UUID", nameof(characteristic));
            if (onResult == null) throw new ArgumentNullException(nameof(onResult));

            SanityCheck();

            _impl.UnsubscribeCharacteristic(peripheral, service.ToString(), characteristic.ToString(), instanceIndex, onResult);
        }

        static void SanityCheck()
        {
            //TODO not implemented
            //if (!_impl.IsReady) throw new InvalidOperationException("Native Interface not ready");
        }

        private static string ToString(IEnumerable<Guid> uuids)
        {
            string str = null;
            if (uuids != null)
            {
                str = string.Join(",", uuids.Select(s => s.ToString()));
                if (str.Length == 0)
                {
                    str = null;
                }
            }
            return str;
        }

        private static Guid[] ToGuidArray(string uuids)
        {
            return uuids?.Split(',').Select(s => s.ToBleGuid()).ToArray() ?? Array.Empty<Guid>();
        }
    }
}
