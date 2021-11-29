using UnityEngine;

namespace Systemic.Unity.Pixels
{
    /// <summary>
    /// Helper static class that implements various color operations with the color information
    /// being stored as a 32 bits value.
    /// In related methods, the intensity is a byte value between 0 (black) and 255 (white).
    /// </summary>
    public static class ColorUtils
    {
        /// <summary>
        /// Converts a (red, green, blue) bytes triplets to a 32 bits color value.
        /// </summary>
        /// <param name="red">The red component as a byte value.</param>
        /// <param name="green">The green component as a byte value.</param>
        /// <param name="blue">The blue component as a byte value.</param>
        /// <returns>A 32 bits color value.</returns>
        public static uint ToColor(byte red, byte green, byte blue)
        {
            return (uint)red << 16 | (uint)green << 8 | (uint)blue;
        }

        /// <summary>
        /// Extracts the red component of a 32 bits color value.
        /// </summary>
        /// <param name="color">The 32 bits color value.</param>
        /// <returns>The red component of the color.</returns>
        public static byte GetRed(uint color)
        {
            return (byte)((color >> 16) & 0xFF);
        }

        /// <summary>
        /// Extracts the green component of a 32 bits color value.
        /// </summary>
        /// <param name="color">The 32 bits color value.</param>
        /// <returns>The green component of the color.</returns>
        public static byte GetGreen(uint color)
        {
            return (byte)((color >> 8) & 0xFF);
        }

        /// <summary>
        /// Extracts the blue component of a 32 bits color value.
        /// </summary>
        /// <param name="color">The 32 bits color value.</param>
        /// <returns>The blue component of the color.</returns>
        public static byte GetBlue(uint color)
        {
            return (byte)((color) & 0xFF);
        }

        /// <summary>
        /// Combines the two colors by selecting the highest value for each component.
        /// </summary>
        /// <param name="color1">The first color to combine.</param>
        /// <param name="color2">The second color to combine.</param>
        /// <returns></returns>
        public static uint CombineColors(uint color1, uint color2)
        {
            byte red = (byte)Mathf.Max(GetRed(color1), GetRed(color2));
            byte green = (byte)Mathf.Max(GetGreen(color1), GetGreen(color2));
            byte blue = (byte)Mathf.Max(GetBlue(color1), GetBlue(color2));
            return ToColor(red, green, blue);
        }

        /// <summary>
        /// Interpolates linearly between two colors each given for a specific timestamp.
        /// </summary>
        /// <param name="color1">The first color.</param>
        /// <param name="timestamp1">The timestamp for the first color.</param>
        /// <param name="color2">The second color.</param>
        /// <param name="timestamp2">The timestamp for the second color.</param>
        /// <param name="time">The time for which to calculate the color.</param>
        /// <returns>The color for the given time.</returns>
        public static uint InterpolateColors(uint color1, int timestamp1, uint color2, int timestamp2, int time)
        {
            // To stick to integer math, we'll scale the values
            int scaler = 1024;
            int scaledPercent = (time - timestamp1) * scaler / (timestamp2 - timestamp1);
            int scaledRed = GetRed(color1) * (scaler - scaledPercent) + GetRed(color2) * scaledPercent;
            int scaledGreen = GetGreen(color1) * (scaler - scaledPercent) + GetGreen(color2) * scaledPercent;
            int scaledBlue = GetBlue(color1) * (scaler - scaledPercent) + GetBlue(color2) * scaledPercent;
            return ToColor((byte)(scaledRed / scaler), (byte)(scaledGreen / scaler), (byte)(scaledBlue / scaler));
        }

        /// <summary>
        /// Interpolates linearly the two intensities each given for a specific timestamp.
        /// </summary>
        /// <param name="intensity1">The first intensity value.</param>
        /// <param name="timestamp1">The timestamp for the first intensity.</param>
        /// <param name="intensity2">The second intensity value.</param>
        /// <param name="timestamp2">The timestamp for the second intensity.</param>
        /// <param name="time">The time for which to calculate the intensity.</param>
        /// <returns>The intensity for the given time.</returns>
        public static byte InterpolateIntensity(byte intensity1, int timestamp1, byte intensity2, int timestamp2, int time)
        {
            int scaler = 1024;
            int scaledPercent = (time - timestamp1) * scaler / (timestamp2 - timestamp1);
            return (byte)((intensity1 * (scaler - scaledPercent) + intensity2 * scaledPercent) / scaler);
        }

        /// <summary>
        /// Modulates the color with the given intensity. The later is a value
        /// between 0 (black) and (white).
        /// </summary>
        /// <param name="color">The color to modulate.</param>
        /// <param name="intensity">The intensity to apply.</param>
        /// <returns></returns>
        public static uint ModulateColor(uint color, byte intensity)
        {
            int red = GetRed(color) * intensity / 255;
            int green = GetGreen(color) * intensity / 255;
            int blue = GetBlue(color) * intensity / 255;
            return ToColor((byte)red, (byte)green, (byte)blue);
        }

        /// <summary>
        /// Returns a color along the following looped color blending:
        /// [position = 0] red -> green -> blue -> red [position = 255].
        /// </summary>
        /// <param name="position">Position on the rainbow wheel.</param>
        /// <param name="intensity">Intensity of the returned color.</param>
        /// <returns>A color.</returns>
        public static uint RainbowWheel(byte position, byte intensity)
        {
            if (position < 85)
            {
                return ToColor((byte)(position * 3 * intensity / 255), (byte)((255 - position * 3) * intensity / 255), 0);
            }
            else if (position < 170)
            {
                position -= 85;
                return ToColor((byte)((255 - position * 3) * intensity / 255), 0, (byte)(position * 3 * intensity / 255));
            }
            else
            {
                position -= 170;
                return ToColor(0, (byte)(position * 3 * intensity / 255), (byte)((255 - position * 3) * intensity / 255));
            }
        }

