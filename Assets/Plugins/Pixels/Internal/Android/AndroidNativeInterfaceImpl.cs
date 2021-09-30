using System;
using UnityEngine;
using UnityEngine.Android;

namespace Systemic.Pixels.Unity.BluetoothLE.Internal.Android
{
    enum AndroidRequestStatus : int
    {
        // From android.bluetooth.BluetoothGatt 
        GATT_SUCCESS = 0, // A GATT operation completed successfully
        GATT_READ_NOT_PERMITTED = 2, // GATT read operation is not permitted
        GATT_WRITE_NOT_PERMITTED = 3, // GATT write operation is not permitted
        GATT_INSUFFICIENT_AUTHENTICATION = 5, // Insufficient authentication for a given operation
        GATT_REQUEST_NOT_SUPPORTED = 6, // The given request is not supported
        GATT_INVALID_OFFSET = 7, // A read or write operation was requested with an invalid offset
        GATT_INVALID_ATTRIBUTE_LENGTH = 13, //  A write operation exceeds the maximum length of the attribute
        GATT_INSUFFICIENT_ENCRYPTION = 15, // Insufficient encryption for a given operation
        GATT_ERROR = 133, // (0x85) Generic error
        GATT_CONNECTION_CONGESTED = 143, // (0x8f) A remote device connection is congested.
        GATT_FAILURE = 257, // (0x101) A GATT operation failed, errors other than the above

        // From Nordic's FailCallback
        REASON_DEVICE_DISCONNECTED = -1,
        REASON_DEVICE_NOT_SUPPORTED = -2,
        REASON_NULL_ATTRIBUTE = -3,
        REASON_REQUEST_FAILED = -4,
        REASON_TIMEOUT = -5,
        REASON_VALIDATION = -6,
        REASON_CANCELLED = -7,
        REASON_BLUETOOTH_DISABLED = -100,

        // From Nordic's RequestCallback
        REASON_REQUEST_INVALID = -1000000,
    }

    sealed class AndroidNativeInterfaceImpl : INativeInterfaceImpl
    {
        sealed class NativeBluetoothDevice : ScannedPeripheral.ISystemDevice, IDisposable
        {
            public NativeBluetoothDevice(AndroidJavaObject device) => Device = device;

            public AndroidJavaObject Device { get; private set; }

            public void Dispose() { Device = null; }
        }

        sealed class NativePeripheral : PeripheralHandle.INativePeripheral, IDisposable
        {
            public NativePeripheral(AndroidJavaObject client) => Client = client;

            public AndroidJavaObject Client { get; private set; }

            public void Dispose() { Client = null; }
        }

        readonly AndroidJavaClass _scannerClass = new AndroidJavaClass("com.systemic.pixels.Scanner");
        readonly AndroidJavaClass _peripheralClass = new AndroidJavaClass("com.systemic.pixels.Peripheral");

        public bool Initialize(NativeBluetoothEventHandler onBluetoothEvent)
        {
#if UNITY_2018_3_OR_NEWER
            if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
            {
                Permission.RequestUserPermission(Permission.FineLocation);
            }
#endif
            onBluetoothEvent((_scannerClass != null) && (_peripheralClass != null));
            //TODO bluetooth availability events
            return true;
        }

        public void Shutdown()
        {
        }

        public bool StartScan(string requiredServiceUuids, Action<ScannedPeripheral> onScannedPeripheral)
        {
            var callback = new ScannerCallback((device, scanResult)
                => onScannedPeripheral(new ScannedPeripheral(new NativeBluetoothDevice(device), scanResult)));
                //TODO try/catch?

            _scannerClass.CallStatic(
                "startScan",
                requiredServiceUuids,
                callback);

            return true;
        }

        public void StopScan()
        {
            _scannerClass.CallStatic("stopScan");
        }

        public PeripheralHandle CreatePeripheral(ulong bluetoothAddress, NativePeripheralConnectionEventHandler onConnectionEvent)
        {
            var device = _peripheralClass.CallStatic<AndroidJavaObject>(
                "getDeviceFromAddress",
                (long)bluetoothAddress);
            if (device == null)
            {
                return new PeripheralHandle();
            }
            else
            {
                var client = new AndroidJavaObject(
                    "com.systemic.pixels.Peripheral",
                    device,
                    new ConnectionObserver(onConnectionEvent));
                return new PeripheralHandle(new NativePeripheral(client));
            }
        }

