using System;
using UnityEngine;

namespace Systemic.Pixels.Unity.BluetoothLE.Internal.Android
{
    sealed class RequestCallback : AndroidJavaProxy
    {
        Action<int> _onRequestDone;

        public RequestCallback(NativeRequestResultHandler onResult)
            : base("com.systemic.pixels.Peripheral$RequestCallback")
            => _onRequestDone = errorCode => onResult(new NativeError(errorCode, "Android error"));

        void onRequestCompleted(AndroidJavaObject device)
        {
            Debug.Log("==> onRequestCompleted");
            _onRequestDone?.Invoke(0); //RequestStatus.GATT_SUCCESS
        }

        void onRequestFailed(AndroidJavaObject device, int status)
        {
            Debug.LogError("==> onRequestFailed with status " + (AndroidRequestStatus)status);
            _onRequestDone?.Invoke(status);
        }

        void onInvalidRequest()
        {
            Debug.LogError("==> onInvalidRequest");
            _onRequestDone?.Invoke((int)AndroidRequestStatus.REASON_REQUEST_INVALID);
        }
    }
}
