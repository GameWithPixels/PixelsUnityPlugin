// Ignore Spelling: Uuids Ble

using System;

namespace Systemic.Unity.Pixels
{
    /// <summary>
    /// Pixel dice Bluetooth Low Energy UUIDs.
    /// </summary>
    public class PixelBleUuids
    {
        /// <summary>
        /// Pixel service UUID.
        /// May be used to filter out Pixel dice during a scan and to access its characteristics.
        /// </summary>
        public static readonly Guid Service = new Guid("a6b90001-7a5a-43f2-a962-350c8edc9b5b");

        /// <summary>
        /// Pixel characteristic UUID for notification and read operations.
        /// May be used to get notified on dice events or read the current state.
        /// </summary>
        public static readonly Guid NotifyCharacteristic = new Guid("a6b90002-7a5a-43f2-a962-350c8edc9b5b");

        /// <summary>
        /// Pixel characteristic UUID for write operations.
        /// May be used to send messages to a dice.
        /// </summary>
        public static readonly Guid WriteCharacteristic = new Guid("a6b90003-7a5a-43f2-a962-350c8edc9b5b");

        //
        // Legacy UUIDs
        //

        /// <summary>
        /// Former Pixel service UUID.
        /// </summary>
        public static readonly Guid LegacyService = new Guid("6e400001-b5a3-f393-e0a9-e50e24dcca9e");

        /// <summary>
        /// Former Pixel characteristic UUID for notification and read operations.
        /// </summary>
        public static readonly Guid LegacyNotifyCharacteristic = new Guid("6e400001-b5a3-f393-e0a9-e50e24dcca9e");

        /// <summary>
        /// Former Pixel characteristic UUID for write operations.
        /// </summary>
        public static readonly Guid LegacyWriteCharacteristic = new Guid("6e400002-b5a3-f393-e0a9-e50e24dcca9e");
    }
}
