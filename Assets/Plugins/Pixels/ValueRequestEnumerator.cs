using System;

namespace Systemic.Unity.BluetoothLE
{
    public class ValueRequestEnumerator<T> : RequestEnumerator
    {
        public T Value { get; private set; }

        public ValueRequestEnumerator(
            Operation operation,
            PeripheralHandle peripheralHandle,
            float timeoutSec,
            Action<PeripheralHandle, NativeValueRequestResultHandler<T>> action)
            : base(operation, peripheralHandle, timeoutSec)
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
