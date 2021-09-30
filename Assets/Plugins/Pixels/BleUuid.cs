using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Systemic.Pixels.Unity.BluetoothLE
{
    public static class BleUuid
    {
        // Convert 16 bit UUID to 128 UUID
        public static Guid ToFullUuid(short shortUuid) => new Guid($"0000{shortUuid:x4}-0000-1000-8000-00805f9b34fb");
    }
}
