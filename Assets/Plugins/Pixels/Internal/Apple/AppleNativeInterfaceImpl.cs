using AOT;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

using RequestIndex = System.Int32; // Can't have uint because Interlocked.Increment() only has support for signed integer in the version of .NET framework used by Unity at this time

//TODO at some point we want to marshal Guid values instead of strings, purely for optimization reasons
namespace Systemic.Pixels.Unity.BluetoothLE.Internal.Apple
{
    internal sealed class AppleNativeInterfaceImpl : INativeInterfaceImpl
    {
        sealed class NativeCBPeripheral : ScannedPeripheral.ISystemDevice
        {
            public NativeCBPeripheral(string peripheralId) => PeripheralId = peripheralId;

            public string PeripheralId { get; }
        }

        sealed class NativePxPeripheral : PeripheralHandle.INativePeripheral
        {
            public NativePxPeripheral(string peripheralId) => PeripheralId = peripheralId;

            public string PeripheralId { get; }
        }

        const string _libName =
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            "PixelsLibraryMacOS";
#elif UNITY_IOS
            "__Internal";
#else
            "unsupported";
#endif

        delegate void CentralStateUpdateHandler(bool isAvailable);
        delegate void DiscoveredPeripheralHandler(string advertisementDataJson);
        delegate void PeripheralConnectionEventHandler(RequestIndex requestIndex, string peripheralId, int connectionEvent, int reason);
        delegate void RequestStatusHandler(RequestIndex requestIndex, int errorCode, string errorMessage);
        delegate void RssiReadHandler(RequestIndex requestIndex, int rssi, int errorCode, string errorMessage);
        delegate void ValueChangedHandler(RequestIndex requestIndex, IntPtr data, UIntPtr length, int errorCode, string errorMessage);

        [DllImport(_libName)]
        private static extern bool pxBleInitialize(CentralStateUpdateHandler onCentralStateUpdate);

        [DllImport(_libName)]
        private static extern void pxBleShutdown();

        [DllImport(_libName)]
        private static extern bool pxBleStartScan(string requiredServicesUuids, bool allowDuplicates, DiscoveredPeripheralHandler onDiscoveredPeripheral);

        [DllImport(_libName)]
        private static extern void pxBleStopScan();

        [DllImport(_libName)]
        private static extern bool pxBleCreatePeripheral(string peripheralId, PeripheralConnectionEventHandler onConnectionEvent, RequestIndex requestIndex);

        [DllImport(_libName)]
        private static extern void pxBleReleasePeripheral(string peripheralId);

        [DllImport(_libName)]
        private static extern void pxBleConnectPeripheral(string peripheralId, string requiredServicesUuids, RequestStatusHandler onRequestStatus, RequestIndex requestIndex);

        [DllImport(_libName)]
        private static extern void pxBleDisconnectPeripheral(string peripheralId, RequestStatusHandler onRequestStatus, RequestIndex requestIndex);

        [DllImport(_libName)]
        private static extern string pxBleGetPeripheralName(string peripheralId);

        [DllImport(_libName)]
        private static extern int pxBleGetPeripheralMtu(string peripheralId);

        [DllImport(_libName)]
        private static extern void pxBleReadPeripheralRssi(string peripheralId, RssiReadHandler onRssiRead, RequestIndex requestIndex);

        [DllImport(_libName)]
        private static extern string pxBleGetPeripheralDiscoveredServices(string peripheralId);

        [DllImport(_libName)]
        private static extern string pxBleGetPeripheralServiceCharacteristics(string peripheralId, string serviceUuid);

        [DllImport(_libName)]
        private static extern ulong pxBleGetCharacteristicProperties(string peripheralId, string serviceUuid, string characteristicUuid, uint instanceIndex);

        [DllImport(_libName)]
        private static extern void pxBleReadCharacteristicValue(string peripheralId, string serviceUuid, string characteristicUuid, uint instanceIndex, ValueChangedHandler onValueChanged, RequestStatusHandler onRequestStatus, RequestIndex requestIndex);

