using System;

namespace Systemic.Unity.BluetoothLE
{
    /// <summary>
    /// Enumerator handling a request to a BLE peripheral that should read a value.
    /// It is meant to be run as coroutine.
    /// </summary>
    /// <typeparam name="T">Type of value read by the request.</typeparam>
    public class ValueRequestEnumerator<T> : RequestEnumerator
    {
        /// <summary>
        /// Value read by the request, only valid if <see cref="RequestEnumerator.IsSuccess"/> is true.
        /// </summary>
        public T Value { get; private set; }

        /// <summary>
        /// Initializes a request with a given operation, peripheral, timeout value and
        /// an action to invoke if the peripheral is valid.
        /// </summary>
        /// <param name="operation">The operation to run.</param>
        /// <param name="nativeHandle">The peripheral for which to run the operation.</param>
        /// <param name="timeoutSec">The timeout in seconds.</param>
        /// <param name="action">The action to invoke if the peripheral is valid.</param>
        internal ValueRequestEnumerator(
            RequestOperation operation,
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
