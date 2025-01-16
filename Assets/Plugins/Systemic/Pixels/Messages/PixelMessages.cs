// Ignore Spelling: rgb mcu Loopback Ack accel

using System;
using System.Runtime.InteropServices;
using UnityEditor.PackageManager;
using UnityEditor.VersionControl;
using UnityEngine;

namespace Systemic.Unity.Pixels.Messages
{
    public interface IPixelMessage
    {
        MessageType type { get; set; }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class WhoAreYou : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.WhoAreYou;
    }

    // Supported chip models
    public enum PixelChipModel : byte
    {
        Unknown = 0,
        nRF52810
    };

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct VersionInfo
    {
        public byte chunkSize; // sizeof(VersionInfo);

        public ushort firmwareVersion; // From makefile
        public uint buildTimestamp;   // From makefile

        // Version of the settings default data and data structure
        public ushort settingsVersion;

        // API compatibility versions
        public ushort compatStandardApiVersion; // WhoAreYou, IAmADie, RollState, BatteryLevel, RequestRssi, Rssi, Blink, BlinkAck
        public ushort compatExtendedApiVersion; // Animations (including anim classes), profile
        public ushort compatManagementApiVersion; // The rest
    };

    public enum PixelRunMode : byte
    {
        User = 0,       // Die is in regular mode
        Validation,     // Validation mode, blinks ID, etc...
        Attract,        // Special logic for displays
        Count,
    };

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DieInfo
    {
        public byte chunkSize; // sizeof(DieInfo);

        public uint pixelId;  // A unique identifier
        public PixelChipModel chipModel;
        public PixelDieType dieType;
        public byte ledCount;  // Number of LEDs
        public PixelColorway colorway; // Physical look
        public PixelRunMode runMode;   // Validation or user or attract mode at the moment
    };

