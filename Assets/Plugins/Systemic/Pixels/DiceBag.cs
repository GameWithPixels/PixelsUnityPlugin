using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using Central = Systemic.Unity.BluetoothLE.Central;
using Peripheral = Systemic.Unity.BluetoothLE.ScannedPeripheral;

namespace Systemic.Unity.Pixels
{
    //TODO document Pixel registration
    /// <summary>
    /// Singleton that manages Bluetooth Low Energy Pixels.
    /// </summary>
    public sealed partial class DiceBag : MonoBehaviour
    {
        // List of known pixels
        readonly HashSet<BlePixel> _pixels = new HashSet<BlePixel>();

        // Pixels to be destroyed in the next frame update
        readonly HashSet<BlePixel> _pixelsToDestroy = new HashSet<BlePixel>();

        // Map of registered Pixels, the key is a Pixel system id and the value its name (may be empty)
        readonly Dictionary<string, string> _registeredPixels = new Dictionary<string, string>();

        // Callbacks for notifying user code
        NotifyUserCallback _notifyUser;
        PlayAudioClipCallback _playAudioClip;

        /// <summary>
        /// The default Pixel connection timeout in seconds.
        /// </summary>
        public const float DefaultConnectionTimeout = 10;

        /// <summary>
        /// Gets the singleton instance.
        /// </summary>
        public static DiceBag Instance { get; private set; }

        /// <summary>
        /// Indicates whether we are scanning for Pixel dice.
        /// </summary>
        public bool IsScanning { get; private set; } //TODO update when Bluetooth radio turned off

        /// <summary>
        /// Gets the list of available (scanned but not connected) Pixel dice.
        /// </summary>
        public Pixel[] AvailablePixels => _pixels.Where(p => p.isAvailable).ToArray();

        /// <summary>
        /// Gets the list of Pixel dice that are connected and ready to communicate.
        /// </summary>
        public Pixel[] ConnectedPixels => _pixels.Where(p => p.isReady).ToArray();

        /// <summary>
        /// Gets the system ids of the registered Pixel dice.
        /// </summary>
        public string[] RegisteredPixelSystemIds => _registeredPixels.Keys.ToArray();

        /// <summary>
        /// An event raised when a Pixel is discovered, may be raised multiple times for
        /// the same Pixel as it receives new advertisement packets from it.
        /// </summary>
        public event System.Action<Pixel> PixelDiscovered;

        #region Scan for Pixels

        /// <summary>
        /// Starts scanning for Pixel dice.
        /// </sumary>
        public void ScanForPixels()
        {
            IsScanning = true;
            Central.PeripheralDiscovered -= OnPeripheralDiscovered;
            Central.PeripheralDiscovered += OnPeripheralDiscovered;
            Central.ScanForPeripheralsWithServices(new[] { BleUuids.ServiceUuid });
        }

        /// <summary>
        /// Stops the current scan.
        /// </sumary>
        public void StopScanForPixels()
        {
            IsScanning = false;
            Central.PeripheralDiscovered -= OnPeripheralDiscovered;
            Central.StopScan();
        }

        /// <summary>
        /// Removes all available Pixel dice.
        /// </summary>
        public void ClearAvailablePixels()
        {
            var pixelsCopy = new List<BlePixel>(_pixels);
            foreach (var pixel in pixelsCopy)
            {
                if (pixel.connectionState == PixelConnectionState.Available)
                {
                    DestroyPixel(pixel);
                }
            }
        }

