using System.Collections;
using Systemic.Unity.Pixels.Messages;
using UnityEngine;

namespace Systemic.Unity.Pixels
{
    partial class Pixel
    {
        public const float AckMessageTimeout = 5;

        #region Message Infrastructure

        void AddMessageHandler(MessageType msgType, MessageReceivedEventHandler newDel)
        {
            if (_messageDelegates.TryGetValue(msgType, out MessageReceivedEventHandler del))
            {
                del += newDel;
                _messageDelegates[msgType] = del;
            }
            else
            {
                _messageDelegates.Add(msgType, newDel);
            }
        }

        void RemoveMessageHandler(MessageType msgType, MessageReceivedEventHandler newDel)
        {
            if (_messageDelegates.TryGetValue(msgType, out MessageReceivedEventHandler del))
            {
                del -= newDel;
                if (del == null)
                {
                    _messageDelegates.Remove(msgType);
                }
                else
                {
                    _messageDelegates[msgType] = del;
                }
            }
        }

        void PostMessage<T>(T message)
            where T : IPixelMessage
        {
            EnsureRunningOnMainThread();

            Debug.Log($"Pixel {SafeName}: Posting message of type {message.GetType()}");

            StartCoroutine(SendMessageAsync(PixelMessageMarshaling.ToByteArray(message)));
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

        public IEnumerator GetDieStateAsync(OperationResultCallback<bool> onResult = null)
        {
            var op = new SendMessageAndWaitForResponseEnumerator<RequestState, RollState>(this);
            yield return op;
            onResult?.Invoke(op.IsSuccess, op.Error);
        }

        public IEnumerator GetDieInfoAsync(OperationResultCallback<bool> onResult = null)
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

        public IEnumerator UpdateBatteryLevelAsync(OperationResultCallback<bool> onResult = null)
        {
            var op = new SendMessageAndProcessResponseWithValue<RequestBatteryLevel, BatteryLevel, float>(this,
                lvlMsg =>
                {
                    bool charging = lvlMsg.charging != 0;
                    bool changed = (batteryLevel != lvlMsg.level) || (isCharging != charging);
                    batteryLevel = lvlMsg.level;
                    isCharging = charging;
                    if (changed)
                    {
                        BatteryLevelChanged?.Invoke(this, batteryLevel, isCharging);
                    }
                    return lvlMsg.level;
                });
            yield return op;
            onResult?.Invoke(op.IsSuccess, op.Error);
        }

        public IEnumerator UpdateRssiAsync(OperationResultCallback<bool> onResult = null)
        {
            var op = new SendMessageAndProcessResponseWithValue<RequestRssi, Rssi, int>(this,
                rssiMsg =>
                {
                    if (rssi != rssiMsg.rssi)
                    {
                        rssi = rssiMsg.rssi;
                        RssiChanged?.Invoke(this, rssi);
                    }
                    return rssiMsg.rssi;
                });
            yield return op;
            onResult?.Invoke(op.IsSuccess, op.Error);
        }

        public IEnumerator SetCurrentDesignAndColorAsync(PixelDesignAndColor design, OperationResultCallback<bool> onResult = null)
        {
            var op = new SendMessageAndProcessResponseEnumerator<SetDesignAndColor, Rssi>(this,
                new SetDesignAndColor() { designAndColor = design },
                _ =>
                {
                    if (designAndColor != design)
                    {
                        designAndColor = design;
                        AppearanceChanged?.Invoke(this, faceCount, designAndColor);
                    }
                });
            yield return op;
            onResult?.Invoke(op.IsSuccess, op.Error);
        }

        public IEnumerator RenameDieAsync(string newName, OperationResultCallback<bool> onResult = null)
        {
            Debug.Log($"Pixel {SafeName}: Renaming to " + newName);

            byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(newName + "\0");
            byte[] nameByteMaxSize = new byte[SetName.NameMaxSize];
            System.Array.Copy(nameBytes, nameByteMaxSize, nameBytes.Length);

            var op = new SendMessageAndWaitForResponseEnumerator<SetName, SetNameAck>(this, new SetName { name = nameByteMaxSize });
            yield return op;
            onResult?.Invoke(op.IsSuccess, op.Error);
        }

        public IEnumerator BlinkAsync(Color color, int count, OperationResultCallback<bool> onResult = null)
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

            var newState = stateMsg.state;
            var newFace = stateMsg.face;
            if (newState != rollState || newFace != face)
            {
                rollState = newState;
                face = newFace;

                // Notify anyone who cares
                RollStateChanged?.Invoke(this, rollState, face + 1);
            }
        }

        void OnTelemetryMessage(IPixelMessage message)
        {
            // Don't bother doing anything with the message if we don't have
            // anybody interested in telemetry data.
            if (_telemetryReceived != null)
            {
                // Notify anyone who cares
                var telem = (AccelerationState)message;
                _telemetryReceived.Invoke(this, telem.data);
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
            _notifyUser?.Invoke(this, text, cancel,
                res => PostMessage(new NotifyUserAck() { okCancel = (byte)(res ? 1 : 0) }));
        }

        void OnPlayAudioClip(IPixelMessage message)
        {
            var playClipMessage = (PlaySound)message;
            _playAudioClip?.Invoke(this, (uint)playClipMessage.clipId);
        }

        #endregion
    }
}
