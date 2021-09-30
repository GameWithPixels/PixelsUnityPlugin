using System;
using UnityEngine;

namespace Systemic.Pixels.Unity.BluetoothLE
{
    //TODO Expose error code
    public class RequestError
    {
    }

    public enum Operation
    {
        ConnectPeripheral,
        DisconnectPeripheral,
        ReadPeripheralRssi,
        ReadCharacteristic,
        WriteCharacteristic,
        SubscribeCharacteristic,
        UnsubscribeCharacteristic,
    }

    public class RequestEnumerator : CustomYieldInstruction
    {
        readonly double _timeout;
        Action _postAction;
        bool _isTimedOut;
        NativeError? _error;

        public Operation Operation { get; }

        public bool IsCompleted => _error.HasValue;

        public bool IsSuccess => _error.HasValue && _error.Value.IsEmpty;

        public bool IsTimedOut => _isTimedOut = _isTimedOut || (Time.realtimeSinceStartupAsDouble >= _timeout);

        public override bool keepWaiting => Run();

        internal RequestEnumerator(Operation operation, float timeoutSec, Action<NativeRequestResultHandler> action, Action postAction = null)
        {
            Operation = operation;
            _timeout = Time.realtimeSinceStartupAsDouble + timeoutSec;
            _postAction = postAction;
            action?.Invoke(SetResult);
        }

        protected void SetResult(NativeError error)
        {
            _error = error;
        }

        bool Run()
        {
            bool done = IsCompleted || IsTimedOut;
            if (done)
            {
                _postAction?.Invoke();
                _postAction = null;
            }
            return !done;
        }
    }

    public class ValueRequestEnumerator<T> : RequestEnumerator
    {
        public T Value { get; private set; }

        public ValueRequestEnumerator(Operation operation, float timeoutSec, Action<NativeValueRequestResultHandler<T>> action, Action postAction = null)
            : base(operation, timeoutSec, null, postAction)
        {
            action((value, error) => { Value = value; SetResult(error); });
        }
    }
}