        // Called by Central when a new Pixel is discovered
        void OnPeripheralDiscovered(Peripheral peripheral)
        {
            // Check if have already a Pixel object for this peripheral
            var pixel = _pixels.FirstOrDefault(d => peripheral.SystemId == d.SystemId);
            if (pixel == null)
            {
                // Never seen this Pixel before
                var dieObj = new GameObject(name);
                dieObj.transform.SetParent(transform);

                pixel = dieObj.AddComponent<BlePixel>();
                pixel.DisconnectedUnexpectedly += () => DestroyPixel(pixel);
                pixel.SubscribeToUserNotifications(_notifyUser);
                pixel.SubscribeToPlayAudioClip(_playAudioClip);

                _pixels.Add(pixel);
            }

            // Discard discovery event if peripheral is not available anymore (it might just have started connecting)
            if (pixel.connectionState <= PixelConnectionState.Available)
            {
                Debug.Log($"Discovered Pixel {peripheral.Name}");

                // Update Pixel
                pixel.Setup(peripheral);

                // And notify
                PixelDiscovered?.Invoke(pixel);
            }
        }

        #endregion

        #region Communicate with Pixels

        /// <summary>
        /// Reset errors on all know Pixel dice.
        /// </summary>
        public void ResetErrors()
        {
            foreach (var pixel in _pixels)
            {
                pixel.ResetLastError();
            }
        }

        /// <summary>
        /// Subscribe to user notifications from Pixel dice.
        /// </summary>
        /// <param name="notifyUserCallback">The callback to be called when a Pixel requires to notify the user.</param>
        public void SubscribeToUserNotifications(NotifyUserCallback notifyUserCallback)
        {
            _notifyUser = notifyUserCallback;
            foreach (var p in _pixels)
            {
                p.SubscribeToUserNotifications(notifyUserCallback);
            }
        }

        /// <summary>
        /// Subscribe to play audio clip notifications from Pixel dice.
        /// </summary>
        /// <param name="playAudioClipCallback">The callback to be called when a Pixel requires to play an audio clip.</param>
        public void SubscribeToPlayAudioClip(PlayAudioClipCallback playAudioClipCallback)
        {
            _playAudioClip = playAudioClipCallback;
            foreach (var p in _pixels)
            {
                p.SubscribeToPlayAudioClip(playAudioClipCallback);
            }
        }

        /// <summary>
        /// Register the given Pixel.
        /// </summary>
        /// <param name="systemId">The system id for the Pixel.</param>
        /// <param name="name">The name of the Pixel, may be empty.</param>
        public void RegisterPixel(string systemId, string name = "")
        {
            if (systemId == null) throw new System.ArgumentNullException(nameof(systemId));
            if (systemId.Length == 0) throw new System.ArgumentException("Cannot be empty", nameof(systemId));
            if (name == null) throw new System.ArgumentNullException(nameof(name));

            _registeredPixels[systemId] = name;
        }

        /// <summary>
        /// Unregister the given Pixel.
        /// </summary>
        /// <param name="systemId">The system id for the Pixel.</param>
        public void UnregisterPixel(string systemId)
        {
            if (systemId == null) throw new System.ArgumentNullException(nameof(systemId));
            if (systemId.Length == 0) throw new System.ArgumentException("Cannot be empty", nameof(systemId));

            if (!_registeredPixels.ContainsKey(systemId))
            {
                Debug.LogError($"Trying to unregister unknown Pixel with id {systemId}");
            }
            else
            {
                var pixel = _pixels.FirstOrDefault(p => p.SystemId == systemId);
                if (pixel != null)
                {
                    UnregisterPixel(pixel);
                }
            }
        }

        /// <summary>
        /// Unregister the given Pixel.
        /// </summary>
        /// <param name="pixel">The Pixel to unregister.</param>
        public void UnregisterPixel(Pixel pixel)
        {
            if (pixel == null) throw new System.ArgumentNullException(nameof(pixel));

            var blePixel = pixel as BlePixel;
            if (!_pixels.Contains(blePixel))
            {
                Debug.LogError("Trying to unregister a Pixel that is either null or unknown");

            }
            else
            {
                if (_pixelsToDestroy.Add(blePixel))
                {
                    if (blePixel.isConnectingOrReady)
                    {
                        blePixel.Disconnect((d, r, s) => DestroyPixel(blePixel), forceDisconnect: true);
                    }
                }
                _pixels.Remove(blePixel);
            }
        }

