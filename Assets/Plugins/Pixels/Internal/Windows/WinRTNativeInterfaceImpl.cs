using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Systemic.Unity.BluetoothLE.Internal.Windows
{
    internal sealed class WinRTNativeInterfaceImpl : INativeInterfaceImpl
    {
        #region INativeDevice and INativePeripheralHandleImpl implementations

        sealed class NativeScannedPeripheral : INativeDevice
        {
            public ulong BluetoothAddress { get; }

            public string Name { get; }

            public bool IsValid => BluetoothAddress != 0;

            public NativeScannedPeripheral(ulong bluetoothAddress, string name)
                => (BluetoothAddress, Name) = (bluetoothAddress, name);
        }

        sealed class NativePeripheral : INativePeripheralHandleImpl
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

        #endregion

        #region Native library bindings

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
        delegate void ValueReadHandler(IntPtr data, UIntPtr length, RequestStatus errorCode);
        delegate void ValueChangedHandler(IntPtr data, UIntPtr length);

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
        private static extern void sgBleConnectPeripheral(ulong peripheralId, string requiredServicesUuids, bool autoReconnect, RequestStatusHandler onRequestStatus);

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
        private static extern void sgBleReadCharacteristicValue(ulong peripheralId, string serviceUuid, string characteristicUuid, uint instanceIndex, ValueReadHandler onValueRead);

        [DllImport(_libName)]
        private static extern void sgBleWriteCharacteristicValue(ulong peripheralId, string serviceUuid, string characteristicUuid, uint instanceIndex, IntPtr data, UIntPtr length, bool withoutResponse, RequestStatusHandler onRequestStatus);

        [DllImport(_libName)]
        private static extern void sgBleSetNotifyCharacteristic(ulong peripheralId, string serviceUuid, string characteristicUuid, uint instanceIndex, ValueChangedHandler onValueChanged, RequestStatusHandler onRequestStatus);

        #endregion

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

        public bool StartScan(string requiredServiceUuids, Action<INativeDevice, NativeAdvertisementDataJson> onScannedPeripheral)
        {
            DiscoveredPeripheralHandler onDiscoveredPeripheral = jsonStr =>
            {
                var adv = JsonUtility.FromJson<NativeAdvertisementDataJson>(jsonStr);
                onScannedPeripheral(new NativeScannedPeripheral(adv.address, adv.name), adv);
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

        private INativePeripheralHandleImpl CreatePeripheral(ulong bluetoothAddress, string debugName, NativeConnectionEventHandler onConnectionEvent)
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
            return success ? new NativePeripheral(bluetoothAddress, debugName, peripheralConnectionEventHandler) : null;
        }

        public INativePeripheralHandleImpl CreatePeripheral(ulong bluetoothAddress, NativeConnectionEventHandler onConnectionEvent)
        {
            return CreatePeripheral(bluetoothAddress, bluetoothAddress.ToString(), onConnectionEvent);
        }

        public INativePeripheralHandleImpl CreatePeripheral(INativeDevice device, NativeConnectionEventHandler onConnectionEvent)
        {
            var p = (NativeScannedPeripheral)device;
            return CreatePeripheral(p.BluetoothAddress, p.Name, onConnectionEvent);
        }

        public void ReleasePeripheral(INativePeripheralHandleImpl peripheralHandle)
        {
            var periph = (NativePeripheral)peripheralHandle;
            periph.Release();
            sgBleReleasePeripheral(GetPeripheralAddress(peripheralHandle));
        }

        public void ConnectPeripheral(INativePeripheralHandleImpl peripheralHandle, string requiredServicesUuids, bool autoReconnect, NativeRequestResultHandler onResult)
        {
            sgBleConnectPeripheral(GetPeripheralAddress(peripheralHandle), requiredServicesUuids, autoReconnect,
                GetRequestStatusHandler(RequestOperation.ConnectPeripheral, peripheralHandle, onResult));
        }

        public void DisconnectPeripheral(INativePeripheralHandleImpl peripheralHandle, NativeRequestResultHandler onResult)
        {
            var periph = (NativePeripheral)peripheralHandle;
            sgBleDisconnectPeripheral(GetPeripheralAddress(peripheralHandle),
                GetRequestStatusHandler(RequestOperation.DisconnectPeripheral, peripheralHandle, onResult));
            //TODO use static C# callback that redirects to peripheral, we might still get a callback on a different thread...
            //periph.ForgetAllValueHandlers(); // We won't get such events anymore
        }

        public string GetPeripheralName(INativePeripheralHandleImpl peripheralHandle)
        {
            return sgBleGetPeripheralName(GetPeripheralAddress(peripheralHandle));
        }

        public int GetPeripheralMtu(INativePeripheralHandleImpl peripheralHandle)
        {
            return sgBleGetPeripheralMtu(GetPeripheralAddress(peripheralHandle));
        }

        public void RequestPeripheralMtu(INativePeripheralHandleImpl peripheralHandle, int mtu, NativeValueRequestResultHandler<int> onMtuResult)
        {
            // No support for MTU request with WinRT Bluetooth, we just return the automatically negotiated MTU
            onMtuResult(GetPeripheralMtu(peripheralHandle), RequestStatus.NotSupported);
        }

        public void ReadPeripheralRssi(INativePeripheralHandleImpl peripheralHandle, NativeValueRequestResultHandler<int> onRssiRead)
        {
            // No support for reading RSSI of connected device with WinRT Bluetooth
            onRssiRead(int.MinValue, RequestStatus.NotSupported);
        }

        public string GetPeripheralDiscoveredServices(INativePeripheralHandleImpl peripheralHandle)
        {
            return sgBleGetPeripheralDiscoveredServices(GetPeripheralAddress(peripheralHandle));
        }

        public string GetPeripheralServiceCharacteristics(INativePeripheralHandleImpl peripheralHandle, string serviceUuid)
        {
            return sgBleGetPeripheralServiceCharacteristics(GetPeripheralAddress(peripheralHandle), serviceUuid);
        }

        public CharacteristicProperties GetCharacteristicProperties(INativePeripheralHandleImpl peripheralHandle, string serviceUuid, string characteristicUuid, uint instanceIndex)
        {
            return (CharacteristicProperties)sgBleGetCharacteristicProperties(GetPeripheralAddress(peripheralHandle), serviceUuid, characteristicUuid, instanceIndex);
        }

        public void ReadCharacteristic(INativePeripheralHandleImpl peripheralHandle, string serviceUuid, string characteristicUuid, uint instanceIndex, NativeValueRequestResultHandler<byte[]> onValueRead)
        {
            var valueReadHandler = GetValueReadHandler(peripheralHandle, onValueRead);
            var periph = (NativePeripheral)peripheralHandle;
            //TODO store handler periph.KeepValueChangedHandler(serviceUuid, characteristicUuid, instanceIndex, valueReadHandler);
            sgBleReadCharacteristicValue(GetPeripheralAddress(peripheralHandle), serviceUuid, characteristicUuid, instanceIndex, valueReadHandler);
        }

        public void WriteCharacteristic(INativePeripheralHandleImpl peripheralHandle, string serviceUuid, string characteristicUuid, uint instanceIndex, byte[] data, bool withoutResponse, NativeRequestResultHandler onResult)
        {
            var ptr = Marshal.AllocHGlobal(data.Length);
            try
            {
                Marshal.Copy(data, 0, ptr, data.Length);
                sgBleWriteCharacteristicValue(GetPeripheralAddress(peripheralHandle), serviceUuid, characteristicUuid, instanceIndex, ptr, (UIntPtr)data.Length, withoutResponse,
                    GetRequestStatusHandler(RequestOperation.WriteCharacteristic, peripheralHandle, onResult));
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        public void SubscribeCharacteristic(INativePeripheralHandleImpl peripheralHandle, string serviceUuid, string characteristicUuid, uint instanceIndex, NativeValueRequestResultHandler<byte[]> onValueChanged, NativeRequestResultHandler onResult)
        {
            var valueChangedHandler = GetValueChangedHandler(peripheralHandle, onValueChanged);
            var periph = (NativePeripheral)peripheralHandle;
            periph.KeepValueChangedHandler(serviceUuid, characteristicUuid, instanceIndex, valueChangedHandler);
            sgBleSetNotifyCharacteristic(GetPeripheralAddress(peripheralHandle), serviceUuid, characteristicUuid, instanceIndex, valueChangedHandler,
                GetRequestStatusHandler(RequestOperation.SubscribeCharacteristic, peripheralHandle, onResult));
        }

        public void UnsubscribeCharacteristic(INativePeripheralHandleImpl peripheralHandle, string serviceUuid, string characteristicUuid, uint instanceIndex, NativeRequestResultHandler onResult)
        {
            sgBleSetNotifyCharacteristic(GetPeripheralAddress(peripheralHandle), serviceUuid, characteristicUuid, instanceIndex, null,
                GetRequestStatusHandler(RequestOperation.UnsubscribeCharacteristic, peripheralHandle, onResult));
            var periph = (NativePeripheral)peripheralHandle;
            periph.ForgetValueChangedHandler(serviceUuid, characteristicUuid, instanceIndex);
        }

        private ulong GetPeripheralAddress(INativePeripheralHandleImpl peripheralHandle)
        {
            return ((NativePeripheral)peripheralHandle).BluetoothAddress;
        }

        private RequestStatusHandler GetRequestStatusHandler(RequestOperation operation, INativePeripheralHandleImpl peripheralHandle, NativeRequestResultHandler onResult)
        {
            var periph = (NativePeripheral)peripheralHandle;
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

        private ValueReadHandler GetValueReadHandler(INativePeripheralHandleImpl peripheralHandle, NativeValueRequestResultHandler<byte[]> onValueRead)
        {
            var periph = (NativePeripheral)peripheralHandle;
            ValueReadHandler valueChangedHandler = (IntPtr data, UIntPtr length, RequestStatus status) =>
            {
                try
                {
                    var array = new byte[(int)length];
                    if (data != IntPtr.Zero)
                    {
                        Marshal.Copy(data, array, 0, array.Length);
                    }
                    onValueRead(array, status);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            };

            return valueChangedHandler;
        }

        private ValueChangedHandler GetValueChangedHandler(INativePeripheralHandleImpl peripheralHandle, NativeValueRequestResultHandler<byte[]> onValueChanged)
        {
            var periph = (NativePeripheral)peripheralHandle;
            ValueChangedHandler valueChangedHandler = (IntPtr data, UIntPtr length) =>
            {
                try
                {
                    byte[] array = null;
                    if (data != IntPtr.Zero)
                    {
                        array = new byte[(int)length];
                        Marshal.Copy(data, array, 0, array.Length);
                    }
                    onValueChanged(array, array != null ? RequestStatus.Success : RequestStatus.Error);
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
