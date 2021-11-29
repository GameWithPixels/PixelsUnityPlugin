using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using Central = Systemic.Unity.BluetoothLE.Central;
using Peripheral = Systemic.Unity.BluetoothLE.ScannedPeripheral;

namespace Systemic.Unity.Pixels
{
    public delegate void ConnectionResultCallback(Pixel pixel, bool ready, string error);
    public delegate void OperationResultCallback<T>(T result, string error);
    public delegate void OperationProgressCallback(Pixel pixel, float progress); // Value between 0 and 1

    public sealed partial class DiceBag : MonoBehaviour
    {
        HashSet<BlePixel> _pixels = new HashSet<BlePixel>();
        HashSet<BlePixel> _pixelsToDestroy = new HashSet<BlePixel>();
        Dictionary<string, string> _registeredPixels = new Dictionary<string, string>(); // Key is system id, value is name (may be empty)

        NotifyUserCallback _notifyUser;
        PlayAudioClipCallback _playAudioClip;

        public const string ConnectTimeoutErrorMessage = "Timeout trying to connect, Pixel may be out of range or turned off";

        public const float DefaultConnectionTimeout = 10; // In seconds

        public static DiceBag Instance { get; private set; }

        public bool IsScanning { get; private set; } //TODO update when Bluetooth radio turned off

        public Pixel[] AvailablePixels => _pixels.Where(p => p.isAvailable).ToArray();

        public Pixel[] ConnectedPixels => _pixels.Where(p => p.isReady).ToArray();

        public string[] RegisteredPixelSystemIds => _registeredPixels.Keys.ToArray();

        public event System.Action<Pixel> PixelDiscovered;

        #region Scan for Pixels

        /// <summary>
        /// Start scanning for new and existing Pixels, filling our lists in the process from
        /// events raised by Central.
        /// </sumary>
        public void ScanForPixels()
        {
            IsScanning = true;
            Central.PeripheralDiscovered -= OnPeripheralDiscovered;
            Central.PeripheralDiscovered += OnPeripheralDiscovered;
            Central.ScanForPeripheralsWithServices(new[] { BleUuids.ServiceUuid });
        }

        /// <summary>
        /// Stops the current scan 
        /// </sumary>
        public void StopScanForPixels()
        {
            IsScanning = false;
            Central.PeripheralDiscovered -= OnPeripheralDiscovered;
            Central.StopScan();
        }

        public void ClearScanList()
        {
            Debug.Log("Clearing scan list");

            var pixelsCopy = new List<BlePixel>(_pixels);
            foreach (var pixel in pixelsCopy)
            {
                if (pixel.connectionState == PixelConnectionState.Available)
                {
                    DestroyPixel(pixel);
                }
            }
        }

        /// <summary>
        /// Called by Central when a new Pixel is discovered!
        /// </sumary>
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

        public void ResetErrors()
        {
            foreach (var pixel in _pixels)
            {
                pixel.ResetLastError();
            }
        }
        public void SubscribeToUserNotifications(NotifyUserCallback notifyUserCallback)
        {
            _notifyUser = notifyUserCallback;
            foreach (var p in _pixels)
            {
                p.SubscribeToUserNotifications(notifyUserCallback);
            }
        }

        public void SubscribeToPlayAudioClip(PlayAudioClipCallback playAudioClipCallback)
        {
            _playAudioClip = playAudioClipCallback;
            foreach (var p in _pixels)
            {
                p.SubscribeToPlayAudioClip(playAudioClipCallback);
            }
        }

        public void RegisterPixel(string systemId, string name = "")
        {
            if (systemId == null) throw new System.ArgumentNullException(nameof(systemId));
            if (systemId.Length == 0) throw new System.ArgumentException("Cannot be empty", nameof(systemId));
            if (name == null) throw new System.ArgumentNullException(nameof(name));

            _registeredPixels[systemId] = name;
        }

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

        // Doc connection counter!
        public Coroutine ConnectPixel(Pixel pixel, System.Func<bool> requestCancelFunc, ConnectionResultCallback resultCallback = null, float connectionTimeout = DefaultConnectionTimeout)
        {
            return ConnectPixels(new Pixel[] { pixel }, requestCancelFunc, resultCallback, connectionTimeout);
        }

        public Coroutine ConnectPixels(IEnumerable<Pixel> pixels, System.Func<bool> requestCancelFunc, ConnectionResultCallback resultCallback = null, float connectionTimeout = DefaultConnectionTimeout)
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

                    // We found the Pixel die, try to connect
                    int index = i; // Capture the current value of i
                    pixel.Connect(connectionTimeout, (_, res, error) => results[index] = res ? "" : error);
                }

                // Wait for all Pixels to connect
                yield return new WaitUntil(() => results.All(msg => msg != null) || UpdateIsCancelledOrTimeout());

                if (isCancelled)
                {
                    // Disconnect any Pixel die that just successfully connected or that are still connecting
                    for (int i = 0; i < pixelsList.Count; ++i)
                    {
                        if (string.IsNullOrEmpty(results[i]))
                        {
                            var pixel = pixelsList[i];
                            pixel?.Disconnect();
                        }
                        resultCallback?.Invoke(pixelsList[i], false, "Connection to Pixel canceled by application");
                    }
                }
                else if (resultCallback != null)
                {
                    // Report connection result(s)
                    for (int i = 0; i < pixelsList.Count; ++i)
                    {
                        bool connected = results[i] == "";
                        Debug.Assert((!connected) || _pixels.Contains(pixelsList[i]));
                        resultCallback.Invoke(pixelsList[i], connected, connected ? null : results[i]);
                    }
                }
            }
        }

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

        /// <summary>
        /// Cleanly destroys a Pixel die, disconnecting if necessary and raising events in the process
        /// Does not remove it from the list though
        /// </sumary>
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

        void OnDisable()
        {
            Instance = null;
        }

        void Start()
        {
            Central.Initialize(); //TODO handle error + user message
        }

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