        /// <summary>
        /// Requests to connect to the given Pixel.
        ///
        /// Each Pixel object maintains a connection counter which is incremented when calling
        /// this method while the die is already connecting or connected.
        /// The counter is decremented for each disconnection call and the die is disconnected
        /// only once the counter reaches zero.
        /// 
        /// This connection counter allows for different parts of the user code to request a connection
        /// or a disconnection without impacting each others.
        /// </summary>
        /// <param name="pixel">The Pixel to connect to.</param>
        /// <param name="requestCancelFunc">A callback which is called for each frame during connection,
        ///                                 it may return true to cancel the connection request.</param>
        /// <param name="onResult">An optional callback that is called when the operation completes
        ///                        successfully (true) or not (false) with an error message.</param>
        /// <param name="connectionTimeout">The connection timeout in seconds.</param>
        /// <returns>The coroutine running the request.</returns>
        public Coroutine ConnectPixel(Pixel pixel, System.Func<bool> requestCancelFunc, ConnectionResultCallback onResult = null, float connectionTimeout = DefaultConnectionTimeout)
        {
            if (pixel == null) throw new System.ArgumentNullException(nameof(pixel));
            if (requestCancelFunc == null) throw new System.ArgumentNullException(nameof(requestCancelFunc));

            return ConnectPixels(new Pixel[] { pixel }, requestCancelFunc, onResult, connectionTimeout);
        }

        /// <summary>
        /// Requests to connect to the given list of Pixel dice.
        ///
        /// Each Pixel object maintains a connection counter which is incremented when calling
        /// this method while the die is already connecting or connected.
        /// The counter is decremented for each disconnection call and the die is disconnected
        /// only once the counter reaches zero.
        /// 
        /// This connection counter allows for different parts of the user code to request a connection
        /// or a disconnection without impacting each others.
        /// </summary>
        /// <param name="pixels">The Pixel dice to connect to.</param>
        /// <param name="requestCancelFunc">A callback which is called for each frame during connection,
        ///                                 it may return true to cancel the connection request.</param>
        /// <param name="onResult">An optional callback that is called when the operation completes
        ///                        successfully (true) or not (false) with an error message.</param>
        /// <param name="connectionTimeout">The connection timeout in seconds.</param>
        /// <returns>The coroutine running the request.</returns>
        public Coroutine ConnectPixels(IEnumerable<Pixel> pixels, System.Func<bool> requestCancelFunc, ConnectionResultCallback onResult = null, float connectionTimeout = DefaultConnectionTimeout)
        {
            if (pixels == null) throw new System.ArgumentNullException(nameof(pixels));
            if (requestCancelFunc == null) throw new System.ArgumentNullException(nameof(requestCancelFunc));

            var pixelsList = new List<BlePixel>();
            foreach (var p in pixels)
            {
                var blePixel = p as BlePixel;
                if ((blePixel == null) || (!_pixels.Contains(p)))
                {
                    Debug.LogError("Some Pixels requested to be connected are either null or unknown");
                    return null;
                }
                pixelsList.Add(blePixel);
            }
            if (pixelsList.Count == 0)
            {
                Debug.LogWarning("Empty list of Pixels requested to be connected");
                return null;
            }

            // Connect
            return StartCoroutine(ConnectAsync());

            IEnumerator ConnectAsync()
            {
                // requestCancelFunc() only need to return true once to cancel the operation
                bool isCancelled = false;
                bool UpdateIsCancelledOrTimeout() => isCancelled |= requestCancelFunc();

                // Array of error message for each Pixel connection attempt
                // - if null: still connecting
                // - if empty string: successfully connected
                var results = new string[pixelsList.Count];
                for (int i = 0; i < pixelsList.Count; ++i)
                {
                    var pixel = pixelsList[i];
                    _registeredPixels[pixel.systemId] = pixel.name;

                    // We found the Pixel, try to connect
                    int index = i; // Capture the current value of i
                    pixel.Connect(connectionTimeout, (_, res, error) => results[index] = res ? "" : error);
                }

                // Wait for all Pixels to connect
                yield return new WaitUntil(() => results.All(msg => msg != null) || UpdateIsCancelledOrTimeout());

                if (isCancelled)
                {
                    // Disconnect any Pixel that just successfully connected or that are still connecting
                    for (int i = 0; i < pixelsList.Count; ++i)
                    {
                        if (string.IsNullOrEmpty(results[i]))
                        {
                            var pixel = pixelsList[i];
                            pixel?.Disconnect();
                        }
                        onResult?.Invoke(pixelsList[i], false, "Connection to Pixel canceled by application");
                    }
                }
                else if (onResult != null)
                {
                    // Report connection result(s)
                    for (int i = 0; i < pixelsList.Count; ++i)
                    {
                        bool connected = results[i] == "";
                        Debug.Assert((!connected) || _pixels.Contains(pixelsList[i]));
                        onResult.Invoke(pixelsList[i], connected, connected ? null : results[i]);
                    }
                }
            }
        }

