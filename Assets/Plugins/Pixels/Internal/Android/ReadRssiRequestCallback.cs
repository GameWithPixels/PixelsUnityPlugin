using UnityEngine;

namespace Systemic.Unity.BluetoothLE.Internal.Android
{
    internal sealed class ReadRssiRequestCallback : AndroidJavaProxy
    {
        NativeValueRequestResultHandler<int> _onRssiRead;

        public ReadRssiRequestCallback(NativeValueRequestResultHandler<int> onRssiRead)
            : base("com.systemic.bluetoothle.Peripheral$RssiRequestCallback")
            => _onRssiRead = onRssiRead;

        // @IntRange(from = -128, to = 20)
        void onRssiRead(AndroidJavaObject device, int rssi)
        {
            Debug.Log($"{RequestOperation.ReadPeripheralRssi} ==> onRssiRead {rssi}");
            _onRssiRead?.Invoke(rssi, RequestStatus.Success);
        }

        void onRequestFailed(AndroidJavaObject device, int status)
        {
            Debug.LogError($"{RequestOperation.ReadPeripheralRssi} ==> onRequestFailed: {(AndroidRequestStatus)status}");
            _onRssiRead?.Invoke(int.MinValue, AndroidNativeInterfaceImpl.ToRequestStatus(status));
        }

        void onInvalidRequest()
        {
            Debug.LogError($"{RequestOperation.ReadPeripheralRssi} ==> onInvalidRequest");
            _onRssiRead?.Invoke(int.MinValue, RequestStatus.InvalidCall);
        }
    }
}
