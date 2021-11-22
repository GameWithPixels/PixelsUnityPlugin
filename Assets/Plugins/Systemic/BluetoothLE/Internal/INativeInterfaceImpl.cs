using System;

namespace Systemic.Unity.BluetoothLE.Internal
{
    /// <summary>
    /// Interface to be implemented for each supported platform, used as an opaque object for a bluetooth device.
    /// </summary>
    internal interface INativeDevice
    {
        /// <summary>
        /// Indicates if the native device is valid.
        /// </summary>
        bool IsValid { get; }
    }

    /// <summary>
    /// Interface to be implemented for each supported platform, used as an opaque object for a BLE peripheral.
    /// </summary>
    internal interface INativePeripheralHandleImpl
    {
        /// <summary>
        /// Indicates if the native peripheral handle is valid.
        /// </summary>
        bool IsValid { get; }
    }

    /// <summary>
    /// The interface for the BLE operations to be implemented for each supported platform.
    /// See <see cref="NativeInterface"/> for more details.
    /// </summary>
    internal interface INativeInterfaceImpl
    {
        bool Initialize(NativeBluetoothEventHandler onBluetoothEvent);

        void Shutdown();

        bool StartScan(string requiredServiceUuids, Action<INativeDevice, NativeAdvertisementDataJson> onScannedPeripheral);

        void StopScan();

        INativePeripheralHandleImpl CreatePeripheral(ulong bluetoothAddress, NativeConnectionEventHandler onConnectionEvent);

        INativePeripheralHandleImpl CreatePeripheral(INativeDevice device, NativeConnectionEventHandler onConnectionEvent);

        void ReleasePeripheral(INativePeripheralHandleImpl peripheralHandle);

        void ConnectPeripheral(INativePeripheralHandleImpl peripheralHandle, string requiredServicesUuids, bool autoReconnect, NativeRequestResultHandler onResult);

        void DisconnectPeripheral(INativePeripheralHandleImpl peripheralHandle, NativeRequestResultHandler onResult);

        string GetPeripheralName(INativePeripheralHandleImpl peripheralHandle);

        int GetPeripheralMtu(INativePeripheralHandleImpl peripheralHandle);

        void RequestPeripheralMtu(INativePeripheralHandleImpl peripheralHandle, int mtu, NativeValueRequestResultHandler<int> onMtuResult);

        void ReadPeripheralRssi(INativePeripheralHandleImpl peripheralHandle, NativeValueRequestResultHandler<int> onRssiRead);

        string GetPeripheralDiscoveredServices(INativePeripheralHandleImpl peripheralHandle);

        string GetPeripheralServiceCharacteristics(INativePeripheralHandleImpl peripheralHandle, string serviceUuid);

        CharacteristicProperties GetCharacteristicProperties(INativePeripheralHandleImpl peripheralHandle, string serviceUuid, string characteristicUuid, uint instanceIndex);

        void ReadCharacteristic(INativePeripheralHandleImpl peripheralHandle, string serviceUuid, string characteristicUuid, uint instanceIndex, NativeValueRequestResultHandler<byte[]> onValueRead);

        void WriteCharacteristic(INativePeripheralHandleImpl peripheralHandle, string serviceUuid, string characteristicUuid, uint instanceIndex, byte[] data, bool withoutResponse, NativeRequestResultHandler onResult);

        void SubscribeCharacteristic(INativePeripheralHandleImpl peripheralHandle, string serviceUuid, string characteristicUuid, uint instanceIndex, NativeValueRequestResultHandler<byte[]> onValueChanged, NativeRequestResultHandler onResult);

        void UnsubscribeCharacteristic(INativePeripheralHandleImpl peripheralHandle, string serviceUuid, string characteristicUuid, uint instanceIndex, NativeRequestResultHandler onResult);
    }
}
