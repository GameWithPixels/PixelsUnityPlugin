using System.Collections;
using Systemic.Unity.Pixels.Messages;
using UnityEngine;

namespace Systemic.Unity.Pixels
{
    partial class Pixel
    {
        public const float AckMessageTimeout = 5;

        #region Message Infrastructure

        void AddMessageHandler(MessageType msgType, MessageReceivedEvent newDel)
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

        void RemoveMessageHandler(MessageType msgType, MessageReceivedEvent newDel)
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
            where T : IPixelMessage
        {
            EnsureRunningOnMainThread();

            Debug.Log($"Pixel {SafeName}: Posting message of type {message.GetType()}");

            StartCoroutine(WriteDataAsync(PixelMessageMarshaling.ToByteArray(message)));
        }

        #endregion

        public void PlayAnimation(int animationIndex)
        {
            PostMessage(new PlayAnimation() { index = (byte)animationIndex });
        }

        public void PlayAnimation(int animationIndex, int remapFace, bool loop)
        {
            PostMessage(new PlayAnimation()
            {
                index = (byte)animationIndex,
                remapFace = (byte)remapFace,
                loop = loop ? (byte)1 : (byte)0
            });
        }

        public void StopAnimation(int animationIndex, int remapIndex)
        {
            PostMessage(new StopAnimation()
            {
                index = (byte)animationIndex,
                remapFace = (byte)remapIndex,
            });
        }

        public void StartAttractMode()
        {
            PostMessage(new AttractMode());
        }

        public IEnumerator GetDieStateAsync(PixelOperationResultHandler<bool> onResult = null)
        {
            var op = new SendMessageAndWaitForResponseEnumerator<RequestState, RollState>(this);
            yield return op;
            onResult?.Invoke(op.IsSuccess, op.Error);
        }

        public IEnumerator GetDieInfoAsync(PixelOperationResultHandler<bool> onResult = null)
        {
            var op = new SendMessageAndWaitForResponseEnumerator<WhoAreYou, IAmADie>(this);
            yield return op;
            onResult?.Invoke(op.IsSuccess, op.Error);
        }

        public void RequestTelemetry(bool on)
        {
            PostMessage(new RequestTelemetry() { telemetry = on ? (byte)1 : (byte)0 });
        }

        public void RequestBulkData()
        {
            PostMessage(new TestBulkSend());
        }

        public void PrepareBulkData()
        {
            PostMessage(new TestBulkReceive());
        }

        public void SetLEDsToRandomColor()
        {
            var msg = new SetAllLEDsToColor();
            uint r = (byte)Random.Range(0, 256);
            uint g = (byte)Random.Range(0, 256);
            uint b = (byte)Random.Range(0, 256);
            msg.color = (r << 16) + (g << 8) + b;
            PostMessage(msg);
        }

        public void SetLEDsToColor(Color color)
        {
            Color32 color32 = color;
            PostMessage(new SetAllLEDsToColor
            {
                color = (uint)((color32.r << 16) + (color32.g << 8) + color32.b)
            });
        }

