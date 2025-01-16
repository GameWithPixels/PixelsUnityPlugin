using System;
using Systemic.Unity.Pixels;
using UnityEngine;

namespace Systemic.Unity.Pixels
{
    /// <summary>
    /// Helper static class that implements various gamma operations on colors.
    /// </summary>
    public static class DiceUtils
    {
        public static int GetLEDCount(PixelDieType dieType)
        {
            switch (dieType)
            {
                case PixelDieType.Unknown:
                    return 0;
                case PixelDieType.D4:
                case PixelDieType.D6:
                case PixelDieType.D6Fudge:
                    return 6;
                case PixelDieType.D6Pipped:
                    return 21;
                case PixelDieType.D8:
                    return 8;
                case PixelDieType.D10:
                case PixelDieType.D00:
                    return 10;
                case PixelDieType.D12:
                    return 12;
                case PixelDieType.D20:
                    return 20;
                default:
                    throw new ArgumentException("Unknown die type", "dieType");
            }
        }

        public static int GetFaceCount(PixelDieType dieType)
        {
            switch (dieType)
            {
                case PixelDieType.Unknown:
                    return 0;
                case PixelDieType.D4:
                    return 4;
                case PixelDieType.D6:
                case PixelDieType.D6Fudge:
                case PixelDieType.D6Pipped:
                    return 6;
                case PixelDieType.D8:
                    return 8;
                case PixelDieType.D10:
                case PixelDieType.D00:
                    return 10;
                case PixelDieType.D12:
                    return 12;
                case PixelDieType.D20:
                    return 20;
                default:
                    throw new ArgumentException("Unknown die type", "dieType");
            }
        }

        public static int GetFaceFromIndex(int faceIndex, PixelDieType dieType, uint? firmwareTimestamp)
        {
            if (IsFirmwareWithBadNormals(firmwareTimestamp))
            {
                // Account for bad normals in firmware 2023-11-17
                switch (dieType)
                {
                    case PixelDieType.D4:
                        if (faceIndex == 3) return 2;
                        if (faceIndex == 2) return 3;
                        if (faceIndex == 5) return 4;
                        return 1;
                    case PixelDieType.D6:
                        if (faceIndex == 4) return 2;
                        if (faceIndex == 3) return 3;
                        if (faceIndex == 2) return 4;
                        if (faceIndex == 1) return 5;
                        return faceIndex + 1;
                }
            }
            return dieType switch
            {
                PixelDieType.D10 => faceIndex,
                PixelDieType.D00 => faceIndex * 10,
                PixelDieType.Unknown => faceIndex,
                _ => faceIndex + 1,
            };
        }

        public static int GetIndexFromFace(int face, PixelDieType dieType, uint? firmwareTimestamp)
        {
            if (IsFirmwareWithBadNormals(firmwareTimestamp))
            {
                // Account for bad normals in firmware 2023-11-17
                switch (dieType)
                {
                    case PixelDieType.D4:
                        if (face == 2) return 3;
                        if (face == 3) return 2;
                        if (face == 4) return 5;
                        return 0;
                    case PixelDieType.D6:
                        if (face == 2) return 4;
                        if (face == 3) return 3;
                        if (face == 4) return 2;
                        if (face == 5) return 1;
                        return face - 1;
                }
            }
            return dieType switch
            {
                PixelDieType.D10 => face,
                PixelDieType.D00 => Mathf.FloorToInt(face / 10),
                PixelDieType.Unknown => face,
                _ => face - 1,
            };
        }

        static bool IsFirmwareWithBadNormals(uint? firmwareTimestamp)
        {
            uint FW_2023_11_17 = 1704150000;
            return firmwareTimestamp.HasValue && firmwareTimestamp.Value <= FW_2023_11_17;
        }

    }
}
