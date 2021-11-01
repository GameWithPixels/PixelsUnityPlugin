using System;
using System.Collections;
using UnityEngine;

namespace Systemic.Pixels.Unity.BluetoothLE
{
    public enum Operation
    {
        ConnectPeripheral,
        DisconnectPeripheral,
        ReadPeripheralRssi,
        RequestPeripheralMtu,
        ReadCharacteristic,
        WriteCharacteristic,
        SubscribeCharacteristic,
        UnsubscribeCharacteristic,
    }

    public class RequestEnumerator : IEnumerator
    {
        readonly double _timeout;
        RequestStatus? _status;

        public Operation Operation { get; }

        public bool IsDone => _status.HasValue;

        public bool IsSuccess => _status.HasValue && (_status.Value == RequestStatus.Success);

        public bool IsTimeout { get; private set; }

        public RequestStatus RequestStatus => _status.HasValue ? _status.Value : RequestStatus.InProgress;

        public string Error => RequestStatus switch
        {
            RequestStatus.Success => null,
            RequestStatus.InProgress => "Operation in progress",
            RequestStatus.Canceled => "Operation canceled",
            RequestStatus.InvalidPeripheral => "Invalid peripheral",
            RequestStatus.InvalidCall => "Invalid call",
            RequestStatus.InvalidParameters => "Invalid parameters",
            RequestStatus.NotSupported => "Operation not supported",
            RequestStatus.ProtocolError => "GATT protocol error",
            RequestStatus.AccessDenied => "Access denied",
            RequestStatus.Timeout => "Timeout",
            _ => "Unknown error",
        };

        public object Current => null;

        protected PeripheralHandle Peripheral { get; }

        internal RequestEnumerator(
            Operation operation,
            PeripheralHandle peripheralHandle,
            float timeoutSec,
            Action<PeripheralHandle, NativeRequestResultHandler> action)
            : this(operation, peripheralHandle, timeoutSec)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            Operation = operation;
            _timeout = timeoutSec == 0 ? 0 : Time.realtimeSinceStartupAsDouble + timeoutSec;
            if (Peripheral.IsValid)
            {
                action?.Invoke(Peripheral, SetResult);
            }
            else
            {
                SetResult(RequestStatus.InvalidPeripheral);
                //TODO check in NativeInterface instead
            }
        }

        protected RequestEnumerator(
            Operation operation,
            PeripheralHandle peripheralHandle,
            float timeoutSec)
        {
            Operation = operation;
            Peripheral = peripheralHandle;
            _timeout = timeoutSec == 0 ? 0 : Time.realtimeSinceStartupAsDouble + timeoutSec;
        }

        protected void SetResult(RequestStatus status)
        {
            // Only keep first error
            if (!_status.HasValue)
            {
                _status = status;
            }
        }

        public virtual bool MoveNext()
        {
            if ((!_status.HasValue) && (_timeout > 0))
            {
                // Update timeout
                if (Time.realtimeSinceStartupAsDouble > _timeout)
                {
                    IsTimeout = true;
                    _status = RequestStatus.Timeout;
                }
            }

            return !_status.HasValue;
        }

        public void Reset()
        {
            // Not supported
        }
    }

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

    public class ConnectRequestEnumerator : RequestEnumerator
    {
        DisconnectRequestEnumerator _disconnect;
        Action _onTimeoutDisconnect;

        public ConnectRequestEnumerator(
            PeripheralHandle peripheral,
            float timeoutSec,
            Action<PeripheralHandle, NativeRequestResultHandler> action,
            Action onTimeoutDisconnect)
            : base(Operation.ConnectPeripheral, peripheral, timeoutSec, action)
        {
            _onTimeoutDisconnect = onTimeoutDisconnect;
        }

        public override bool MoveNext()
        {
            bool done;

            if (_disconnect == null)
            {
                done = !base.MoveNext();

                // Did we fail with a timeout?
                if (done && IsTimeout && Peripheral.IsValid)
                {
                    _onTimeoutDisconnect?.Invoke();

                    // Cancel connection attempt
                    _disconnect = new DisconnectRequestEnumerator(Peripheral);
                    done = !_disconnect.MoveNext();
                }
            }
            else
            {
                done = !_disconnect.MoveNext();
            }

            return !done;
        }
    }

    public class DisconnectRequestEnumerator : RequestEnumerator
    {
        bool _released;

        public DisconnectRequestEnumerator(PeripheralHandle peripheral)
            : base(Operation.DisconnectPeripheral, peripheral, 0)
        {
            if (Peripheral.IsValid)
            {
                NativeInterface.DisconnectPeripheral(peripheral, SetResult);
            }
            else
            {
                SetResult(RequestStatus.InvalidPeripheral);
            }
        }

        public override bool MoveNext()
        {
            bool done = !base.MoveNext();

            // Are we done with the disconnect?
            if (done && Peripheral.IsValid && (!_released))
            {
                // Release peripheral even if the disconnect might have failed
                NativeInterface.ReleasePeripheral(Peripheral);
                _released = true;
            }

            return !done;
        }
    }
}
