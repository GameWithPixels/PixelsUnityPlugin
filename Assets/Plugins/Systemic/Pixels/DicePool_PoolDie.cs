using Dice;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

using Central = Systemic.Unity.BluetoothLE.Central;
using Peripheral = Systemic.Unity.BluetoothLE.ScannedPeripheral;

partial class DicePool
{
    public const string ConnectTimeoutErrorMessage = "Timeout trying to connect, Die may be out of range or turned off";

    sealed class PoolDie : Die
    {
        /// <summary>
        /// This data structure mirrors the data in firmware/bluetooth/bluetooth_stack.cpp
        /// </sumary>
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct PixelAdvertisingData
        {
            // Die type identification
            public DieDesignAndColor designAndColor; // Physical look, also only 8 bits
            public byte faceCount; // Which kind of dice this is

            // Device ID
            public uint deviceId;

            // Current state
            public DieRollState rollState; // Indicates whether the dice is being shaken
            public byte currentFace; // Which face is currently up
            public byte batteryLevel; // 0 -> 255
        };

        // The underlying BLE device
        Peripheral _peripheral;

        // Count how many time Connect() was called, so we only disconnect after the same number of calls to Disconnect()
        int _connectionCount;

        // Connection internal events
        ConnectionResultHandler _onConnectionResult;
        ConnectionResultHandler _onDisconnectionResult;

        public delegate void ConnectionResultHandler(Die die, bool success, string error);

        /// <summary>
        /// Event triggered when die got disconnected for other reasons than a call to Disconnect().
        /// Most likely the BLE device was turned off or got out of range.
        /// </summary>
        public event System.Action DisconnectedUnexpectedly;

        public string SystemId => _peripheral?.SystemId;

        public void Setup(Peripheral peripheral)
        {
            EnsureRunningOnMainThread();

            if (peripheral == null) throw new System.ArgumentNullException(nameof(peripheral));

            if (_peripheral == null)
            {
                Debug.Assert(connectionState == DieConnectionState.Invalid);
                connectionState = DieConnectionState.Available;
            }
            else if (_peripheral.SystemId != peripheral.SystemId)
            {
                throw new System.InvalidOperationException("Trying to assign another peripheral to Die");
            }

            _peripheral = peripheral;
            systemId = _peripheral.SystemId;
            name = _peripheral.Name;

            if (_peripheral.ManufacturerData?.Count > 0)
            {
                // Marshall the data into the struct we expect
                int size = Marshal.SizeOf(typeof(PixelAdvertisingData));
                if (_peripheral.ManufacturerData.Count == size)
                {
                    System.IntPtr ptr = Marshal.AllocHGlobal(size);
                    Marshal.Copy(_peripheral.ManufacturerData.ToArray(), 0, ptr, size);
                    var advData = Marshal.PtrToStructure<PixelAdvertisingData>(ptr);
                    Marshal.FreeHGlobal(ptr);

                    // Update die data
                    bool appearanceChanged = faceCount != advData.faceCount || designAndColor != advData.designAndColor;
                    bool rollStateChanged = state != advData.rollState || face != advData.currentFace;
                    faceCount = advData.faceCount;
                    designAndColor = advData.designAndColor;
                    state = advData.rollState;
                    face = advData.currentFace;
                    batteryLevel = advData.batteryLevel / 255f;
                    rssi = _peripheral.Rssi;

                    // Trigger callbacks
                    BatteryLevelChanged?.Invoke(this, batteryLevel, charging);
                    if (appearanceChanged)
                    {
                        AppearanceChanged?.Invoke(this, faceCount, designAndColor);
                    }
                    if (rollStateChanged)
                    {
                        StateChanged?.Invoke(this, state, face);
                    }
                    RssiChanged?.Invoke(this, rssi);
                }
                else
                {
                    Debug.LogError($"Die {name}: incorrect advertising data length {_peripheral.ManufacturerData.Count}, expected: {size}");
                }
            }
        }

        public void ResetLastError()
        {
            EnsureRunningOnMainThread();

            lastError = DieLastError.None;
        }

