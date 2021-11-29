using System;
using System.Collections;
using System.Linq;
using Systemic.Unity.BluetoothLE;
using Systemic.Unity.Pixels;
using Systemic.Unity.Pixels.Messages;
using UnityEngine;

/// <summary>
/// Scripts used by example scenes.
/// </summary>
//! @ingroup Unity_CSharp
namespace Systemic.Unity.Examples
{
    /// <summary>
    /// Demonstrates scanning for Pixels dice, connecting to one of them, retrieving information from it,
    /// sending messages and getting notifications.
    /// </summary>
    public class BleConsole : MonoBehaviour
    {
        void OnEnable()
        {
            // Initialize the library
            Central.Initialize();
        }

        void OnDisable()
        {
            // Shutdown the library
            Central.Shutdown();
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

            // Filter peripherals with the Pixel service UUID
            Central.ScanForPeripheralsWithServices(new[] { BleUuids.ServiceUuid });

            // Wait until a Pixel is found
            while (Central.ScannedPeripherals.Length == 0)
            {
                yield return null;
            }

            Central.StopScan();

            Debug.Log($"Found {Central.ScannedPeripherals.Length} Pixels");

            //
            // Connect
            //

            // Get the first peripheral
            var peripheral = Central.ScannedPeripherals[0];

            Debug.Log($"Connecting to Pixel named {peripheral.Name}...");

            // Connect with a connection event callback that simply logs the connection state
            var request = Central.ConnectPeripheralAsync(peripheral, (_, connected)
                => Debug.Log(connected ? "Connected!" : "Not connected!"));
            yield return request;

            // Check connection result
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

            // Assert that we got the expected characteristics
            Debug.Assert(notifyCharacteristicUuid == BleUuids.NotifyCharacteristicUuid);
            Debug.Assert(writeCharacteristicUuid == BleUuids.WriteCharacteristicUuid);

            //
            // Send messages
            //

            // Callback that display the contents of a message
            void OnReceivedData(byte[] data)
            {
                // First byte is the type of message
                Debug.Log($"Characteristic value change notification =>"
                    + $"{(MessageType)data[0]} {string.Join(" ", data.Skip(1).Select(b => b.ToString("X2")))}");
            }

            // Subscribe to get Pixel events (such as roll state) and responses from queries
            yield return Central.SubscribeCharacteristicAsync(
                peripheral, BleUuids.ServiceUuid, notifyCharacteristicUuid, OnReceivedData);
            Debug.Log("Subscribed to characteristic");

            // Helper method to send a message to a Pixel
            IEnumerator SendMessageAsync(MessageType messageType)
            {
                Debug.Log("Sending message: " + messageType);
                yield return Central.WriteCharacteristicAsync(
                    peripheral, BleUuids.ServiceUuid, writeCharacteristicUuid, new byte[] { (byte)messageType });
            }

            // Identify Pixel
            yield return new WaitForSecondsRealtime(2);
            yield return SendMessageAsync(MessageType.WhoAreYou);

            // Get state
            yield return new WaitForSecondsRealtime(2);
            yield return SendMessageAsync(MessageType.RequestRollState);

            // Get RSSI
            yield return new WaitForSecondsRealtime(2);
            yield return SendMessageAsync(MessageType.RequestRssi);

            // And disconnect
            yield return new WaitForSecondsRealtime(2);
            Debug.Log("Disconnecting...");
            yield return Central.DisconnectPeripheralAsync(peripheral);
        }
    }
}