        /// <summary>
        /// Requests to disconnect to the given Pixel.
        /// 
        /// If a connection was requested several times before disconnecting, the same number
        /// of calls must be made to this methods for the disconnection to occur, unless
        /// <paramref name="forceDisconnect"/> is true.
        /// </summary>
        /// <param name="pixel">The Pixel to disconnect from.</param>
        /// <param name="forceDisconnect">Whether to disconnect even if there were more connection requests
        ///                               than calls to disconnect.</param>
        /// <returns>The coroutine running the request.</returns>
        public Coroutine DisconnectPixel(Pixel pixel, bool forceDisconnect = false)
        {
            if (pixel == null) throw new System.ArgumentNullException(nameof(pixel));
            var blePixel = (BlePixel)pixel;
            return StartCoroutine(DisconnectAsync());

            IEnumerator DisconnectAsync()
            {
                if (!_pixels.Contains(blePixel))
                {
                    Debug.LogError($"Trying to disconnect unknown Pixel {blePixel.name}");
                }
                else
                {
                    bool? res = null;
                    blePixel.Disconnect((d, r, s) => res = r, forceDisconnect);

                    yield return new WaitUntil(() => res.HasValue);
                }
            }
        }

        // Cleanly destroys a Pixel instance, disconnecting if necessary and raising events in the process
        void DestroyPixel(BlePixel pixel)
        {
            Debug.Assert(pixel);
            if (pixel)
            {
                GameObject.Destroy(pixel.gameObject);
            }
            _pixels.Remove(pixel);
            _pixelsToDestroy.Remove(pixel);
        }

        #endregion

        #region Unity messages

        // Called when the behaviour becomes enabled and active
        void OnEnable()
        {
            // Safeguard
            if ((Instance != null) && (Instance != this))
            {
                Debug.LogError($"A second instance of {typeof(DiceBag)} got spawned, now destroying it");
                Destroy(this);
            }
            Instance = this;
        }

        // Called when the behaviour becomes disabled or inactive
        void OnDisable()
        {
            Instance = null;
        }

        // Start is called before the first frame update
        void Start()
        {
            Central.Initialize(); //TODO handle error + user message
        }

        // Update is called once per frame
        void Update()
        {
            List<BlePixel> destroyNow = null;
            foreach (var pixel in _pixelsToDestroy)
            {
                if (!pixel.isConnectingOrReady)
                {
                    if (destroyNow == null)
                    {
                        destroyNow = new List<BlePixel>();
                    }
                    destroyNow.Add(pixel);
                }
            }
            if (destroyNow != null)
            {
                foreach (var pixel in destroyNow)
                {
                    DestroyPixel(pixel);
                }
            }
        }

        #endregion
    }
}
