using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Systemic.Unity.BluetoothLE
{
    /// <summary>
    /// A static class with methods for discovering, connecting to, and interacting with Bluetooth Low Energy peripherals.
    /// 
    /// Use the <see cref="ScanForPeripheralsWithServices"/> method to discover available BLE peripherals.
    /// Then connect to a scanned peripheral with a call to <see cref="ConnectPeripheralAsync"/>
    ///
    /// Once connected, the peripheral can be queried for its name, MTU, RSSI, services and characteristics.
    /// Characteristics can be read, written and subscribed to.
    ///
    /// Be sure to disconnect the peripheral once it is not needed anymore.
    ///
    /// This class leverages <see cref="NativeInterface"/> to perform most of its operations.
    ///
    /// Calls from any other thread than the main thread will throw an exception.
    /// 
    /// Any method ending by Async returns an enumerator which is meant to be run as a coroutine.
    /// 
    /// A <see cref="GameObject"/> named SystemicBleCentral is created upon calling <see cref="Initialize"/>
    /// and destroyed on calling <see cref="Shutdown"/>.
    /// </remarks>
    public static class Central
    {
        #region MonoBehaviour

        /// <summary>
        /// Internal <see cref="MonoBehaviour"/> that runs queued <see cref="Action"/> on each Unity's call to <see cref="Update"/>.
        /// </summary>
        sealed class CentralBehaviour : MonoBehaviour
        {
            // Our action queue
            ConcurrentQueue<Action> _actionQueue = new ConcurrentQueue<Action>();

            /// <summary>
            /// Queues an action to be invoked on the next frame update.
            /// </summary>
            /// <param name="action">The action to be invoked on the next update.</param>
            public void EnqueueAction(Action action)
            {
                _actionQueue.Enqueue(action);
            }

            void Start()
            {
                // Safeguard
                if (_instance != this)
                {
                    Debug.LogError($"A second instance of {typeof(CentralBehaviour)} got spawned, now destroying it");
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
        }

        // Keeps a reference to the behaviour instance
        static CentralBehaviour _instance;

        // When set to true, the behaviour will destroy itself on the next frame update
        static bool _autoDestroy;

        // Queues an action to be invoked on the next frame update
        static void EnqueueAction(Action action)
        {
            _instance?.EnqueueAction(action);
        }

        // Queues an action to be invoked on the next frame update but only if the peripheral is still in our list
        static void EnqueuePeripheralAction(PeripheralInfo pinf, Action action)
        {
            _instance?.EnqueueAction(() =>
            {
                Debug.Assert(pinf.ScannedPeripheral != null);
                if (_peripherals.ContainsKey(pinf.ScannedPeripheral?.SystemId))
                {
                    action();
                }
            });
        }

        // Creates the internal behaviour
        static void CreateBehaviour()
        {
            _autoDestroy = false;
            if (!_instance)
            {
                var go = new GameObject("SystemicBleCentral");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _instance = go.AddComponent<CentralBehaviour>();
            }
        }

        // Schedule the internal behaviour to be destroyed on the next frame update
        static void ScheduleDestroy()
        {
            _autoDestroy = true;
        }

        #endregion

        // Keeps a bunch of information about a known peripheral
        class PeripheralInfo
        {
            public ScannedPeripheral ScannedPeripheral;
            public NativePeripheralHandle NativeHandle;
            public Guid[] RequiredServices;
            public Action<ScannedPeripheral, bool> ConnectionEvent;
            public bool IsReady;
        }

        // All scanned peripherals, key is the peripheral SystemId, items are never removed except on shutdown
        static readonly Dictionary<string, PeripheralInfo> _peripherals = new Dictionary<string, PeripheralInfo>();

        /// <summary>
        /// The default timeout value (in seconds) for requests send to a BLE peripheral.
        /// </summary>
        public const int RequestDefaultTimeout = 10;

        /// <summary>
        /// Indicates whether <see cref="Central"/> is ready for scanning and connecting to peripherals.
        /// Reasons for not being ready are:
        /// - <see cref="Initialize"/> hasn't been called, or <see cref="Shutdown"/> was called afterwards
        /// - initialization is still on-going
        /// - the device either doesn't have a Bluetooth radio or it's radio is turned off
        /// </summary>
        public static bool IsReady { get; private set; }

        /// <summary>
        /// Indicates whether a scan is currently on-going.
        /// </summary>
        public static bool IsScanning { get; private set; }

        /// <summary>
        /// Occurs when a peripheral is discovered or re-discovered.
        /// Discovery happens each time <see cref="Central"/> receives a discovery packet
        /// from a peripheral. This may happen at a frequency between several times per second
        /// and every few seconds.
        /// </summary>
        public static event Action<ScannedPeripheral> PeripheralDiscovered;

        /// <summary>
        /// List of all scanned peripherals since <see cref="Initialize"/> was called.
        /// The list is cleared on <see cref="Shutdown"/>.
        /// </summary>
        public static ScannedPeripheral[] ScannedPeripherals
        {
            get
            {
                EnsureRunningOnMainThread();

                return _peripherals.Values
                    .Select(pinf => pinf.ScannedPeripheral)
                    .ToArray();
            }
        }

        /// <summary>
        /// List of peripherals to which <see cref="Central"/> is connected.
        /// </summary>
        public static ScannedPeripheral[] ConnectedPeripherals
        {
            get
            {
                EnsureRunningOnMainThread();

                return _peripherals.Values
                    .Where(pinf => pinf.IsReady)
                    .Select(pinf => pinf.ScannedPeripheral)
                    .ToArray();
            }
        }

        /*! \name Static class life cycle */
        //! @{

        /// <summary>
        /// Initializes the static class.
        /// The <see cref="IsReady"/> property is set to <c>true</c> once <see cref="Central"/> is ready
        /// to scan for and connect to BLE peripherals.
        /// </summary>
        /// <returns>
        /// Indicates whether the call has succeeded.
        /// - if <c>true</c> is returned, the static class might not be ready yet
        /// - if <c>false</c> is returned, there is probably something wrong with the platform specific native plugin
        /// </returns>
        public static bool Initialize()
        {
            EnsureRunningOnMainThread();

            Debug.Log("[BLE] Initializing...");

            CreateBehaviour();

            // Initialize NativeInterface and subscribe to get notified when the Bluetooth radio status changes
            bool success = NativeInterface.Initialize(status =>
            {
                Debug.Log($"[BLE] Bluetooth radio status: {status}");
                IsReady = status == BluetoothStatus.Enabled;
                IsScanning = IsScanning && IsReady;
            });

            if (!success)
            {
                Debug.LogError("[BLE] Failed to initialize");
            }

            return success;
        }

        /// <summary>
        /// Shutdowns the static class:
        /// - any pending scan is stopped
        /// - all peripherals are disconnected and removed
        /// </summary>
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

        //! @}
        /*! \name Peripherals scanning */
        //! @{

        /// <summary>
        /// Starts a scan for BLE peripherals.
        /// 
        /// Specifying one more service required for the peripherals will save battery on mobile devices.
        /// </summary>
        /// <param name="serviceUuids">List of services that the peripheral should advertise, may be empty or null.</param>
        /// <returns>Indicates whether the call has succeeded. It will fail if <see cref="IsReady"/> is <c>false</c>.</returns>
        /// <remarks>If a scan is already in progress, it will be replaced by this one.</remarks>
        public static bool ScanForPeripheralsWithServices(IEnumerable<Guid> serviceUuids = null)
        {
            EnsureRunningOnMainThread();

            // We must be ready
            if (!IsReady)
            {
                Debug.LogError("[BLE] Central not ready for scanning");
                return false;
            }

            // Make sure we don't have a null array
            var requiredServices = serviceUuids?.ToArray() ?? Array.Empty<Guid>();

            // Start scanning
            IsScanning = NativeInterface.StartScan(serviceUuids, scannedPeripheral =>
            {
                EnqueueAction(() =>
                {
                    Debug.Log($"[BLE:{scannedPeripheral.Name}] Peripheral discovered with address={scannedPeripheral.BluetoothAddress}, RSSI={scannedPeripheral.Rssi})");

                    // Keep track of discovered peripherals
                    if (!_peripherals.TryGetValue(scannedPeripheral.SystemId, out PeripheralInfo pinf))
                    {
                        _peripherals[scannedPeripheral.SystemId] = pinf = new PeripheralInfo();
                    }
                    pinf.ScannedPeripheral = scannedPeripheral;
                    pinf.RequiredServices = requiredServices;

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

        /// <summary>
        /// Stops any on-going BLE scan.
        /// </summary>
        public static void StopScan()
        {
            EnsureRunningOnMainThread();

            Debug.Log($"[BLE] Stopping scan");

            NativeInterface.StopScan();
            IsScanning = false;
        }

        //! @}
        /*! \name Peripheral connection and disconnection */
        //! @{

        /// <summary>
        /// Asynchronously connects to a discovered peripheral.
        /// 
        /// The enumerator will stop on either of these conditions:
        /// - the connection succeeded
        /// - <see cref="DisconnectPeripheralAsync"/> was called for the given peripheral
        /// - the connection didn't succeeded after the given timeout value
        /// - an error occurred while trying to connect
        ///
        /// Once connected to the peripheral, <see cref="Central"/> sends a request to change the peripheral's MTU
        /// to highest possible value. In the likely event that the peripheral doesn't support the requested MTU
        /// because it's too high, it will nonetheless change its MTU to the highest value it can achieve.
        ///
        /// Once the MTU is changed, <see cref="Central"/> notifies the caller that the peripheral is ready to be used
        /// by invoking the <paramref name="onConnectionEvent"/> handler with the second argument set to <c>true</c>.
        ///
        /// Upon a disconnection (whichever the cause), <see cref="Central"/> notifies the caller by invoking
        /// the <paramref name="onConnectionEvent"/> handler with the second argument set to <c>false</c>.
        ///
        /// Check <see cref="RequestEnumerator"/> members for more details.
        /// </summary>
        /// <param name="peripheral">Scanned peripheral to connect to.</param>
        /// <param name="onConnectionEvent">Called each time the connection state changes, the peripheral is passed as
        /// the first argument and the connection state as the second argument (<c>true</c> means connected).</param>
        /// <param name="timeoutSec">The timeout value, in seconds. The default is zero in which case the request will never timeout.</param>
        /// <returns>
        /// An enumerator meant to be run as a coroutine.
        /// See <see cref="RequestEnumerator"/> properties to get the request status.
        /// </returns>
        public static RequestEnumerator ConnectPeripheralAsync(ScannedPeripheral peripheral, Action<ScannedPeripheral, bool> onConnectionEvent, float timeoutSec = 0)
        {
            //TODO what happens if a connect request is already being processed
            if (timeoutSec < 0) throw new ArgumentException(nameof(timeoutSec) + " must be greater or equal to zero", nameof(timeoutSec));

            EnsureRunningOnMainThread();

            // Get peripheral state
            PeripheralInfo pinf = GetPeripheralInfo(peripheral);

            // We need a valid peripheral
            if (!pinf.NativeHandle.IsValid)
            {
                // Create new native peripheral handle
                pinf.IsReady = false;
                pinf.NativeHandle = NativeInterface.CreatePeripheral(peripheral,
                    (connectionEvent, reason) => EnqueuePeripheralAction(pinf, () =>
                    {
                        Debug.Log($"[BLE:{pinf.ScannedPeripheral.Name}] Got connection event `{connectionEvent}`{(reason == ConnectionEventReason.Success ? "" : $" with reason `{reason}`")}, isReady={pinf.IsReady}");
                        OnPeripheralConnectionEvent(pinf, connectionEvent, reason);
                    }));

                // Check that the above call worked
                if (pinf.NativeHandle.IsValid)
                {
                    Debug.Log($"[BLE:{pinf.ScannedPeripheral.Name}] Got peripheral");
                }
                else
                {
                    Debug.LogError($"[BLE:{pinf.ScannedPeripheral.Name}] Failed to create peripheral");
                }
            }

            // Attempt connecting until we got a success, a timeout or an unexpected error
            return new ConnectRequestEnumerator(pinf.NativeHandle, timeoutSec,
                (_, onResult) =>
                {
                    Debug.Assert(pinf.NativeHandle.IsValid); // Already checked by RequestEnumerator

                    Debug.Log($"[BLE:{pinf.ScannedPeripheral.Name}] Connecting with timeout of {timeoutSec}s...");
                    pinf.ConnectionEvent = onConnectionEvent;
                    Connect(pinf, onResult);

                    static void Connect(PeripheralInfo pinf, NativeRequestResultHandler onResult)
                    {
                        NativeInterface.ConnectPeripheral(
                            pinf.NativeHandle,
                            pinf.RequiredServices,
                            false, //TODO autoConnect
                            status => EnqueuePeripheralAction(pinf, () =>
                            {
                                Debug.Log($"[BLE:{pinf.ScannedPeripheral.Name}] Connect result is `{status}`");

                                if (pinf.NativeHandle.IsValid
                                    && ((status == RequestStatus.Timeout) || (status == RequestStatus.AccessDenied)))
                                {
                                    // Try again on timeout or access denied (which might happen
                                    // if the peripheral got turned off or out of range if the middle of the connection process)
                                    Debug.Log($"[BLE:{pinf.ScannedPeripheral.Name}] Re-connecting...");
                                    Connect(pinf, onResult);
                                }
                                else
                                {
                                    // Invalid state, give up connecting
                                    onResult(status);
                                }
                            }));
                    }
                },
                () => Debug.LogWarning($"[BLE:{pinf.ScannedPeripheral.Name}] Connection timeout, canceling..."));

            // Connection event callback
            static void OnPeripheralConnectionEvent(PeripheralInfo pinf, ConnectionEvent connectionEvent, ConnectionEventReason reason)
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
                    //Debug.Log($"[BLE:{pinf.ScannedPeripheral.Name}] Peripheral ready, setting MTU");

                    if (pinf.NativeHandle.IsValid)
                    {
                        // Change MTU to maximum (note: MTU can only be set once)
                        NativeInterface.RequestPeripheralMtu(pinf.NativeHandle, NativeInterface.MaxMtu,
                            (mtu, status) => EnqueuePeripheralAction(pinf, () =>
                            {
                                Debug.Log($"[BLE:{pinf.ScannedPeripheral.Name}] MTU {(status == RequestStatus.Success ? "changed to" : "kept at")} {mtu} bytes");
                                if ((status != RequestStatus.Success) && (status != RequestStatus.NotSupported))
                                {
                                    Debug.LogError($"[BLE:{pinf.ScannedPeripheral.Name}] Failed to change MTU, result is `{status}`");
                                }

                                if (pinf.NativeHandle.IsValid)
                                {
                                    // We're done and ready
                                    Debug.Log($"[BLE:{pinf.ScannedPeripheral.Name}] Ready");

                                    Debug.Assert(!pinf.IsReady);
                                    pinf.IsReady = true;

                                    // Notify
                                    pinf.ConnectionEvent?.Invoke(pinf.ScannedPeripheral, true);
                                }
                            }));
                    }
                }
                else if (pinf.IsReady)
                {
                    // We got disconnected
                    Debug.Log($"[BLE:{pinf.ScannedPeripheral.Name}] Disconnected");

                    // We were previously connected
                    pinf.IsReady = false;

                    // Notify
                    pinf.ConnectionEvent?.Invoke(pinf.ScannedPeripheral, false);
                }
            }
        }

        /// <summary>
        /// Disconnects a peripheral.
        /// </summary>
        /// <param name="peripheral">Scanned peripheral to disconnect from.</param>
        /// <returns>
        /// An enumerator meant to be run as a coroutine.
        /// See <see cref="RequestEnumerator"/> properties to get the request status.
        /// </returns>
        public static RequestEnumerator DisconnectPeripheralAsync(ScannedPeripheral peripheral)
        {
            EnsureRunningOnMainThread();

            var pinf = GetPeripheralInfo(peripheral);
            var nativeHandle = pinf.NativeHandle;
            pinf.NativeHandle = new NativePeripheralHandle();

            return new DisconnectRequestEnumerator(nativeHandle);
        }

        //! @}
        /*! \name Peripheral operations
         *  Only valid once a peripheral is connected. */
        //! @{

        /// <summary>
        /// Returns the name of the given peripheral.
        /// </summary>
        /// <param name="peripheral">A connected peripheral.</param>
        /// <returns>The peripheral name.</returns>
        /// <remarks>The peripheral must be connected.</remarks>
        public static string GetPeripheralName(ScannedPeripheral peripheral)
        {
            EnsureRunningOnMainThread();

            var nativeHandle = GetPeripheralInfo(peripheral).NativeHandle;
            return nativeHandle.IsValid ? NativeInterface.GetPeripheralName(nativeHandle) : null;
        }

        /// <summary>
        /// Returns the Maximum Transmission Unit (MTU) for the given peripheral.
        /// 
        /// The MTU is the maximum length of a packet that can be send to the BLE peripheral.
        /// However the BLE protocol uses 3 bytes, so the maximum data size that can be given
        /// to <see cref="WriteCharacteristicAsync"/> is 3 bytes less than the MTU.
        /// </summary>
        /// <param name="peripheral">A connected peripheral.</param>
        /// <returns>The peripheral MTU.</returns>
        /// <remarks>The peripheral must be connected.</remarks>
        public static int GetPeripheralMtu(ScannedPeripheral peripheral)
        {
            //TODO check if MTU is 23 or 20 (and update comment if it's the later)
            EnsureRunningOnMainThread();

            var nativeHandle = GetPeripheralInfo(peripheral).NativeHandle;
            return nativeHandle.IsValid ? NativeInterface.GetPeripheralMtu(nativeHandle) : 0;
        }

        /// <summary>
        /// Asynchronously reads the current Received Signal Strength Indicator (RSSI) for the given peripheral.
        /// 
        /// It gives an indication of the connection quality.
        /// </summary>
        /// <param name="peripheral">A connected peripheral.</param>
        /// <param name="timeoutSec">The maximum allowed time for the request, in seconds.</param>
        /// <returns>
        /// An enumerator meant to be run as a coroutine.
        /// See <see cref="ValueRequestEnumerator<>"/> properties to get the RSSI value and the request status.
        /// </returns>
        /// <remarks>The peripheral must be connected.</remarks>
        public static ValueRequestEnumerator<int> ReadPeripheralRssi(ScannedPeripheral peripheral, float timeoutSec = RequestDefaultTimeout)
        {
            EnsureRunningOnMainThread();

            var nativeHandle = GetPeripheralInfo(peripheral).NativeHandle;
            return new ValueRequestEnumerator<int>(RequestOperation.ReadPeripheralRssi, nativeHandle, timeoutSec,
                (p, onResult) => NativeInterface.ReadPeripheralRssi(p, onResult));
        }

        /// <summary>
        /// Returns the list of discovered services for the given peripheral.
        /// </summary>
        /// <param name="peripheral">A connected peripheral.</param>
        /// <returns>The list of discovered services.</returns>
        /// <remarks>The peripheral must be connected.</remarks>
        public static Guid[] GetPeripheralDiscoveredServices(ScannedPeripheral peripheral)
        {
            EnsureRunningOnMainThread();

            var nativeHandle = GetPeripheralInfo(peripheral).NativeHandle;
            return nativeHandle.IsValid ? NativeInterface.GetPeripheralDiscoveredServices(nativeHandle) : null;
        }

        /// <summary>
        /// Returns the list of discovered characteristics of the given peripheral's service.
        /// 
        /// The same characteristic may be listed several times according to the peripheral's configuration.
        /// </summary>
        /// <param name="peripheral">A connected peripheral.</param>
        /// <param name="serviceUuid">A service UUID.</param>
        /// <returns>The list of discovered characteristics of a service.</returns>
        /// <remarks>The peripheral must be connected.</remarks>
        public static Guid[] GetPeripheralServiceCharacteristics(ScannedPeripheral peripheral, Guid serviceUuid)
        {
            EnsureRunningOnMainThread();

            var nativeHandle = GetPeripheralInfo(peripheral).NativeHandle;
            return nativeHandle.IsValid ? NativeInterface.GetPeripheralServiceCharacteristics(nativeHandle, serviceUuid) : null;
        }

        /// <summary>
        /// Returns the BLE properties of the specified service's characteristic for the given peripheral.
        /// </summary>
        /// <param name="peripheral">A connected peripheral.</param>
        /// <param name="serviceUuid">A service UUID.</param>
        /// <param name="characteristicUuid">A characteristic UUID.</param>
        /// <param name="instanceIndex">The instance index of the characteristic if listed more than once for the service, default is zero.</param>
        /// <returns>The BLE properties of a service's characteristic.</returns>
        /// <remarks>The peripheral must be connected.</remarks>
        public static CharacteristicProperties GetCharacteristicProperties(ScannedPeripheral peripheral, Guid serviceUuid, Guid characteristicUuid, uint instanceIndex = 0)
        {
            EnsureRunningOnMainThread();

            var nativeHandle = GetPeripheralInfo(peripheral).NativeHandle;
            return nativeHandle.IsValid ? NativeInterface.GetCharacteristicProperties(nativeHandle, serviceUuid, characteristicUuid, instanceIndex) : CharacteristicProperties.None;
        }

        /// <summary>
        /// Asynchronously reads the value of the specified service's characteristic for the given peripheral.
        /// </summary>
        /// <param name="peripheral">A connected peripheral.</param>
        /// <param name="serviceUuid">A service UUID.</param>
        /// <param name="characteristicUuid">A characteristic UUID.</param>
        /// <param name="timeoutSec">The maximum allowed time for the request, in seconds.</param>
        /// <returns>
        /// An enumerator meant to be run as a coroutine.
        /// See <see cref="RequestEnumerator"/> properties to get the request status.
        /// </returns>
        /// <remarks>The peripheral must be connected.</remarks>
        public static ValueRequestEnumerator<byte[]> ReadCharacteristicAsync(ScannedPeripheral peripheral, Guid serviceUuid, Guid characteristicUuid, float timeoutSec = RequestDefaultTimeout)
        {
            return ReadCharacteristicAsync(peripheral, serviceUuid, characteristicUuid, 0, timeoutSec);
        }

        /// <summary>
        /// Asynchronously reads the value of a service's characteristic for the given peripheral.
        /// </summary>
        /// <param name="peripheral">A connected peripheral.</param>
        /// <param name="serviceUuid">A service UUID.</param>
        /// <param name="characteristicUuid">A characteristic UUID.</param>
        /// <param name="instanceIndex">The instance index of the characteristic if listed more than once for the service.</param>
        /// <param name="timeoutSec">The maximum allowed time for the request, in seconds.</param>
        /// <returns>
        /// An enumerator meant to be run as a coroutine.
        /// See <see cref="ValueRequestEnumerator<>"/> properties to get the characteristic value and the request status.
        /// </returns>
        /// <remarks>The peripheral must be connected.</remarks>
        public static ValueRequestEnumerator<byte[]> ReadCharacteristicAsync(ScannedPeripheral peripheral, Guid serviceUuid, Guid characteristicUuid, uint instanceIndex, float timeoutSec = RequestDefaultTimeout)
        {
            EnsureRunningOnMainThread();

            //TODO implement characteristic value read
            return null;
            //var pinf = GetPeripheralInfo(peripheral);
            //return new RequestEnumerator(Operation.ReadCharacteristic, pinf.NativeHandle, timeoutSec,
            //    (p, onResult) => NativeInterface.ReadCharacteristic(
            //        p, serviceUuid, characteristicUuid, instanceIndex,
            //        onValueChanged: GetNativeValueChangedHandler(pinf, onValueChanged, onResult),
            //        onResult: onResult));
        }

        /// <summary>
        /// Asynchronously write to the specified service's characteristic for the given peripheral
        /// and wait for the peripheral to respond.
        /// </summary>
        /// <param name="peripheral">A connected peripheral.</param>
        /// <param name="serviceUuid">A service UUID.</param>
        /// <param name="characteristicUuid">A characteristic UUID.</param>
        /// <param name="data">The data to write to the characteristic.</param>
        /// <param name="timeoutSec">The maximum allowed time for the request, in seconds.</param>
        /// <returns>
        /// An enumerator meant to be run as a coroutine.
        /// See <see cref="RequestEnumerator"/> properties to get the request status.
        /// </returns>
        /// <remarks>The peripheral must be connected.</remarks>
        public static RequestEnumerator WriteCharacteristicAsync(ScannedPeripheral peripheral, Guid serviceUuid, Guid characteristicUuid, byte[] data, float timeoutSec = RequestDefaultTimeout)
        {
            return WriteCharacteristicAsync(peripheral, serviceUuid, characteristicUuid, 0, data, false, timeoutSec);
        }

        /// <summary>
        /// Asynchronously write to the specified service's characteristic for the given peripheral.
        /// </summary>
        /// <param name="peripheral">A connected peripheral.</param>
        /// <param name="serviceUuid">A service UUID.</param>
        /// <param name="characteristicUuid">A characteristic UUID.</param>
        /// <param name="data">The data to write to the characteristic.</param>
        /// <param name="withoutResponse">Whether to wait for the peripheral to respond.</param>
        /// <param name="timeoutSec">The maximum allowed time for the request, in seconds.</param>
        /// <returns>
        /// An enumerator meant to be run as a coroutine.
        /// See <see cref="RequestEnumerator"/> properties to get the request status.
        /// </returns>
        /// <remarks>The peripheral must be connected.</remarks>
        public static RequestEnumerator WriteCharacteristicAsync(ScannedPeripheral peripheral, Guid serviceUuid, Guid characteristicUuid, byte[] data, bool withoutResponse = false, float timeoutSec = RequestDefaultTimeout)
        {
            return WriteCharacteristicAsync(peripheral, serviceUuid, characteristicUuid, 0, data, withoutResponse, timeoutSec);
        }

        /// <summary>
        /// Asynchronously write to the specified service's characteristic for the given peripheral.
        /// </summary>
        /// <param name="peripheral">A connected peripheral.</param>
        /// <param name="serviceUuid">A service UUID.</param>
        /// <param name="characteristicUuid">A characteristic UUID.</param>
        /// <param name="instanceIndex">The instance index of the characteristic if listed more than once for the service.</param>
        /// <param name="data">The data to write to the characteristic.</param>
        /// <param name="withoutResponse">Whether to wait for the peripheral to respond.</param>
        /// <param name="timeoutSec">The maximum allowed time for the request, in seconds.</param>
        /// <returns>
        /// An enumerator meant to be run as a coroutine.
        /// See <see cref="RequestEnumerator"/> properties to get the request status.
        /// </returns>
        /// <remarks>The peripheral must be connected.</remarks>
        public static RequestEnumerator WriteCharacteristicAsync(ScannedPeripheral peripheral, Guid serviceUuid, Guid characteristicUuid, uint instanceIndex, byte[] data, bool withoutResponse = false, float timeoutSec = RequestDefaultTimeout)
        {
            EnsureRunningOnMainThread();

            var nativeHandle = GetPeripheralInfo(peripheral).NativeHandle;
            return new RequestEnumerator(RequestOperation.WriteCharacteristic, nativeHandle, timeoutSec,
                (p, onResult) => NativeInterface.WriteCharacteristic(
                    p, serviceUuid, characteristicUuid, instanceIndex, data, withoutResponse, onResult));
        }

        /// <summary>
        /// Asynchronously subscribe for value changes of the specified service's characteristic for the given peripheral.
        /// </summary>
        /// <param name="peripheral">A connected peripheral.</param>
        /// <param name="serviceUuid">A service UUID.</param>
        /// <param name="characteristicUuid">A characteristic UUID.</param>
        /// <param name="onValueChanged">The callback to be invoked when the characteristic value changes.</param>
        /// <param name="timeoutSec">The maximum allowed time for the request, in seconds.</param>
        /// <returns>
        /// An enumerator meant to be run as a coroutine.
        /// See <see cref="RequestEnumerator"/> properties to get the request status.
        /// </returns>
        /// <remarks>The peripheral must be connected.</remarks>
        public static RequestEnumerator SubscribeCharacteristicAsync(ScannedPeripheral peripheral, Guid serviceUuid, Guid characteristicUuid, Action<byte[]> onValueChanged, float timeoutSec = RequestDefaultTimeout)
        {
            return SubscribeCharacteristicAsync(peripheral, serviceUuid, characteristicUuid, 0, onValueChanged, timeoutSec);
        }

        /// <summary>
        /// Asynchronously subscribe for value changes of the specified service's characteristic for the given peripheral.
        /// </summary>
        /// <param name="peripheral">A connected peripheral.</param>
        /// <param name="serviceUuid">A service UUID.</param>
        /// <param name="characteristicUuid">A characteristic UUID.</param>
        /// <param name="instanceIndex">The instance index of the characteristic if listed more than once for the service.</param>
        /// <param name="onValueChanged">The callback to be invoked when the characteristic value changes.</param>
        /// <param name="timeoutSec">The maximum allowed time for the request, in seconds.</param>
        /// <returns>
        /// An enumerator meant to be run as a coroutine.
        /// See <see cref="RequestEnumerator"/> properties to get the request status.
        /// </returns>
        /// <remarks>The peripheral must be connected.</remarks>
        public static RequestEnumerator SubscribeCharacteristicAsync(ScannedPeripheral peripheral, Guid serviceUuid, Guid characteristicUuid, uint instanceIndex, Action<byte[]> onValueChanged, float timeoutSec = RequestDefaultTimeout)
        {
            EnsureRunningOnMainThread();

            var pinf = GetPeripheralInfo(peripheral);
            return new RequestEnumerator(RequestOperation.SubscribeCharacteristic, pinf.NativeHandle, timeoutSec,
                (p, onResult) => NativeInterface.SubscribeCharacteristic(
                    p, serviceUuid, characteristicUuid, instanceIndex,
                    onValueChanged: GetNativeValueChangedHandler(pinf, onValueChanged, onResult),
                    onResult: onResult));
        }

        /// <summary>
        /// Asynchronously unsubscribe from the specified service's characteristic for the given peripheral.
        /// </summary>
        /// <param name="peripheral">A connected peripheral.</param>
        /// <param name="serviceUuid">A service UUID.</param>
        /// <param name="characteristicUuid">A characteristic UUID.</param>
        /// <param name="instanceIndex">The instance index of the characteristic if listed more than once for the service.</param>
        /// <param name="timeoutSec">The maximum allowed time for the request, in seconds.</param>
        /// <returns>
        /// An enumerator meant to be run as a coroutine.
        /// See <see cref="RequestEnumerator"/> properties to get the request status.
        /// </returns>
        /// <remarks>The peripheral must be connected.</remarks>
        public static RequestEnumerator UnsubscribeCharacteristicAsync(ScannedPeripheral peripheral, Guid serviceUuid, Guid characteristicUuid, uint instanceIndex = 0, float timeoutSec = RequestDefaultTimeout)
        {
            EnsureRunningOnMainThread();

            var nativeHandle = GetPeripheralInfo(peripheral).NativeHandle;
            return new RequestEnumerator(RequestOperation.UnsubscribeCharacteristic, nativeHandle, timeoutSec,
                (p, onResult) => NativeInterface.UnsubscribeCharacteristic(
                    p, serviceUuid, characteristicUuid, instanceIndex, onResult));
        }

        //! @}

        // Throws an exception if we are not running on the main thread
        private static void EnsureRunningOnMainThread()
        {
            if (System.Threading.Thread.CurrentThread.ManagedThreadId != 1)
            {
                throw new InvalidOperationException($"Methods of type {nameof(Central)} can only be called from the main thread");
            }
        }

        // Retrieves the stored peripheral state for the given scanned peripheral
        private static PeripheralInfo GetPeripheralInfo(ScannedPeripheral scannedPeripheral)
        {
            EnsureRunningOnMainThread();

            _peripherals.TryGetValue(scannedPeripheral.SystemId, out PeripheralInfo pinf);
            return pinf ?? throw new ArgumentException(nameof(scannedPeripheral), $"No peripheral found with SystemId={scannedPeripheral.SystemId}");
        }

        //TODO it doesn't seem correct to call onResult!
        private static NativeValueRequestResultHandler<byte[]> GetNativeValueChangedHandler(PeripheralInfo pinf, Action<byte[]> onValueChanged, NativeRequestResultHandler onResult)
        {
            return (data, status) =>
            {
                try
                {
                    if (status == RequestStatus.Success)
                    {
                        Debug.Assert(data != null);
                        EnqueuePeripheralAction(pinf, () => onValueChanged(data));
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
