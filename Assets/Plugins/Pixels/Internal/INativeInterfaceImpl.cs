using System;

namespace Systemic.Pixels.Unity.BluetoothLE.Internal
{
    internal interface INativeInterfaceImpl
    {
        bool Initialize(NativeBluetoothEventHandler onBluetoothEvent);

        void Shutdown();

        bool StartScan(string requiredServiceUuids, Action<ScannedPeripheral> onScannedPeripheral);

        void StopScan();

        PeripheralHandle CreatePeripheral(ulong bluetoothAddress, NativePeripheralConnectionEventHandler onConnectionEvent);

        PeripheralHandle CreatePeripheral(IScannedPeripheral scannedPeripheral, NativePeripheralConnectionEventHandler onConnectionEvent);

        void ReleasePeripheral(PeripheralHandle peripheral);

        void ConnectPeripheral(PeripheralHandle peripheral, string requiredServicesUuids, bool autoConnect, NativeRequestResultHandler onResult);

        void DisconnectPeripheral(PeripheralHandle peripheral, NativeRequestResultHandler onResult);

        string GetPeripheralName(PeripheralHandle peripheral);

        int GetPeripheralMtu(PeripheralHandle peripheral);

        void RequestPeripheralMtu(PeripheralHandle peripheral, int mtu, NativeValueRequestResultHandler<int> onMtuResult);

        void ReadPeripheralRssi(PeripheralHandle peripheral, NativeValueRequestResultHandler<int> onRssiRead);

        string GetPeripheralDiscoveredServices(PeripheralHandle peripheral);

        string GetPeripheralServiceCharacteristics(PeripheralHandle peripheral, string serviceUuid);

        CharacteristicProperties GetCharacteristicProperties(PeripheralHandle peripheral, string serviceUuid, string characteristicUuid, uint instanceIndex);

        void ReadCharacteristic(PeripheralHandle peripheral, string serviceUuid, string characteristicUuid, uint instanceIndex, NativeValueChangedHandler onValueChanged, NativeRequestResultHandler onResult);

        void WriteCharacteristic(PeripheralHandle peripheral, string serviceUuid, string characteristicUuid, uint instanceIndex, byte[] data, bool withoutResponse, NativeRequestResultHandler onResult);

        void SubscribeCharacteristic(PeripheralHandle peripheral, string serviceUuid, string characteristicUuid, uint instanceIndex, NativeValueChangedHandler onValueChanged, NativeRequestResultHandler onResult);

        void UnsubscribeCharacteristic(PeripheralHandle peripheral, string serviceUuid, string characteristicUuid, uint instanceIndex, NativeRequestResultHandler onResult);
    }
}
