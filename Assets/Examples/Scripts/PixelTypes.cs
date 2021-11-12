
using System;

public enum PixelMessageType : byte
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

public enum PixelRollState : byte
{
    Unknown = 0,
    OnFace,
    Handling,
    Rolling,
    Crooked
};

public class PixelUuids
{
    public static readonly Guid ServiceUuid = new Guid("6e400001-b5a3-f393-e0a9-e50e24dcca9e");
    public static readonly Guid NotifyCharacteristicUuid = new Guid("6e400001-b5a3-f393-e0a9-e50e24dcca9e");
    public static readonly Guid WriteCharacteristicUuid = new Guid("6e400002-b5a3-f393-e0a9-e50e24dcca9e");
}