using UnityEngine;

namespace Systemic.Pixels.Unity.BluetoothLE.Internal.Android
{
    internal sealed class DataReceivedCallback : AndroidJavaProxy
    {
        NativeValueChangedHandler _onDataReceived;

        public DataReceivedCallback(NativeValueChangedHandler onDataReceived)
            : base("no.nordicsemi.android.ble.callback.DataReceivedCallback")
            => _onDataReceived = onDataReceived;

        void onDataReceived(AndroidJavaObject device, AndroidJavaObject data)
        {
            using var javaArray = data.Call<AndroidJavaObject>("getValue");
            _onDataReceived?.Invoke(JavaUtils.ToDotNetArray(javaArray), RequestStatus.Success); // No notification with error on Android
        }
    }
}
