using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Linq;
using Systemic.Pixels.Unity.BluetoothLE;
using UnityEngine;
using UnityEngine.UI;

public enum PixelMessageType : byte
{
    None = 0,
    WhoAreYou,
    IAmADie,
    RollState,
    Telemetry,
    BulkSetup,
    BulkSetupAck,
    BulkData,
    BulkDataAck,
    TransferAnimSet,
    TransferAnimSetAck,
    TransferAnimSetFinished,
    TransferSettings,
    TransferSettingsAck,
    TransferSettingsFinished,
    TransferTestAnimSet,
    TransferTestAnimSetAck,
    TransferTestAnimSetFinished,
    DebugLog,
    PlayAnim,
    PlayAnimEvent,
    StopAnim,
    PlaySound,
    RequestRollState,
    RequestAnimSet,
    RequestSettings,
    RequestTelemetry,
    ProgramDefaultAnimSet,
    ProgramDefaultAnimSetFinished,
    Flash,
    FlashFinished,
    RequestDefaultAnimSetColor,
    DefaultAnimSetColor,
    RequestBatteryLevel,
    BatteryLevel,
    RequestRssi,
    Rssi,
    Calibrate,
    CalibrateFace,
    NotifyUser,
    NotifyUserAck,
    TestHardware,
    SetStandardState,
    SetLEDAnimState,
    SetBattleState,
    ProgramDefaultParameters,
    ProgramDefaultParametersFinished,
    SetDesignAndColor,
    SetDesignAndColorAck,
    SetCurrentBehavior,
    SetCurrentBehaviorAck,
    SetName,
    SetNameAck,

    // Testing
    TestBulkSend,
    TestBulkReceive,
    SetAllLEDsToColor,
    AttractMode,
    PrintNormals,
    PrintA2DReadings,
    LightUpFace,
    SetLEDToColor,
    DebugAnimController,
}

public enum DieRollState : byte
{
    Unknown = 0,
    OnFace,
    Handling,
    Rolling,
    Crooked
}; 

public class BleController : MonoBehaviour
{
    public static readonly Guid ServiceUuid = new Guid("6e400001-b5a3-f393-e0a9-e50e24dcca9e");
    public static readonly Guid NotifyCharacteristicUuid = new Guid("6e400001-b5a3-f393-e0a9-e50e24dcca9e");
    public static readonly Guid WriteCharacteristicUuid = new Guid("6e400002-b5a3-f393-e0a9-e50e24dcca9e");

    [SerializeField]
    Text _statusText = null;

    [SerializeField]
    Text _valueText = null;

    ScannedPeripheral _peripheral;

    int _notifCounter;
    ConcurrentQueue<string> _queue = new ConcurrentQueue<string>();

    public static ConcurrentQueue<Action> ActionQueue = new ConcurrentQueue<Action>();

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
        _statusText.text = $"{Central.IsScanning} - {Central.ScannedPeripherals.Length} / {Central.ConnectedPeripherals.Length}";

        while (_queue.TryDequeue(out string txt))
        {
            //Debug.Log(txt);
            _valueText.text = txt;
        }

