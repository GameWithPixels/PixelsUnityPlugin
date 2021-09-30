using System;
using System.Collections.Generic;
using System.Linq;
using Systemic.Pixels.Unity.BluetoothLE.Internal;

namespace Systemic.Pixels.Unity.BluetoothLE
{
    public enum ConnectionEvent
    {
        /**
         * Called when the Android device started connecting to given device.
         * The {@link #onDeviceConnected(BluetoothDevice)} will be called when the device is connected,
         * or {@link #onDeviceFailedToConnect(BluetoothDevice, int)} if connection will fail.
         *
         * @param device the device that got connected.
         */
        Connecting,

        /**
         * Called when the device has been connected. This does not mean that the application may start
         * communication. Service discovery will be handled automatically after this call.
         *
         * @param device the device that got connected.
         */
        Connected,

        /**
         * Called when the device failed to connect.
         * @param device the device that failed to connect.
         * @param reason the reason of failure.
         */
        FailedToConnect, // + reason

        /**
         * Method called when all initialization requests has been completed.
         *
         * @param device the device that get ready.
         */
        Ready,

        /**
         * Called when user initialized disconnection.
         *
         * @param device the device that gets disconnecting.
         */
        Disconnecting,

        /**
         * Called when the device has disconnected (when the callback returned
         * {@link BluetoothGattCallback#onConnectionStateChange(BluetoothGatt, int, int)} with state
         * DISCONNECTED).
         *
         * @param device the device that got disconnected.
         * @param reason of the disconnect (mapped from the status code reported by the GATT
         *               callback to the library specific status codes).
         */
        Disconnected, // + reason
    }

    // See https://github.com/NordicSemiconductor/Android-BLE-Library/blob/1c8339013e678a864302209618435de5707207dd/ble/src/main/java/no/nordicsemi/android/ble/observer/ConnectionObserver.java
    public enum ConnectionEventReason
    {
        Unknown = -1,
        /** The disconnection was initiated by the user. */
        Success = 0,
        /** The local device initiated disconnection. */
        REASON_TERMINATE_LOCAL_HOST = 1,
        /** The remote device initiated graceful disconnection. */
        REASON_TERMINATE_PEER_USER = 2,
        /**
		 * This reason will only be reported when {@link ConnectRequest#useAutoConnect(boolean)}} was
		 * called with parameter set to true, and connection to the device was lost for any reason
		 * other than graceful disconnection initiated by the peer user.
		 * <p>
		 * Android will try to reconnect automatically.
		 */
        Unreachable = 3, // LinkLoss
        /** The device does not have required services. */
        NotSupported = 4,
        /** Connection attempt was canceled. */
        Cancelled = 5,
        ProtocolError,
        AccessDenied,

        /**
		 * The connection timed out. The device might have reboot, is out of range, turned off
		 * or doesn't respond for another reason.
		 */
        REASON_TIMEOUT = 10,
    }

    [Flags]
    public enum CharacteristicProperties : ulong
    {
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

    public delegate void NativeBluetoothEventHandler(bool available); //TODO use enum for states
    public delegate void NativePeripheralConnectionEventHandler(ConnectionEvent connectionEvent, ConnectionEventReason reason);
    public delegate void NativePeripheralCreatedHandler(PeripheralHandle peripheralHandle);
    public delegate void NativeRequestResultHandler(NativeError error); // On success error is empty
    public delegate void NativeValueRequestResultHandler<T>(T value, NativeError error); // On success error is empty
    public delegate void NativeValueChangedHandler(byte[] data, NativeError error); // No error if data != null

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

        public static PeripheralHandle CreatePeripheral(ScannedPeripheral peripheral, NativePeripheralConnectionEventHandler onConnectionEvent)
        {
            if (peripheral.SystemDevice == null) throw new ArgumentException("ScannedPeripheral has null SystemDevice", nameof(peripheral));
            if (onConnectionEvent == null) throw new ArgumentNullException(nameof(onConnectionEvent));

            SanityCheck();

            return _impl.CreatePeripheral(peripheral, onConnectionEvent);
        }

        public static void ReleasePeripheral(PeripheralHandle peripheral)
        {
            if (peripheral.SystemClient == null) throw new ArgumentException("PeripheralHandle has a null SystemClient", nameof(peripheral));

            SanityCheck();

            _impl.ReleasePeripheral(peripheral);
        }

        public static void ConnectPeripheral(PeripheralHandle peripheral, IEnumerable<Guid> requiredServices, NativeRequestResultHandler onResult)
        {
            if (peripheral.SystemClient == null) throw new ArgumentException("PeripheralHandle has a null SystemClient", nameof(peripheral));
            if (onResult == null) throw new ArgumentNullException(nameof(onResult));

            SanityCheck();

            _impl.ConnectPeripheral(peripheral, ToString(requiredServices), onResult);
        }

        public static void DisconnectPeripheral(PeripheralHandle peripheral, NativeRequestResultHandler onResult)
        {
            if (peripheral.SystemClient == null) throw new ArgumentException("PeripheralHandle has a null SystemClient", nameof(peripheral));
            if (onResult == null) throw new ArgumentNullException(nameof(onResult));

            SanityCheck();

            _impl.DisconnectPeripheral(peripheral, onResult);
        }

        public static string GetPeripheralName(PeripheralHandle peripheral)
        {
            if (peripheral.SystemClient == null) throw new ArgumentException("PeripheralHandle has a null SystemClient", nameof(peripheral));

            SanityCheck();

            return _impl.GetPeripheralName(peripheral);
        }

