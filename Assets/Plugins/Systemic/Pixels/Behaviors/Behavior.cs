using System.Runtime.InteropServices;

namespace Systemic.Unity.Pixels.Behaviors
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [System.Serializable]
    public class Behavior
    {
        public ushort rulesOffset;
        public ushort rulesCount;
    }
}
