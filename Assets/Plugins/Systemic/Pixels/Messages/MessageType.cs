
/// <summary>
/// Set of classes to represent all supported Pixel messages as well as en enum <see cref="MessageType"/>
/// with all the message types and a marshaling helper <see cref="PixelMessageMarshaling"/>.
/// </summary>
namespace Systemic.Unity.Pixels.Messages
{
    /// <summary>
    /// Lists all the Pixel dice message types.
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
        TransferAnimationSet,
        TransferAnimationSetAck,
        TransferAnimationSetFinished,
        TransferSettings,
        TransferSettingsAck,
        TransferSettingsFinished,
        _unused1,
        _unused2,
        _unused3,
        DebugLog,
        PlayAnimation,
        PlayAnimationEvent,
        StopAnimation,
        RemoteAction,
        RequestRollState,
        RequestAnimationSet,
        RequestSettings,
        RequestTelemetry,
        ProgramDefaultAnimationSet,
        ProgramDefaultAnimationSetFinished,
        Blink,
        BlinkAck,
        RequestDefaultAnimationSetColor,
        DefaultAnimationSetColor,
        RequestBatteryLevel,
        BatteryLevel,
        RequestRssi,
        Rssi,
        Calibrate,
        CalibrateFace,
        NotifyUser,
        NotifyUserAck,
        TestHardware,
        StoreValue,
        StoreValueAck,
        SetTopLevelState,
        ProgramDefaultParameters,
        ProgramDefaultParametersFinished,
        SetDesignAndColor,
        SetDesignAndColorAck,
        SetCurrentBehavior,
        SetCurrentBehaviorAck,
        SetName,
        SetNameAck,
        PowerOperation,
        ExitValidation,
        TransferInstantAnimationSet,
        TransferInstantAnimationSetAck,
        TransferInstantAnimationSetFinished,
        PlayInstantAnimation,
        StopAllAnimations,
        RequestTemperature,
        Temperature,
        SetBatteryControllerMode,
        _unused4,
        Discharge,
        BlinkId,
        BlinkIdAck,
        TransferTest,
        TransferTestAck,
        TransferTestFinished,
        ClearSettings,
        ClearSettingsAck,
    }
}
