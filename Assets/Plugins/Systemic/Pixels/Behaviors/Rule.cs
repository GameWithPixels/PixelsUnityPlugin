using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;

namespace Systemic.Unity.Pixels.Behaviors
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [System.Serializable]
    public class Rule
    {
        public ushort condition;
        public ushort actionOffset;
        public ushort actionCount;
        public ushort actionCountPadding;
    }
}
