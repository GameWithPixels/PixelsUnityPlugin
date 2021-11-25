
namespace Systemic.Unity.Pixels.Messages
{
    /// <summary>
    /// Lists all the Pixels message types.
    /// The value is used for the first byte of data in a Pixel message to identify it's type.
    /// </summary>
    /// <remarks>
    /// These message identifiers have to match up with the ones on the firmware.
    /// </remarks>
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

}
