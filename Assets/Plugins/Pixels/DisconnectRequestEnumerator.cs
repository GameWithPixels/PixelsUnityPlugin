
namespace Systemic.Unity.BluetoothLE
{
    public sealed class DisconnectRequestEnumerator : RequestEnumerator
    {
        bool _released;

        public DisconnectRequestEnumerator(NativePeripheralHandle peripheral)
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
