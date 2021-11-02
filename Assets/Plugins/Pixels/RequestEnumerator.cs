using System;
using System.Collections;
using UnityEngine;

namespace Systemic.Unity.BluetoothLE
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

        protected NativePeripheralHandle Peripheral { get; }

        internal RequestEnumerator(
            Operation operation,
            NativePeripheralHandle nativeHandle,
            float timeoutSec,
            Action<NativePeripheralHandle, NativeRequestResultHandler> action)
            : this(operation, nativeHandle, timeoutSec)
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
            NativePeripheralHandle nativeHandle,
            float timeoutSec)
        {
            Operation = operation;
            Peripheral = nativeHandle;
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
}
