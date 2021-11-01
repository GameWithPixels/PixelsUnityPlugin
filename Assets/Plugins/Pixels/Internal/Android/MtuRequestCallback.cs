using UnityEngine;

namespace Systemic.Pixels.Unity.BluetoothLE.Internal.Android
{
    internal sealed class MtuRequestCallback : AndroidJavaProxy
    {
        NativeValueRequestResultHandler<int> _onMtuResult;

        public MtuRequestCallback(NativeValueRequestResultHandler<int> onMtuResult)
            : base("com.systemic.pixels.Peripheral$MtuRequestCallback")
            => _onMtuResult = onMtuResult;

        void onMtuChanged(AndroidJavaObject device, int mtu)
        {
            Debug.Log($"{Operation.RequestPeripheralMtu} ==> onMtuChanged: {mtu}");
            _onMtuResult?.Invoke(mtu, RequestStatus.Success);
        }

        void onRequestFailed(AndroidJavaObject device, int status)
        {
            Debug.LogError($"{Operation.RequestPeripheralMtu} ==> onRequestFailed: {(AndroidRequestStatus)status}");
            _onMtuResult?.Invoke(0, AndroidNativeInterfaceImpl.ToRequestStatus(status));
        }

        void onInvalidRequest()
        {
            Debug.LogError($"{Operation.RequestPeripheralMtu} ==> onInvalidRequest");
            _onMtuResult?.Invoke(0, RequestStatus.InvalidCall);
        }
    }
}
