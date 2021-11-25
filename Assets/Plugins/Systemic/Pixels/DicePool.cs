using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using Central = Systemic.Unity.BluetoothLE.Central;
using Peripheral = Systemic.Unity.BluetoothLE.ScannedPeripheral;

namespace Systemic.Unity.Pixels
{
    public interface IPersistentEditPixelsList
    {
        IEditPixel AddNewPixel(Pixel pixel);
        //var editPixel = AppDataSet.Instance.AddNewDie(pixel);
        //AppDataSet.Instance.SaveData();

        void RemovePixel(IEditPixel editPixel);
        //AppDataSet.Instance.DeleteDie(editPixel);
        //AppDataSet.Instance.SaveData();

        IEditPixel[] GetEditPixels();
        //AppDataSet.Instance.dice
    }

    public interface IDialogBox
    {
        bool Show(string title, string message, string okMessage = "Ok", string cancelMessage = null, System.Action<bool> closeAction = null);
    }

    public interface IAudioPlayer
    {
        void PlayAudioClip(uint clipId);
    }

    public delegate void PixelOperationResultHandler<T>(T result, string error);
    public delegate void PixelOperationProgressHandler(Pixel pixel, float progress); // Value between 0 and 1

    public sealed partial class DicePool : MonoBehaviour
    {
        readonly List<BlePixel> _pool = new List<BlePixel>();

        [SerializeField]
        IPersistentEditDiceList _editDiceList = null;

        [SerializeField]
        IDialogBox _dialogBox = null;

        [SerializeField]
        IAudioPlayer _audioPlayer = null;

        [SerializeField]
        float _connectionTimeout = 10.0f;

        [SerializeField]
        float _scanTimeout = 5.0f;

        public const string ConnectTimeoutErrorMessage = "Timeout trying to connect, Pixel may be out of range or turned off";

        public static DicePool Instance { get; private set; }

        public float ConnectionTimeout => _connectionTimeout;

        public float ScanTimeout => _scanTimeout;

        public Pixel[] ScannedPixels => _pool.ToArray();

        #region Scanning

        // Multiple things may request bluetooth scanning, so we need to arbitrate when
        // we actually ask Central to scan or not. This counter will let us know
        // exactly when to start or stop asking central.
        int _scanRequestCount = 0;

        /// <summary>
        /// Start scanning for new and existing Pixels, filling our lists in the process from
        /// events triggered by Central.
        /// </sumary>
        public void ScanForPixels()
        {
            _scanRequestCount++;
            if (_scanRequestCount == 1)
            {
                Central.PeripheralDiscovered += OnPeripheralDiscovered;
                Central.ScanForPeripheralsWithServices(new[] { BleUuids.ServiceUuid });
            }
            else
            {
                Debug.Log("Already scanning, scanRequestCount=" + _scanRequestCount);
            }
        }

        /// <summary>
        /// Stops the current scan 
        /// </sumary>
        public void StopScanForPixels()
        {
            if (_scanRequestCount == 0)
            {
                Debug.LogError("Pool not currently scanning");
            }
            else
            {
                _scanRequestCount--;
                if (_scanRequestCount == 0)
                {
                    Central.PeripheralDiscovered -= OnPeripheralDiscovered;
                    Central.StopScan();
                }
                // Else ignore
            }
        }