    // No need to make room for null terminator
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct MessageString
    {
        public char char0;
        public char char1;
        public char char2;
        public char char3;
        public char char4;
        public char char5;
        public char char6;
        public char char7;
        public char char8;
        public char char9;
        public char char10;
        public char char11;
        public char char12;
        public char char13;
        public char char14;
        public char char15;
        public char char16;
        public char char17;
        public char char18;
        public char char19;
        public char char20;
        public char char21;
        public char char22;
        public char char23;
        public char char24;
        public char char25;
        public char char26;
        public char char27;
        public char char28;
        public char char29;
        public char char30;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CustomDesignAndColorName
    {
        public byte chunkSize; // sizeof(CustomDesignAndColorName);

        // Set only when designAndColor is custom
        public MessageString name;
    };

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DieName
    {
        public byte chunkSize; // = sizeof(DieName);

        public MessageString name;
    };

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SettingsInfo
    {
        public byte chunkSize; // sizeof(SettingsInfo);

        public uint profileDataHash;
        public uint availableFlash;   // Amount of available flash to store data
        public uint totalUsableFlash; // Total amount of flash that can be used to store data
    };

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct StatusInfo
    {
        public byte chunkSize; // sizeof(StatusInfo);

        // Battery info
        public byte batteryLevelPercent;
        public PixelBatteryState batteryState;

        // Roll info
        public PixelRollState rollState;
        public byte rollFaceIndex; // This is the current face index
    };

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class IAmADie: IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.IAmADie;

        public VersionInfo versionInfo;
        public DieInfo dieInfo;
        public CustomDesignAndColorName customDesignAndColorName;
        public DieName dieName;
        public SettingsInfo settingsInfo;
        public StatusInfo statusInfo;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class IAmADieLegacy : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.IAmADie;

        public byte ledCount; // Which kind of dice this is
        public PixelColorway colorway;
        public PixelDieType dieType;
        public uint dataSetHash;
        public uint pixelId; // A unique identifier
        public ushort availableFlashSize; // Available flash memory size for storing settings
        public uint buildTimestamp; // Firmware build timestamp

        // Roll state
        public PixelRollState rollState;
        public byte rollFaceIndex;

        // Battery level
        public byte batteryLevelPercent;
        public PixelBatteryState batteryState;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class RollState : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.RollState;
        public PixelRollState state;
        public byte faceIndex;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class Telemetry : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.Telemetry;

        public short accXTimes1000;
        public short accYTimes1000;
        public short accZTimes1000;

        public int faceConfidenceTimes1000;

        public uint time;

        public PixelRollState rollState;

        public byte faceIndex;

        // Battery and power
        public byte batteryLevelPercent;
        public PixelBatteryState batteryState;
        public byte voltageTimes50;
        public byte vCoilTimes50;

        // RSSI
        public sbyte rssi;
        public byte channelIndex;

        // Temperature
        public short mcuTemperatureTimes100;
        public short batteryTemperatureTimes100;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class BulkSetup : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.BulkSetup;
        public short size;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class BulkSetupAck : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.BulkSetupAck;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class BulkData : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.BulkData;
        public byte size;
        public ushort offset;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = Marshaling.MaxDataSize)]
        public byte[] data;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class BulkDataAck : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.BulkDataAck;
        public ushort offset;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class TransferAnimationSet : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.TransferAnimationSet;
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
    public class TransferAnimationSetAck : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.TransferAnimationSetAck;
        public byte result;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class TransferAnimationSetFinished : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.TransferAnimationSetFinished;
    }

    public enum TransferTestAnimationSetAckType : byte
    {
        Download = 0,
        UpToDate,
        NoMemory
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class RequestAnimationSet : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.RequestAnimationSet;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class TransferSettings : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.TransferSettings;
        public byte count;
        public short totalAnimationByteSize;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class TransferSettingsAck : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.TransferSettingsAck;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class TransferSettingsFinished : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.TransferSettingsFinished;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class RequestSettings : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.RequestSettings;
    }

    public enum TelemetryRequestMode : byte
    {
        Off = 0,
        Once = 1,
        Repeat = 2,
    };

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class RequestTelemetry : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.RequestTelemetry;
        public TelemetryRequestMode requestMode;
        public ushort minInterval; // Milliseconds, 0 for no cap on rate
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DebugLog : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.DebugLog;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = Marshaling.MaxDataSize)]
        public byte[] data;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class PlayAnimation : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.PlayAnimation;
        public byte index;
        public byte remapFace;
        public byte loop;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class PlayAnimationEvent : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.PlayAnimationEvent;
        public byte @event;
        public byte remapFace;
        public byte loop;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class StopAnimation : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.StopAnimation;
        public byte index;
        public byte remapFace;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class RemoteAction : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.RemoteAction;
        public ushort actionId;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class RequestRollState : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.RequestRollState;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class ProgramDefaultAnimationSet : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.ProgramDefaultAnimationSet;
        public uint color;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class ProgramDefaultAnimationSetFinished : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.ProgramDefaultAnimationSetFinished;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class Blink : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.Blink;
        public byte flashCount;
        public ushort duration;
        public uint color;
        public uint faceMask;
        public byte fade;
        public byte loop;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class BlinkAck : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.BlinkAck;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class RequestDefaultAnimationSetColor : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.RequestDefaultAnimationSetColor;
    }


    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DefaultAnimationSetColor : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.DefaultAnimationSetColor;
        public uint color;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class RequestBatteryLevel : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.RequestBatteryLevel;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class BatteryLevel : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.BatteryLevel;
        public byte levelPercent;
        public PixelBatteryState batteryState;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class RequestRssi : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.RequestRssi;
        public TelemetryRequestMode requestMode;
        public ushort minInterval; // Milliseconds, 0 for no cap on rate
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class Rssi : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.Rssi;
        public sbyte value;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class Calibrate : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.Calibrate;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class CalibrateFace : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.CalibrateFace;
        public byte faceIndex;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class NotifyUser : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.NotifyUser;
        public byte timeout_s;
        public byte ok; // Boolean
        public byte cancel; // Boolean
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = Marshaling.MaxDataSize - 4)]
        public byte[] data;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class NotifyUserAck : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.NotifyUserAck;
        public byte okCancel; // Boolean
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class TestHardware : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.TestHardware;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class SetTopLevelState : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.SetTopLevelState;
        public byte state; // See TopLevelState enumeration
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class ProgramDefaultParameters : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.ProgramDefaultParameters;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class ProgramDefaultParametersFinished : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.ProgramDefaultParametersFinished;
    }

    public class SetDesignAndColor : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.SetDesignAndColor;
        public PixelDieType dieType;
        public PixelColorway colorway;
    };

    public class SetDesignAndColorAck : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.SetDesignAndColorAck;
    };

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class SetCurrentBehavior : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.SetCurrentBehavior;
        public byte currentBehaviorIndex;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class SetCurrentBehaviorAck : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.SetCurrentBehaviorAck;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class SetName : IPixelMessage
    {
        public const int NameMaxSize = 25; // Including zero terminating character

        public MessageType type { get; set; } = MessageType.SetName;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = NameMaxSize)]
        public byte[] name;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class SetNameAck : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.SetNameAck;
    }

    public enum PixelPowerOperation : byte
    {
        TurnOff = 0,
        Reset,
        Sleep,
    };

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class PowerOperation : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.PowerOperation;

        public PixelPowerOperation operation;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class ExitValidation : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.ExitValidation;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class TransferInstantAnimationSet : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.TransferInstantAnimationSet;

        public ushort paletteSize;
        public ushort rgbKeyFrameCount;
        public ushort rgbTrackCount;
        public ushort keyFrameCount;
        public ushort trackCount;

        public ushort animationCount;
        public ushort animationSize;

        public uint hash;
    }

    public enum TransferInstantAnimationSetAckType : byte
    {
        Download = 0,
        UpToDate,
        NoMemory
    };

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class TransferInstantAnimationSetAck : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.TransferInstantAnimationSetAck;

        public TransferInstantAnimationSetAckType ackType;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class TransferInstantAnimationSetFinished : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.TransferInstantAnimationSetFinished;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class PlayInstantAnimation : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.PlayInstantAnimation;

        public byte animation;
        public byte faceIndex;  // Assumes that an animation was made for face 20
        public byte loopCount;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class StopAllAnimations : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.StopAllAnimations;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class RequestTemperature : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.RequestTemperature;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class Temperature : IPixelMessage
    {
        public MessageType type { get; set; } = MessageType.Temperature;

        public short mcuTemperatureTimes100;
        public short batteryTemperatureTimes100;
    }
}

