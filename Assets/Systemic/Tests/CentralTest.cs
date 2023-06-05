using NUnit.Framework;
using System;
using System.Collections;
using System.Linq;
using Systemic.Unity.Pixels;
using Systemic.Unity.Pixels.Messages;
using UnityEngine;
using UnityEngine.TestTools;

namespace Systemic.Unity.BluetoothLE.Test
{
    public class CentralTest
    {
        ScannedPeripheral _peripheral;
        bool _peripheralIsConnected;

        IEnumerator WaitUntilWithTimeout(Func<bool> condition, float timeoutSec = 5)
        {
            float timeout = Time.realtimeSinceStartup + timeoutSec;
            while ((!condition()) && (Time.realtimeSinceStartup < timeout))
            {
                yield return null;
            }
        }

        IEnumerator WaitCentralReady()
        {
            yield return WaitUntilWithTimeout(() => Central.IsReady);
            Assert.IsTrue(Central.IsReady, "Central not ready");
        }

        IEnumerator SelectPeripheral()
        {
            Central.ScanForPeripheralsWithServices(new[] { PixelBleUuids.Service });
            yield return WaitUntilWithTimeout(() =>
            {
                _peripheral = Central.ScannedPeripherals.FirstOrDefault(p => p.Services.Contains(PixelBleUuids.Service));
                return _peripheral != null;
            });
            Central.StopScan();
            Assert.NotNull(_peripheral, "No Pixel peripheral found");
        }

        IEnumerator ConnectAsync()
        {
            Assert.NotNull(_peripheral, "No peripheral selected");
            var p = _peripheral;
            var request = Central.ConnectPeripheralAsync(
                p, (_, connected) =>
                {
                    Assert.AreEqual(p, _);
                    _peripheralIsConnected = connected;
                });
            yield return request;
            Assert.IsTrue(request.IsSuccess, "Connect failed");

            Assert.IsTrue(Central.GetPeripheralMtu(_peripheral) > NativeInterface.MinMtu, "MTU not changed");
            Assert.IsFalse(string.IsNullOrEmpty(Central.GetPeripheralName(_peripheral)), "Null or empty name");
            Assert.AreEqual(Central.GetDiscoveredServices(_peripheral)?.Length, 4, "Unexpected number of services");
        }

        IEnumerator DisconnectAsync()
        {
            Assert.NotNull(_peripheral, "No peripheral selected");
            var request = Central.DisconnectPeripheralAsync(_peripheral);
            yield return request;
            Assert.IsTrue(request.IsSuccess, "Disconnect failed");
        }

        IEnumerator ReadRssiAsync()
        {
            Assert.NotNull(_peripheral, "No peripheral selected");
            var request = Central.ReadPeripheralRssi(_peripheral);
            yield return request;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            Assert.IsFalse(request.IsSuccess, "RSSI should fail on Windows");
#else
            Assert.IsTrue(request.IsSuccess, "RSSI failed");
#endif
        }

        IEnumerator SubscribeAsync()
        {
            Assert.NotNull(_peripheral, "No peripheral selected");
            var request = Central.SubscribeCharacteristicAsync(_peripheral, PixelBleUuids.Service, PixelBleUuids.NotifyCharacteristic, OnReceivedData);
            yield return request;
            Assert.IsTrue(request.IsSuccess, "Subscribe failed");
        }

        IEnumerator UnsubscribeAsync()
        {
            Assert.NotNull(_peripheral, "No peripheral selected");
            var request = Central.UnsubscribeCharacteristicAsync(_peripheral, PixelBleUuids.Service, PixelBleUuids.NotifyCharacteristic);
            yield return request;
            Assert.IsTrue(request.IsSuccess, "Unsubscribe failed");
        }

        IEnumerator ReadValueAsync()
        {
            Assert.NotNull(_peripheral, "No peripheral selected");
            var request = Central.ReadCharacteristicAsync(_peripheral, PixelBleUuids.Service, PixelBleUuids.NotifyCharacteristic);
            yield return request;
            Assert.IsTrue(request.IsSuccess, "Read failed");
        }

        IEnumerator SendMessageAsync(MessageType messageType, bool withoutResponse = false)
        {
            Assert.NotNull(_peripheral, "No peripheral selected");
            var request = Central.WriteCharacteristicAsync(_peripheral, PixelBleUuids.Service, PixelBleUuids.WriteCharacteristic, new byte[] { (byte)messageType }, withoutResponse: withoutResponse);
            yield return request;
            Assert.IsTrue(request.IsSuccess, "Write failed");
        }

        void OnReceivedData(byte[] data)
        {
            Assert.IsNotNull(data, "Received null data");
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            Central.Initialize();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            Central.Shutdown();
        }

        [UnityTest]
        public IEnumerator Simple()
        {
            yield return WaitCentralReady();
            yield return SelectPeripheral();
            yield return ConnectAsync();
            yield return DisconnectAsync();
        }
    }
}