        public void ClearScanList()
        {
            Debug.Log("Clearing scan list");

            var pixelsCopy = new List<BlePixel>(_pool);
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
            Debug.Log($"Discovered Pixel {peripheral.Name}");

            // If the Pixel die exists, tell it that it's advertising now
            // otherwise create it (and tell it that its advertising :)
            var pixel = _pool.FirstOrDefault(d => peripheral.SystemId == d.SystemId);
            if (pixel == null)
            {
                // Never seen this Pixel before
                var dieObj = new GameObject(name);
                dieObj.transform.SetParent(transform);

                pixel = dieObj.AddComponent<BlePixel>();
                pixel.DisconnectedUnexpectedly += () => DestroyPixel(pixel);
                pixel.NotifyUserReceived += (_, cancel, text, ackCallback) => _dialogBox.Show("Message from " + name, text, "Ok", cancel ? "Cancel" : null, ackCallback);
                pixel.PlayAudioClipReceived += (_, clipId) => _audioPlayer.PlayAudioClip(clipId);

                _pool.Add(pixel);
            }

            pixel.Setup(peripheral);

            var editPixel = _editPixels.Keys.FirstOrDefault(d => d.systemId == pixel.SystemId);
            if (editPixel != null)
            {
                SetDieForEditDie(editPixel, pixel);
                Debug.Log($"Pairing discovered Pixel: {pixel.SystemId} - {pixel.name}");
            }
            else
            {
                Debug.Log($"Discovered Pixel is unpaired: {pixel.SystemId} - {pixel.name}");
            }

            if (pixel.connectionState != PixelConnectionState.Available)
            {
                // All other are errors
                Debug.LogError($"Discovered Pixel {pixel.name} in invalid state: {pixel.connectionState}");
                //TODO pixel.SetConnectionState(DieConnectionState.Available);
            }

            PixelDiscovered?.Invoke(pixel);
        }

        #endregion

        #region BLE Pixel management

        Dictionary<IEditPixel, BlePixel> _editPixels = new Dictionary<IEditPixel, BlePixel>();
        Dictionary<IEditPixel, BlePixel> _editPixelsToDestroy = new Dictionary<IEditPixel, BlePixel>();

        public IEnumerable<IEditPixel> allEditPixels => _editPixels.Keys.ToArray();

        public delegate void PixelDiscoveredHandler(Pixel pixel);
        public static event PixelDiscoveredHandler PixelDiscovered;

        public delegate void EditPixelEventHandler(IEditPixel editPixel);
        public static event EditPixelEventHandler EditPixelAdded;
        public static event EditPixelEventHandler RemovingEditPixel;

        public static event EditPixelEventHandler PixelConnected;
        public static event EditPixelEventHandler PixelDisconnected;

        public void ResetErrors()
        {
            foreach (var pixel in _pool)
            {
                pixel.ResetLastError();
            }
        }

        public Coroutine AddDiscoveredPixel(Pixel discoveredPixel, PixelOperationProgressHandler onProgress = null)
        {
            return AddDiscoveredPixels(new[] { discoveredPixel }, onProgress);
        }

        public Coroutine AddDiscoveredPixels(IEnumerable<Pixel> discoveredPixels, PixelOperationProgressHandler onProgress = null)
        {
            if (discoveredPixels == null) throw new System.ArgumentNullException(nameof(discoveredPixels));

            var pixelsList = discoveredPixels.ToArray();
            return StartCoroutine(AddDiscoveredDiceCr());

            IEnumerator AddDiscoveredDiceCr()
            {
                for (int i = 0; i < pixelsList.Length; ++i)
                {
                    var pixel = pixelsList[i] as BlePixel;
                    if ((pixel == null) || (!_pool.Contains(pixel)))
                    {
                        Debug.LogError("Attempting to add unknown Pixel " + pixel.name);
                        continue;
                    }

                    onProgress?.Invoke(pixel, (float)(i + 1) / pixelsList.Length);

                    // Here we wait a couple frames to give the programming box a chance to show up
                    // on PC at least the attempt to connect can freeze the app
                    yield return null;
                    yield return null;

                    IEditPixel AddNewDie()
                    {
                        // Add a new entry in the dataset
                        var editPixel = _editPixelsList.AddNewPixel(pixel);

                        // And in our map
                        _editPixels.Add(editPixel, null);
                        EditPixelAdded?.Invoke(editPixel);
                        SetDieForEditDie(editPixel, pixel);

                        return editPixel;
                    }

                    if (!string.IsNullOrEmpty(pixel.systemId))
                    {
                        AddNewDie();
                    }
                    else
                    {
                        Debug.LogError($"Pixel {pixel.name} doesn't have a system id");
                    }
                }
            }
        }

