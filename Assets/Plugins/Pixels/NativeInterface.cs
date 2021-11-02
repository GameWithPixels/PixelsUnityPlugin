using System;
using System.Collections.Generic;
using System.Linq;
using Systemic.Unity.BluetoothLE.Internal;

namespace Systemic.Unity.BluetoothLE
{
    public static class NativeInterface
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

        public static NativePeripheralHandle CreatePeripheral(ulong bluetoothAddress, NativeConnectionEventHandler onConnectionEventChanged)
        {
            if (bluetoothAddress == 0) throw new ArgumentException("Empty bluetooth address", nameof(bluetoothAddress));
            if (onConnectionEventChanged == null) throw new ArgumentNullException(nameof(onConnectionEventChanged));

            SanityCheck();

            return _impl.CreatePeripheral(bluetoothAddress, onConnectionEventChanged);
        }

        public static NativePeripheralHandle CreatePeripheral(ScannedPeripheral scannedPeripheral, NativeConnectionEventHandler onConnectionEvent)
        {
            if (scannedPeripheral == null) throw new ArgumentNullException(nameof(scannedPeripheral));
            if (!((IScannedPeripheral)scannedPeripheral).IsValid) throw new ArgumentException("Invalid ScannedPeripheral", nameof(scannedPeripheral));
            if (onConnectionEvent == null) throw new ArgumentNullException(nameof(onConnectionEvent));

            SanityCheck();

            return _impl.CreatePeripheral(scannedPeripheral, onConnectionEvent);
        }

        public static void ReleasePeripheral(NativePeripheralHandle nativePeripheralHandle)
        {
            SanityCheck();

            if (nativePeripheralHandle.IsValid)
            {
                _impl.ReleasePeripheral(nativePeripheralHandle);
            }
        }

        public static void ConnectPeripheral(NativePeripheralHandle nativePeripheralHandle, IEnumerable<Guid> requiredServices, bool autoConnect, NativeRequestResultHandler onResult)
        {
            if (!nativePeripheralHandle.IsValid) throw new ArgumentException("Invalid NativePeripheralHandle", nameof(nativePeripheralHandle));
            if (onResult == null) throw new ArgumentNullException(nameof(onResult));

            SanityCheck();

            _impl.ConnectPeripheral(nativePeripheralHandle, ToString(requiredServices), autoConnect, onResult);
        }

        public static void DisconnectPeripheral(NativePeripheralHandle nativePeripheralHandle, NativeRequestResultHandler onResult)
        {
            if (!nativePeripheralHandle.IsValid) throw new ArgumentException("Invalid NativePeripheralHandle", nameof(nativePeripheralHandle));
            if (onResult == null) throw new ArgumentNullException(nameof(onResult));

            SanityCheck();

            _impl.DisconnectPeripheral(nativePeripheralHandle, onResult);
        }

        public static string GetPeripheralName(NativePeripheralHandle nativePeripheralHandle)
        {
            if (!nativePeripheralHandle.IsValid) throw new ArgumentException("Invalid NativePeripheralHandle", nameof(nativePeripheralHandle));

            SanityCheck();

            return _impl.GetPeripheralName(nativePeripheralHandle);
        }

        public static int GetPeripheralMtu(NativePeripheralHandle nativePeripheralHandle)
        {
            if (!nativePeripheralHandle.IsValid) throw new ArgumentException("Invalid NativePeripheralHandle", nameof(nativePeripheralHandle));

            SanityCheck();

            return _impl.GetPeripheralMtu(nativePeripheralHandle);
        }

        // See MinMTU and MaxMTU, supported on Android only
        public static void RequestPeripheralMtu(NativePeripheralHandle nativePeripheralHandle, int mtu, NativeValueRequestResultHandler<int> onMtuResult)
        {
            if (!nativePeripheralHandle.IsValid) throw new ArgumentException("Invalid NativePeripheralHandle", nameof(nativePeripheralHandle));
            if ((mtu < MinMtu) || (mtu > MaxMtu)) throw new ArgumentException($"MTU must be between {MinMtu} and {MaxMtu}", nameof(mtu));
            if (onMtuResult == null) throw new ArgumentNullException(nameof(onMtuResult));

            SanityCheck();

            _impl.RequestPeripheralMtu(nativePeripheralHandle, mtu, onMtuResult);
        }

        // See MinMTU and MaxMTU, supported on Apple and Android
        public static void ReadPeripheralRssi(NativePeripheralHandle nativePeripheralHandle, NativeValueRequestResultHandler<int> onRssiRead)
        {
            if (!nativePeripheralHandle.IsValid) throw new ArgumentException("Invalid NativePeripheralHandle", nameof(nativePeripheralHandle));
            if (onRssiRead == null) throw new ArgumentNullException(nameof(onRssiRead));

            SanityCheck();

            _impl.ReadPeripheralRssi(nativePeripheralHandle, onRssiRead);
        }

        public static Guid[] GetPeripheralDiscoveredServices(NativePeripheralHandle nativePeripheralHandle)
        {
            if (!nativePeripheralHandle.IsValid) throw new ArgumentException("Invalid NativePeripheralHandle", nameof(nativePeripheralHandle));

            return ToGuidArray(_impl.GetPeripheralDiscoveredServices(nativePeripheralHandle));
        }