        [DllImport(_libName)]
        private static extern void pxBleWriteCharacteristicValue(string peripheralId, string serviceUuid, string characteristicUuid, uint instanceIndex, IntPtr data, UIntPtr length, bool withoutResponse, RequestStatusHandler onRequestStatus, RequestIndex requestIndex);

        [DllImport(_libName)]
        private static extern void pxBleSetNotifyCharacteristic(string peripheralId, string serviceUuid, string characteristicUuid, uint instanceIndex, ValueChangedHandler onValueChanged, RequestStatusHandler onRequestStatus, RequestIndex requestIndex);

        static NativeBluetoothEventHandler _onBluetoothEvent;
        static DiscoveredPeripheralHandler _onDiscoveredPeripheral; // We can have only one scan at a time, so one handler
        static Dictionary<RequestIndex, NativePeripheralConnectionEventHandler> _onConnectionEventHandlers = new Dictionary<RequestIndex, NativePeripheralConnectionEventHandler>();
        static Dictionary<RequestIndex, NativeRequestResultHandler> _onRequestStatusHandlers = new Dictionary<RequestIndex, NativeRequestResultHandler>();
        static Dictionary<RequestIndex, NativeValueRequestResultHandler<int>> _onRssiReadHandlers = new Dictionary<RequestIndex, NativeValueRequestResultHandler<int>>();
        static Dictionary<RequestIndex, NativeValueChangedHandler> _onValueChangedHandlers = new Dictionary<RequestIndex, NativeValueChangedHandler>();
        static volatile RequestIndex _requestIndex;

        [MonoPInvokeCallback(typeof(CentralStateUpdateHandler))]
        static void OnCentralStateUpdate(bool isAvailable)
        {
            _onBluetoothEvent(isAvailable);
        }