        public Coroutine ConnectPixel(IEditPixel editPixel, System.Func<bool> requestCancelFunc, System.Action<IEditPixel, bool, string> pixelReadyCallback = null)
        {
            return ConnectPixels(new IEditPixel[] { editPixel }, requestCancelFunc, pixelReadyCallback);
        }

        public Coroutine ConnectPixels(IEnumerable<IEditPixel> editPixels, System.Func<bool> requestCancelFunc, System.Action<IEditPixel, bool, string> pixelReadyCallback = null)
        {
            if (editPixels == null) throw new System.ArgumentNullException(nameof(editPixels));
            if (requestCancelFunc == null) throw new System.ArgumentNullException(nameof(requestCancelFunc));

            var editPixelsList = editPixels.ToArray();
            if (!editPixelsList.All(d => _editPixels.ContainsKey(d)))
            {
                Debug.LogError("Some Edit Pixel are not valid");
                return null;
            }
            else
            {
                return StartCoroutine(ConnectPixelsCr());

                IEnumerator ConnectPixelsCr()
                {
                    // requestCancelFunc() only need to return true once to cancel the operation
                    bool isCancelled = false;
                    bool UpdateIsCancelledOrTimeout() => isCancelled |= requestCancelFunc();

                    if (editPixelsList.Any(ed => GetBlePixel(ed) == null))
                    {
                        ScanForPixels();

                        Debug.Log($"Scanning for Pixel {string.Join(", ", editPixelsList.Select(d => d.name))} with timeout of {ScanTimeout}s");

                        // Wait for all Pixels to be scanned, or timeout
                        float scanTimeout = Time.realtimeSinceStartup + ScanTimeout;
                        yield return new WaitUntil(() => editPixelsList.All(ed => GetBlePixel(ed) != null) || (Time.realtimeSinceStartup > scanTimeout) || UpdateIsCancelledOrTimeout());

                        StopScanForPixels();
                    }

                    // Array of error message for each Pixel connection attempt
                    // - if null: still connecting
                    // - if empty string: successfully connected
                    var results = new string[editPixelsList.Length];
                    for (int i = 0; i < editPixelsList.Length; ++i)
                    {
                        var editPixel = editPixelsList[i];
                        var pixel = GetBlePixel(editPixel);
                        if (pixel != null)
                        {
                            Debug.Assert(_pool.Contains(pixel));

                            // We found the Pixel die, try to connect
                            int index = i; // Capture the current value of i
                            pixel.Connect((_, res, error) => results[index] = res ? "" : error);
                        }
                        else
                        {
                            results[i] = ConnectTimeoutErrorMessage;
                        }
                    }

                    // Wait for all Pixels to connect
                    yield return new WaitUntil(() => results.All(msg => msg != null) || UpdateIsCancelledOrTimeout());

                    if (isCancelled)
                    {
                        // Disconnect any Pixel die that just successfully connected or that is still connecting
                        for (int i = 0; i < editPixelsList.Length; ++i)
                        {
                            if (string.IsNullOrEmpty(results[i]))
                            {
                                var pixel = GetBlePixel(editPixelsList[i]);
                                pixel?.Disconnect();
                            }
                            pixelReadyCallback?.Invoke(editPixelsList[i], false, "Connection to Pixel canceled by application");
                        }
                    }
                    else if (pixelReadyCallback != null)
                    {
                        // Report connection result(s)
                        for (int i = 0; i < editPixelsList.Length; ++i)
                        {
                            bool connected = results[i] == "";
                            pixelReadyCallback.Invoke(editPixelsList[i], connected, connected ? null : results[i]);
                        }
                    }
                }
            }
        }