        public PeripheralHandle CreatePeripheral(ScannedPeripheral peripheral, NativePeripheralConnectionEventHandler onConnectionEvent)
        {
            var client = new AndroidJavaObject(
                "com.systemic.pixels.Peripheral",
                GetDevice(peripheral),
                new ConnectionObserver(onConnectionEvent));
            return new PeripheralHandle(new NativePeripheral(client));
        }

        public void ReleasePeripheral(PeripheralHandle peripheral)
        {
            ((NativePeripheral)peripheral.SystemClient).Dispose();
        }

        public void ConnectPeripheral(PeripheralHandle peripheral, string requiredServicesUuids, NativeRequestResultHandler onResult)
        {
            GetClient(peripheral).Call(
                "connect",
                requiredServicesUuids,
                new RequestCallback(onResult));
        }

        public void DisconnectPeripheral(PeripheralHandle peripheral, NativeRequestResultHandler onResult)
        {
            GetClient(peripheral).Call(
                "disconnect",
                new RequestCallback(onResult));
        }

        public string GetPeripheralName(PeripheralHandle peripheral)
        {
            return GetClient(peripheral).Call<string>("getName");
        }

        public int GetPeripheralMtu(PeripheralHandle peripheral)
        {
            return GetClient(peripheral).Call<int>("getMtu");
        }

        public void RequestPeripheralMtu(PeripheralHandle peripheral, int mtu, NativeValueRequestResultHandler<int> onMtuResult)
        {
            GetClient(peripheral).Call(
                "requestMtu",
                mtu,
                new MtuRequestCallback(onMtuResult));
        }

        public void ReadPeripheralRssi(PeripheralHandle peripheral, NativeValueRequestResultHandler<int> onRssiRead)
        {
            GetClient(peripheral).Call(
                "readRssi",
                new RssiRequestCallback(onRssiRead));
        }

        public string GetPeripheralDiscoveredServices(PeripheralHandle peripheral)
        {
            return GetClient(peripheral).Call<string>("getDiscoveredServices");
        }

        public string GetPeripheralServiceCharacteristics(PeripheralHandle peripheral, string serviceUuid)
        {
            return GetClient(peripheral).Call<string>(
                "getServiceCharacteristics",
                serviceUuid);
        }

        public CharacteristicProperties GetCharacteristicProperties(PeripheralHandle peripheral, string serviceUuid, string characteristicUuid, uint instanceIndex)
        {
            return (CharacteristicProperties)GetClient(peripheral).Call<int>(
                "getCharacteristicProperties",
                serviceUuid,
                characteristicUuid,
                (int)instanceIndex);
        }

        public void ReadCharacteristic(PeripheralHandle peripheral, string serviceUuid, string characteristicUuid, uint instanceIndex, NativeValueChangedHandler onValueChanged, NativeRequestResultHandler onResult)
        {
            GetClient(peripheral).Call(
                "readCharacteristic",
                serviceUuid,
                characteristicUuid,
                (int)instanceIndex,
                new DataReceivedCallback(onValueChanged),
                new RequestCallback(onResult));
        }

        public void WriteCharacteristic(PeripheralHandle peripheral, string serviceUuid, string characteristicUuid, uint instanceIndex, byte[] data, bool withoutResponse, NativeRequestResultHandler onResult)
        {
            GetClient(peripheral).Call(
                "writeCharacteristic",
                serviceUuid,
                characteristicUuid,
                (int)instanceIndex,
                JavaUtils.ToSignedArray(data),
                withoutResponse,
                new RequestCallback(onResult));
        }

        // No notification with error on Android
        public void SubscribeCharacteristic(PeripheralHandle peripheral, string serviceUuid, string characteristicUuid, uint instanceIndex, NativeValueChangedHandler onValueChanged, NativeRequestResultHandler onResult)
        {
            GetClient(peripheral).Call(
                "subscribeCharacteristic",
                serviceUuid,
                characteristicUuid,
                (int)instanceIndex,
                new DataReceivedCallback(onValueChanged),
                new RequestCallback(onResult));
        }

        public void UnsubscribeCharacteristic(PeripheralHandle peripheral, string serviceUuid, string characteristicUuid, uint instanceIndex, NativeRequestResultHandler onResult)
        {
            GetClient(peripheral).Call(
                "unsubscribeCharacteristic",
                serviceUuid,
                characteristicUuid,
                (int)instanceIndex,
                new RequestCallback(onResult));
        }

        AndroidJavaObject GetDevice(ScannedPeripheral scannedPeripheral) => ((NativeBluetoothDevice)scannedPeripheral.SystemDevice).Device;

        AndroidJavaObject GetClient(PeripheralHandle peripheralHandle) => ((NativePeripheral)peripheralHandle.SystemClient).Client;
    }
}
