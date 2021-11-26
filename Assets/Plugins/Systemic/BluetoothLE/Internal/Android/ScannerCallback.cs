using UnityEngine;

namespace Systemic.Unity.BluetoothLE.Internal.Android
{
    internal sealed class ScannerCallback : AndroidJavaProxy
    {
        public delegate void ScanResultHandler(AndroidJavaObject device, NativeAdvertisementDataJson advertisementData);

        ScanResultHandler _onScanResult;

        public ScannerCallback(ScanResultHandler onScanResult)
            : base("com.systemic.bluetoothle.Scanner$ScannerCallback")
            => _onScanResult = onScanResult;

        void onScanResult(AndroidJavaObject device, string advertisementDataJson)
        {
            Debug.Log($"[BLE] Scan ==> onScanResult: {advertisementDataJson}");

            _onScanResult?.Invoke(device, JsonUtility.FromJson<NativeAdvertisementDataJson>(advertisementDataJson));
        }

        void onScanFailed(string error)
        {
            Debug.Log($"[BLE] Scan ==> onScanFailed: {error}");
        }
    }
}