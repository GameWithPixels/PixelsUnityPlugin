
namespace Systemic.Unity.Pixels
{
    /// <summary>
    /// Available combinations of Pixel designs and colors.
    /// </summary>
    public enum PixelDesignAndColor : byte
    {
        Unknown = 0,
        Generic,
        V3_Orange,
        V4_BlackClear,
        V4_WhiteClear,
        V5_Grey,
        V5_White,
        V5_Black,
        V5_Gold,
        Onyx_Back,
        Hematite_Grey,
        Midnight_Galaxy,
        Aurora_Sky
    }

    /// <summary>
    /// Pixel roll states.
    /// </summary>
    public enum PixelRollState : byte
    {
        Unknown = 0,
        OnFace,
        Handling,
        Rolling,
        Crooked
    };

    /// <summary>
    /// Pixel connection states.
    /// </summary>
    public enum PixelConnectionState
    {
        Invalid = -1,   // This is the value right after creation
        Available,      // This is a Pixel we knew about and scanned
        Connecting,     // This Pixel is in the process of being connected to
        Identifying,    // Getting info from the Pixel, making sure it is valid to be used (right firmware, etc...)
        Ready,          // Pixel is ready for communications
        Disconnecting,  // We are currently disconnecting from this Pixel
    }

    public enum PixelLastError
    {
        None = 0,
        ConnectionError,
        Disconnected
    }
}
