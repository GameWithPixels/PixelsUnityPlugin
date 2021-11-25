using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Systemic.Unity.Pixels.Messages
{
    public static class PixelMessageMarshaling
    {
        public const int maxDataSize = 100;

        public static IPixelMessage FromByteArray(byte[] data)
        {
            IPixelMessage ret = null;
            if (data.Length > 0)
            {
                MessageType type = (MessageType)data[0];
                switch (type)
                {
                    case MessageType.RollState:
                        ret = FromByteArray<DieMessageRollState>(data);
                        break;
                    case MessageType.WhoAreYou:
                        ret = FromByteArray<DieMessageWhoAreYou>(data);
                        break;
                    case MessageType.IAmADie:
                        {
                            var baseData = new byte[Marshal.SizeOf<DieMessageIAmADieMarshalledData>()];
                            if (data.Length > baseData.Length)
                            {
                                System.Array.Copy(data, baseData, baseData.Length);
                                var baseMsg = FromByteArray<DieMessageIAmADieMarshalledData>(baseData);
                                if (baseMsg != null)
                                {
                                    var strData = new byte[data.Length - baseData.Length - 1]; // It's ok if size is zero
                                    System.Array.Copy(data, baseData.Length, strData, 0, strData.Length);
                                    var str = Encoding.UTF8.GetString(strData);
                                    ret = new DieMessageIAmADie
                                    {
                                        faceCount = baseMsg.faceCount,
                                        designAndColor = baseMsg.designAndColor,
                                        padding = baseMsg.padding,
                                        dataSetHash = baseMsg.dataSetHash,
                                        deviceId = baseMsg.deviceId,
                                        flashSize = baseMsg.flashSize,
                                        versionInfo = str,
                                    };
                                }
                            }
                        }
                        break;
                    case MessageType.Telemetry:
                        ret = FromByteArray<DieMessageAcc>(data);
                        break;
                    case MessageType.BulkSetup:
                        ret = FromByteArray<DieMessageBulkSetup>(data);
                        break;
                    case MessageType.BulkData:
                        ret = FromByteArray<DieMessageBulkData>(data);
                        break;
                    case MessageType.BulkSetupAck:
                        ret = FromByteArray<DieMessageBulkSetupAck>(data);
                        break;
                    case MessageType.BulkDataAck:
                        ret = FromByteArray<DieMessageBulkDataAck>(data);
                        break;
                    case MessageType.TransferAnimSet:
                        ret = FromByteArray<DieMessageTransferAnimSet>(data);
                        break;
                    case MessageType.TransferAnimSetAck:
                        ret = FromByteArray<DieMessageTransferAnimSetAck>(data);
                        break;
                    case MessageType.TransferAnimSetFinished:
                        ret = FromByteArray<DieMessageTransferAnimSetFinished>(data);
                        break;
                    case MessageType.TransferTestAnimSet:
                        ret = FromByteArray<DieMessageTransferTestAnimSet>(data);
                        break;
                    case MessageType.TransferTestAnimSetAck:
                        ret = FromByteArray<DieMessageTransferTestAnimSetAck>(data);
                        break;
                    case MessageType.TransferTestAnimSetFinished:
                        ret = FromByteArray<DieMessageTransferTestAnimSetFinished>(data);
                        break;
                    case MessageType.TransferSettings:
                        ret = FromByteArray<DieMessageTransferSettings>(data);
                        break;
                    case MessageType.TransferSettingsAck:
                        ret = FromByteArray<DieMessageTransferSettingsAck>(data);
                        break;
                    case MessageType.TransferSettingsFinished:
                        ret = FromByteArray<DieMessageTransferSettingsFinished>(data);
                        break;
                    case MessageType.DebugLog:
                        ret = FromByteArray<DieMessageDebugLog>(data);
                        break;
                    case MessageType.PlayAnim:
                        ret = FromByteArray<DieMessagePlayAnim>(data);
                        break;
                    case MessageType.PlayAnimEvent:
                        ret = FromByteArray<DieMessagePlayAnimEvent>(data);
                        break;
                    case MessageType.PlaySound:
                        ret = FromByteArray<DieMessagePlaySound>(data);
                        break;
                    case MessageType.StopAnim:
                        ret = FromByteArray<DieMessageStopAnim>(data);
                        break;
                    case MessageType.RequestRollState:
                        ret = FromByteArray<DieMessageRequestState>(data);
                        break;
                    case MessageType.RequestAnimSet:
                        ret = FromByteArray<DieMessageRequestAnimSet>(data);
                        break;
                    case MessageType.RequestSettings:
                        ret = FromByteArray<DieMessageRequestSettings>(data);
                        break;
                    case MessageType.RequestTelemetry:
                        ret = FromByteArray<DieMessageRequestTelemetry>(data);
                        break;
                    case MessageType.FlashFinished:
                        ret = FromByteArray<DieMessageFlashFinished>(data);
                        break;
                    case MessageType.ProgramDefaultAnimSetFinished:
                        ret = FromByteArray<DieMessageProgramDefaultAnimSetFinished>(data);
                        break;
                    case MessageType.DefaultAnimSetColor:
                        ret = FromByteArray<DieMessageDefaultAnimSetColor>(data);
                        break;
                    case MessageType.BatteryLevel:
#if PLATFORM_ANDROID
                        var modifiedData = new byte[13];
                        modifiedData[0] = data[0];
                        System.Array.Copy(data, 1, modifiedData, 4, 9);
                        ret = FromByteArray<DieMessageBatteryLevel>(modifiedData);
#else
                        ret = FromByteArray<DieMessageBatteryLevel>(data);
#endif
                        break;
                    case MessageType.RequestBatteryLevel:
                        ret = FromByteArray<DieMessageRequestBatteryLevel>(data);
                        break;
                    case MessageType.RequestRssi:
                        ret = FromByteArray<DieMessageRequestRssi>(data);
                        break;
                    case MessageType.Rssi:
                        ret = FromByteArray<DieMessageRssi>(data);
                        break;
                    case MessageType.Calibrate:
                        ret = FromByteArray<DieMessageCalibrate>(data);
                        break;
                    case MessageType.CalibrateFace:
                        ret = FromByteArray<DieMessageCalibrateFace>(data);
                        break;
                    case MessageType.NotifyUser:
                        ret = FromByteArray<DieMessageNotifyUser>(data);
                        break;
                    case MessageType.NotifyUserAck:
                        ret = FromByteArray<DieMessageNotifyUserAck>(data);
                        break;
                    case MessageType.TestHardware:
                        ret = FromByteArray<DieMessageTestHardware>(data);
                        break;
                    case MessageType.SetStandardState:
                        ret = FromByteArray<DieMessageSetStandardState>(data);
                        break;
                    case MessageType.SetLEDAnimState:
                        ret = FromByteArray<DieMessageSetLEDAnimState>(data);
                        break;
                    case MessageType.SetBattleState:
                        ret = FromByteArray<DieMessageSetBattleState>(data);
                        break;
                    case MessageType.ProgramDefaultParameters:
                        ret = FromByteArray<DieMessageProgramDefaultParameters>(data);
                        break;
                    case MessageType.ProgramDefaultParametersFinished:
                        ret = FromByteArray<DieMessageProgramDefaultParametersFinished>(data);
                        break;
                    case MessageType.AttractMode:
                        ret = FromByteArray<DieMessageAttractMode>(data);
                        break;
                    case MessageType.PrintNormals:
                        ret = FromByteArray<DieMessagePrintNormals>(data);
                        break;
                    case MessageType.SetDesignAndColor:
                        ret = FromByteArray<DieMessageSetDesignAndColor>(data);
                        break;
                    case MessageType.SetDesignAndColorAck:
                        ret = FromByteArray<DieMessageSetDesignAndColorAck>(data);
                        break;
                    case MessageType.SetCurrentBehavior:
                        ret = FromByteArray<DieMessageSetCurrentBehavior>(data);
                        break;
                    case MessageType.SetCurrentBehaviorAck:
                        ret = FromByteArray<DieMessageSetCurrentBehaviorAck>(data);
                        break;
                    case MessageType.SetName:
                        ret = FromByteArray<DieMessageSetName>(data);
                        break;
                    case MessageType.SetNameAck:
                        ret = FromByteArray<DieMessageSetNameAck>(data);
                        break;
                    case MessageType.DebugAnimController:
                        ret = FromByteArray<DieMessageDebugAnimController>(data);
                        break;
                    default:
                        throw new System.Exception("Unhandled DieMessage type " + type.ToString() + " for marshaling");
                }
            }
            return ret;
        }

        static T FromByteArray<T>(byte[] data)
            where T : class, IPixelMessage
        {
            int size = Marshal.SizeOf<T>();
            if (data.Length == size)
            {
                System.IntPtr ptr = Marshal.AllocHGlobal(size);
                try
                {
                    Marshal.Copy(data, 0, ptr, size);
                    return (T)Marshal.PtrToStructure(ptr, typeof(T));
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }
            }
            else
            {
                Debug.LogError("Wrong message length for type " + typeof(T).Name);
                return null;
            }
        }

        public static byte[] ToByteArray<T>(T message)
            where T : IPixelMessage
        {
            int size = Marshal.SizeOf(typeof(T));
            System.IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(message, ptr, false);
            byte[] ret = new byte[size];
            Marshal.Copy(ptr, ret, 0, size);
            Marshal.FreeHGlobal(ptr);
            return ret;
        }

        static private Dictionary<System.Type, MessageType> _messageTypes = new Dictionary<System.Type, MessageType>();

        public static MessageType GetMessageType<T>()
            where T : IPixelMessage, new()
        {
            if (!_messageTypes.TryGetValue(typeof(T), out MessageType type))
            {
                type = (new T()).type;
                _messageTypes.Add(typeof(T), type);
            }
            return type;
        }
    }
}
