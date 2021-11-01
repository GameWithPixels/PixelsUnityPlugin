
namespace Systemic.Pixels.Unity.BluetoothLE
{
    internal interface INativePeripheral
    {
        bool IsValid { get; }
    }

    // Readonly struct
    public struct PeripheralHandle
    {
        internal PeripheralHandle(INativePeripheral client) => NativePeripheral = client;

        internal INativePeripheral NativePeripheral { get; }

        public bool IsValid => NativePeripheral?.IsValid ?? false;
    }
}
