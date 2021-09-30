using System;

namespace Systemic.Pixels.Unity.BluetoothLE
{
    public static class StringExtension
    {
        public static Guid ToBleGuid(this string bleUuid)
        {
            if (bleUuid == null)
                throw new ArgumentNullException(nameof(bleUuid));
            if (bleUuid.Length == 36)
                return new Guid(bleUuid);
            if (bleUuid.Length <= 8)
                return new Guid($"{bleUuid.PadLeft(8, '0')}-0000-1000-8000-00805F9B34FB");

            throw new ArgumentException("Invalid BLE UUID string: " + bleUuid, nameof(bleUuid));
        }
    }
}