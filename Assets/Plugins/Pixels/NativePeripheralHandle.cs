
namespace Systemic.Unity.BluetoothLE
{
    internal interface INativePeripheral
    {
        bool IsValid { get; }
    }

    // Readonly struct
    public struct NativePeripheralHandle
    {
        internal NativePeripheralHandle(INativePeripheral client) => NativePeripheral = client;

        internal INativePeripheral NativePeripheral { get; }

        public bool IsValid => NativePeripheral?.IsValid ?? false;
    }
}
