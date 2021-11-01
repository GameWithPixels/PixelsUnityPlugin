using System;
using UnityEngine;

namespace Systemic.Unity.BluetoothLE.Internal.Android
{
    internal sealed class RequestCallback : AndroidJavaProxy
    {
        Operation _operation;
        NativeRequestResultHandler _onResult;

        public RequestCallback(Operation operation, NativeRequestResultHandler onResult)
            : base("com.systemic.bluetoothle.Peripheral$RequestCallback")
            => (_operation, _onResult) = (operation, onResult);

        void onRequestCompleted(AndroidJavaObject device)
        {
            Debug.Log($"{_operation} ==> onRequestCompleted");
            _onResult?.Invoke(0); //RequestStatus.GATT_SUCCESS
        }

        void onRequestFailed(AndroidJavaObject device, int status)
        {
            Debug.LogError($"{_operation} ==> onRequestFailed: {(AndroidRequestStatus)status}");
            _onResult?.Invoke(AndroidNativeInterfaceImpl.ToRequestStatus(status));
        }

        void onInvalidRequest()
        {
            Debug.LogError($"{_operation} ==> onInvalidRequest");
            _onResult?.Invoke(RequestStatus.InvalidCall);
        }
    }
}
