using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Linq;
using Systemic.Unity.BluetoothLE;
using Systemic.Unity.Pixels;
using Systemic.Unity.Pixels.Messages;
using UnityEngine;
using UnityEngine.UI;

namespace Systemic.Unity.Examples
{
    /// <summary>
    /// Demonstrates various Bluetooth operations on Pixels dice.
    /// One may scan for Pixels, connect to one, retrieve information from and send messages to it.
    /// 
    /// 
    /// The buttons are grayed out when the functionality it will use is not available
    /// (for example, the "write" button is not available when the application is not
    /// connected to a Pixel die).
    /// </summary>
    public class BleController : MonoBehaviour
    {
        [Tooltip("Status, displayed on top")]
        [SerializeField]
        Text _statusText = null;

        [Tooltip("Last read value, displayed on bottom")]
        [SerializeField]
        Text _lastValueText = null;

        [Tooltip("Start scan button")]
        [SerializeField]
        Button _startScanBtn = null;

        [Tooltip("Stop scan button")]
        [SerializeField]
        Button _stopScanBtn = null;

        [Tooltip("Connect button")]
        [SerializeField]
        Button _connectBtn = null;

        [Tooltip("Bluetooth shutdown button")]
        [SerializeField]
        Button _shutdownBtn = null;

        [Tooltip("Buttons for running various Bluetooth operations")]
        [SerializeField]
        Button[] _operationsBtn = null;

        // The peripheral being used and its connection status
        ScannedPeripheral _peripheral;
        bool _peripheralIsConnected;

        // Counts notifications (for debug purposes)
        int _notificationCounter;

        // Action queue, run on each frame update
        ConcurrentQueue<Action> _actionQueue = new ConcurrentQueue<Action>();

        #region Unity messages

        void OnEnable()
        {
            // Clear out text output
            _statusText.text = _lastValueText.text = string.Empty;

            // Initialize the library
            Central.Initialize();
        }

        void OnDisable()
        {
            // Shutdown the library
            Central.Shutdown();
        }

        // Update is called once per frame
        void Update()
        {
            // Update buttons intractability based on Bluetooth state
            _startScanBtn.interactable = Central.IsReady && !Central.IsScanning;
            _stopScanBtn.interactable = Central.IsReady && Central.IsScanning;
            _connectBtn.interactable = Central.IsReady && (Central.ScannedPeripherals.Length > 0) && (!_peripheralIsConnected);
            _shutdownBtn.interactable = Central.IsReady;
            foreach (var btn in _operationsBtn)
            {
                btn.interactable = Central.IsReady && (_peripheral != null) && _peripheralIsConnected;
            }

            // Update status text
            _statusText.text = Central.IsReady ? (Central.IsScanning ? "Scanning" : "Ready") : "Unavailable";
            if (Central.ScannedPeripherals.Length > 0)
            {
                _statusText.text += $"- Found {Central.ScannedPeripherals.Length} Pixel(s)";
            }

            // Run action queue
            while (_actionQueue.TryDequeue(out Action act))
            {
                act();
            }
        }

        #endregion

        #region Public methods

        /// <summary>
        /// Starts a new scan.
        /// </summary>
        public void StartScan()
        {
            // Filter peripherals with the Pixels service UUID
            Central.ScanForPeripheralsWithServices(new[] { BleUuids.ServiceUuid });
        }

        /// <summary>
        /// Stops an on-going scan, if any.
        /// </summary>
        public void StopScan()
        {
            Central.StopScan();
        }

        /// <summary>
        /// Connect to first scanned peripheral.
        /// </summary>
        public void Connect()
        {
            StartCoroutine(ConnectAsync());
        }

        /// <summary>
        /// Disconnect from peripheral.
        /// </summary>
        public void Disconnect()
        {
            StartCoroutine(Central.DisconnectPeripheralAsync(_peripheral));
        }

        /// <summary>
        /// Retrieve and display info for connected peripheral.
        /// </summary>
        public void GetInfo()
        {
            _lastValueText.text = "MTU: " + Central.GetPeripheralMtu(_peripheral);

            Debug.Log("Name: " + Central.GetPeripheralName(_peripheral));
            Debug.Log("Services: " + string.Join(", ", Central.GetPeripheralDiscoveredServices(_peripheral).Select(g => g.ToString())));
            Debug.Log("Characteristics: " + string.Join(", ", Central.GetPeripheralServiceCharacteristics(_peripheral, BleUuids.ServiceUuid).Select(g => g.ToString())));
            Debug.Log("Notify characteristic properties: " + Central.GetCharacteristicProperties(_peripheral, BleUuids.ServiceUuid, BleUuids.NotifyCharacteristicUuid));
            Debug.Log("Write characteristic properties: " + Central.GetCharacteristicProperties(_peripheral, BleUuids.ServiceUuid, BleUuids.WriteCharacteristicUuid));

            StartCoroutine(ReadRssiAsync());
        }

        /// <summary>
        /// Subscribes to Pixel's notification characteristic.
        /// </summary>
        public void Subscribe()
        {
            StartCoroutine(SubscribeAsync());
        }

