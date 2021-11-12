using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Linq;
using Systemic.Unity.BluetoothLE;
using Systemic.Unity.Pixel;
using UnityEngine;
using UnityEngine.UI;

public class BleController : MonoBehaviour
{
    [SerializeField]
    Text _statusText = null;

    [SerializeField]
    Text _valueText = null;

    [SerializeField]
    Button _startScanBtn = null;

    [SerializeField]
    Button _stopScanBtn = null;

    [SerializeField]
    Button _connectBtn = null;

    [SerializeField]
    Button _shutdownBtn = null;

    [SerializeField]
    Button[] _operationsBtn = null;

    // The peripheral to use
    ScannedPeripheral _peripheral;
    bool _peripheralIsConnected;
    int _notifCounter;

    ConcurrentQueue<string> _displayQueue = new ConcurrentQueue<string>();
    ConcurrentQueue<Action> _actionQueue = new ConcurrentQueue<Action>();

    void OnEnable()
    {
        _statusText.text = _valueText.text = string.Empty;

        Central.Initialize();
    }

    void OnDisable()
    {
        Central.Shutdown();
    }

    // Update is called once per frame
    void Update()
    {
        // Update buttons
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

        // Update value text
        while (_displayQueue.TryDequeue(out string txt))
        {
            //Debug.Log(txt);
            _valueText.text = txt;
        }

        // Run queue
        while (_actionQueue.TryDequeue(out Action act))
        {
            act();
        }
    }

    public void StartScan()
    {
        Central.ScanForPeripheralsWithServices(new[] { BleUuids.ServiceUuid });
    }

    public void StopScan()
    {
        Central.StopScan();
    }

    public void Connect()
    {
        StartCoroutine(ConnectAsync());
    }

    public void Disconnect()
    {
        StartCoroutine(Central.DisconnectPeripheralAsync(_peripheral));
    }

    public void GetInfo()
    {
        _displayQueue.Enqueue("MTU: " + Central.GetPeripheralMtu(_peripheral));

        Debug.Log("Name: " + Central.GetPeripheralName(_peripheral));
        Debug.Log("Services: " + string.Join(", ", Central.GetPeripheralDiscoveredServices(_peripheral).Select(g => g.ToString())));
        Debug.Log("Characteristics: " + string.Join(", ", Central.GetPeripheralServiceCharacteristics(_peripheral, BleUuids.ServiceUuid).Select(g => g.ToString())));
        Debug.Log("Notify characteristic properties: " + Central.GetCharacteristicProperties(_peripheral, BleUuids.ServiceUuid, BleUuids.NotifyCharacteristicUuid));
        Debug.Log("Write characteristic properties: " + Central.GetCharacteristicProperties(_peripheral, BleUuids.ServiceUuid, BleUuids.WriteCharacteristicUuid));

        StartCoroutine(ReadRssiAsync());
    }

    public void Subscribe()
    {
        StartCoroutine(SubscribeAsync());
    }

    public void Unsubscribe()
    {
        StartCoroutine(UnsubscribeAsync());
    }

    public void Read()
    {
        StartCoroutine(ReadValueAsync());
    }

    public void SendWhoAreYou()
    {
        StartCoroutine(SendMessageAsync(MessageType.WhoAreYou));
    }

    public void SendWhoAreYouWithoutResponse()
    {
        StartCoroutine(SendMessageAsync(MessageType.WhoAreYou, withoutResponse: true));
    }

    public void SendWhoAreYouX20()
    {
        for (int i = 0; i < 20; ++i)
        {
            StartCoroutine(SendMessageAsync(MessageType.WhoAreYou));
        }
    }

    public void Shutdown()
    {
        Central.Shutdown();
    }

    public void ConnectAndSendMessages()
    {
        StartCoroutine(ConnectAndSendMessagesAsync());
    }