        public void Connect(ConnectionResultHandler onConnectionResult = null)
        {
            EnsureRunningOnMainThread();

            void IncrementConnectCount()
            {
                ++_connectionCount;
                Debug.Log($"Die {SafeName}: Connecting, counter={_connectionCount}");
            }

            switch (connectionState)
            {
                default:
                    string error = $"Invalid die state {connectionState} while attempting to connect";
                    Debug.LogError($"Die {SafeName}: {error}");
                    onConnectionResult?.Invoke(this, false, error);
                    break;
                case DieConnectionState.Available:
                    IncrementConnectCount();
                    Debug.Assert(_connectionCount == 1);
                    this._onConnectionResult += onConnectionResult;
                    DoConnect();
                    break;
                case DieConnectionState.Connecting:
                case DieConnectionState.Identifying:
                    // Already in the process of connecting, just add the callback and wait
                    IncrementConnectCount();
                    this._onConnectionResult += onConnectionResult;
                    break;
                case DieConnectionState.Ready:
                    // Trigger the callback immediately
                    IncrementConnectCount();
                    onConnectionResult?.Invoke(this, true, null);
                    break;
            }
        }

        public void Disconnect(ConnectionResultHandler onDisconnectionResult = null, bool forceDisconnect = false)
        {
            EnsureRunningOnMainThread();

            switch (connectionState)
            {
                default:
                    // Die not connected
                    onDisconnectionResult?.Invoke(this, true, null);
                    break;
                case DieConnectionState.Ready:
                case DieConnectionState.Connecting:
                case DieConnectionState.Identifying:
                    Debug.Assert(_connectionCount > 0);
                    _connectionCount = forceDisconnect ? 0 : Mathf.Max(0, _connectionCount - 1);

                    Debug.Log($"Die {SafeName}: Disconnecting, counter={_connectionCount}, forceDisconnect={forceDisconnect}");

                    if (_connectionCount == 0)
                    {
                        // Register to be notified when disconnection is complete
                        this._onDisconnectionResult += onDisconnectionResult;
                        DoDisconnect();
                    }
                    else
                    {
                        // Trigger the callback immediately
                        onDisconnectionResult(this, true, null);
                    }
                    break;
            }
        }

