using UnityEngine;

namespace Systemic.Pixels.Unity.BluetoothLE.Internal.Android
{
    sealed class RssiRequestCallback : AndroidJavaProxy
    {
        NativeValueRequestResultHandler<int> _onRssiRead;

        public RssiRequestCallback(NativeValueRequestResultHandler<int> onRssiRead)
            : base("com.systemic.pixels.Peripheral$RssiRequestCallback")
            => _onRssiRead = onRssiRead;

        // @IntRange(from = -128, to = 20)
        void onRssiRead(AndroidJavaObject device, int rssi)
        {
            Debug.Log("==> onRssiRead " + rssi);
            _onRssiRead?.Invoke(rssi, NativeError.Empty);
        }

        void onRequestFailed(AndroidJavaObject device, int status)
        {
            Debug.LogError("==> onRequestFailed with status " + (AndroidRequestStatus)status);
            _onRssiRead?.Invoke(0, new NativeError(status, "Android error"));
        }

        void onInvalidRequest()
        {
            Debug.LogError("==> onInvalidRequest");
            _onRssiRead?.Invoke(0, new NativeError((int)AndroidRequestStatus.REASON_REQUEST_INVALID, "Android error"));
        }
    }
}