        public IEnumerator UpdateBatteryLevelAsync(PixelOperationResultHandler<bool> onResult = null)
        {
            var op = new SendMessageAndProcessResponseWithValue<RequestBatteryLevel, BatteryLevel, float>(this,
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

        public IEnumerator UpdateRssiAsync(PixelOperationResultHandler<bool> onResult = null)
        {
            var op = new SendMessageAndProcessResponseWithValue<RequestRssi, Rssi, int>(this,
                rssiMsg =>
                {
                    rssi = rssiMsg.rssi;
                    RssiChanged?.Invoke(this, rssiMsg.rssi);
                    return rssiMsg.rssi;
                });
            yield return op;
            onResult?.Invoke(op.IsSuccess, op.Error);
        }

        public IEnumerator SetCurrentDesignAndColorAsync(PixelDesignAndColor design, PixelOperationResultHandler<bool> onResult = null)
        {
            var op = new SendMessageAndProcessResponseEnumerator<SetDesignAndColor, Rssi>(this,
                new SetDesignAndColor() { designAndColor = design },
                _ =>
                {
                    designAndColor = design;
                    AppearanceChanged?.Invoke(this, faceCount, designAndColor);
                });
            yield return op;
            onResult?.Invoke(op.IsSuccess, op.Error);
        }

        public IEnumerator RenameDieAsync(string newName, PixelOperationResultHandler<bool> onResult = null)
        {
            Debug.Log($"Pixel {SafeName}: Renaming to " + newName);

            byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(newName + "\0");
            byte[] nameByte10 = new byte[10]; // 10 is the declared size in DieMessageSetName. There is probably a better way to do this...
            System.Array.Copy(nameBytes, nameByte10, nameBytes.Length);

            var op = new SendMessageAndWaitForResponseEnumerator<SetName, SetNameAck>(this, new SetName { name = nameByte10 });
            yield return op;
            onResult?.Invoke(op.IsSuccess, op.Error);
        }

        public IEnumerator BlinkAsync(Color color, int count, PixelOperationResultHandler<bool> onResult = null)
        {
            Color32 color32 = color;
            var msg = new Blink
            {
                color = (uint)((color32.r << 16) + (color32.g << 8) + color32.b),
                flashCount = (byte)count,
            };
            var op = new SendMessageAndWaitForResponseEnumerator<Blink, BlinkFinished>(this, msg);
            yield return op;
            onResult?.Invoke(op.IsSuccess, op.Error);
        }

        public void StartHardwareTest()
        {
            PostMessage(new TestHardware());
        }

        public void StartCalibration()
        {
            PostMessage(new Calibrate());
        }

        public void CalibrateFace(int face)
        {
            PostMessage(new CalibrateFace() { face = (byte)face });
        }

        public void SetStandardMode()
        {
            PostMessage(new SetStandardState());
        }

        public void SetLEDAnimatorMode()
        {
            PostMessage(new SetLEDAnimState());
        }

        public void SetBattleMode()
        {
            PostMessage(new SetBattleState());
        }

        public void DebugAnimController()
        {
            PostMessage(new DebugAnimationController());
        }

        public IEnumerator PrintNormalsAsync()
        {
            for (int i = 0; i < 20; ++i)
            {
                var msg = new PrintNormals { face = (byte)i };
                PostMessage(msg);
                yield return new WaitForSeconds(0.5f);
            }
        }

        public void ResetParams()
        {
            PostMessage(new ProgramDefaultParameters());
        }

        #region MessageHandlers

        void OnIAmADieMessage(IPixelMessage message)
        {
            var idMsg = (IAmADie)message;
            bool appearanceChanged = faceCount != idMsg.faceCount || designAndColor != idMsg.designAndColor;
            faceCount = idMsg.faceCount;
            designAndColor = idMsg.designAndColor;
            dataSetHash = idMsg.dataSetHash;
            flashSize = idMsg.flashSize;
            firmwareVersionId = idMsg.versionInfo;
            Debug.Log($"Pixel {SafeName}: {flashSize} bytes available for data, current dataset hash {dataSetHash:X08}, firmware version is {firmwareVersionId}");
            if (appearanceChanged)
            {
                AppearanceChanged?.Invoke(this, faceCount, designAndColor);
            }
        }

        void OnRollStateMessage(IPixelMessage message)
        {
            // Handle the message
            var stateMsg = (RollState)message;
            Debug.Log($"Pixel {SafeName}: State is {stateMsg.state}, {stateMsg.face}");

            var newState = (PixelRollState)stateMsg.state;
            var newFace = stateMsg.face;
            if (newState != rollState || newFace != face)
            {
                rollState = newState;
                face = newFace;

                // Notify anyone who cares
                StateChanged?.Invoke(this, rollState, face);
            }
        }

        void OnTelemetryMessage(IPixelMessage message)
        {
            // Don't bother doing anything with the message if we don't have
            // anybody interested in telemetry data.
            if (_TelemetryReceived != null)
            {
                // Notify anyone who cares
                var telem = (AccelerationState)message;
                _TelemetryReceived.Invoke(this, telem.data);
            }
        }

        void OnDebugLogMessage(IPixelMessage message)
        {
            var dlm = (DebugLog)message;
            string text = System.Text.Encoding.UTF8.GetString(dlm.data, 0, dlm.data.Length);
            Debug.Log($"Pixel {SafeName}: {text}");
        }

        void OnNotifyUserMessage(IPixelMessage message)
        {
            var notifyUserMsg = (NotifyUser)message;
            //bool ok = notifyUserMsg.ok != 0;
            bool cancel = notifyUserMsg.cancel != 0;
            //float timeout = notifyUserMsg.timeout_s;
            string text = System.Text.Encoding.UTF8.GetString(notifyUserMsg.data, 0, notifyUserMsg.data.Length);
            NotifyUserReceived(this, cancel, text,
                res => PostMessage(new NotifyUserAck() { okCancel = (byte)(res ? 1 : 0) }));
        }

        void OnPlayAudioClip(IPixelMessage message)
        {
            var playClipMessage = (PlaySound)message;
            PlayAudioClipReceived?.Invoke(this, (uint)playClipMessage.clipId);
        }

        #endregion
    }
}
