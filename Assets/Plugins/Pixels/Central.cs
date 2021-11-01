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
            _instance?._actionQueue.Enqueue(action);
        }

        static void EnqueueAction(PeripheralState ps, Action action)
        {
            _instance?._actionQueue.Enqueue(() =>
            {
                if (IsValidPeripheral(ps))
                {
                    action();
                }
            });
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

        void OnDestroy()
        {
            if (!_autoDestroy)
            {
                Central.Shutdown();
            }
        }

        #endregion

        class PeripheralState
        {
            public ScannedPeripheral ScannedPeripheral;
            public PeripheralHandle PeripheralHandle;
            public Guid[] RequiredServices;
            public Action<ScannedPeripheral, bool> ConnectionEvent;
            public bool IsReady;
        }

        public const int RequestDefaultTimeout = 10;

        // Dictionary key is peripheral SystemId, items are never removed except on shutdown
        static Dictionary<string, PeripheralState> _peripherals = new Dictionary<string, PeripheralState>();

        public static bool IsReady { get; private set; }

        public static bool IsScanning { get; private set; }

        public static event Action<ScannedPeripheral> PeripheralDiscovered;

        public static ScannedPeripheral[] ScannedPeripherals
        {
            get
            {
                EnsureRunningOnMainThread();

                return _peripherals.Values
                    .Select(ps => ps.ScannedPeripheral)
                    .ToArray();
            }
        }

        public static ScannedPeripheral[] ConnectedPeripherals
        {
            get
            {
                EnsureRunningOnMainThread();

                return _peripherals.Values
                    .Where(ps => ps.IsReady)
                    .Select(ps => ps.ScannedPeripheral)
                    .ToArray();
            }
        }

        public static bool Initialize()
        {
            EnsureRunningOnMainThread();

            // Already initialized?
            if (IsReady)
            {
                return true;
            }

            CreateBehaviour();

            bool success = NativeInterface.Initialize(status =>
            {
                Debug.Log($"[BLE] Bluetooth status: {status}");
                IsReady = status == BluetoothStatus.Enabled;
                IsScanning = IsScanning && IsReady;
            });

            if (!success)
            {
                Debug.LogError("[BLE] Failed to initialize");
            }

            return success;
        }

        public static void Shutdown()
        {
            EnsureRunningOnMainThread();

            Debug.Log("[BLE] Shutting down");

            // Reset states
            _peripherals.Clear();
            IsScanning = IsReady = false;

            // Shutdown native interface and destroy companion mono behavior
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
            EnsureRunningOnMainThread();

            if (!IsReady)
            {
                Debug.LogError("[BLE] Central not ready for scanning");
                return false;
            }

            var requiredServices = serviceUuids?.ToArray() ?? Array.Empty<Guid>();
            IsScanning = NativeInterface.StartScan(serviceUuids, scannedPeripheral =>
            {
                // Not running on main thread!
                Debug.Log($"[BLE] Discovered peripheral {scannedPeripheral.Name}, rssi={scannedPeripheral.Rssi}, addr={scannedPeripheral.BluetoothAddress})");

                EnqueueAction(() =>
                {
                    if (!_peripherals.TryGetValue(scannedPeripheral.SystemId, out PeripheralState ps))
                    {
                        _peripherals[scannedPeripheral.SystemId] = ps = new PeripheralState();
                    }
                    ps.ScannedPeripheral = scannedPeripheral;
                    ps.RequiredServices = requiredServices;

                    // Notify
                    PeripheralDiscovered?.Invoke(scannedPeripheral);
                });
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
            EnsureRunningOnMainThread();

            Debug.Log($"[BLE] Stopping scan");

            NativeInterface.StopScan();
            IsScanning = false;
        }

        //TODO what happens if a connect request is already being processed
        public static RequestEnumerator ConnectPeripheralAsync(ScannedPeripheral peripheral, Action<ScannedPeripheral, bool> onConnectionEvent, float timeoutSec = 0)
        {
            if (timeoutSec < 0) throw new ArgumentException(nameof(timeoutSec) + " must be greater or equal to zero", nameof(timeoutSec));

            EnsureRunningOnMainThread();

            PeripheralState ps = GetPeripheralState(peripheral);

            if (!ps.PeripheralHandle.IsValid)
            {
                ps.IsReady = false;
                ps.PeripheralHandle = NativeInterface.CreatePeripheral(peripheral,
                    (connectionEvent, reason) => EnqueueAction(ps, () =>
                    {
                        Debug.Log($"[BLE:{ps.ScannedPeripheral.Name}] Got connection event `{connectionEvent}`{(reason == ConnectionEventReason.Success ? "" : $" with reason `{reason}`")}, isReady={ps.IsReady}");
                        OnPeripheralConnectionEvent(ps, connectionEvent, reason);
                    }));

                if (ps.PeripheralHandle.IsValid)
                {
                    Debug.Log($"[BLE:{ps.ScannedPeripheral.Name}] Got peripheral");
                }
                else
                {
                    Debug.LogError($"[BLE:{ps.ScannedPeripheral.Name}] Failed to create peripheral");
                }
            }

            return new ConnectRequestEnumerator(ps.PeripheralHandle, timeoutSec,
                (_, onResult) =>
                {
                    Debug.Assert(ps.PeripheralHandle.IsValid); // Already checked by RequestEnumerator

                    Debug.Log($"[BLE:{ps.ScannedPeripheral.Name}] Connecting with timeout of {timeoutSec}s...");
                    ps.ConnectionEvent = onConnectionEvent;
                    Connect(ps, onResult);

                    static void Connect(PeripheralState ps, NativeRequestResultHandler onResult)
                    {
                        NativeInterface.ConnectPeripheral(
                            ps.PeripheralHandle,
                            ps.RequiredServices,
                            false, //TODO autoConnect
                            status => EnqueueAction(ps, () =>
                            {
                                Debug.Log($"[BLE:{ps.ScannedPeripheral.Name}] Connect result is `{status}`");

                                if (ps.PeripheralHandle.IsValid
                                    && ((status == RequestStatus.Timeout) || (status == RequestStatus.AccessDenied)))
                                {
                                    // Try again
                                    Debug.Log($"[BLE:{ps.ScannedPeripheral.Name}] Re-connecting...");
                                    Connect(ps, onResult);
                                }
                                else
                                {
                                    onResult(status);
                                }
                            }));
                    }
                },
                () => Debug.LogWarning($"[BLE:{ps.ScannedPeripheral.Name}] Connection timeout, canceling..."));

            static void OnPeripheralConnectionEvent(PeripheralState ps, ConnectionEvent connectionEvent, ConnectionEventReason reason)
            {
                bool ready = connectionEvent == ConnectionEvent.Ready;
                bool disconnected = connectionEvent == ConnectionEvent.Disconnected || connectionEvent == ConnectionEvent.FailedToConnect;

                if (!disconnected && !ready)
                {
                    // Nothing to do
                    return;
                }

                if (ready)
                {
                    //Debug.Log($"[BLE:{ps.ScannedPeripheral.Name}] Peripheral ready, setting MTU");

                    if (ps.PeripheralHandle.IsValid)
                    {
                        // Change MTU to maximum (note: MTU can only be set once)
                        NativeInterface.RequestPeripheralMtu(ps.PeripheralHandle, NativeInterface.MaxMtu,
                            (mtu, status) => EnqueueAction(ps, () =>
                            {
                                Debug.Log($"[BLE:{ps.ScannedPeripheral.Name}] MTU {(status == RequestStatus.Success ? "changed to" : "kept at")} {mtu} bytes");
                                if ((status != RequestStatus.Success) && (status != RequestStatus.NotSupported))
                                {
                                    Debug.LogError($"[BLE:{ps.ScannedPeripheral.Name}] Failed to change MTU, result is `{status}`");
                                }

                                if (ps.PeripheralHandle.IsValid)
                                {
                                    // We're done and ready
                                    Debug.Log($"[BLE:{ps.ScannedPeripheral.Name}] Ready");

                                    Debug.Assert(!ps.IsReady);
                                    ps.IsReady = true;

                                    ps.ConnectionEvent?.Invoke(ps.ScannedPeripheral, true);
                                }
                            }));
                    }
                }
                else if (ps.IsReady)
                {
                    // We got disconnected
                    Debug.Log($"[BLE:{ps.ScannedPeripheral.Name}] Disconnected");

                    // We were previously connected
                    ps.IsReady = false;

                    ps.ConnectionEvent?.Invoke(ps.ScannedPeripheral, false);
                }
            }
        }

        public static RequestEnumerator DisconnectPeripheralAsync(ScannedPeripheral peripheral)
        {
            EnsureRunningOnMainThread();

            var ps = GetPeripheralState(peripheral);
            var peripheralHandle = ps.PeripheralHandle;
            ps.PeripheralHandle = new PeripheralHandle();

            return new DisconnectRequestEnumerator(peripheralHandle);
        }

        public static string GetPeripheralName(ScannedPeripheral peripheral)
        {
            EnsureRunningOnMainThread();

            var peripheralHandle = GetPeripheralState(peripheral).PeripheralHandle;
            return peripheralHandle.IsValid ? NativeInterface.GetPeripheralName(peripheralHandle) : null;
        }

        public static int GetPeripheralMtu(ScannedPeripheral peripheral)
        {
            EnsureRunningOnMainThread();

            var peripheralHandle = GetPeripheralState(peripheral).PeripheralHandle;
            return peripheralHandle.IsValid ? NativeInterface.GetPeripheralMtu(peripheralHandle) : 0;
        }

        public static ValueRequestEnumerator<int> ReadPeripheralRssi(ScannedPeripheral peripheral, float timeoutSec = RequestDefaultTimeout)
        {
            EnsureRunningOnMainThread();

            var peripheralHandle = GetPeripheralState(peripheral).PeripheralHandle;
            return new ValueRequestEnumerator<int>(Operation.ReadPeripheralRssi, peripheralHandle, timeoutSec,
                (p, onResult) => NativeInterface.ReadPeripheralRssi(p, onResult));
        }

        public static Guid[] GetPeripheralDiscoveredServices(ScannedPeripheral peripheral)
        {
            EnsureRunningOnMainThread();

            var peripheralHandle = GetPeripheralState(peripheral).PeripheralHandle;
            return peripheralHandle.IsValid ? NativeInterface.GetPeripheralDiscoveredServices(peripheralHandle) : null;
        }

        public static Guid[] GetPeripheralServiceCharacteristics(ScannedPeripheral peripheral, Guid serviceUuid)
        {
            EnsureRunningOnMainThread();

            var peripheralHandle = GetPeripheralState(peripheral).PeripheralHandle;
            return peripheralHandle.IsValid ? NativeInterface.GetPeripheralServiceCharacteristics(peripheralHandle, serviceUuid) : null;
        }

        public static CharacteristicProperties GetCharacteristicProperties(ScannedPeripheral peripheral, Guid serviceUuid, Guid characteristicUuid, uint instanceIndex = 0)
        {
            EnsureRunningOnMainThread();

            var peripheralHandle = GetPeripheralState(peripheral).PeripheralHandle;
            return peripheralHandle.IsValid ? NativeInterface.GetCharacteristicProperties(peripheralHandle, serviceUuid, characteristicUuid, instanceIndex) : CharacteristicProperties.None;
        }

        public static RequestEnumerator ReadCharacteristicAsync(ScannedPeripheral peripheral, Guid serviceUuid, Guid characteristicUuid, Action<byte[]> onValueChanged, float timeoutSec = RequestDefaultTimeout)
        {
            return ReadCharacteristicAsync(peripheral, serviceUuid, characteristicUuid, 0, onValueChanged, timeoutSec);
        }

        public static RequestEnumerator ReadCharacteristicAsync(ScannedPeripheral peripheral, Guid serviceUuid, Guid characteristicUuid, uint instanceIndex, Action<byte[]> onValueChanged, float timeoutSec = RequestDefaultTimeout)
        {
            EnsureRunningOnMainThread();

            var peripheralHandle = GetPeripheralState(peripheral).PeripheralHandle;
            return new RequestEnumerator(Operation.ReadCharacteristic, peripheralHandle, timeoutSec,
                (p, onResult) => NativeInterface.ReadCharacteristic(
                    p, serviceUuid, characteristicUuid, instanceIndex,
                    onValueChanged: GetNativeHandler(onValueChanged, onResult),
                    onResult: onResult));
        }

        public static RequestEnumerator WriteCharacteristicAsync(ScannedPeripheral peripheral, Guid serviceUuid, Guid characteristicUuid, byte[] data, float timeoutSec = RequestDefaultTimeout)
        {
            return WriteCharacteristicAsync(peripheral, serviceUuid, characteristicUuid, 0, data, false, timeoutSec);
        }

        public static RequestEnumerator WriteCharacteristicAsync(ScannedPeripheral peripheral, Guid serviceUuid, Guid characteristicUuid, byte[] data, bool withoutResponse = false, float timeoutSec = RequestDefaultTimeout)
        {
            return WriteCharacteristicAsync(peripheral, serviceUuid, characteristicUuid, 0, data, withoutResponse, timeoutSec);
        }

        public static RequestEnumerator WriteCharacteristicAsync(ScannedPeripheral peripheral, Guid serviceUuid, Guid characteristicUuid, uint instanceIndex, byte[] data, bool withoutResponse = false, float timeoutSec = RequestDefaultTimeout)
        {
            EnsureRunningOnMainThread();

            var peripheralHandle = GetPeripheralState(peripheral).PeripheralHandle;
            return new RequestEnumerator(Operation.WriteCharacteristic, peripheralHandle, timeoutSec,
                (p, onResult) => NativeInterface.WriteCharacteristic(
                    p, serviceUuid, characteristicUuid, instanceIndex, data, withoutResponse, onResult));
        }

        public static RequestEnumerator SubscribeCharacteristicAsync(ScannedPeripheral peripheral, Guid serviceUuid, Guid characteristicUuid, Action<byte[]> onValueChanged, float timeoutSec = RequestDefaultTimeout)
        {
            return SubscribeCharacteristicAsync(peripheral, serviceUuid, characteristicUuid, 0, onValueChanged, timeoutSec);
        }

        public static RequestEnumerator SubscribeCharacteristicAsync(ScannedPeripheral peripheral, Guid serviceUuid, Guid characteristicUuid, uint instanceIndex, Action<byte[]> onValueChanged, float timeoutSec = RequestDefaultTimeout)
        {
            EnsureRunningOnMainThread();

            var peripheralHandle = GetPeripheralState(peripheral).PeripheralHandle;
            return new RequestEnumerator(Operation.SubscribeCharacteristic, peripheralHandle, timeoutSec,
                (p, onResult) => NativeInterface.SubscribeCharacteristic(
                    p, serviceUuid, characteristicUuid, instanceIndex,
                    onValueChanged: GetNativeHandler(onValueChanged, onResult),
                    onResult: onResult));
        }

        public static RequestEnumerator UnsubscribeCharacteristicAsync(ScannedPeripheral peripheral, Guid serviceUuid, Guid characteristicUuid, uint instanceIndex = 0, float timeoutSec = RequestDefaultTimeout)
        {
            EnsureRunningOnMainThread();

            var peripheralHandle = GetPeripheralState(peripheral).PeripheralHandle;
            return new RequestEnumerator(Operation.UnsubscribeCharacteristic, peripheralHandle, timeoutSec,
                (p, onResult) => NativeInterface.UnsubscribeCharacteristic(
                    p, serviceUuid, characteristicUuid, instanceIndex, onResult));
        }

        static void EnsureRunningOnMainThread()
        {
            if (System.Threading.Thread.CurrentThread.ManagedThreadId != 1)
            {
                throw new InvalidOperationException($"Methods of type {nameof(Central)} can only be called from the main thread");
            }
        }

        static PeripheralState GetPeripheralState(ScannedPeripheral scannedPeripheral)
        {
            EnsureRunningOnMainThread();

            _peripherals.TryGetValue(scannedPeripheral.SystemId, out PeripheralState ps);
            return ps ?? throw new ArgumentException(nameof(scannedPeripheral), $"No peripheral found with SystemId={scannedPeripheral.SystemId}");
        }

        static bool IsValidPeripheral(PeripheralState ps)
        {
            EnsureRunningOnMainThread();

            Debug.Assert(ps.ScannedPeripheral != null);
            return _peripherals.ContainsKey(ps.ScannedPeripheral?.SystemId);
        }

        static NativeValueChangedHandler GetNativeHandler(Action<byte[]> onValueChanged, NativeRequestResultHandler onResult)
        {
            return (data, status) =>
            {
                // Not running on main thread!
                try
                {
                    if (status == RequestStatus.Success)
                    {
                        Debug.Assert(data != null);
                        EnqueueAction(() => onValueChanged(data));
                    }
                    else
                    {
                        Debug.Assert(data == null);
                        onResult(status);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            };
        }
    }
}
