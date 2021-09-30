using System.Collections.Generic;

namespace Systemic.Pixels.Unity.BluetoothLE.Internal
{
    internal sealed class AdvertisementDataJson
    {
        public string systemId = default;
        public ulong address = default;
        public string name = default;
        public int rssi = default;
        public int txPowerLevel = default;
        public bool isConnectable = default;
        public byte[] manufacturerData = default;
        public Dictionary<string, byte[]> servicesData = default;
        public string[] services = default;
        public string[] overflowServiceUUIDs = default;
        public string[] solicitedServiceUUIDs = default;
    }
}
