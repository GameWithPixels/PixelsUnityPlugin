
using System;

namespace Systemic.Unity.Pixels
{
    /// <summary>
    /// Lists all the Pixels message types.
    /// The value is used for the first byte of data in a Pixel message to identify it's type.
    /// </summary>
    public enum MessageType : byte
    {
        None = 0,
        WhoAreYou,
        IAmADie,
        RollState,
        Telemetry,
        BulkSetup,
        BulkSetupAck,
        BulkData,
        BulkDataAck,
        TransferAnimSet,
        TransferAnimSetAck,
        TransferAnimSetFinished,
        TransferSettings,
        TransferSettingsAck,
        TransferSettingsFinished,
        TransferTestAnimSet,
        TransferTestAnimSetAck,
        TransferTestAnimSetFinished,
        DebugLog,
        PlayAnim,
        PlayAnimEvent,
        StopAnim,
        PlaySound,
        RequestRollState,
        RequestAnimSet,
        RequestSettings,
        RequestTelemetry,
        ProgramDefaultAnimSet,
        ProgramDefaultAnimSetFinished,
        Flash,
        FlashFinished,
        RequestDefaultAnimSetColor,
        DefaultAnimSetColor,
        RequestBatteryLevel,
        BatteryLevel,
        RequestRssi,
        Rssi,
        Calibrate,
        CalibrateFace,
        NotifyUser,
        NotifyUserAck,
        TestHardware,
        SetStandardState,
        SetLEDAnimState,
        SetBattleState,
        ProgramDefaultParameters,
        ProgramDefaultParametersFinished,
        SetDesignAndColor,
        SetDesignAndColorAck,
        SetCurrentBehavior,
        SetCurrentBehaviorAck,
        SetName,
        SetNameAck,

        // Testing
        TestBulkSend,
        TestBulkReceive,
        SetAllLEDsToColor,
        AttractMode,
        PrintNormals,
        PrintA2DReadings,
        LightUpFace,
        SetLEDToColor,
        DebugAnimController,
    }

    /// <summary>
    /// Pixel roll states.
    /// </summary>
    public enum RollState : byte
    {
        Unknown = 0,
        OnFace,
        Handling,
        Rolling,
        Crooked
    };

    /// <summary>
    /// Pixels Bluetooth Low Energy UUIDs.
    /// </summary>
    public class BleUuids
    {
        /// <summary>
        /// Pixel service UUID.
        /// May be used to filter out Pixels during a scan and to access its characteristics.
        /// </summary>
        public static readonly Guid ServiceUuid = new Guid("6e400001-b5a3-f393-e0a9-e50e24dcca9e");

        /// <summary>
        /// Pixels characteristic UUID for notification and read operations.
        /// May be used to get notified on dice events or read the current state.
        /// </summary>
        public static readonly Guid NotifyCharacteristicUuid = new Guid("6e400001-b5a3-f393-e0a9-e50e24dcca9e");

        /// <summary>
        /// Pixels characteristic UUID for write operations.
        /// May be used to send messages to a dice.
        /// </summary>
        public static readonly Guid WriteCharacteristicUuid = new Guid("6e400002-b5a3-f393-e0a9-e50e24dcca9e");
    }
}
