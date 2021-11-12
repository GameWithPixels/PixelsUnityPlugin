using System;
using System.Collections;
using System.Linq;
using Systemic.Unity.BluetoothLE;
using Systemic.Unity.Pixel;
using UnityEngine;

public class BleConsole: MonoBehaviour
{
    void OnEnable()
    {
        Central.Initialize();
    }

    void OnDisable()
    {
        Central.Shutdown();
    }

    // Update is called once per frame
    void Update()
    {
    }

    IEnumerator Start()
    {
        yield return null;

        Debug.Log("Waiting for Central to be ready...");
        while (!Central.IsReady)
        {
            yield return null;
        }

        //
        // Scan
        //

        Debug.Log("Scanning for Pixels...");

        Central.ScanForPeripheralsWithServices(new[] { BleUuids.ServiceUuid });

        while (Central.ScannedPeripherals.Length == 0)
        {
            yield return null;
        }

        Central.StopScan();

        Debug.Log($"Found {Central.ScannedPeripherals.Length} Pixels");

        //
        // Connect
        //

        var peripheral = Central.ScannedPeripherals.First(p => p.Services.Contains(BleUuids.ServiceUuid));

        Debug.Log($"Connecting to Pixel named {peripheral.Name}...");

        var request = Central.ConnectPeripheralAsync(
            peripheral, (_, connected) => Debug.Log(connected ? "Connected!" : "Not connected!"));
        yield return request;

        if (!request.IsSuccess)
        {
            Debug.LogError("Failed to connect, aborting sequence");
            yield break;
        }

        //
        // Display dice info
        //

        Debug.Log("Pixel info:");
        Debug.Log(" * Name: " + peripheral.Name);
        Debug.Log(" * MTU: " + Central.GetPeripheralMtu(peripheral));

        // Enumerate characteristics (we could also directly retrieve them by their UUID)
        var characteristics = Central.GetPeripheralServiceCharacteristics(peripheral, BleUuids.ServiceUuid);
        Guid notifyCharacteristicUuid, writeCharacteristicUuid;
        for (int i = 0; i < characteristics.Length; ++i)
        {
            var uuid = characteristics[i];
            var props = Central.GetCharacteristicProperties(peripheral, BleUuids.ServiceUuid, uuid);
            Debug.Log($" * Characteristic #{i} properties: " + props);

            if ((props & CharacteristicProperties.Notify) != 0)
            {
                notifyCharacteristicUuid = uuid;
            }
            if ((props & CharacteristicProperties.Write) != 0)
            {
                writeCharacteristicUuid = uuid;
            }
        }

        Debug.Assert(notifyCharacteristicUuid == BleUuids.NotifyCharacteristicUuid);
        Debug.Assert(writeCharacteristicUuid == BleUuids.WriteCharacteristicUuid);

        //
        // Send messages
        //

        void OnReceivedData(byte[] data)
        {
            Debug.Log($"Characteristic value change notification =>"
                + $" {string.Join(" ", data.Select(b => b.ToString("X2")))}");
        }

        yield return Central.SubscribeCharacteristicAsync(
            peripheral, BleUuids.ServiceUuid, notifyCharacteristicUuid, OnReceivedData);
        Debug.Log("Subscribed to characteristic");

        IEnumerator SendMessageAsync(MessageType messageType)
        {
            Debug.Log("Sending message: " + messageType);
            yield return Central.WriteCharacteristicAsync(
                peripheral, BleUuids.ServiceUuid, writeCharacteristicUuid, new byte[] { (byte)messageType });
        }

        yield return new WaitForSecondsRealtime(2);
        yield return SendMessageAsync(MessageType.WhoAreYou);

        yield return new WaitForSecondsRealtime(2);
        yield return SendMessageAsync(MessageType.RequestRollState);

        yield return new WaitForSecondsRealtime(2);
        yield return SendMessageAsync(MessageType.RequestRssi);

        yield return new WaitForSecondsRealtime(2);
        Debug.Log("Disconnecting...");
        yield return Central.DisconnectPeripheralAsync(peripheral);
    }
}