        [MonoPInvokeCallback(typeof(DiscoveredPeripheralHandler))]
        static void OnDiscoveredPeripheral(string advertisementDataJson)
        {
            try
            {
                _onDiscoveredPeripheral(advertisementDataJson);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        [MonoPInvokeCallback(typeof(PeripheralConnectionEventHandler))]
        static void OnPeripheralConnectionStatusChanged(RequestIndex requestIndex, string peripheralId, int connectionEvent, int reason)
        {
            try
            {
                NativePeripheralConnectionEventHandler handler;
                lock (_onConnectionEventHandlers)
                {
                    _onConnectionEventHandlers.TryGetValue(requestIndex, out handler);
                }
                if (handler != null)
                {
                    handler((ConnectionEvent)connectionEvent, (ConnectionEventReason)reason);
                }
                else
                {
                    Debug.LogError($"ConnectionEvent handler #{requestIndex} not found");
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        [MonoPInvokeCallback(typeof(RequestStatusHandler))]
        static void OnRequestStatus(RequestIndex requestIndex, int errorCode, string errorMessage)
        {
            try
            {
                NativeRequestResultHandler handler;
                lock (_onConnectionEventHandlers)
                {
                    if (_onRequestStatusHandlers.TryGetValue(requestIndex, out handler))
                    {
                        _onRequestStatusHandlers.Remove(requestIndex);
                    }
                }
                if (handler != null)
                {
                    handler(new NativeError(errorCode, errorMessage));
                }
                else
                {
                    Debug.LogError($"RequestStatus handler #{requestIndex} not found");
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        [MonoPInvokeCallback(typeof(RssiReadHandler))]
        static void OnRssiReadHandler(RequestIndex requestIndex, int rssi, int errorCode, string errorMessage)
        {
            NativeValueRequestResultHandler<int> handler;
            lock (_onRssiReadHandlers)
            {
                _onRssiReadHandlers.TryGetValue(requestIndex, out handler);
            }
            if (handler != null)
            {
                handler(rssi, new NativeError(errorCode, errorMessage));
            }
            else
            {
                Debug.LogError($"RssiReadHandler handler #{requestIndex} not found");
            }
        }

        [MonoPInvokeCallback(typeof(ValueChangedHandler))]
        static void OnValueChangedHandler(RequestIndex requestIndex, IntPtr data, UIntPtr length, int errorCode, string errorMessage)
        {
            var array = new byte[(int)length];
            if (data != IntPtr.Zero)
            {
                Marshal.Copy(data, array, 0, array.Length);
            }
            try
            {
                NativeValueChangedHandler handler;
                lock (_onValueChangedHandlers)
                {
                    _onValueChangedHandlers.TryGetValue(requestIndex, out handler);
                }
                if (handler != null)
                {
                    handler(array, new NativeError(errorCode, errorMessage));
                }
                else
                {
                    Debug.LogError($"ValueChanged handler #{requestIndex} not found");
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public bool Initialize(NativeBluetoothEventHandler onBluetoothEvent)
        {
            _onBluetoothEvent = onBluetoothEvent;
            return pxBleInitialize(OnCentralStateUpdate);
        }

        public void Shutdown()
        {
            pxBleShutdown();
        }

        public bool StartScan(string requiredServiceUuids, Action<ScannedPeripheral> onScannedPeripheral)
        {
            _onDiscoveredPeripheral = jsonStr =>
            {
                var adv = JsonUtility.FromJson<AdvertisementDataJson>(jsonStr);
                onScannedPeripheral(new ScannedPeripheral(new NativeCBPeripheral(adv.systemId), adv));
            };
            return pxBleStartScan(requiredServiceUuids, true, OnDiscoveredPeripheral);
        }

        public void StopScan()
        {
            pxBleStopScan();
        }

        // Not available on Apple systems
        public PeripheralHandle CreatePeripheral(ulong bluetoothAddress, NativePeripheralConnectionEventHandler onConnectionEvent)
        {
            return new PeripheralHandle();
        }

        public PeripheralHandle CreatePeripheral(ScannedPeripheral peripheral, NativePeripheralConnectionEventHandler onConnectionEvent)
        {
            var requestIndex = Interlocked.Increment(ref _requestIndex);
            lock (_onConnectionEventHandlers)
            {
                _onConnectionEventHandlers.Add(requestIndex, onConnectionEvent);
            }

            string peripheralId = GetPeripheralId(peripheral);
            bool success = pxBleCreatePeripheral(peripheralId, OnPeripheralConnectionStatusChanged, requestIndex);
            return new PeripheralHandle(success ? new NativePxPeripheral(peripheralId) : null);
        }

        public void ReleasePeripheral(PeripheralHandle peripheral)
        {
            pxBleReleasePeripheral(GetPeripheralId(peripheral));
        }

        public void ConnectPeripheral(PeripheralHandle peripheral, string requiredServicesUuids, NativeRequestResultHandler onResult)
        {
            var requestIndex = SetRequestHandler(onResult);

            pxBleConnectPeripheral(GetPeripheralId(peripheral), requiredServicesUuids, OnRequestStatus, requestIndex);
        }

        public void DisconnectPeripheral(PeripheralHandle peripheral, NativeRequestResultHandler onResult)
        {
            var requestIndex = SetRequestHandler(onResult);

            pxBleDisconnectPeripheral(GetPeripheralId(peripheral), OnRequestStatus, requestIndex);
        }

        public string GetPeripheralName(PeripheralHandle peripheral)
        {
            return pxBleGetPeripheralName(GetPeripheralId(peripheral));
        }

        public int GetPeripheralMtu(PeripheralHandle peripheral)
        {
            return pxBleGetPeripheralMtu(GetPeripheralId(peripheral));
        }

        public void RequestPeripheralMtu(PeripheralHandle peripheral, int mtu, NativeValueRequestResultHandler<int> onMtuResult)
        {
            // No support for MTU request with Apple Core Bluetooth, we just return the automatically negotiated MTU
            onMtuResult(GetPeripheralMtu(peripheral), new NativeError((int)Error.NotSupported));
        }

        public void ReadPeripheralRssi(PeripheralHandle peripheral, NativeValueRequestResultHandler<int> onRssiRead)
        {
            var requestIndex = Interlocked.Increment(ref _requestIndex);
            lock (_onRssiReadHandlers)
            {
                _onRssiReadHandlers.Add(requestIndex, onRssiRead);
            }

            pxBleReadPeripheralRssi(GetPeripheralId(peripheral), OnRssiReadHandler, requestIndex);
        }

        public string GetPeripheralDiscoveredServices(PeripheralHandle peripheral)
        {
            return pxBleGetPeripheralDiscoveredServices(GetPeripheralId(peripheral));
        }

        public string GetPeripheralServiceCharacteristics(PeripheralHandle peripheral, string serviceUuid)
        {
            return pxBleGetPeripheralServiceCharacteristics(GetPeripheralId(peripheral), serviceUuid);
        }

        public CharacteristicProperties GetCharacteristicProperties(PeripheralHandle peripheral, string serviceUuid, string characteristicUuid, uint instanceIndex)
        {
            return (CharacteristicProperties)pxBleGetCharacteristicProperties(GetPeripheralId(peripheral), serviceUuid, characteristicUuid, instanceIndex);
        }

        public void ReadCharacteristic(PeripheralHandle peripheral, string serviceUuid, string characteristicUuid, uint instanceIndex, NativeValueChangedHandler onValueChanged, NativeRequestResultHandler onResult)
        {
            var requestIndex = SetRequestHandler(onResult);
            lock (_onValueChangedHandlers)
            {
                _onValueChangedHandlers.Add(requestIndex, onValueChanged);
            }

            pxBleReadCharacteristicValue(GetPeripheralId(peripheral), serviceUuid, characteristicUuid, instanceIndex, OnValueChangedHandler, OnRequestStatus, requestIndex);
        }

        public void WriteCharacteristic(PeripheralHandle peripheral, string serviceUuid, string characteristicUuid, uint instanceIndex, byte[] data, bool withoutResponse, NativeRequestResultHandler onResult)
        {
            var requestIndex = SetRequestHandler(onResult);

            var ptr = Marshal.AllocHGlobal(data.Length);
            try
            {
                Marshal.Copy(data, 0, ptr, data.Length);
                pxBleWriteCharacteristicValue(GetPeripheralId(peripheral), serviceUuid, characteristicUuid, instanceIndex, ptr, (UIntPtr)data.Length, withoutResponse, OnRequestStatus, requestIndex);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        public void SubscribeCharacteristic(PeripheralHandle peripheral, string serviceUuid, string characteristicUuid, uint instanceIndex, NativeValueChangedHandler onValueChanged, NativeRequestResultHandler onResult)
        {
            var requestIndex = SetRequestHandler(onResult);
            lock (_onValueChangedHandlers)
            {
                _onValueChangedHandlers.Add(requestIndex, onValueChanged);
            }

            pxBleSetNotifyCharacteristic(GetPeripheralId(peripheral), serviceUuid, characteristicUuid, instanceIndex, OnValueChangedHandler, OnRequestStatus, requestIndex);
        }

        public void UnsubscribeCharacteristic(PeripheralHandle peripheral, string serviceUuid, string characteristicUuid, uint instanceIndex, NativeRequestResultHandler onResult)
        {
            var requestIndex = SetRequestHandler(onResult);

            pxBleSetNotifyCharacteristic(GetPeripheralId(peripheral), serviceUuid, characteristicUuid, instanceIndex, null, OnRequestStatus, requestIndex);
        }

        private string GetPeripheralId(ScannedPeripheral scannedPeripheral) => ((NativeCBPeripheral)scannedPeripheral.SystemDevice).PeripheralId;

        private string GetPeripheralId(PeripheralHandle peripheralHandle) => ((NativePxPeripheral)peripheralHandle.SystemClient).PeripheralId;

        private RequestIndex SetRequestHandler(NativeRequestResultHandler onResult)
        {
            var requestIndex = Interlocked.Increment(ref _requestIndex);
            lock (_onRequestStatusHandlers)
            {
                _onRequestStatusHandlers.Add(requestIndex, onResult);
            }
            return requestIndex;
        }
    }
}
