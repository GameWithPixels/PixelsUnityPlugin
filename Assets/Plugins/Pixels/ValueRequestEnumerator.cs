using System;

namespace Systemic.Unity.BluetoothLE
{
    public class ValueRequestEnumerator<T> : RequestEnumerator
    {
        public T Value { get; private set; }

        public ValueRequestEnumerator(
            Operation operation,
            NativePeripheralHandle nativeHandle,
            float timeoutSec,
            Action<NativePeripheralHandle, NativeValueRequestResultHandler<T>> action)
            : base(operation, nativeHandle, timeoutSec)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            if (Peripheral.IsValid)
            {
                action(Peripheral, (value, error) =>
                {
                    Value = value;
                    SetResult(error);
                });
            }
            else
            {
                SetResult(RequestStatus.InvalidPeripheral);
            }
        }
    }
}