        while (ActionQueue.TryDequeue(out Action act))
        {
            act();
        }
    }

    public void StartScan()
    {
        Central.ScanForPeripheralsWithServices(new[] { ServiceUuid });
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
        _queue.Enqueue("MTU: " + Central.GetPeripheralMtu(_peripheral));

        Debug.Log("Name: " + Central.GetPeripheralName(_peripheral));
        Debug.Log("Services: " + string.Join(", ", Central.GetPeripheralDiscoveredServices(_peripheral).Select(g => g.ToString())));
        Debug.Log("Characteristics: " + string.Join(", ", Central.GetPeripheralServiceCharacteristics(_peripheral, ServiceUuid).Select(g => g.ToString())));
        Debug.Log("Notify characteristic properties: " + Central.GetCharacteristicProperties(_peripheral, ServiceUuid, NotifyCharacteristicUuid));
        Debug.Log("Write characteristic properties: " + Central.GetCharacteristicProperties(_peripheral, ServiceUuid, WriteCharacteristicUuid));

        StartCoroutine(ReadRssiAsync());
    }

    public void Subscribe()
    {
        StartCoroutine(SubscribeAsync());
    }

    public void SendWhoAreYou()
    {
        StartCoroutine(SendMessageAsync(PixelMessageType.WhoAreYou));
    }

    public void SendWhoAreYouWithoutResponse()
    {
        StartCoroutine(SendMessageAsync(PixelMessageType.WhoAreYou, withoutResponse: true));
    }

    public void SendWhoAreYouX20()
    {
        for (int i = 0; i < 20; ++i)
        {
            StartCoroutine(SendMessageAsync(PixelMessageType.WhoAreYou));
        }
    }

    public void Read()
    {
        StartCoroutine(Central.ReadCharacteristicAsync(_peripheral, ServiceUuid, NotifyCharacteristicUuid, OnReceivedData));
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
        var p = Central.ScannedPeripherals.First(p => p.Services.Contains(ServiceUuid));
        var request = Central.ConnectPeripheralAsync(
            p, connected => Debug.Log(connected ? "Connected!" : "Disconnected!"));
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
        yield return Central.SubscribeCharacteristicAsync(_peripheral, ServiceUuid, NotifyCharacteristicUuid, OnReceivedData);
        Debug.Log("Subscribed to messages");
    }

    IEnumerator SendMessageAsync(PixelMessageType messageType, bool withoutResponse = false)
    {
        yield return Central.WriteCharacteristicAsync(_peripheral, ServiceUuid, WriteCharacteristicUuid, new byte[] { (byte)messageType }, withoutResponse: withoutResponse);
        Debug.Log("Message send: " + messageType);
    }

    void OnReceivedData(byte[] data)
    {
        ++_notifCounter;
        string str = $"{_notifCounter} => {string.Join(" ", data.Select(b => b.ToString("X2")))}";
        Debug.Log("Read data " + str);

        switch ((PixelMessageType)data[0])
        {
            case PixelMessageType.IAmADie:
                str = $"Received die welcome message => face count:{data[1]}";
                break;
            case PixelMessageType.RollState:
                str = $"Received roll state => {(DieRollState)data[1]}, face:{1 + data[2]}";
                break;
            case PixelMessageType.Rssi:
                {
                    uint rssi = (((uint)data[1]) << 8) + data[2];
                    str = $"Received RSSI => {rssi}";
                }
                break;
        }
        _queue.Enqueue(str);
    }

    IEnumerator ConnectAndSendMessagesAsync()
    {
        yield return new WaitForSecondsRealtime(0.1f);

        //
        // Scan
        //

        Debug.Log("Scanning for Pixels...");

        Central.ScanForPeripheralsWithServices(new[] { ServiceUuid });

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

        var p = Central.ScannedPeripherals.First(p => p.Services.Contains(ServiceUuid));
        var request = Central.ConnectPeripheralAsync(
            p, connected => Debug.Log(connected ? "Connected!" : "Failed to connect!"));
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
        Debug.Log(" * Notify characteristic properties: " + Central.GetCharacteristicProperties(_peripheral, ServiceUuid, NotifyCharacteristicUuid));
        Debug.Log(" * Write characteristic properties: " + Central.GetCharacteristicProperties(_peripheral, ServiceUuid, WriteCharacteristicUuid));

        //
        // Send messages
        //

        yield return SubscribeAsync();

        yield return new WaitForSecondsRealtime(2);
        yield return SendMessageAsync(PixelMessageType.WhoAreYou);

        yield return new WaitForSecondsRealtime(2);
        yield return SendMessageAsync(PixelMessageType.RequestRollState);

        yield return new WaitForSecondsRealtime(2);
        yield return SendMessageAsync(PixelMessageType.RequestRssi);
    }
}
