using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Dice
{
    public delegate void DieOperationResultHandler<T>(T result, string error);
    public delegate void DieOperationProgressHandler(float progress); // Value between 0 and 1

    public interface IDialogBox
    {
        bool ShowDialogBox(string title, string message, string okMessage = "Ok", string cancelMessage = null, System.Action<bool> closeAction = null);
    }

    public interface IAudioPlayer
    {
        void PlayAudioClip(uint clipId);
    }


    partial class Die
    {
        public const float AckMessageTimeout = 5;

        #region Message Infrastructure

        void AddMessageHandler(DieMessageType msgType, MessageReceivedEvent newDel)
        {
            if (messageDelegates.TryGetValue(msgType, out MessageReceivedEvent del))
            {
                del += newDel;
                messageDelegates[msgType] = del;
            }
            else
            {
                messageDelegates.Add(msgType, newDel);
            }
        }

        void RemoveMessageHandler(DieMessageType msgType, MessageReceivedEvent newDel)
        {
            if (messageDelegates.TryGetValue(msgType, out MessageReceivedEvent del))
            {
                del -= newDel;
                if (del == null)
                {
                    messageDelegates.Remove(msgType);
                }
                else
                {
                    messageDelegates[msgType] = del;
                }
            }
        }

        void PostMessage<T>(T message)
            where T : IDieMessage
        {
            EnsureRunningOnMainThread();

            Debug.Log($"Die {SafeName}: Posting message of type {message.GetType()}");

            StartCoroutine(WriteDataAsync(DieMessages.ToByteArray(message)));
        }

        #endregion

        public void PlayAnimation(int animationIndex)
        {
            PostMessage(new DieMessagePlayAnim() { index = (byte)animationIndex });
        }

        public void PlayAnimation(int animationIndex, int remapFace, bool loop)
        {
            PostMessage(new DieMessagePlayAnim()
            {
                index = (byte)animationIndex,
                remapFace = (byte)remapFace,
                loop = loop ? (byte)1 : (byte)0
            });
        }

        public void StopAnimation(int animationIndex, int remapIndex)
        {
            PostMessage(new DieMessageStopAnim()
            {
                index = (byte)animationIndex,
                remapFace = (byte)remapIndex,
            });
        }

        public void StartAttractMode()
        {
            PostMessage(new DieMessageAttractMode());
        }

        public IEnumerator GetDieStateAsync(DieOperationResultHandler<bool> onResult = null)
        {
            var op = new SendMessageAndWaitForResponseEnumerator<DieMessageRequestState, DieMessageState>(this);
            yield return op;
            onResult?.Invoke(op.IsSuccess, op.Error);
        }

        public IEnumerator GetDieInfoAsync(DieOperationResultHandler<bool> onResult = null)
        {
            var op = new SendMessageAndWaitForResponseEnumerator<DieMessageWhoAreYou, DieMessageIAmADie>(this);
            yield return op;
            onResult?.Invoke(op.IsSuccess, op.Error);
        }

        public void RequestTelemetry(bool on)
        {
            PostMessage(new DieMessageRequestTelemetry() { telemetry = on ? (byte)1 : (byte)0 });
        }

        public void RequestBulkData()
        {
            PostMessage(new DieMessageTestBulkSend());
        }

        public void PrepareBulkData()
        {
            PostMessage(new DieMessageTestBulkReceive());
        }

        public void SetLEDsToRandomColor()
        {
            var msg = new DieMessageSetAllLEDsToColor();
            uint r = (byte)Random.Range(0, 256);
            uint g = (byte)Random.Range(0, 256);
            uint b = (byte)Random.Range(0, 256);
            msg.color = (r << 16) + (g << 8) + b;
            PostMessage(msg);
        }

        public void SetLEDsToColor(Color color)
        {
            Color32 color32 = color;
            PostMessage(new DieMessageSetAllLEDsToColor
            {
                color = (uint)((color32.r << 16) + (color32.g << 8) + color32.b)
            });
        }

        public IEnumerator UpdateBatteryLevelAsync(DieOperationResultHandler<bool> onResult = null)
        {
            var op = new SendMessageAndProcessResponseWithValue<DieMessageRequestBatteryLevel, DieMessageBatteryLevel, float>(this,
                lvlMsg =>
                {
                    batteryLevel = lvlMsg.level;
                    charging = lvlMsg.charging != 0;
                    BatteryLevelChanged?.Invoke(this, lvlMsg.level, lvlMsg.charging != 0);
                    return lvlMsg.level;
                });
            yield return op;
            onResult?.Invoke(op.IsSuccess, op.Error);
        }

        public IEnumerator UpdateRssiAsync(DieOperationResultHandler<bool> onResult = null)
        {
            var op = new SendMessageAndProcessResponseWithValue<DieMessageRequestRssi, DieMessageRssi, int>(this,
                rssiMsg =>
                {
                    rssi = rssiMsg.rssi;
                    RssiChanged?.Invoke(this, rssiMsg.rssi);
                    return rssiMsg.rssi;
                });
            yield return op;
            onResult?.Invoke(op.IsSuccess, op.Error);
        }

        public IEnumerator SetCurrentDesignAndColorAsync(DieDesignAndColor design, DieOperationResultHandler<bool> onResult = null)
        {
            var op = new SendMessageAndProcessResponseEnumerator<DieMessageSetDesignAndColor, DieMessageRssi>(this,
                new DieMessageSetDesignAndColor() { designAndColor = design },
                _ =>
                {
                    designAndColor = design;
                    AppearanceChanged?.Invoke(this, faceCount, designAndColor);
                });
            yield return op;
            onResult?.Invoke(op.IsSuccess, op.Error);
        }

        public IEnumerator RenameDieAsync(string newName, DieOperationResultHandler<bool> onResult = null)
        {
            Debug.Log($"Die {SafeName}: Renaming to " + newName);

            byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(newName + "\0");
            byte[] nameByte10 = new byte[10]; // 10 is the declared size in DieMessageSetName. There is probably a better way to do this...
            System.Array.Copy(nameBytes, nameByte10, nameBytes.Length);

            var op = new SendMessageAndWaitForResponseEnumerator<DieMessageSetName, DieMessageSetNameAck>(this, new DieMessageSetName { name = nameByte10 });
            yield return op;
            onResult?.Invoke(op.IsSuccess, op.Error);
        }

        public IEnumerator BlinkAsync(Color color, int count, DieOperationResultHandler<bool> onResult = null)
        {
            Color32 color32 = color;
            var msg = new DieMessageFlash
            {
                color = (uint)((color32.r << 16) + (color32.g << 8) + color32.b),
                flashCount = (byte)count,
            };
            var op = new SendMessageAndWaitForResponseEnumerator<DieMessageFlash, DieMessageFlashFinished>(this, msg);
            yield return op;
            onResult?.Invoke(op.IsSuccess, op.Error);
        }

        public void StartHardwareTest()
        {
            PostMessage(new DieMessageTestHardware());
        }

        public void StartCalibration()
        {
            PostMessage(new DieMessageCalibrate());
        }

        public void CalibrateFace(int face)
        {
            PostMessage(new DieMessageCalibrateFace() { face = (byte)face });
        }

        public void SetStandardMode()
        {
            PostMessage(new DieMessageSetStandardState());
        }

        public void SetLEDAnimatorMode()
        {
            PostMessage(new DieMessageSetLEDAnimState());
        }

        public void SetBattleMode()
        {
            PostMessage(new DieMessageSetBattleState());
        }

        public void DebugAnimController()
        {
            PostMessage(new DieMessageDebugAnimController());
        }

        public IEnumerator PrintNormalsAsync()
        {
            for (int i = 0; i < 20; ++i)
            {
                var msg = new DieMessagePrintNormals { face = (byte)i };
                PostMessage(msg);
                yield return new WaitForSeconds(0.5f);
            }
        }

        public void ResetParams()
        {
            PostMessage(new DieMessageProgramDefaultParameters());
        }

        #region MessageHandlers

        void OnIAmADieMessage(IDieMessage message)
        {
            var idMsg = (DieMessageIAmADie)message;
            bool appearanceChanged = faceCount != idMsg.faceCount || designAndColor != idMsg.designAndColor;
            faceCount = idMsg.faceCount;
            designAndColor = idMsg.designAndColor;
            dataSetHash = idMsg.dataSetHash;
            flashSize = idMsg.flashSize;
            firmwareVersionId = idMsg.versionInfo;
            Debug.Log($"Die {SafeName}: {flashSize} bytes available for data, current dataset hash {dataSetHash:X08}, firmware version is {firmwareVersionId}");
            if (appearanceChanged)
            {
                AppearanceChanged?.Invoke(this, faceCount, designAndColor);
            }
        }

        void OnStateMessage(IDieMessage message)
        {
            // Handle the message
            var stateMsg = (DieMessageState)message;
            Debug.Log($"Die {SafeName}: State is {stateMsg.state}, {stateMsg.face}");

            var newState = (DieRollState)stateMsg.state;
            var newFace = stateMsg.face;
            if (newState != state || newFace != face)
            {
                state = newState;
                face = newFace;

                // Notify anyone who cares
                StateChanged?.Invoke(this, state, face);
            }
        }

        void OnTelemetryMessage(IDieMessage message)
        {
            // Don't bother doing anything with the message if we don't have
            // anybody interested in telemetry data.
            if (_TelemetryReceived != null)
            {
                // Notify anyone who cares
                var telem = (DieMessageAcc)message;
                _TelemetryReceived.Invoke(this, telem.data);
            }
        }

        void OnDebugLogMessage(IDieMessage message)
        {
            var dlm = (DieMessageDebugLog)message;
            string text = System.Text.Encoding.UTF8.GetString(dlm.data, 0, dlm.data.Length);
            Debug.Log($"Die {SafeName}: {text}");
        }

        void OnNotifyUserMessage(IDieMessage message)
        {
            var notifyUserMsg = (DieMessageNotifyUser)message;
            //bool ok = notifyUserMsg.ok != 0;
            bool cancel = notifyUserMsg.cancel != 0;
            //float timeout = notifyUserMsg.timeout_s;
            string text = System.Text.Encoding.UTF8.GetString(notifyUserMsg.data, 0, notifyUserMsg.data.Length);
            NotifyUserReceived(this, cancel, text,
                res => PostMessage(new DieMessageNotifyUserAck() { okCancel = (byte)(res ? 1 : 0) }));
        }

        void OnPlayAudioClip(IDieMessage message)
        {
            var playClipMessage = (DieMessagePlaySound)message;
            PlayAudioClipReceived?.Invoke(this, (uint)playClipMessage.clipId);
        }

        #endregion
    }
}