        void DoConnect()
        {
            Debug.Assert(connectionState == DieConnectionState.Available);
            if (connectionState == DieConnectionState.Available)
            {
                connectionState = DieConnectionState.Connecting;
                StartCoroutine(ConnectAsync());

                IEnumerator ConnectAsync()
                {
                    Debug.Log($"Die {SafeName}: Connecting...");
                    Systemic.Unity.BluetoothLE.RequestEnumerator connectRequest = null;
                    connectRequest = Central.ConnectPeripheralAsync(
                        _peripheral,
                        // Forward connection event it our behavior is still valid and the request hasn't timed-out
                        // (in which case the disconnect event is already taken care by the code following the yield below)
                        (p, connected) => { if ((this != null) && (!connectRequest.IsTimeout)) OnConnectionEvent(p, connected); },
                        DicePool.Instance.ConnectionTimeout);

                    yield return connectRequest;
                    string lastRequestError = connectRequest.Error;

                    bool canceled = connectionState != DieConnectionState.Connecting;
                    if (!canceled)
                    {
                        string error = null;
                        if (connectRequest.IsSuccess)
                        {
                            // Now connected to die, get characteristics and subscribe before switching to Identifying state
                            var pixelService = Systemic.Unity.Pixels.BleUuids.ServiceUuid;
                            var subscribeCharacteristic = Systemic.Unity.Pixels.BleUuids.NotifyCharacteristicUuid;
                            var writeCharacteristic = Systemic.Unity.Pixels.BleUuids.WriteCharacteristicUuid;

                            var characteristics = Central.GetPeripheralServiceCharacteristics(_peripheral, pixelService);
                            if ((characteristics != null) && characteristics.Contains(subscribeCharacteristic) && characteristics.Contains(writeCharacteristic))
                            {
                                var subscribeRequest = Central.SubscribeCharacteristicAsync(
                                    _peripheral, pixelService, subscribeCharacteristic,
                                    // Forward value change event if our behavior is still valid
                                    data => { if (this != null) { OnValueChanged(data); } });

                                yield return subscribeRequest;
                                lastRequestError = subscribeRequest.Error;

                                if (subscribeRequest.IsTimeout)
                                {
                                    error = ConnectTimeoutErrorMessage;
                                }    
                                else if (!subscribeRequest.IsSuccess)
                                {
                                    error = $"Subscribe request failed, {subscribeRequest.Error}";
                                }
                            }
                            else if (characteristics == null)
                            {
                                error = $"Characteristics request failed";
                            }
                            else
                            {
                                error = "Missing required characteristics";
                            }
                        }
                        else if (connectRequest.IsTimeout)
                        {
                            error = ConnectTimeoutErrorMessage;
                        }
                        else
                        {
                            error = $"Connection failed: {connectRequest.Error}";
                        }

                        // Check that we are still in the connecting state
                        canceled = connectionState != DieConnectionState.Connecting;
                        if ((!canceled) && (error == null))
                        {
                            // Move on to identification
                            yield return DoIdentifyAsync(req =>
                            {
                                lastRequestError = req.Error;
                                error = req.IsTimeout ? ConnectTimeoutErrorMessage : req.Error;
                            });

                            // Check connection state
                            canceled = connectionState != DieConnectionState.Identifying;
                            //TODO we need a counter, in case another connect is already going on
                        }

                        if (!canceled)
                        {
                            if (error == null)
                            {
                                // Die is finally ready, awesome!
                                connectionState = DieConnectionState.Ready;

                                // Notify success
                                NotifyConnectionResult();
                            }
                            else
                            {
                                // Trigger callback
                                NotifyConnectionResult(error);

                                // Updating info didn't work, disconnect the die
                                DoDisconnect(DieLastError.ConnectionError);
                            }
                        }
                    }

                    if (canceled)
                    {
                        // Wrong state => we got canceled, just abort without notifying
                        Debug.LogWarning($"Die {SafeName}: connect sequence interrupted, last request error is: {lastRequestError}");
                    }
                }
            }

            IEnumerator DoIdentifyAsync(System.Action<IOperationEnumerator> onResult)
            {
                Debug.Assert(connectionState == DieConnectionState.Connecting);

                // We're going to identify the die
                connectionState = DieConnectionState.Identifying;

                // Reset error
                SetLastError(DieLastError.None);

                // Ask the die who it is!
                var request = new SendMessageAndWaitForResponseEnumerator<DieMessageWhoAreYou, DieMessageIAmADie>(this) as IOperationEnumerator;
                yield return request;

                // Continue identification if we are still in the identify state
                if (request.IsSuccess && (connectionState == DieConnectionState.Identifying))
                {
                    // Get the die initial state
                    Debug.LogWarning($"Sending State");
                    request = new SendMessageAndWaitForResponseEnumerator<DieMessageRequestState, DieMessageState>(this);
                    yield return request;
                }

                // Report result
                onResult(request);
            }

            void OnConnectionEvent(Peripheral p, bool connected)
            {
                Debug.Assert(_peripheral.SystemId == p.SystemId);

                Debug.Log($"Die {SafeName}: {(connected ? "Connected" : "Disconnected")}");

                if ((!connected) && (connectionState != DieConnectionState.Disconnecting))
                {
                    if ((connectionState == DieConnectionState.Connecting) || (connectionState == DieConnectionState.Identifying))
                    {
                        NotifyConnectionResult("Disconnected unexpectedly");
                    }
                    else
                    {
                        Debug.LogError($"{SafeName}: Got disconnected unexpectedly while in state {connectionState}");
                    }

                    // Reset connection count
                    _connectionCount = 0;

                    connectionState = DieConnectionState.Available;
                    SetLastError(DieLastError.Disconnected);

                    DisconnectedUnexpectedly?.Invoke();
                }
            }

            void OnValueChanged(byte[] data)
            {
                Debug.Assert(data != null);

                // Process the message coming from the actual die!
                var message = DieMessages.FromByteArray(data);
                if (message != null)
                {
                    Debug.Log($"Die {SafeName}: Received message of type {message.GetType()}");

                    if (messageDelegates.TryGetValue(message.type, out MessageReceivedEvent del))
                    {
                        del.Invoke(message);
                    }
                }
            }
        }