        public static int GetPeripheralMtu(PeripheralHandle peripheral)
        {
            if (peripheral.SystemClient == null) throw new ArgumentException("PeripheralHandle has a null SystemClient", nameof(peripheral));

            SanityCheck();

            return _impl.GetPeripheralMtu(peripheral);
        }

        // See MinMTU and MaxMTU, supported on Android only
        public static void RequestPeripheralMtu(PeripheralHandle peripheral, int mtu, NativeValueRequestResultHandler<int> onMtuResult)
        {
            if (peripheral.SystemClient == null) throw new ArgumentException("PeripheralHandle has a null SystemClient", nameof(peripheral));
            if ((mtu < MinMtu) || (mtu > MaxMtu)) throw new ArgumentException($"MTU must be between {MinMtu} and {MaxMtu}", nameof(mtu));
            if (onMtuResult == null) throw new ArgumentNullException(nameof(onMtuResult));

            SanityCheck();

            _impl.RequestPeripheralMtu(peripheral, mtu, onMtuResult);
        }

        // See MinMTU and MaxMTU, supported on Apple and Android
        public static void ReadPeripheralRssi(PeripheralHandle peripheral, NativeValueRequestResultHandler<int> onRssiRead)
        {
            if (peripheral.SystemClient == null) throw new ArgumentException("PeripheralHandle has a null SystemClient", nameof(peripheral));
            if (onRssiRead == null) throw new ArgumentNullException(nameof(onRssiRead));

            SanityCheck();

            _impl.ReadPeripheralRssi(peripheral, onRssiRead);
        }

        public static Guid[] GetPeripheralDiscoveredServices(PeripheralHandle peripheral)
        {
            if (peripheral.SystemClient == null) throw new ArgumentException("PeripheralHandle has a null SystemClient", nameof(peripheral));

            return ToGuidArray(_impl.GetPeripheralDiscoveredServices(peripheral));
        }

        public static Guid[] GetPeripheralServiceCharacteristics(PeripheralHandle peripheral, Guid service)
        {
            if (peripheral.SystemClient == null) throw new ArgumentException("PeripheralHandle has a null SystemClient", nameof(peripheral));
            if (service == Guid.Empty) throw new ArgumentException("Empty service UUID", nameof(service));

            return ToGuidArray(_impl.GetPeripheralServiceCharacteristics(peripheral, service.ToString()));
        }

        public static CharacteristicProperties GetCharacteristicProperties(PeripheralHandle peripheral, Guid service, Guid characteristic, uint instanceIndex)
        {
            if (peripheral.SystemClient == null) throw new ArgumentException("PeripheralHandle has a null SystemClient", nameof(peripheral));
            if (service == Guid.Empty) throw new ArgumentException("Empty service UUID", nameof(service));
            if (characteristic == Guid.Empty) throw new ArgumentException("Empty characteristic UUID", nameof(characteristic));

            SanityCheck();

            return _impl.GetCharacteristicProperties(peripheral, service.ToString(), characteristic.ToString(), instanceIndex);
        }

        public static void ReadCharacteristic(PeripheralHandle peripheral, Guid service, Guid characteristic, uint instanceIndex, NativeValueChangedHandler onValueChanged, NativeRequestResultHandler onResult)
        {
            if (peripheral.SystemClient == null) throw new ArgumentException("PeripheralHandle has a null SystemClient", nameof(peripheral));
            if (service == Guid.Empty) throw new ArgumentException("Empty service UUID", nameof(service));
            if (characteristic == Guid.Empty) throw new ArgumentException("Empty characteristic UUID", nameof(characteristic));
            if (onValueChanged == null) throw new ArgumentNullException(nameof(onValueChanged));
            if (onResult == null) throw new ArgumentNullException(nameof(onResult));

            SanityCheck();

            _impl.ReadCharacteristic(peripheral, service.ToString(), characteristic.ToString(), instanceIndex, onValueChanged, onResult);
        }

        public static void WriteCharacteristic(PeripheralHandle peripheral, Guid service, Guid characteristic, uint instanceIndex, byte[] data, bool withResponse, NativeRequestResultHandler onResult)
        {
            if (peripheral.SystemClient == null) throw new ArgumentException("PeripheralHandle has a null SystemClient", nameof(peripheral));
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
            if (peripheral.SystemClient == null) throw new ArgumentException("PeripheralHandle has a null SystemClient", nameof(peripheral));
            if (service == Guid.Empty) throw new ArgumentException("Empty service UUID", nameof(service));
            if (characteristic == Guid.Empty) throw new ArgumentException("Empty characteristic UUID", nameof(characteristic));
            if (onValueChanged == null) throw new ArgumentNullException(nameof(onValueChanged));
            if (onResult == null) throw new ArgumentNullException(nameof(onResult));

            SanityCheck();

            _impl.SubscribeCharacteristic(peripheral, service.ToString(), characteristic.ToString(), instanceIndex, onValueChanged, onResult);
        }

        public static void UnsubscribeCharacteristic(PeripheralHandle peripheral, Guid service, Guid characteristic, uint instanceIndex, NativeRequestResultHandler onResult)
        {
            if (peripheral.SystemClient == null) throw new ArgumentException("PeripheralHandle has a null SystemClient", nameof(peripheral));
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
