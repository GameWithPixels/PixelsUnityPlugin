using System.Runtime.InteropServices;

namespace Systemic.Unity.Pixels.Messages
{
    public interface IPixelMessage
    {
        MessageType type { get; set; }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageWhoAreYou
        : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.WhoAreYou;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageIAmADieMarshalledData
        : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.IAmADie;

        public byte faceCount; // Which kind of dice this is
        public DieDesignAndColor designAndColor; // Physical look
        public byte padding;
        public uint dataSetHash;
        public uint deviceId; // A unique identifier
        public ushort flashSize;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageIAmADie : DieMessageIAmADieMarshalledData
    {
        public string versionInfo; // Firmware version string, i.e. "10_05_21", variable size
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageRollState
        : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.RollState;
        public DieRollState state;
        public byte face;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageAcc
        : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.Telemetry;

        public AccelFrame data;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageBulkSetup
        : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.BulkSetup;
        public short size;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageBulkSetupAck
        : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.BulkSetupAck;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageBulkData
        : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.BulkData;
        public byte size;
        public ushort offset;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = PixelMessageMarshaling.maxDataSize)]
        public byte[] data;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageBulkDataAck
        : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.BulkDataAck;
        public ushort offset;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageTransferAnimSet
        : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.TransferAnimSet;
        public ushort paletteSize;
        public ushort rgbKeyFrameCount;
        public ushort rgbTrackCount;
        public ushort keyFrameCount;
        public ushort trackCount;
        public ushort animationCount;
        public ushort animationSize;
        public ushort conditionCount;
        public ushort conditionSize;
        public ushort actionCount;
        public ushort actionSize;
        public ushort ruleCount;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageTransferAnimSetAck
        : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.TransferAnimSetAck;
        public byte result;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageTransferAnimSetFinished
        : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.TransferAnimSetFinished;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageTransferTestAnimSet
        : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.TransferTestAnimSet;

        public ushort paletteSize;
        public ushort rgbKeyFrameCount;
        public ushort rgbTrackCount;
        public ushort keyFrameCount;
        public ushort trackCount;
        public ushort animationSize;
        public uint hash;
    }

    public enum TransferTestAnimSetAckType : byte
    {
        Download = 0,
        UpToDate,
        NoMemory
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageTransferTestAnimSetAck
        : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.TransferTestAnimSetAck;
        public TransferTestAnimSetAckType ackType;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageTransferTestAnimSetFinished
        : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.TransferTestAnimSetFinished;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageRequestAnimSet
        : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.RequestAnimSet;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageTransferSettings
        : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.TransferSettings;
        public byte count;
        public short totalAnimationByteSize;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageTransferSettingsAck
        : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.TransferSettingsAck;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageTransferSettingsFinished
        : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.TransferSettingsFinished;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageRequestSettings
        : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.RequestSettings;
    }


    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageRequestTelemetry
        : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.RequestTelemetry;
        public byte telemetry;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageDebugLog
        : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.DebugLog;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = PixelMessageMarshaling.maxDataSize)]
        public byte[] data;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessagePlayAnim
        : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.PlayAnim;
        public byte index;
        public byte remapFace;
        public byte loop;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessagePlayAnimEvent
        : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.PlayAnimEvent;
        public byte evt;
        public byte remapFace;
        public byte loop;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageStopAnim
        : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.StopAnim;
        public byte index;
        public byte remapFace;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessagePlaySound
        : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.PlaySound;
        public ushort clipId;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageRequestState
        : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.RequestRollState;
    }


    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageProgramDefaultAnimSet
        : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.ProgramDefaultAnimSet;
        public uint color;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageProgramDefaultAnimSetFinished
        : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.ProgramDefaultAnimSetFinished;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageFlash
        : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.Flash;
        public byte flashCount;
        public uint color;
    }


    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageFlashFinished
        : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.FlashFinished;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageRequestDefaultAnimSetColor
        : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.RequestDefaultAnimSetColor;
    }


    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageDefaultAnimSetColor
        : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.DefaultAnimSetColor;
        public uint color;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageTestBulkSend
        : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.TestBulkSend;
    }


    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageTestBulkReceive
        : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.TestBulkReceive;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageSetAllLEDsToColor
    : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.SetAllLEDsToColor;
        public uint color;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageBatteryLevel
    : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.BatteryLevel;
#if PLATFORM_ANDROID
        // We need padding on ARMv7 to have 4 bytes alignment for float types
        private byte _padding1, _padding2, _padding3;
#endif
        public float level;
        public float voltage;
        public byte charging;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageRequestBatteryLevel
    : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.RequestBatteryLevel;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageRssi
    : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.Rssi;
        public short rssi;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageRequestRssi
    : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.RequestRssi;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageCalibrate
    : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.Calibrate;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageCalibrateFace
    : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.CalibrateFace;
        public byte face;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageNotifyUser
    : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.NotifyUser;
        public byte timeout_s;
        public byte ok; // Boolean
        public byte cancel; // Boolean
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = PixelMessageMarshaling.maxDataSize - 4)]
        public byte[] data;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageNotifyUserAck
    : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.NotifyUserAck;
        public byte okCancel; // Boolean
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageTestHardware
    : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.TestHardware;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageSetStandardState
    : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.SetStandardState;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageSetLEDAnimState
    : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.SetLEDAnimState;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageSetBattleState
    : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.SetBattleState;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageProgramDefaultParameters
    : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.ProgramDefaultParameters;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageProgramDefaultParametersFinished
    : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.ProgramDefaultParametersFinished;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageAttractMode
    : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.AttractMode;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessagePrintNormals
    : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.PrintNormals;
        public byte face;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageSetDesignAndColor
    : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.SetDesignAndColor;
        public DieDesignAndColor designAndColor;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageSetDesignAndColorAck
    : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.SetDesignAndColorAck;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageSetCurrentBehavior
    : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.SetCurrentBehavior;
        public byte currentBehaviorIndex;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageSetCurrentBehaviorAck
    : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.SetCurrentBehaviorAck;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageSetName
    : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.SetName;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
        public byte[] name;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageSetNameAck
    : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.SetNameAck;
    }


    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DieMessageDebugAnimController
    : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.DebugAnimController;
    }
    
}

