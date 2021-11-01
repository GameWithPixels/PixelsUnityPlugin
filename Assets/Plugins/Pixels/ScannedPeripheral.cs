using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Systemic.Pixels.Unity.BluetoothLE
{
    internal interface INativeDevice
    {
        bool IsValid { get; }
    }

    internal interface IScannedPeripheral
    {
        INativeDevice NativeDevice { get; }

        bool IsValid { get; }

        string Name { get; }
    }

    // Readonly class
    public class ScannedPeripheral : IScannedPeripheral
    {
        INativeDevice _nativeDevice;

        internal ScannedPeripheral(INativeDevice nativeDevice, Internal.AdvertisementDataJson advertisementData)
        {
            if (nativeDevice == null) throw new ArgumentNullException(nameof(nativeDevice));
            if (!nativeDevice.IsValid) throw new ArgumentException("Invalid native device", nameof(nativeDevice));
            if (advertisementData == null) throw new ArgumentNullException(nameof(advertisementData));

            _nativeDevice = nativeDevice;
            SystemId = advertisementData.systemId;
            BluetoothAddress = advertisementData.address;
            Name = advertisementData.name;
            Rssi = advertisementData.rssi;
            TxPowerLevel = advertisementData.txPowerLevel;
            IsConnectable = advertisementData.isConnectable;
            ManufacturerData = Array.AsReadOnly((advertisementData.manufacturerData ?? Array.Empty<byte>()).ToArray());
            ServicesData = new ReadOnlyDictionary<string, byte[]>(Clone(advertisementData.servicesData));
            Services = Array.AsReadOnly(ToGuidArray(advertisementData.services));
            OverflowServices = Array.AsReadOnly(ToGuidArray(advertisementData.overflowServiceUUIDs));
            SolicitedServices = Array.AsReadOnly(ToGuidArray(advertisementData.solicitedServiceUUIDs));
        }

        INativeDevice IScannedPeripheral.NativeDevice => _nativeDevice;

        bool IScannedPeripheral.IsValid => _nativeDevice?.IsValid ?? false;

        public string SystemId { get; }

        public ulong BluetoothAddress { get; }

        public string Name { get; }

        public int Rssi { get; }

        public int TxPowerLevel { get; }

        public bool IsConnectable { get; }

        public IReadOnlyList<byte> ManufacturerData { get; }

        public IReadOnlyDictionary<string, byte[]> ServicesData { get; }

        public IReadOnlyList<Guid> Services { get; }

        public IReadOnlyList<Guid> OverflowServices { get; }

        public IReadOnlyList<Guid> SolicitedServices { get; }

        private static Guid[] ToGuidArray(string[] uuids)
        {
            return uuids?.Select(s => s.ToBleGuid()).ToArray() ?? Array.Empty<Guid>();
        }

        private static IDictionary<string, byte[]> Clone(IDictionary<string, byte[]> servicesData)
        {
            var clone = new Dictionary<string, byte[]>();
            if (servicesData != null)
            {
                foreach (var kv in servicesData)
                {
                    clone.Add(kv.Key, kv.Value?.ToArray());
                }
            }
            return clone;
        }
    }
}
