using UnityEngine;

namespace Systemic.Pixels.Unity.BluetoothLE.Internal.Android
{
    sealed class MtuRequestCallback : AndroidJavaProxy
    {
        NativeValueRequestResultHandler<int> _onMtuResult;

        public MtuRequestCallback(NativeValueRequestResultHandler<int> onMtuResult)
            : base("com.systemic.pixels.Peripheral$MtuRequestCallback")
            => _onMtuResult = onMtuResult;

        void onMtuChanged(AndroidJavaObject device, int mtu)
        {
            Debug.Log("==> onMtuChanged " + mtu);
            _onMtuResult?.Invoke(mtu, NativeError.Empty);
        }

        void onRequestFailed(AndroidJavaObject device, int status)
        {
            Debug.LogError("==> onRequestFailed with status " + (AndroidRequestStatus)status);
            _onMtuResult?.Invoke(0, new NativeError(status, "Android error"));
        }

        void onInvalidRequest()
        {
            Debug.LogError("==> onInvalidRequest");
            _onMtuResult?.Invoke(0, new NativeError((int)AndroidRequestStatus.REASON_REQUEST_INVALID, "Android error"));
        }
    }
}
