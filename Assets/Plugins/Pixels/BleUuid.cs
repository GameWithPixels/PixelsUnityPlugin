using System;

namespace Systemic.Unity.BluetoothLE
{
    /// <summary>
    /// Helper class for Bluetooth UUIDs
    /// </summary>
    public static class BleUuid
    {
        /// <summary>
        /// Convert a 16 bits Bluetooth LE UUID to a full 128 bit UUID
        /// </summary>
        /// <param name="shortUuid">A short BLE UUID (16 bits)</param>
        /// <returns>128 bit UUID as a <see cref="Guid"/></returns>
        public static Guid ToFullUuid(short shortUuid) => new Guid($"0000{shortUuid:x4}-0000-1000-8000-00805f9b34fb");
    }
}