    IEnumerator ConnectAsync()
    {
        var p = Central.ScannedPeripherals.First(p => p.Services.Contains(BleUuids.ServiceUuid));
        var request = Central.ConnectPeripheralAsync(
            p, (_, connected) =>
            {
                _peripheralIsConnected = connected;
                Debug.Log(connected ? "Connected!" : "Disconnected!");
            });
        yield return request;

        if (request.IsSuccess)
        {
            _peripheral = p;
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
        yield return Central.SubscribeCharacteristicAsync(_peripheral, BleUuids.ServiceUuid, BleUuids.NotifyCharacteristicUuid, OnReceivedData);
        Debug.Log("Subscribed to characteristic");
    }

    IEnumerator UnsubscribeAsync()
    {
        yield return Central.UnsubscribeCharacteristicAsync(_peripheral, BleUuids.ServiceUuid, BleUuids.NotifyCharacteristicUuid);
        Debug.Log("Unsubscribed from characteristic");
    }

    IEnumerator ReadValueAsync()
    {
        var request = Central.ReadCharacteristicAsync(_peripheral, BleUuids.ServiceUuid, BleUuids.NotifyCharacteristicUuid);
        yield return request;
        if (request.IsSuccess)
        {
            Debug.Log("Characteristic value size = " + request.Value.Length);
            DisplayMessageInfo(request.Value);
        }
        else
        {
            Debug.LogError("Failed to read characteristic");
        }
    }

    IEnumerator SendMessageAsync(MessageType messageType, bool withoutResponse = false)
    {
        Debug.Log("Sending message: " + messageType);
        yield return Central.WriteCharacteristicAsync(_peripheral, BleUuids.ServiceUuid, BleUuids.WriteCharacteristicUuid, new byte[] { (byte)messageType }, withoutResponse: withoutResponse);
    }

    void OnReceivedData(byte[] data)
    {
        ++_notifCounter;
        Debug.Log($"Characteristic value change notification #{_notifCounter} =>" +
            $" {string.Join(" ", data.Select(b => b.ToString("X2")))}");
        DisplayMessageInfo(data);
    }

    void DisplayMessageInfo(byte[] msg)
    {
        if (msg?.Length > 0)
        {
            string str;
            switch ((MessageType)msg[0])
            {
                case MessageType.IAmADie:
                    str = $"Welcome message => face count:{msg[1]}";
                    break;
                case MessageType.RollState:
                    str = $"Roll state => {(RollState)msg[1]}, face:{1 + msg[2]}";
                    break;
                case MessageType.Rssi:
                    str = $"RSSI => {(((uint)msg[1]) << 8) + msg[2]}";
                    break;
                default:
                    str = $"Message => {string.Join(" ", msg.Select(b => b.ToString("X2")))}";
                    break;
            }

            _displayQueue.Enqueue(str);
        }
    }

    IEnumerator ConnectAndSendMessagesAsync()
    {
        yield return new WaitForSecondsRealtime(0.1f);

        //
        // Scan
        //

        Debug.Log("Scanning for Pixels...");

        Central.ScanForPeripheralsWithServices(new[] { BleUuids.ServiceUuid });

        //yield return new WaitForSecondsRealtime(5);

        while (Central.ScannedPeripherals.Length == 0)
        {
            yield return null;
        }

        Central.StopScan();

        Debug.Log($"Found {Central.ScannedPeripherals.Length}!");

        //
        // Connect
        //

        Debug.Log($"Connecting to Pixel...");

        var p = Central.ScannedPeripherals.First(p => p.Services.Contains(BleUuids.ServiceUuid));
        var request = Central.ConnectPeripheralAsync(
            p, (_, connected) => Debug.Log(connected ? "Connected!" : "Failed to connect!"));
        yield return request;

        if (!request.IsSuccess)
        {
            Debug.LogError("Failed to connect, aborting sequence");
            yield break;
        }

        _peripheral = p;

        //
        // Display dice info
        //

        Debug.Log("Pixel info:");
        Debug.Log(" * Name: " + p.Name);
        Debug.Log(" * MTU: " + Central.GetPeripheralMtu(_peripheral));
        Debug.Log(" * Notify characteristic properties: " + Central.GetCharacteristicProperties(_peripheral, BleUuids.ServiceUuid, BleUuids.NotifyCharacteristicUuid));
        Debug.Log(" * Write characteristic properties: " + Central.GetCharacteristicProperties(_peripheral, BleUuids.ServiceUuid, BleUuids.WriteCharacteristicUuid));

        //
        // Send messages
        //

        yield return SubscribeAsync();

        yield return new WaitForSecondsRealtime(2);
        yield return SendMessageAsync(MessageType.WhoAreYou);

        yield return new WaitForSecondsRealtime(2);
        yield return SendMessageAsync(MessageType.RequestRollState);

        yield return new WaitForSecondsRealtime(2);
        yield return SendMessageAsync(MessageType.RequestRssi);
    }
}
