using UnityEngine;

namespace Systemic.Unity.BluetoothLE.Internal.Android
{
    internal sealed class MtuRequestCallback : AndroidJavaProxy
    {
        NativeValueRequestResultHandler<int> _onMtuResult;

        public MtuRequestCallback(NativeValueRequestResultHandler<int> onMtuResult)
            : base("com.systemic.bluetoothle.Peripheral$MtuRequestCallback")
            => _onMtuResult = onMtuResult;

        void onMtuChanged(AndroidJavaObject device, int mtu)
        {
            Debug.Log($"{RequestOperation.RequestPeripheralMtu} ==> onMtuChanged: {mtu}");
            _onMtuResult?.Invoke(mtu, RequestStatus.Success);
        }

        void onRequestFailed(AndroidJavaObject device, int status)
        {
            Debug.LogError($"{RequestOperation.RequestPeripheralMtu} ==> onRequestFailed: {(AndroidRequestStatus)status}");
            _onMtuResult?.Invoke(0, AndroidNativeInterfaceImpl.ToRequestStatus(status));
        }

        void onInvalidRequest()
        {
            Debug.LogError($"{RequestOperation.RequestPeripheralMtu} ==> onInvalidRequest");
            _onMtuResult?.Invoke(0, RequestStatus.InvalidCall);
        }
    }
}
