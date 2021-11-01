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
        sealed class NativeCBPeripheral : INativeDevice
        {
            public string PeripheralId { get; }

            public bool IsValid => PeripheralId != null;

            public NativeCBPeripheral(string peripheralId)
                => PeripheralId = peripheralId;
        }

        sealed class NativePxPeripheral : INativePeripheral
        {
            Dictionary<string, RequestIndex> _valueChangedRequestIndices = new Dictionary<string, RequestIndex>();

            public string PeripheralId { get; }

            public bool IsValid => PeripheralId != null;

            public RequestIndex ConnectionEventRequestIndex { get; }

            public NativePxPeripheral(string peripheralId, RequestIndex connectionEventRequestIndex)
                => (PeripheralId, ConnectionEventRequestIndex) = (peripheralId, connectionEventRequestIndex);

            public void AddValueChangedHandlerRequestIndex(string serviceUuid, string characteristicUuid, uint instanceIndex, RequestIndex valueChangedRequestIndex)
            {
                lock (_valueChangedRequestIndices)
                {
                    _valueChangedRequestIndices[$"{serviceUuid}:{characteristicUuid}#{instanceIndex}"] = valueChangedRequestIndex;
                }
            }

            public RequestIndex GetAndRemoveValueChangedHandlerRequestIndex(string serviceUuid, string characteristicUuid, uint instanceIndex)
            {
                RequestIndex index = 0;
                lock (_valueChangedRequestIndices)
                {
                    string key = $"{serviceUuid}:{characteristicUuid}#{instanceIndex}";
                    Debug.Assert(_valueChangedRequestIndices.ContainsKey(key));
                    if (_valueChangedRequestIndices.TryGetValue(key, out index))
                    {
                        _valueChangedRequestIndices.Remove(key);
                    }
                }
                return index;
            }

            public void ForgetAllValueHandlers()
            {
                lock (_valueChangedRequestIndices)
                {
                    _valueChangedRequestIndices.Clear();
                }
            }
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
        delegate void RequestStatusHandler(RequestIndex requestIndex, int errorCode);
        delegate void RssiReadHandler(RequestIndex requestIndex, int rssi, int errorCode);
        delegate void ValueChangedHandler(RequestIndex requestIndex, IntPtr data, UIntPtr length, int errorCode);

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
        static Dictionary<RequestIndex, (Operation, NativeRequestResultHandler)> _onRequestStatusHandlers = new Dictionary<RequestIndex, (Operation, NativeRequestResultHandler)>();
        static Dictionary<RequestIndex, NativeValueRequestResultHandler<int>> _onRssiReadHandlers = new Dictionary<RequestIndex, NativeValueRequestResultHandler<int>>();
        static Dictionary<RequestIndex, NativeValueChangedHandler> _onValueChangedHandlers = new Dictionary<RequestIndex, NativeValueChangedHandler>();
        static volatile RequestIndex _requestIndex;

        [MonoPInvokeCallback(typeof(CentralStateUpdateHandler))]
        static void OnCentralStateUpdate(bool available)
        {
            try
            {
                _onBluetoothEvent(available ? BluetoothStatus.Enabled : BluetoothStatus.Disabled);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
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
        static void OnPeripheralConnectionEvent(RequestIndex requestIndex, string peripheralId, int connectionEvent, int reason)
        {
            try
            {
                // Get C# callback
                NativePeripheralConnectionEventHandler handler;
                lock (_onConnectionEventHandlers)
                {
                    _onConnectionEventHandlers.TryGetValue(requestIndex, out handler);
                }

                if (handler != null)
                {
                    // Notify user code
                    handler((ConnectionEvent)connectionEvent, (ConnectionEventReason)reason);
                }
                else
                {
                    // Callback not found
                    Debug.LogError($"ConnectionEvent handler #{requestIndex} not found, request connection event and reason: {(ConnectionEvent)connectionEvent}, {(ConnectionEventReason)reason}");
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        [MonoPInvokeCallback(typeof(RequestStatusHandler))]
        static void OnRequestStatus(RequestIndex requestIndex, int errorCode)
        {
            try
            {
                var err = (AppleBluetoothError)errorCode;

                // Get C# callback
                Operation op;
                NativeRequestResultHandler handler;
                lock (_onConnectionEventHandlers)
                {
                    if (_onRequestStatusHandlers.TryGetValue(requestIndex, out (Operation, NativeRequestResultHandler) item))
                    {
                        _onRequestStatusHandlers.Remove(requestIndex);
                    }
                    (op, handler) = item;
                }

                if (handler != null)
                {
                    // Log success or error
                    if (err == AppleBluetoothError.Success)
                    {
                        Debug.Log($"{op} ==> Request successful");
                    }
                    else
                    {
                        Debug.LogError($"{op} ==> Request failed: {err} ({errorCode})");
                    }

                    // Notify user code
                    handler(ToRequestStatus(errorCode));
                }
                else
                {
                    // Callback not found
                    Debug.LogError($"RequestStatus handler #{requestIndex} not found, request error code: {err} (0x{errorCode:X})");
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        [MonoPInvokeCallback(typeof(RssiReadHandler))]
        static void OnRssiReadHandler(RequestIndex requestIndex, int rssi, int errorCode)
        {
            try
            {
                var err = (AppleBluetoothError)errorCode;

                // Get C# callback
                NativeValueRequestResultHandler<int> handler;
                lock (_onRssiReadHandlers)
                {
                    _onRssiReadHandlers.TryGetValue(requestIndex, out handler);
                }

                if (handler != null)
                {
                    // Log success or error
                    if (err == AppleBluetoothError.Success)
                    {
                        Debug.Log($"{Operation.ReadPeripheralRssi} ==> Request successful");
                    }
                    else
                    {
                        Debug.LogError($"{Operation.ReadPeripheralRssi} ==> Request failed: {err} ({errorCode})");
                    }

                    // Notify user code
                    handler(rssi, ToRequestStatus(errorCode));
                }
                else
                {
                    // Callback not found
                    Debug.LogError($"RssiReadHandler handler #{requestIndex} not found, request error code: {err} (0x{errorCode:X})");
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        [MonoPInvokeCallback(typeof(ValueChangedHandler))]
        static void OnValueChangedHandler(RequestIndex requestIndex, IntPtr data, UIntPtr length, int errorCode)
        {
            try
            {
                // Get C# callback
                NativeValueChangedHandler handler;
                lock (_onValueChangedHandlers)
                {
                    _onValueChangedHandlers.TryGetValue(requestIndex, out handler);
                }

                if (handler != null)
                {
                    // Notify user code
                    var array = new byte[(int)length];
                    if (data != IntPtr.Zero)
                    {
                        Marshal.Copy(data, array, 0, array.Length);
                    }
                    handler(array, ToRequestStatus(errorCode));
                }
                else
                {
                    Debug.LogError($"ValueChanged handler #{requestIndex} not found, request error code: {(AppleBluetoothError)errorCode} (0x{errorCode:X})");
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

        public PeripheralHandle CreatePeripheral(IScannedPeripheral scannedPeripheral, NativePeripheralConnectionEventHandler onConnectionEvent)
        {
            var requestIndex = Interlocked.Increment(ref _requestIndex);
            lock (_onConnectionEventHandlers)
            {
                _onConnectionEventHandlers.Add(requestIndex, onConnectionEvent);
            }

            string peripheralId = GetPeripheralId(scannedPeripheral);
            bool success = pxBleCreatePeripheral(peripheralId, OnPeripheralConnectionEvent, requestIndex);
            return new PeripheralHandle(success ? new NativePxPeripheral(peripheralId, requestIndex) : null);
        }

        public void ReleasePeripheral(PeripheralHandle peripheral)
        {
            var pxPeripheral = (NativePxPeripheral)peripheral.NativePeripheral;
            pxBleReleasePeripheral(pxPeripheral.PeripheralId);
            lock (_onConnectionEventHandlers)
            {
                _onConnectionEventHandlers.Remove(pxPeripheral.ConnectionEventRequestIndex);
            }
        }

        //TODO on iOS connect waits indefinitely and autoConnect is ignored
        public void ConnectPeripheral(PeripheralHandle peripheral, string requiredServicesUuids, bool autoConnect, NativeRequestResultHandler onResult)
        {
            var requestIndex = SetRequestHandler(Operation.ConnectPeripheral, onResult);

            pxBleConnectPeripheral(GetPeripheralId(peripheral), requiredServicesUuids, OnRequestStatus, requestIndex);
        }

        public void DisconnectPeripheral(PeripheralHandle peripheral, NativeRequestResultHandler onResult)
        {
            var pxPeripheral = (NativePxPeripheral)peripheral.NativePeripheral;
            var requestIndex = SetRequestHandler(Operation.DisconnectPeripheral, onResult);

            pxBleDisconnectPeripheral(pxPeripheral.PeripheralId, OnRequestStatus, requestIndex);
            pxPeripheral.ForgetAllValueHandlers();
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
            onMtuResult(GetPeripheralMtu(peripheral), RequestStatus.NotSupported);
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
            var requestIndex = SetRequestHandler(Operation.ReadCharacteristic, onResult);
            lock (_onValueChangedHandlers)
            {
                _onValueChangedHandlers.Add(requestIndex, onValueChanged);
            }

            pxBleReadCharacteristicValue(GetPeripheralId(peripheral), serviceUuid, characteristicUuid, instanceIndex, OnValueChangedHandler, OnRequestStatus, requestIndex);
        }

        public void WriteCharacteristic(PeripheralHandle peripheral, string serviceUuid, string characteristicUuid, uint instanceIndex, byte[] data, bool withoutResponse, NativeRequestResultHandler onResult)
        {
            var requestIndex = SetRequestHandler(Operation.WriteCharacteristic, onResult);

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
            var pxPeripheral = (NativePxPeripheral)peripheral.NativePeripheral;
            var requestIndex = SetRequestHandler(Operation.SubscribeCharacteristic, onResult);
            lock (_onValueChangedHandlers)
            {
                _onValueChangedHandlers.Add(requestIndex, onValueChanged);
            }
            pxPeripheral.AddValueChangedHandlerRequestIndex(serviceUuid, characteristicUuid, instanceIndex, requestIndex);

            pxBleSetNotifyCharacteristic(pxPeripheral.PeripheralId, serviceUuid, characteristicUuid, instanceIndex, OnValueChangedHandler, OnRequestStatus, requestIndex);
        }

        public void UnsubscribeCharacteristic(PeripheralHandle peripheral, string serviceUuid, string characteristicUuid, uint instanceIndex, NativeRequestResultHandler onResult)
        {
            var pxPeripheral = (NativePxPeripheral)peripheral.NativePeripheral;
            var requestIndex = SetRequestHandler(Operation.UnsubscribeCharacteristic, onResult);

            pxBleSetNotifyCharacteristic(pxPeripheral.PeripheralId, serviceUuid, characteristicUuid, instanceIndex, null, OnRequestStatus, requestIndex);
            requestIndex = pxPeripheral.GetAndRemoveValueChangedHandlerRequestIndex(serviceUuid, characteristicUuid, instanceIndex);
            lock (_onValueChangedHandlers)
            {
                _onValueChangedHandlers.Remove(requestIndex);
            }
        }

        private string GetPeripheralId(IScannedPeripheral scannedPeripheral)
        {
            return ((NativeCBPeripheral)scannedPeripheral.NativeDevice).PeripheralId;
        }

        private string GetPeripheralId(PeripheralHandle peripheralHandle)
        {
            return ((NativePxPeripheral)peripheralHandle.NativePeripheral).PeripheralId;
        }

        private RequestIndex SetRequestHandler(Operation operation, NativeRequestResultHandler onResult)
        {
            var requestIndex = Interlocked.Increment(ref _requestIndex);
            lock (_onRequestStatusHandlers)
            {
                _onRequestStatusHandlers.Add(requestIndex, (operation, onResult));
            }
            return requestIndex;
        }

        enum AppleBluetoothError : int
        {
            CBErrorUnknown = -1,                // An unknown error occurred.
            CBErrorInvalidParameters = -2,      // The specified parameters are invalid.
            CBErrorInvalidHandle = -3,          // The specified attribute handle is invalid.
            CBErrorNotConnected = -4,           // The device isn't currently connected.
            CBErrorOutOfSpace = -5,             // The device has run out of space to complete the intended operation.
            CBErrorOperationCancelled = -6,     // The error represents a canceled operation.
            CBErrorConnectionTimeout = -7,      // The connection timed out.
            CBErrorPeripheralDisconnected = -8, // The peripheral disconnected.
            CBErrorUUIDNotAllowed = -9,         // The specified UUID isn't permitted.
            CBErrorAlreadyAdvertising = -10,    // The peripheral is already advertising.
            CBErrorConnectionFailed = -11,      // The connection failed.
            CBErrorConnectionLimitReached = -12,// The device already has the maximum number of connections.
            CBErrorUnknownDevice = -13,         // The device is unknown.
            CBErrorOperationNotSupported = -14, // The operation isn't supported.

            CBATTErrorSuccess = 0x00,                       // The ATT command or request successfully completed.
            CBATTErrorInvalidHandle = 0x01,                 // The attribute handle is invalid on this peripheral.
            CBATTErrorReadNotPermitted = 0x02,              // The permissions prohibit reading the attribute?s value.
            CBATTErrorWriteNotPermitted = 0x03,             // The permissions prohibit writing the attribute?s value.
            CBATTErrorInvalidPdu = 0x04,                    // The attribute Protocol Data Unit (PDU) is invalid.
            CBATTErrorInsufficientAuthentication = 0x05,    // Reading or writing the attribute?s value failed for lack of authentication.
            CBATTErrorRequestNotSupported = 0x06,           // The attribute server doesn't support the request received from the client.
            CBATTErrorInvalidOffset = 0x07,                 // The specified offset value was past the end of the attribute?s value.
            CBATTErrorInsufficientAuthorization = 0x08,     // Reading or writing the attribute?s value failed for lack of authorization.
            CBATTErrorPrepareQueueFull = 0x09,              // The prepare queue is full, as a result of there being too many write requests in the queue.
            CBATTErrorAttributeNotFound = 0x0A,             // The attribute wasn't found within the specified attribute handle range.
            CBATTErrorAttributeNotLong = 0x0B,              // The ATT read blob request can?t read or write the attribute.
            CBATTErrorInsufficientEncryptionKeySize = 0x0C, // The encryption key size used for encrypting this link is insufficient.
            CBATTErrorInvalidAttributeValueLength = 0x0D,   // The length of the attribute?s value is invalid for the intended operation.
            CBATTErrorUnlikelyError = 0x0E,                 // The ATT request encountered an unlikely error and wasn't completed.
            CBATTErrorInsufficientEncryption = 0x0F,        // Reading or writing the attribute?s value failed for lack of encryption.
            CBATTErrorUnsupportedGroupType = 0x10,          // The attribute type isn't a supported grouping attribute as defined by a higher-layer specification.
            CBATTErrorInsufficientResources = 0x11,         // Resources are insufficient to complete the ATT request.

            Success = 0,
            InvalidPeripheralId = unchecked((int)0x80000001),
            Disconnected = unchecked((int)0x80000100),
            InvalidCall = unchecked((int)0x80000101),
            InvalidParameters = unchecked((int)0x80000102),
            Canceled = unchecked((int)0x80000103),
        }

        static RequestStatus ToRequestStatus(int errorCode)
        {
            if (errorCode > 0)
            {
                return RequestStatus.ProtocolError;
            }
            else return errorCode switch
            {
                0 => RequestStatus.Success,

                //(int)AppleBluetoothError.UnknownError => RequestStatus.Error,
                (int)AppleBluetoothError.InvalidPeripheralId => RequestStatus.InvalidPeripheral,
                (int)AppleBluetoothError.Disconnected => RequestStatus.Disconnected,
                (int)AppleBluetoothError.InvalidCall => RequestStatus.InvalidCall,
                (int)AppleBluetoothError.InvalidParameters => RequestStatus.InvalidParameters,
                (int)AppleBluetoothError.Canceled => RequestStatus.Canceled,

                (int)AppleBluetoothError.CBErrorInvalidParameters => RequestStatus.InvalidParameters,
                (int)AppleBluetoothError.CBErrorInvalidHandle => RequestStatus.InvalidParameters,
                (int)AppleBluetoothError.CBErrorNotConnected => RequestStatus.Disconnected,
                //(int)AppleBluetoothError.CBErrorOutOfSpace => RequestStatus.Error,
                (int)AppleBluetoothError.CBErrorOperationCancelled => RequestStatus.Canceled,
                (int)AppleBluetoothError.CBErrorConnectionTimeout => RequestStatus.Timeout,
                (int)AppleBluetoothError.CBErrorPeripheralDisconnected => RequestStatus.Disconnected,
                //(int)AppleBluetoothError.CBErrorUUIDNotAllowed => RequestStatus.Error,
                //(int)AppleBluetoothError.CBErrorAlreadyAdvertising => RequestStatus.Error,
                //(int)AppleBluetoothError.CBErrorConnectionFailed => RequestStatus.Error,
                //(int)AppleBluetoothError.CBErrorConnectionLimitReached => RequestStatus.Error,
                //(int)AppleBluetoothError.CBErrorUnknownDevice => RequestStatus.Error,
                (int)AppleBluetoothError.CBErrorOperationNotSupported => RequestStatus.NotSupported,

                _ => RequestStatus.Error,
            };
        }
    }
}
