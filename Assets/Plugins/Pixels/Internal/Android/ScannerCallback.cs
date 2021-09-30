using UnityEngine;

namespace Systemic.Pixels.Unity.BluetoothLE.Internal.Android
{
    sealed class ScannerCallback : AndroidJavaProxy
    {
        public delegate void ScanResultHandler(AndroidJavaObject device, AdvertisementDataJson advertisementData);

        ScanResultHandler _onScanResult;

        public ScannerCallback(ScanResultHandler onScanResult)
            : base("com.systemic.pixels.Scanner$ScannerCallback")
            => _onScanResult = onScanResult;

        void onScanResult(AndroidJavaObject device, string advertisementDataJson)
        {
            Debug.Log(advertisementDataJson);

            _onScanResult?.Invoke(device, JsonUtility.FromJson<AdvertisementDataJson>(advertisementDataJson));
        }

        void onScanFailed(string error)
        {
            Debug.Log(error);
        }
    }
}