        public static Guid[] GetPeripheralServiceCharacteristics(NativePeripheralHandle peripheral, Guid service)
        {
            if (!peripheral.IsValid) throw new ArgumentException("Invalid NativePeripheralHandle", nameof(peripheral));
            if (service == Guid.Empty) throw new ArgumentException("Empty service UUID", nameof(service));

            return ToGuidArray(_impl.GetPeripheralServiceCharacteristics(peripheral, service.ToString()));
        }

        public static CharacteristicProperties GetCharacteristicProperties(NativePeripheralHandle nativePeripheralHandle, Guid service, Guid characteristic, uint instanceIndex)
        {
            if (!nativePeripheralHandle.IsValid) throw new ArgumentException("Invalid NativePeripheralHandle", nameof(nativePeripheralHandle));
            if (service == Guid.Empty) throw new ArgumentException("Empty service UUID", nameof(service));
            if (characteristic == Guid.Empty) throw new ArgumentException("Empty characteristic UUID", nameof(characteristic));

            SanityCheck();

            return _impl.GetCharacteristicProperties(nativePeripheralHandle, service.ToString(), characteristic.ToString(), instanceIndex);
        }

        public static void ReadCharacteristic(NativePeripheralHandle nativePeripheralHandle, Guid service, Guid characteristic, uint instanceIndex, NativeValueRequestResultHandler<byte[]> onValueChanged, NativeRequestResultHandler onResult)
        {
            if (!nativePeripheralHandle.IsValid) throw new ArgumentException("Invalid NativePeripheralHandle", nameof(nativePeripheralHandle));
            if (service == Guid.Empty) throw new ArgumentException("Empty service UUID", nameof(service));
            if (characteristic == Guid.Empty) throw new ArgumentException("Empty characteristic UUID", nameof(characteristic));
            if (onValueChanged == null) throw new ArgumentNullException(nameof(onValueChanged));
            if (onResult == null) throw new ArgumentNullException(nameof(onResult));

            SanityCheck();

            _impl.ReadCharacteristic(nativePeripheralHandle, service.ToString(), characteristic.ToString(), instanceIndex, onValueChanged, onResult);
        }

        public static void WriteCharacteristic(NativePeripheralHandle nativePeripheralHandle, Guid service, Guid characteristic, uint instanceIndex, byte[] data, bool withResponse, NativeRequestResultHandler onResult)
        {
            if (!nativePeripheralHandle.IsValid) throw new ArgumentException("Invalid NativePeripheralHandle", nameof(nativePeripheralHandle));
            if (service == Guid.Empty) throw new ArgumentException("Empty service UUID", nameof(service));
            if (characteristic == Guid.Empty) throw new ArgumentException("Empty characteristic UUID", nameof(characteristic));
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Length == 0) throw new ArgumentException("Empty data set", nameof(data));
            if (onResult == null) throw new ArgumentNullException(nameof(onResult));

            SanityCheck();

            _impl.WriteCharacteristic(nativePeripheralHandle, service.ToString(), characteristic.ToString(), instanceIndex, data, withResponse, onResult);
        }

        public static void SubscribeCharacteristic(NativePeripheralHandle nativePeripheralHandle, Guid service, Guid characteristic, uint instanceIndex, NativeValueRequestResultHandler<byte[]> onValueChanged, NativeRequestResultHandler onResult)
        {
            if (!nativePeripheralHandle.IsValid) throw new ArgumentException("Invalid NativePeripheralHandle", nameof(nativePeripheralHandle));
            if (service == Guid.Empty) throw new ArgumentException("Empty service UUID", nameof(service));
            if (characteristic == Guid.Empty) throw new ArgumentException("Empty characteristic UUID", nameof(characteristic));
            if (onValueChanged == null) throw new ArgumentNullException(nameof(onValueChanged));
            if (onResult == null) throw new ArgumentNullException(nameof(onResult));

            SanityCheck();

            _impl.SubscribeCharacteristic(nativePeripheralHandle, service.ToString(), characteristic.ToString(), instanceIndex, onValueChanged, onResult);
        }

        public static void UnsubscribeCharacteristic(NativePeripheralHandle nativePeripheralHandle, Guid service, Guid characteristic, uint instanceIndex, NativeRequestResultHandler onResult)
        {
            if (!nativePeripheralHandle.IsValid) throw new ArgumentException("Invalid NativePeripheralHandle", nameof(nativePeripheralHandle));
            if (service == Guid.Empty) throw new ArgumentException("Empty service UUID", nameof(service));
            if (characteristic == Guid.Empty) throw new ArgumentException("Empty characteristic UUID", nameof(characteristic));
            if (onResult == null) throw new ArgumentNullException(nameof(onResult));

            SanityCheck();

            _impl.UnsubscribeCharacteristic(nativePeripheralHandle, service.ToString(), characteristic.ToString(), instanceIndex, onResult);
        }

        private static void SanityCheck()
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