        /// <summary>
        /// Unsubscribes from characteristic.
        /// </summary>
        public void Unsubscribe()
        {
            StartCoroutine(UnsubscribeAsync());
        }

        /// <summary>
        /// Reads Pixel's notification characteristic.
        /// </summary>
        public void Read()
        {
            StartCoroutine(ReadValueAsync());
        }

        /// <summary>
        /// Sends message to identify Pixel.
        /// </summary>
        public void SendWhoAreYou()
        {
            StartCoroutine(SendMessageAsync(MessageType.WhoAreYou));
        }

        /// <summary>
        /// Sends message to identify Pixel, but doesn't wait for an acknowledgment that the message was received.
        /// This is for testing. In a real application it's usually best to always request a response.
        /// </summary>
        public void SendWhoAreYouWithoutResponse()
        {
            StartCoroutine(SendMessageAsync(MessageType.WhoAreYou, withoutResponse: true));
        }

        /// <summary>
        /// Sends 20 messages to the peripheral.
        /// This is for testing.
        /// </summary>
        public void SendWhoAreYouX20()
        {
            for (int i = 0; i < 20; ++i)
            {
                StartCoroutine(SendMessageAsync(MessageType.WhoAreYou));
            }
        }

        /// <summary>
        /// Shutdowns the library.
        /// </summary>
        public void Shutdown()
        {
            Central.Shutdown();
        }

        #endregion

        #region Private methods

        IEnumerator ConnectAsync()
        {
            // Get the first peripheral
            // (will throw an exception if there is none)
            var myPeripheral = Central.ScannedPeripherals[0];
            var request = Central.ConnectPeripheralAsync(
                myPeripheral, (_, connected) =>
                {
                    Debug.Assert(_ == myPeripheral);
                    // Store and log connection state
                    _peripheralIsConnected = connected;
                    Debug.Log(connected ? "Connected!" : "Disconnected!");
                });
            yield return request;

            // Check connection result
            if (request.IsSuccess)
            {
                // Keep peripheral and get information about it
                _peripheral = myPeripheral;
                Debug.Log("Got peripheral");
                GetInfo();
            }
            else
            {
                Debug.LogError("Failed to connect");
            }
        }

        IEnumerator ReadRssiAsync()
        {
            var request = Central.ReadPeripheralRssi(_peripheral);
            yield return request;
            if (request.IsSuccess)
            {
                Debug.Log("RSSI = " + request.Value);
            }
            else
            {
                Debug.LogError("Failed to read RSSI");
            }
        }

        IEnumerator SubscribeAsync()
        {
            yield return Central.SubscribeCharacteristicAsync(
                _peripheral,
                BleUuids.ServiceUuid,
                BleUuids.NotifyCharacteristicUuid,
                OnReceivedData);
            Debug.Log("Subscribed to characteristic");
        }

        IEnumerator UnsubscribeAsync()
        {
            yield return Central.UnsubscribeCharacteristicAsync(
                _peripheral,
                BleUuids.ServiceUuid,
                BleUuids.NotifyCharacteristicUuid);
            Debug.Log("Unsubscribed from characteristic");
        }

        IEnumerator ReadValueAsync()
        {
            var request = Central.ReadCharacteristicAsync(
                _peripheral,
                BleUuids.ServiceUuid,
                BleUuids.NotifyCharacteristicUuid);
            yield return request;
            if (request.IsSuccess)
            {
                Debug.Log("Characteristic value size = " + request.Value.Length);
                DisplayMessage(request.Value);
            }
            else
            {
                Debug.LogError("Failed to read characteristic");
            }
        }

        IEnumerator SendMessageAsync(MessageType messageType, bool withoutResponse = false)
        {
            Debug.Log("Sending message: " + messageType);
            yield return Central.WriteCharacteristicAsync(
                _peripheral,
                BleUuids.ServiceUuid,
                BleUuids.WriteCharacteristicUuid,
                new byte[] { (byte)messageType },
                withoutResponse: withoutResponse);
        }

        void OnReceivedData(byte[] data)
        {
            // We count notifications so we can track how many messages we got
            ++_notificationCounter;

            // Log and display message
            Debug.Log($"Characteristic value change notification #{_notificationCounter} =>" +
                $" {string.Join(" ", data.Select(b => b.ToString("X2")))}");
            DisplayMessage(data);
        }

        void DisplayMessage(byte[] msg)
        {
            if (msg?.Length > 0)
            {
                // Display message contents
                _lastValueText.text = (MessageType)msg[0] switch
                {
                    MessageType.IAmADie => $"Welcome message => face count:{msg[1]}",
                    MessageType.RollState => $"Roll state => {(PixelRollState)msg[1]}, face:{1 + msg[2]}",
                    MessageType.Rssi => $"RSSI => {(((uint)msg[1]) << 8) + msg[2]}",
                    _ => $"Message => {string.Join(" ", msg.Select(b => b.ToString("X2")))}",
                };
            }
        }

        #endregion
    }
}