        /// <summary>
        /// Returns the gamma of the given intensity.
        /// </summary>
        /// <param name="intensity"></param>
        /// <returns>The gamma value.</returns>
        public static byte Gamma8(byte intensity)
        {
            return _gammaTable[intensity]; // 0-255 in, 0-255 out
        }

        /// <summary>
        /// Returns the gamma transformation of the given color.
        /// </summary>
        /// <param name="color">The color to transform.</param>
        /// <returns>The gamma transformed color.</returns>
        public static uint Gamma(uint color)
        {
            byte r = Gamma8(GetRed(color));
            byte g = Gamma8(GetGreen(color));
            byte b = Gamma8(GetBlue(color));
            return ToColor(r, g, b);
        }

        /// <summary>
        /// Returns the gamma transformation of the given color.
        /// </summary>
        /// <param name="color">The color to transform.</param>
        /// <returns>The gamma transformed color.</returns>
        public static Color32 Gamma(Color32 color)
        {
            byte r = Gamma8(color.r);
            byte g = Gamma8(color.g);
            byte b = Gamma8(color.b);
            return new Color32(r, g, b, 255);
        }

        /// <summary>
        /// Returns the intensity corresponding to the given gamma value.
        /// </summary>
        /// <param name="gamma">A gamma value.</param>
        /// <returns>The intensity for the gamma value.</returns>
        public static byte ReverseGamma8(byte gamma)
        {
            return _reverseGammaTable[gamma]; // 0-255 in, 0-255 out
        }

        /// <summary>
        /// Returns the reverse gamma transformation of the given color.
        /// </summary>
        /// <param name="color">The color to transform.</param>
        /// <returns>The reverse gamma transformed color.</returns>
        public static Color32 ReverseGamma(Color32 color)
        {
            byte r = ReverseGamma8(color.r);
            byte g = ReverseGamma8(color.g);
            byte b = ReverseGamma8(color.b);
            return new Color32(r, g, b, 255);
        }

        #region Gamma tables

        static readonly byte[] _gammaTable =
        {
              0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
              0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
              1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  2,
              2,  2,  2,  2,  2,  2,  2,  3,  3,  3,  3,  3,  3,  3,  4,  4,
              4,  4,  4,  5,  5,  5,  5,  6,  6,  6,  6,  6,  7,  7,  7,  8,
              8,  8,  8,  9,  9,  9, 10, 10, 10, 11, 11, 12, 12, 12, 13, 13,
             14, 14, 14, 15, 15, 16, 16, 17, 17, 18, 18, 19, 19, 20, 20, 21,
             22, 22, 23, 23, 24, 25, 25, 26, 27, 27, 28, 29, 29, 30, 31, 32,
             32, 33, 34, 35, 35, 36, 37, 38, 39, 40, 40, 41, 42, 43, 44, 45,
             46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 60, 61, 62,
             63, 64, 65, 67, 68, 69, 70, 72, 73, 74, 76, 77, 78, 80, 81, 82,
             84, 85, 87, 88, 90, 91, 93, 94, 96, 97, 99,101,102,104,105,107,
            109,111,112,114,116,118,119,121,123,125,127,129,131,132,134,136,
            138,140,142,144,147,149,151,153,155,157,159,162,164,166,168,171,
            173,175,178,180,182,185,187,190,192,195,197,200,202,205,207,210,
            213,215,218,221,223,226,229,232,235,237,240,243,246,249,252,255,
        };

        static readonly byte[] _reverseGammaTable =
        {
            0, 70, 80, 87, 92, 97, 101, 105, 108, 112, 114, 117, 119, 122, 124, 126,
            128, 130, 132, 134, 135, 137, 138, 140, 141, 143, 144, 146, 147, 148, 149,
            151, 152, 153, 154, 155, 156, 157, 158, 159, 160, 161, 162, 163, 164, 165,
            166, 167, 168, 169, 170, 170, 171, 172, 173, 174, 174, 175, 176, 177, 177,
            178, 179, 180, 180, 181, 182, 182, 183, 184, 184, 185, 186, 186, 187, 188,
            188, 189, 189, 190, 191, 191, 192, 192, 193, 194, 194, 195, 195, 196, 196,
            197, 197, 198, 198, 199, 200, 200, 201, 201, 202, 202, 203, 203, 204, 204,
            204, 205, 205, 206, 206, 207, 207, 208, 208, 209, 209, 210, 210, 210, 211,
            211, 212, 212, 213, 213, 214, 214, 214, 215, 215, 216, 216, 216, 217, 217,
            218, 218, 218, 219, 219, 220, 220, 220, 221, 221, 222, 222, 222, 223, 223,
            223, 224, 224, 224, 225, 225, 226, 226, 226, 227, 227, 227, 228, 228, 228,
            229, 229, 229, 230, 230, 230, 231, 231, 231, 232, 232, 232, 233, 233, 233,
            234, 234, 234, 235, 235, 235, 236, 236, 236, 237, 237, 237, 237, 238, 238,
            238, 239, 239, 239, 240, 240, 240, 241, 241, 241, 241, 242, 242, 242, 243,
            243, 243, 243, 244, 244, 244, 245, 245, 245, 245, 246, 246, 246, 247, 247,
            247, 247, 248, 248, 248, 248, 249, 249, 249, 249, 250, 250, 250, 251, 251,
            251, 251, 252, 252, 252, 252, 253, 253, 253, 253, 254, 254, 254, 254, 255,
        };
        
        #endregion
    }
}
