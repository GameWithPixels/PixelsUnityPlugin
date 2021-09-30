
namespace Systemic.Pixels.Unity.BluetoothLE
{
    public struct PeripheralHandle
    {
        public interface INativePeripheral { }

        public PeripheralHandle(INativePeripheral client) => SystemClient = client;

        public INativePeripheral SystemClient { get; }

        public bool IsEmpty => SystemClient == null;
    }
}
