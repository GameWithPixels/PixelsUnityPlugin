using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Systemic.Unity.BluetoothLE.Internal.Windows
{
    internal sealed class WinRTNativeInterfaceImpl : INativeInterfaceImpl
    {
        sealed class NativeScannedPeripheral : INativeDevice
        {
            public ulong BluetoothAddress { get; }

            public bool IsValid => BluetoothAddress != 0;

            public NativeScannedPeripheral(ulong bluetoothAddress) => BluetoothAddress = bluetoothAddress;
        }

        sealed class NativePeripheral : INativePeripheral
        {
            // Keep references to all callbacks so they are not reclaimed by the GC
            PeripheralConnectionEventHandler _peripheralConnectionStatusChanged;
            HashSet<RequestStatusHandler> _requestStatusHandlers = new HashSet<RequestStatusHandler>();
            Dictionary<string, ValueChangedHandler> _valueChangedHandlers = new Dictionary<string, ValueChangedHandler>();

            static HashSet<NativePeripheral> _releasedPeripherals = new HashSet<NativePeripheral>();

            public NativePeripheral(ulong bluetoothAddress, string name, PeripheralConnectionEventHandler onPeripheralConnectionStatusChanged)
                => (BluetoothAddress, Name, _peripheralConnectionStatusChanged) = (bluetoothAddress, name, onPeripheralConnectionStatusChanged);

            public ulong BluetoothAddress { get; }

            public string Name { get; }

            public bool IsValid => BluetoothAddress != 0;

            public void KeepRequestHandler(RequestStatusHandler onRequestStatus)
            {
                lock (_requestStatusHandlers)
                {
                    _requestStatusHandlers.Add(onRequestStatus);
                }
            }

            public void ForgetRequestHandler(RequestStatusHandler onRequestStatus)
            {
                lock (_requestStatusHandlers)
                {
                    Debug.Assert(_requestStatusHandlers.Contains(onRequestStatus));
                    _requestStatusHandlers.Remove(onRequestStatus);
                }
                CheckReleased();
            }

            public void KeepValueChangedHandler(string serviceUuid, string characteristicUuid, uint instanceIndex, ValueChangedHandler onValueChanged)
            {
                lock (_valueChangedHandlers)
                {
                    _valueChangedHandlers[$"{serviceUuid}:{characteristicUuid}#{instanceIndex}"] = onValueChanged;
                }
            }

            public void ForgetValueChangedHandler(string serviceUuid, string characteristicUuid, uint instanceIndex)
            {
                lock (_valueChangedHandlers)
                {
                    string key = $"{serviceUuid}:{characteristicUuid}#{instanceIndex}";
                    Debug.Assert(_valueChangedHandlers.ContainsKey(key));
                    _valueChangedHandlers.Remove(key);
                }
                CheckReleased();
            }

            //public void ForgetAllValueChangedHandlers()
            //{
            //    lock (_valueChangedHandlers)
            //    {
            //        _valueChangedHandlers.Clear();
            //    }
            //    CheckReleased();
            //}

            public void Release()
            {
                lock (_requestStatusHandlers)
                {
                    // Keep a reference to ourselves until all handlers have been cleared out
                    if ((_requestStatusHandlers.Count > 0) && _releasedPeripherals.Add(this))
                    {
                        Debug.Log($"[BLE:{Name}] Added to WinRT release list");
                    }
                }
            }

            void CheckReleased()
            {
                lock (_requestStatusHandlers)
                {
                    if ((_requestStatusHandlers.Count == 0) && _releasedPeripherals.Remove(this))
                    {
                        Debug.Log($"[BLE:{Name}] Removed from WinRT release list");
                    }
                }
            }

            //~NativePeripheral()
            //{
            //    Debug.LogError($"[BLE:{Name}] WinRT GC");
            //}
        }

        const string _libName =
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            "LibWinRTBle";
#else
            "unsupported";
#endif

        delegate void CentralStateUpdateHandler(bool isAvailable);
        delegate void DiscoveredPeripheralHandler([MarshalAs(UnmanagedType.LPStr)] string advertisementDataJson);
        delegate void RequestStatusHandler(RequestStatus errorCode);
        delegate void PeripheralConnectionEventHandler(ulong peripheralId, int connectionEvent, int reason);
        delegate void ValueChangedHandler(IntPtr data, UIntPtr length, RequestStatus errorCode);

        [DllImport(_libName)]
        private static extern bool sgBleInitialize(bool apartmentSingleThreaded, CentralStateUpdateHandler onCentralStateUpdate);

        [DllImport(_libName)]
        private static extern void sgBleShutdown();

        [DllImport(_libName)]
        private static extern bool sgBleStartScan(string requiredServicesUuids, DiscoveredPeripheralHandler onDiscoveredPeripheral);

        [DllImport(_libName)]
        private static extern void sgBleStopScan();

        [DllImport(_libName)]
        private static extern bool sgBleCreatePeripheral(ulong peripheralId, PeripheralConnectionEventHandler onConnectionEvent);

        [DllImport(_libName)]
        private static extern void sgBleReleasePeripheral(ulong peripheralId);

        [DllImport(_libName)]
        private static extern void sgBleConnectPeripheral(ulong peripheralId, string requiredServicesUuids, bool autoConnect, RequestStatusHandler onRequestStatus);

        [DllImport(_libName)]
        private static extern void sgBleDisconnectPeripheral(ulong peripheralId, RequestStatusHandler onRequestStatus);

        [DllImport(_libName)]
        private static extern string sgBleGetPeripheralName(ulong peripheralId);

        [DllImport(_libName)]
        private static extern int sgBleGetPeripheralMtu(ulong peripheralId);

        [DllImport(_libName)]
        private static extern string sgBleGetPeripheralDiscoveredServices(ulong peripheralId);

        [DllImport(_libName)]
        private static extern string sgBleGetPeripheralServiceCharacteristics(ulong peripheralId, string serviceUuid);

        [DllImport(_libName)]
        private static extern ulong sgBleGetCharacteristicProperties(ulong peripheralId, string serviceUuid, string characteristicUuid, uint instanceIndex);

        [DllImport(_libName)]
        private static extern void sgBleReadCharacteristicValue(ulong peripheralId, string serviceUuid, string characteristicUuid, uint instanceIndex, ValueChangedHandler onValueChanged, RequestStatusHandler onRequestStatus);

        [DllImport(_libName)]
        private static extern void sgBleWriteCharacteristicValue(ulong peripheralId, string serviceUuid, string characteristicUuid, uint instanceIndex, IntPtr data, UIntPtr length, bool withoutResponse, RequestStatusHandler onRequestStatus);

        [DllImport(_libName)]
        private static extern void sgBleSetNotifyCharacteristic(ulong peripheralId, string serviceUuid, string characteristicUuid, uint instanceIndex, ValueChangedHandler onValueChanged, RequestStatusHandler onRequestStatus);

        // Keep a reference to state update and discovery callbacks so they are not reclaimed by the GC
        private static CentralStateUpdateHandler _onCentralStateUpdate;
        private static DiscoveredPeripheralHandler _onDiscoveredPeripheral;

        public bool Initialize(NativeBluetoothEventHandler onBluetoothEvent)
        {
            CentralStateUpdateHandler onCentralStateUpdate = available => onBluetoothEvent(available ? BluetoothStatus.Enabled : BluetoothStatus.Disabled);
            bool success = sgBleInitialize(true, onCentralStateUpdate);
            if (success)
            {
                _onCentralStateUpdate = onCentralStateUpdate;
            }
            return success;
        }

        public void Shutdown()
        {
            sgBleShutdown();
            // Keep callback _onCentralStateUpdate
        }

        public bool StartScan(string requiredServiceUuids, Action<ScannedPeripheral> onScannedPeripheral)
        {
            DiscoveredPeripheralHandler onDiscoveredPeripheral = jsonStr =>
            {
                var adv = JsonUtility.FromJson<AdvertisementDataJson>(jsonStr);
                onScannedPeripheral(new ScannedPeripheral(new NativeScannedPeripheral(adv.address), adv));
            };
            // Starts a new scan if on is already in progress
            bool success = sgBleStartScan(requiredServiceUuids, onDiscoveredPeripheral);
            if (success)
            {
                // Store callback now that scan is started
                _onDiscoveredPeripheral = onDiscoveredPeripheral;
            }
            return success;
        }

        public void StopScan()
        {
            sgBleStopScan();
            _onDiscoveredPeripheral = null;
        }

        private NativePeripheralHandle CreatePeripheral(ulong bluetoothAddress, string debugName, NativeConnectionEventHandler onConnectionEvent)
        {
            PeripheralConnectionEventHandler peripheralConnectionEventHandler = (ulong peripheralId, int connectionEvent, int reason) =>
            {
                try
                {
                    onConnectionEvent((ConnectionEvent)connectionEvent, (ConnectionEventReason)reason);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            };

            bool success = sgBleCreatePeripheral(bluetoothAddress, peripheralConnectionEventHandler);
            return new NativePeripheralHandle(success ? new NativePeripheral(bluetoothAddress, debugName, peripheralConnectionEventHandler) : null);
        }

        public NativePeripheralHandle CreatePeripheral(ulong bluetoothAddress, NativeConnectionEventHandler onConnectionEvent)
        {
            return CreatePeripheral(bluetoothAddress, bluetoothAddress.ToString(), onConnectionEvent);
        }

        public NativePeripheralHandle CreatePeripheral(IScannedPeripheral scannedPeripheral, NativeConnectionEventHandler onConnectionEvent)
        {
            return CreatePeripheral(GetPeripheralAddress(scannedPeripheral), scannedPeripheral.Name, onConnectionEvent);
        }

        public void ReleasePeripheral(NativePeripheralHandle peripheral)
        {
            var periph = (NativePeripheral)peripheral.NativePeripheral;
            periph.Release();
            sgBleReleasePeripheral(GetPeripheralAddress(peripheral));
        }

        public void ConnectPeripheral(NativePeripheralHandle peripheral, string requiredServicesUuids, bool autoConnect, NativeRequestResultHandler onResult)
        {
            sgBleConnectPeripheral(GetPeripheralAddress(peripheral), requiredServicesUuids, autoConnect,
                GetRequestStatusHandler(RequestOperation.ConnectPeripheral, peripheral, onResult));
        }

        public void DisconnectPeripheral(NativePeripheralHandle peripheral, NativeRequestResultHandler onResult)
        {
            var periph = (NativePeripheral)peripheral.NativePeripheral;
            sgBleDisconnectPeripheral(GetPeripheralAddress(peripheral),
                GetRequestStatusHandler(RequestOperation.DisconnectPeripheral, peripheral, onResult));
            //TODO use static C# callback that redirects to peripheral, we might still get a callback on a different thread...
            //periph.ForgetAllValueHandlers(); // We won't get such events anymore
        }

        public string GetPeripheralName(NativePeripheralHandle peripheral)
        {
            return sgBleGetPeripheralName(GetPeripheralAddress(peripheral));
        }

        public int GetPeripheralMtu(NativePeripheralHandle peripheral)
        {
            return sgBleGetPeripheralMtu(GetPeripheralAddress(peripheral));
        }

        public void RequestPeripheralMtu(NativePeripheralHandle peripheral, int mtu, NativeValueRequestResultHandler<int> onMtuResult)
        {
            // No support for MTU request with WinRT Bluetooth, we just return the automatically negotiated MTU
            onMtuResult(GetPeripheralMtu(peripheral), RequestStatus.NotSupported);
        }

        public void ReadPeripheralRssi(NativePeripheralHandle peripheral, NativeValueRequestResultHandler<int> onRssiRead)
        {
            // No support for reading RSSI of connected device with WinRT Bluetooth
            onRssiRead(int.MinValue, RequestStatus.NotSupported);
        }

        public string GetPeripheralDiscoveredServices(NativePeripheralHandle peripheral)
        {
            return sgBleGetPeripheralDiscoveredServices(GetPeripheralAddress(peripheral));
        }

        public string GetPeripheralServiceCharacteristics(NativePeripheralHandle peripheral, string serviceUuid)
        {
            return sgBleGetPeripheralServiceCharacteristics(GetPeripheralAddress(peripheral), serviceUuid);
        }

        public CharacteristicProperties GetCharacteristicProperties(NativePeripheralHandle peripheral, string serviceUuid, string characteristicUuid, uint instanceIndex)
        {
            return (CharacteristicProperties)sgBleGetCharacteristicProperties(GetPeripheralAddress(peripheral), serviceUuid, characteristicUuid, instanceIndex);
        }

        public void ReadCharacteristic(NativePeripheralHandle peripheral, string serviceUuid, string characteristicUuid, uint instanceIndex, NativeValueRequestResultHandler<byte[]> onValueChanged, NativeRequestResultHandler onResult)
        {
            var valueChangedHandler = GetValueChangedHandler(peripheral, onValueChanged);
            var periph = (NativePeripheral)peripheral.NativePeripheral;
            periph.KeepValueChangedHandler(serviceUuid, characteristicUuid, instanceIndex, valueChangedHandler);
            sgBleReadCharacteristicValue(GetPeripheralAddress(peripheral), serviceUuid, characteristicUuid, instanceIndex, valueChangedHandler,
                GetRequestStatusHandler(RequestOperation.ReadCharacteristic, peripheral, onResult));
            //TODO when to forget value changed handler?
        }

        public void WriteCharacteristic(NativePeripheralHandle peripheral, string serviceUuid, string characteristicUuid, uint instanceIndex, byte[] data, bool withoutResponse, NativeRequestResultHandler onResult)
        {
            var ptr = Marshal.AllocHGlobal(data.Length);
            try
            {
                Marshal.Copy(data, 0, ptr, data.Length);
                sgBleWriteCharacteristicValue(GetPeripheralAddress(peripheral), serviceUuid, characteristicUuid, instanceIndex, ptr, (UIntPtr)data.Length, withoutResponse,
                    GetRequestStatusHandler(RequestOperation.WriteCharacteristic, peripheral, onResult));
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        public void SubscribeCharacteristic(NativePeripheralHandle peripheral, string serviceUuid, string characteristicUuid, uint instanceIndex, NativeValueRequestResultHandler<byte[]> onValueChanged, NativeRequestResultHandler onResult)
        {
            var valueChangedHandler = GetValueChangedHandler(peripheral, onValueChanged);
            var periph = (NativePeripheral)peripheral.NativePeripheral;
            periph.KeepValueChangedHandler(serviceUuid, characteristicUuid, instanceIndex, valueChangedHandler);
            sgBleSetNotifyCharacteristic(GetPeripheralAddress(peripheral), serviceUuid, characteristicUuid, instanceIndex, valueChangedHandler,
                GetRequestStatusHandler(RequestOperation.SubscribeCharacteristic, peripheral, onResult));
        }

        public void UnsubscribeCharacteristic(NativePeripheralHandle peripheral, string serviceUuid, string characteristicUuid, uint instanceIndex, NativeRequestResultHandler onResult)
        {
            sgBleSetNotifyCharacteristic(GetPeripheralAddress(peripheral), serviceUuid, characteristicUuid, instanceIndex, null,
                GetRequestStatusHandler(RequestOperation.UnsubscribeCharacteristic, peripheral, onResult));
            var periph = (NativePeripheral)peripheral.NativePeripheral;
            periph.ForgetValueChangedHandler(serviceUuid, characteristicUuid, instanceIndex);
        }

        private ulong GetPeripheralAddress(IScannedPeripheral scannedPeripheral)
        {
            return ((NativeScannedPeripheral)scannedPeripheral.NativeDevice).BluetoothAddress;
        }

        private ulong GetPeripheralAddress(NativePeripheralHandle peripheral)
        {
            return ((NativePeripheral)peripheral.NativePeripheral).BluetoothAddress;
        }

        private RequestStatusHandler GetRequestStatusHandler(RequestOperation operation, NativePeripheralHandle peripheral, NativeRequestResultHandler onResult)
        {
            var periph = (NativePeripheral)peripheral.NativePeripheral;
            RequestStatusHandler onRequestStatus = null;
            onRequestStatus = errorCode =>
            {
                try
                {
                    // Log success or error
                    if (errorCode == RequestStatus.Success)
                    {
                        Debug.Log($"{operation} ==> Request successful");
                    }
                    else
                    {
                        Debug.LogError($"{operation} ==> Request failed: {errorCode}");
                    }

                    // We can forget about this handler instance, it won't be called anymore
                    periph.ForgetRequestHandler(onRequestStatus);

                    // Notify user code
                    onResult(errorCode);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            };
            periph.KeepRequestHandler(onRequestStatus);
            return onRequestStatus;
        }

        private ValueChangedHandler GetValueChangedHandler(NativePeripheralHandle peripheral, NativeValueRequestResultHandler<byte[]> onValueChanged)
        {
            var periph = (NativePeripheral)peripheral.NativePeripheral;
            ValueChangedHandler valueChangedHandler = (IntPtr data, UIntPtr length, RequestStatus errorCode) =>
            {
                try
                {
                    var array = new byte[(int)length];
                    if (data != IntPtr.Zero)
                    {
                        Marshal.Copy(data, array, 0, array.Length);
                    }
                    onValueChanged(array, errorCode);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            };

            return valueChangedHandler;
        }
    }
}
