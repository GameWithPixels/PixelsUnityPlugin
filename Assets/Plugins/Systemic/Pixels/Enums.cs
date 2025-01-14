// Ignore Spelling: Colorway

namespace Systemic.Unity.Pixels
{
    /// <summary>
    /// The different types of dice.
    /// </summary>
    public enum PixelDieType: byte
    {
        Unknown = 0,
        D4,
        D6,
        D8,
        D10,
        D00,
        D12,
        D20,
        D6Pipped,
        D6Fudge,
    }

    /// <summary>
    /// Available Pixels dice colorways.
    /// </summary>
    public enum PixelColorway: byte
    {
        Unknown = 0,
        OnyxBlack,
        HematiteGrey,
        MidnightGalaxy,
        AuroraSky,
        Clear,
        WhiteAurora,
        Custom = 0xFF,
    }

    /// <summary>
    /// Pixels dice roll states.
    /// </summary>
    public enum PixelRollState : byte
    {
        // The die roll state could not be determined.
        Unknown = 0,

        // The die finished rolling and is now on a face, and it looked like a proper roll.
        Rolled,

        // The die is being handled.
        Handling,

        // The die is rolling.
        Rolling,

        // The die finished rolling but is not on a valid face.
        Crooked,

        // The die is not moving and, as far as we know, it has either never moved or it didn't move enough to trigger a roll.
        OnFace,
    };

    /// <summary>
    /// Pixel battery states.
    /// </summary>
    public enum PixelBatteryState : byte
    {
        /// Battery looks fine, nothing is happening.
        Ok = 0,

        /// Battery level is low, notify user they should recharge.
        Low,

        /// Battery is currently recharging.
        Charging,

        /// Battery is full and finished charging.
        Done,

        /// Coil voltage is bad, die is probably positioned incorrectly.
        /// Note that currently this state is triggered during transition between charging and not charging...
        BadCharging,

        /// Charge state doesn't make sense (charging but no coil voltage detected for instance).
        Error,
    }

    /// <summary>
    /// Pixel connection states.
    /// </summary>
    public enum PixelConnectionState
    {
        /// This is the value right after creation.
        Invalid = -1,

        /// This is a Pixel we knew about and scanned.
        Available,

        /// We are currently connecting to this Pixel.
        Connecting,

        /// Getting info from the Pixel, making sure it is valid to be used (correct firmware version, etc.).
        Identifying,

        /// Pixel is ready for communications.
        Ready,

        /// We are currently disconnecting from this Pixel.
        Disconnecting,
    }

    /// <summary>
    /// Identify an error encountered while communicating with a PixeL.
    /// </summary>
    public enum PixelError
    {
        /// No error.
        None = 0,

        /// An error occurred during the connection.
        ConnectionError,

        /// The Pixel is disconnected.
        Disconnected,
    }
}
