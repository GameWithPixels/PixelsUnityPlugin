using System;

namespace Systemic.Unity.BluetoothLE
{
    public sealed class ConnectRequestEnumerator : RequestEnumerator
    {
        DisconnectRequestEnumerator _disconnect;
        Action _onTimeoutDisconnect;

        public ConnectRequestEnumerator(
            NativePeripheralHandle peripheral,
            float timeoutSec,
            Action<NativePeripheralHandle, NativeRequestResultHandler> action,
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
}