        void NotifyConnectionResult(string error = null)
        {
            if (error != null)
            {
                Debug.LogError($"Die {SafeName}: {error}");
            }

            var callbackCopy = _onConnectionResult;
            _onConnectionResult = null;
            callbackCopy?.Invoke(this, error == null, error);
        }

        /// <summary>
        /// Disconnects a die, doesn't remove it from the pool though
        /// </sumary>
        void DoDisconnect(DieLastError error = DieLastError.None)
        {
            if (error != DieLastError.None)
            {
                // We're disconnecting because of an error
                SetLastError(error);
            }

            Debug.Assert(isConnectingOrReady);
            if (isConnectingOrReady)
            {
                _connectionCount = 0;
                connectionState = DieConnectionState.Disconnecting;
                StartCoroutine(DisconnectAsync());

                IEnumerator DisconnectAsync()
                {
                    Debug.Log($"Die {SafeName}: Disconnecting...");
                    yield return Central.DisconnectPeripheralAsync(_peripheral);

                    Debug.Assert(_connectionCount == 0);
                    connectionState = DieConnectionState.Available;

                    var callbackCopy = _onDisconnectionResult;
                    _onDisconnectionResult = null;
                    callbackCopy?.Invoke(this, true, null); // Always return a success
                }
            }
        }

        void SetLastError(DieLastError newError)
        {
            lastError = newError;
            if (lastError != DieLastError.None)
            {
                GotError?.Invoke(this, newError);
            }
        }

        class WriteDataEnumerator : IOperationEnumerator
        {
            readonly Systemic.Unity.BluetoothLE.RequestEnumerator _request;

            public bool IsDone => _request.IsDone;

            public bool IsTimeout => _request.IsTimeout;

            public bool IsSuccess => _request.IsSuccess;

            public string Error => _request.Error;

            public object Current => _request.Current;

            public WriteDataEnumerator(Peripheral peripheral, byte[] bytes, float timeout)
            {
                var pixelService = Systemic.Unity.Pixels.BleUuids.ServiceUuid;
                var writeCharacteristic = Systemic.Unity.Pixels.BleUuids.WriteCharacteristicUuid;
                _request = Central.WriteCharacteristicAsync(peripheral, pixelService, writeCharacteristic, bytes, timeout);
            }

            public bool MoveNext()
            {
                return _request.MoveNext();
            }

            public void Reset()
            {
                _request.Reset();
            }
        }

        protected override IOperationEnumerator WriteDataAsync(byte[] bytes, float timeout = 0)
        {
            EnsureRunningOnMainThread();

            Debug.Log($"Die {SafeName}: Sending message {(DieMessageType)bytes?.FirstOrDefault()}");

            return new WriteDataEnumerator(_peripheral, bytes, timeout);

        }

        void OnDestroy()
        {
            DisconnectedUnexpectedly = null;
            _onConnectionResult = null;
            _onDisconnectionResult = null;

            bool disconnect = isConnectingOrReady;
            _connectionCount = 0;
            connectionState = DieConnectionState.Invalid;

            Debug.Log($"Die {name}: destroyed (was connecting or connected: {disconnect})");

            if (disconnect)
            {
                Debug.Assert(_peripheral != null);

                // Start Disconnect coroutine on DicePool since we are getting destroyed
                var pool = DicePool.Instance;
                if (pool && pool.gameObject.activeInHierarchy)
                {
                    pool.StartCoroutine(Central.DisconnectPeripheralAsync(_peripheral));
                }
            }
        }
    }
}
