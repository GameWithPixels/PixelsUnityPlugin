using System;

namespace Systemic.Unity.BluetoothLE.Internal
{
    internal interface INativeInterfaceImpl
    {
        bool Initialize(NativeBluetoothEventHandler onBluetoothEvent);

        void Shutdown();

        bool StartScan(string requiredServiceUuids, Action<ScannedPeripheral> onScannedPeripheral);

        void StopScan();

        NativePeripheralHandle CreatePeripheral(ulong bluetoothAddress, NativeConnectionEventHandler onConnectionEvent);

        NativePeripheralHandle CreatePeripheral(IScannedPeripheral scannedPeripheral, NativeConnectionEventHandler onConnectionEvent);

        void ReleasePeripheral(NativePeripheralHandle peripheral);

        void ConnectPeripheral(NativePeripheralHandle peripheral, string requiredServicesUuids, bool autoConnect, NativeRequestResultHandler onResult);

        void DisconnectPeripheral(NativePeripheralHandle peripheral, NativeRequestResultHandler onResult);

        string GetPeripheralName(NativePeripheralHandle peripheral);

        int GetPeripheralMtu(NativePeripheralHandle peripheral);

        void RequestPeripheralMtu(NativePeripheralHandle peripheral, int mtu, NativeValueRequestResultHandler<int> onMtuResult);

        void ReadPeripheralRssi(NativePeripheralHandle peripheral, NativeValueRequestResultHandler<int> onRssiRead);

        string GetPeripheralDiscoveredServices(NativePeripheralHandle peripheral);

        string GetPeripheralServiceCharacteristics(NativePeripheralHandle peripheral, string serviceUuid);

        CharacteristicProperties GetCharacteristicProperties(NativePeripheralHandle peripheral, string serviceUuid, string characteristicUuid, uint instanceIndex);

        void ReadCharacteristic(NativePeripheralHandle peripheral, string serviceUuid, string characteristicUuid, uint instanceIndex, NativeValueRequestResultHandler<byte[]> onValueChanged, NativeRequestResultHandler onResult);

        void WriteCharacteristic(NativePeripheralHandle peripheral, string serviceUuid, string characteristicUuid, uint instanceIndex, byte[] data, bool withoutResponse, NativeRequestResultHandler onResult);

        void SubscribeCharacteristic(NativePeripheralHandle peripheral, string serviceUuid, string characteristicUuid, uint instanceIndex, NativeValueRequestResultHandler<byte[]> onValueChanged, NativeRequestResultHandler onResult);

        void UnsubscribeCharacteristic(NativePeripheralHandle peripheral, string serviceUuid, string characteristicUuid, uint instanceIndex, NativeRequestResultHandler onResult);
    }
}
