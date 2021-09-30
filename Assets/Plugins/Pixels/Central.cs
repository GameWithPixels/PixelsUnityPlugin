using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Systemic.Pixels.Unity.BluetoothLE
{
    public sealed class Central : MonoBehaviour
    {
        #region MonoBehaviour

        static Central _instance;
        static bool _autoDestroy;
        ConcurrentQueue<Action> _actionQueue = new ConcurrentQueue<Action>();

        static void EnqueueAction(Action action)
        {
            if (_instance != null)
            {
                _instance._actionQueue.Enqueue(action);
            }
        }

        static void CreateBehaviour()
        {
            _autoDestroy = false;
            if (!_instance)
            {
                var go = new GameObject("PixelsBleCentral");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<Central>();
            }
        }

        static void ScheduleDestroy()
        {
            _autoDestroy = true;
        }

        void Start()
        {
            // Safeguard
            if (_instance != this)
            {
                Debug.LogError("A second instance of Central got spawned, now destroying it");
                Destroy(this);
            }
        }

        void Update()
        {
            while (_actionQueue.TryDequeue(out Action act))
            {
                act?.Invoke();
            }
            if (_autoDestroy)
            {
                Destroy(_instance);
                _instance = null;
            }
        }

        #endregion

        class PeripheralState
        {
            public ScannedPeripheral ScannedPeripheral;
            public PeripheralHandle PeripheralHandle;
            public Guid[] RequiredServices;
            public bool IsReady;
            public bool FailedToConnect;
            public bool AutoConnect;
            public NativeRequestResultHandler ConnectErrorHandler;
        }

        public static int ConnectionTimeout { get; set; } = 30;

        public static int RequestTimeout { get; set; } = 30;

        // Dictionary key is peripheral SystemId
        static Dictionary<string, PeripheralState> _peripherals = new Dictionary<string, PeripheralState>();

        public static event Action<ScannedPeripheral> PeripheralDiscovered;

        public static ScannedPeripheral[] ScannedPeripherals
        {
            get
            {
                lock (_peripherals)
                {
                    return _peripherals.Values
                        .Select(ps => ps.ScannedPeripheral)
                        .ToArray();
                }
            }
        }

        public static ScannedPeripheral[] ConnectedPeripherals
        {
            get
            {
                lock (_peripherals)
                {
                    return _peripherals.Values
                        .Where(ps => ps.IsReady)
                        .Select(ps => ps.ScannedPeripheral)
                        .ToArray();
                }
            }
        }

        public static bool IsScanning { get; private set; }

        public static bool Initialize()
        {
            CreateBehaviour();

            bool success = NativeInterface.Initialize(available =>
            {
                Debug.Log($"[BLE] Bluetooth status: {(available ? "" : "not")} available");
                if (!available)
                {
                    IsScanning = false;
                }
            });

            if (!success)
            {
                Debug.LogError("[BLE] Failed to initialize");
            }

            return success;
        }

        public static void Shutdown()
        {
            Debug.Log("[BLE] Shutting down");

            NativeInterface.Shutdown();
            ScheduleDestroy();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="serviceUuids">Can be null (but may use more battery life)</param>
        /// <returns></returns>
        /// <remarks>If a scan is already in progress, it will replace it by the new one</remarks>
        public static bool ScanForPeripheralsWithServices(IEnumerable<Guid> serviceUuids = null)
        {
            ClearScannedPeripherals();

            var requiredServices = serviceUuids?.ToArray() ?? Array.Empty<Guid>();
            IsScanning = NativeInterface.StartScan(serviceUuids, scannedPeripheral =>
            {
                lock (_peripherals)
                {
                    if (!_peripherals.TryGetValue(scannedPeripheral.SystemId, out PeripheralState ps))
                    {
                        _peripherals[scannedPeripheral.SystemId] = ps = new PeripheralState();
                    }
                    ps.ScannedPeripheral = scannedPeripheral;
                    ps.RequiredServices = requiredServices;
                }

                // Notify
                EnqueueAction(() => PeripheralDiscovered?.Invoke(scannedPeripheral));
            });

            if (IsScanning)
            {
                Debug.Log($"[BLE] Starting scan for BLE peripherals with services {serviceUuids?.Select(g => g.ToString()).Aggregate((a, b) => a + ", " + b)}");
            }
            else
            {
                Debug.LogError("[BLE] Failed to start scanning for peripherals");
            }

            return IsScanning;
        }

        public static void StopScan()
        {
            NativeInterface.StopScan();
            IsScanning = false;
        }

        public static void ClearScannedPeripherals()
        {
            lock (_peripherals)
            {
                var keysToRemove = _peripherals.Where(kv => kv.Value.PeripheralHandle.IsEmpty).Select(kv => kv.Key).ToArray();
                if (keysToRemove.Length == _peripherals.Count)
                {
                    _peripherals.Clear();
                }
                else
                {
                    foreach (var k in keysToRemove)
                    {
                        _peripherals.Remove(k);
                    }
                }
            }
        }

        public static RequestEnumerator ConnectPeripheralAsync(ScannedPeripheral peripheral, Action<ScannedPeripheral, bool> onConnectionEvent)
        {
            PeripheralState ps = GetPeripheralState(peripheral);
            return new RequestEnumerator(Operation.ConnectPeripheral, ConnectionTimeout,
                onResult =>
                {
                    if (ps.PeripheralHandle.IsEmpty)
                    {
                        ps.IsReady = false;
                        ps.PeripheralHandle = NativeInterface.CreatePeripheral(peripheral,
                            (connectionEvent, reason) => EnqueueAction(() => OnPeripheralConnectionEvent(ps, onConnectionEvent, connectionEvent, reason)));
                    }

                    //TODO could already be attempting to connect
                    if (ps.PeripheralHandle.IsEmpty)
                    {
                        onResult(new NativeError((int)Error.Unknown));
                    }
                    else if (!ps.IsReady)
                    {
                        ps.ConnectErrorHandler = onResult;
                        InternalConnect(ps);
                    }
                });
            //TODO on timeout, stop further connection attempt (including the on-going one)
        }

        static void InternalConnect(PeripheralState ps)
        {
            Debug.Log($"[BLE:{ps.ScannedPeripheral.Name}] Connecting...");

            ps.AutoConnect = true;
            ps.FailedToConnect = false;
            NativeInterface.ConnectPeripheral(
                ps.PeripheralHandle,
                ps.RequiredServices,
                error => { }); //TODO if (!error.IsEmpty) ps.ConnectErrorHandler(error); });
        }

        static void OnPeripheralConnectionEvent(PeripheralState ps, Action<ScannedPeripheral, bool> onConnectionEvent, ConnectionEvent connectionEvent, ConnectionEventReason reason)
        {
            Debug.Log($"[BLE:{ps.ScannedPeripheral.Name}] ConnectionEvent => {connectionEvent}, reason: {reason}");

            bool ready = connectionEvent == ConnectionEvent.Ready;
            bool disconnected = connectionEvent == ConnectionEvent.Disconnected || connectionEvent == ConnectionEvent.FailedToConnect;

            if (!disconnected && !ready)
            {
                // Nothing to do
                return;
            }

            ps.IsReady = ready;
            ps.FailedToConnect = (reason == ConnectionEventReason.NotSupported) || (connectionEvent == ConnectionEvent.FailedToConnect);
            if (ps.AutoConnect && disconnected)
            {
                if (reason == ConnectionEventReason.NotSupported)
                {
                    ps.AutoConnect = false;
                }
                else if (reason == ConnectionEventReason.Success)
                {
                    Debug.Log($"[BLE:{ps.ScannedPeripheral.Name}] Device disconnected normally, disabling auto-connect");
                    ps.AutoConnect = false;
                }
                else
                {
                    InternalConnect(ps);
                }
            }

            if (ready)
            {
                // Note: MTU can only be set once
                NativeInterface.RequestPeripheralMtu(ps.PeripheralHandle, NativeInterface.MaxMtu,
                    (mtu, error) => Debug.Log($"[BLE:{ps.ScannedPeripheral.Name}] MTU changed to " + mtu)); ;
                //TODO handle error + wait on response before having peripheral set to ready
            }

            onConnectionEvent(ps.ScannedPeripheral, ready);

            if (ready)
            {
                ps.ConnectErrorHandler?.Invoke(NativeError.Empty);
            }
        }

        public static RequestEnumerator DisconnectPeripheralAsync(ScannedPeripheral peripheral)
        {
            var ps = GetPeripheralState(peripheral);
            var nativePeripheral = ps.PeripheralHandle;
            ps.AutoConnect = false;
            ps.PeripheralHandle = new PeripheralHandle();

            return new RequestEnumerator(Operation.DisconnectPeripheral, ConnectionTimeout,
                onResult => NativeInterface.DisconnectPeripheral(nativePeripheral, onResult),
                postAction: () => NativeInterface.ReleasePeripheral(nativePeripheral));
        }

        public static string GetPeripheralName(ScannedPeripheral peripheral)
        {
            var nativePeripheral = GetPeripheralState(peripheral).PeripheralHandle;
            return NativeInterface.GetPeripheralName(nativePeripheral);
        }

        public static int GetPeripheralMtu(ScannedPeripheral peripheral)
        {
            var nativePeripheral = GetPeripheralState(peripheral).PeripheralHandle;
            return NativeInterface.GetPeripheralMtu(nativePeripheral);
        }

        public static ValueRequestEnumerator<int> ReadPeripheralRssi(ScannedPeripheral peripheral)
        {
            var nativePeripheral = GetPeripheralState(peripheral).PeripheralHandle;
            return new ValueRequestEnumerator<int>(Operation.ReadPeripheralRssi, RequestTimeout,
                onResult => NativeInterface.ReadPeripheralRssi(nativePeripheral, onResult));
        }

        public static Guid[] GetPeripheralDiscoveredServices(ScannedPeripheral peripheral)
        {
            var nativePeripheral = GetPeripheralState(peripheral).PeripheralHandle;
            return NativeInterface.GetPeripheralDiscoveredServices(nativePeripheral);
        }

        public static Guid[] GetPeripheralServiceCharacteristics(ScannedPeripheral peripheral, Guid serviceUuid)
        {
            var nativePeripheral = GetPeripheralState(peripheral).PeripheralHandle;
            return NativeInterface.GetPeripheralServiceCharacteristics(nativePeripheral, serviceUuid);
        }

        public static CharacteristicProperties GetCharacteristicProperties(ScannedPeripheral peripheral, Guid serviceUuid, Guid characteristicUuid, uint instanceIndex = 0)
        {
            var nativePeripheral = GetPeripheralState(peripheral).PeripheralHandle;
            return NativeInterface.GetCharacteristicProperties(nativePeripheral, serviceUuid, characteristicUuid, instanceIndex);
        }

        public static RequestEnumerator ReadCharacteristicAsync(ScannedPeripheral peripheral, Guid serviceUuid, Guid characteristicUuid, Action<byte[]> onValueChanged)
        {
            return ReadCharacteristicAsync(peripheral, serviceUuid, characteristicUuid, 0, onValueChanged);
        }

        public static RequestEnumerator ReadCharacteristicAsync(ScannedPeripheral peripheral, Guid serviceUuid, Guid characteristicUuid, uint instanceIndex, Action<byte[]> onValueChanged)
        {
            var nativePeripheral = GetPeripheralState(peripheral).PeripheralHandle;
            return new RequestEnumerator(Operation.ReadCharacteristic, RequestTimeout,
                onResult => NativeInterface.ReadCharacteristic(
                    nativePeripheral, serviceUuid, characteristicUuid, instanceIndex,
                    onValueChanged: (data, error) => { if (!error.IsEmpty) onResult(error); else EnqueueAction(() => onValueChanged(data)); },
                    onResult: onResult));
        }

        public static RequestEnumerator WriteCharacteristicAsync(ScannedPeripheral peripheral, Guid serviceUuid, Guid characteristicUuid, byte[] data, bool withoutResponse = false)
        {
            return WriteCharacteristicAsync(peripheral, serviceUuid, characteristicUuid, 0, data, withoutResponse);
        }

        public static RequestEnumerator WriteCharacteristicAsync(ScannedPeripheral peripheral, Guid serviceUuid, Guid characteristicUuid, uint instanceIndex, byte[] data, bool withoutResponse = false)
        {
            var nativePeripheral = GetPeripheralState(peripheral).PeripheralHandle;
            return new RequestEnumerator(Operation.WriteCharacteristic, RequestTimeout,
                onResult => NativeInterface.WriteCharacteristic(
                    nativePeripheral, serviceUuid, characteristicUuid, instanceIndex, data, withoutResponse, onResult));
        }

        public static RequestEnumerator SubscribeCharacteristicAsync(ScannedPeripheral peripheral, Guid serviceUuid, Guid characteristicUuid, Action<byte[]> onValueChanged)
        {
            return SubscribeCharacteristicAsync(peripheral, serviceUuid, characteristicUuid, 0, onValueChanged);
        }

        public static RequestEnumerator SubscribeCharacteristicAsync(ScannedPeripheral peripheral, Guid serviceUuid, Guid characteristicUuid, uint instanceIndex, Action<byte[]> onValueChanged)
        {
            var nativePeripheral = GetPeripheralState(peripheral).PeripheralHandle;
            return new RequestEnumerator(Operation.SubscribeCharacteristic, RequestTimeout,
                onResult => NativeInterface.SubscribeCharacteristic(
                    nativePeripheral, serviceUuid, characteristicUuid, instanceIndex,
                    onValueChanged: (data, error) => { if (!error.IsEmpty) onResult(error); else EnqueueAction(() => onValueChanged(data)); },
                    onResult: onResult));
        }

        public static RequestEnumerator UnsubscribeCharacteristicAsync(ScannedPeripheral peripheral, Guid serviceUuid, Guid characteristicUuid, uint instanceIndex = 0)
        {
            var nativePeripheral = GetPeripheralState(peripheral).PeripheralHandle;
            return new RequestEnumerator(Operation.UnsubscribeCharacteristic, RequestTimeout,
                onResult => NativeInterface.UnsubscribeCharacteristic(
                    nativePeripheral, serviceUuid, characteristicUuid, instanceIndex, onResult));
        }

        static PeripheralState GetPeripheralState(ScannedPeripheral scannedPeripheral)
        {
            lock (_peripherals)
            {
                _peripherals.TryGetValue(scannedPeripheral.SystemId, out PeripheralState ps);
                return ps ?? throw new ArgumentException(nameof(scannedPeripheral));
            }
        }
    }
}
