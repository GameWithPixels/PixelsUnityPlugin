using System.Runtime.InteropServices;

namespace Systemic.Unity.Pixels.Behaviors
{
    /// <summary>
    /// A behavior which is made of a list of rules.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [System.Serializable]
    public class Behavior
    {
        public ushort rulesOffset;
        public ushort rulesCount;
    }
}