        public Coroutine DisconnectPixel(IEditPixel editPixel, bool forceDisconnect = false)
        {
            return StartCoroutine(DisconnectPixelCr());

            IEnumerator DisconnectPixelCr()
            {
                if (!_editPixels.ContainsKey(editPixel))
                {
                    Debug.LogError($"Trying to disconnect unknown Edit Pixel {editPixel.name}");
                }
                else
                {
                    var pixel = GetBlePixel(editPixel);
                    if (pixel != null)
                    {
                        if (!_pool.Contains(pixel))
                        {
                            Debug.LogError($"Trying attempting to disconnect unknown Pixel {editPixel.name}");
                        }
                        else
                        {
                            bool? res = null;
                            pixel.Disconnect((d, r, s) => res = r, forceDisconnect);

                            yield return new WaitUntil(() => res.HasValue);
                        }
                    }
                }
            }
        }

        public void ForgetDie(IEditPixel editPixel)
        {
            if (!_editPixels.ContainsKey(editPixel))
            {
                Debug.LogError($"Trying to forget unknown Edit Pixel {editPixel.name}");
            }
            else
            {
                RemovingEditPixel?.Invoke(editPixel);

                var pixel = GetBlePixel(editPixel);
                if (pixel != null)
                {
                    Debug.Assert(_pool.Contains(pixel));

                    _editPixelsToDestroy.Add(editPixel, pixel);
                    if (pixel.isConnectingOrReady)
                    {
                        pixel.Disconnect((d, r, s) => DestroyPixel(pixel), forceDisconnect: true);
                    }
                }

                _editPixelsList.RemovePixel(editPixel);
                _editPixels.Remove(editPixel);
            }
        }

        BlePixel GetBlePixel(IEditPixel editPixel)
        {
            if (!_editPixels.TryGetValue(editPixel, out BlePixel pixel))
            {
                _editPixelsToDestroy.TryGetValue(editPixel, out pixel);
            }
            return pixel;
        }

        void SetDieForEditDie(IEditPixel editPixel, BlePixel pixel)
        {
            if (pixel != GetBlePixel(editPixel))
            {
                Debug.Assert((pixel == null) || _pool.Contains(pixel));
                if (pixel == null)
                {
                    PixelDisconnected?.Invoke(editPixel);
                }
                if (_editPixels.ContainsKey(editPixel))
                {
                    _editPixels[editPixel] = pixel;
                }
                else
                {
                    Debug.Assert(pixel == null);
                }
                if (pixel != null)
                {
                    PixelConnected?.Invoke(editPixel);
                }
            }
        }

        /// <summary>
        /// Cleanly destroys a Pixel die, disconnecting if necessary and triggering events in the process
        /// Does not remove it from the list though
        /// </sumary>
        void DestroyPixel(BlePixel pixel)
        {
            SetDieForEditDie(_editPixels.FirstOrDefault(kv => kv.Value == pixel).Key, null);
            GameObject.Destroy(pixel.gameObject);
            _pool.Remove(pixel);
        }

        #endregion

        #region Unity messages

        void OnEnable()
        {
            // Safeguard
            if ((Instance != null) && (Instance != this))
            {
                Debug.LogError($"A second instance of {typeof(DicePool)} got spawned, now destroying it");
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

            // Load our pool from JSON!
            var editPixelList = _editPixelsList.GetEditPixels();
            if (editPixelList != null)
            {
                foreach (var editPixel in editPixelList)
                {
                    // Create a disconnected Pixel die
                    _editPixels.Add(editPixel, null);
                    EditPixelAdded?.Invoke(editPixel);
                }
            }
        }

        void Update()
        {
            List<BlePixel> destroyNow = null;
            foreach (var kv in _editPixelsToDestroy)
            {
                if (!kv.Value.isConnectingOrReady)
                {
                    if (destroyNow == null)
                    {
                        destroyNow = new List<BlePixel>();
                    }
                    destroyNow.Add(kv.Value);
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
