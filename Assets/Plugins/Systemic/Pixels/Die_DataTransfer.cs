using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using System.Linq;

namespace Dice
{
    partial class Die
    {
        protected string SafeName => this != null ? name : "(destroyed)";

        public IEnumerator UploadBulkDataAsync(byte[] bytes, DieOperationResultHandler<bool> onResult = null, DieOperationProgressHandler onProgress = null)
        {
            // Keep name locally in case our game object gets destroyed along the way
            string name = SafeName;

            short remainingSize = (short)bytes.Length;
            Debug.Log($"Die {name}: Sending {remainingSize} bytes of bulk data");
            onProgress?.Invoke(0);

            // Send setup message
            IOperationEnumerator sendMsg = new SendMessageAndWaitForResponseEnumerator<DieMessageBulkSetup, DieMessageBulkSetupAck>(this, new DieMessageBulkSetup { size = remainingSize });
            yield return sendMsg;

            if (sendMsg.IsSuccess)
            {
                Debug.Log($"Die {name}: die is ready, sending data");

                // Then transfer data
                ushort offset = 0;
                while (remainingSize > 0)
                {
                    var data = new DieMessageBulkData()
                    {
                        offset = offset,
                        size = (byte)Mathf.Min(remainingSize, DieMessages.maxDataSize),
                        data = new byte[DieMessages.maxDataSize],
                    };

                    System.Array.Copy(bytes, offset, data.data, 0, data.size);

                    //Debug.Log($"Die {name}: sending Bulk Data (offset: 0x" + data.offset.ToString("X") + ", length: " + data.size + ")");
                    //StringBuilder hexdumpBuilder = new StringBuilder();
                    //for (int i = 0; i < data.data.Length; ++i)
                    //{
                    //    if (i % 8 == 0)
                    //    {
                    //        hexdumpBuilder.AppendLine();
                    //    }
                    //    hexdumpBuilder.Append(data.data[i].ToString("X02") + " ");
                    //}
                    //Debug.Log(hexdumpBuilder.ToString());

                    sendMsg = new SendMessageAndWaitForResponseEnumerator<DieMessageBulkData, DieMessageBulkDataAck>(this, data);
                    yield return sendMsg;

                    if (sendMsg.IsSuccess)
                    {
                        remainingSize -= data.size;
                        offset += data.size;
                        onProgress?.Invoke((float)offset / bytes.Length);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            if (sendMsg.IsSuccess)
            {
                Debug.Log($"Die {name}: finished sending bulk data");
                onResult?.Invoke(true, null);
            }
            else
            {
                Debug.LogError($"Die {name}: failed to upload data, {sendMsg.Error}");
                onResult?.Invoke(false, sendMsg.Error);
            }
        }

        public IEnumerator DownloadBulkDataAsync(DieOperationResultHandler<byte[]> onResult = null, DieOperationProgressHandler onProgress = null)
        {
            // Keep name locally in case our game object gets destroyed along the way
            string name = SafeName;

            // Wait for setup message
            short size = 0;
            var waitForMsg = new WaitForMessageEnumerator<DieMessageBulkSetup>(this);
            yield return waitForMsg;

            byte[] buffer = null;
            string error = null;
            if (waitForMsg.IsSuccess)
            {
                size = waitForMsg.Message.size;
                float timeout = Time.realtimeSinceStartup + AckMessageTimeout;

                // Allocate a byte buffer
                buffer = new byte[size];
                ushort totalDataReceived = 0;

                // Setup bulk receive handler
                void bulkReceived(IDieMessage msg)
                {
                    timeout = Time.realtimeSinceStartup + AckMessageTimeout;

                    var bulkMsg = (DieMessageBulkData)msg;
                    if ((bulkMsg.offset + bulkMsg.size) < buffer.Length)
                    {
                        System.Array.Copy(bulkMsg.data, 0, buffer, bulkMsg.offset, bulkMsg.size);

                        // Sum data receive before sending any other message
                        totalDataReceived += bulkMsg.size;
                        onProgress?.Invoke((float)totalDataReceived / bulkMsg.size);

                        // Send acknowledgment (no need to do it synchronously)
                        PostMessage(new DieMessageBulkDataAck { offset = totalDataReceived });
                    }
                    else
                    {
                        Debug.LogError($"Die {name}: got bulk data from die that is too big");
                    }
                }

                AddMessageHandler(DieMessageType.BulkData, bulkReceived);
                try
                {
                    // Send acknowledgment to the die, so it may transfer bulk data immediately
                    PostMessage(new DieMessageBulkSetupAck());

                    // Wait for all the bulk data to be received
                    yield return new WaitUntil(() => (totalDataReceived == size) || (timeout > Time.realtimeSinceStartup));
                }
                finally
                {
                    // We're done
                    RemoveMessageHandler(DieMessageType.BulkData, bulkReceived);
                }

                if (totalDataReceived == size)
                {
                    Debug.Assert(error == null);
                    Debug.Log($"Die {name}: done downloading bulk data");
                }
                else
                {
                    error = "Incomplete upload";
                }
            }
            else
            {
                error = "Couldn't initiate bulk upload";
            }

            if (error != null)
            {
                onResult?.Invoke(buffer, null);
            }
            else
            {
                Debug.LogError($"Die {name}: error downloading bulk data, {error}");
                onResult?.Invoke(null, error);
            }
        }

        public IEnumerator UploadDataSetAsync(DataSet set, DieOperationResultHandler<bool> onResult = null, DieOperationProgressHandler onProgress = null)
        {
            // Keep name locally in case our game object gets destroyed along the way
            string name = SafeName;

            // Prepare the die
            var prepareDie = new DieMessageTransferAnimSet
            {
                paletteSize = set.animationBits.getPaletteSize(),
                rgbKeyFrameCount = set.animationBits.getRGBKeyframeCount(),
                rgbTrackCount = set.animationBits.getRGBTrackCount(),
                keyFrameCount = set.animationBits.getKeyframeCount(),
                trackCount = set.animationBits.getTrackCount(),
                animationCount = set.getAnimationCount(),
                animationSize = (ushort)set.animations.Sum((anim) => Marshal.SizeOf(anim.GetType())),
                conditionCount = set.getConditionCount(),
                conditionSize = (ushort)set.conditions.Sum((cond) => Marshal.SizeOf(cond.GetType())),
                actionCount = set.getActionCount(),
                actionSize = (ushort)set.actions.Sum((action) => Marshal.SizeOf(action.GetType())),
                ruleCount = set.getRuleCount(),
            };
            //StringBuilder builder = new StringBuilder();
            //builder.AppendLine("Animation Data to be sent:");
            //builder.AppendLine("palette: " + prepareDie.paletteSize * Marshal.SizeOf<byte>());
            //builder.AppendLine("rgb keyframes: " + prepareDie.rgbKeyFrameCount + " * " + Marshal.SizeOf<Animations.RGBKeyframe>());
            //builder.AppendLine("rgb tracks: " + prepareDie.rgbTrackCount + " * " + Marshal.SizeOf<Animations.RGBTrack>());
            //builder.AppendLine("keyframes: " + prepareDie.keyFrameCount + " * " + Marshal.SizeOf<Animations.Keyframe>());
            //builder.AppendLine("tracks: " + prepareDie.trackCount + " * " + Marshal.SizeOf<Animations.Track>());
            //builder.AppendLine("animations: " + prepareDie.animationCount + ", " + prepareDie.animationSize);
            //builder.AppendLine("conditions: " + prepareDie.conditionCount + ", " + prepareDie.conditionSize);
            //builder.AppendLine("actions: " + prepareDie.actionCount + ", " + prepareDie.actionSize);
            //builder.AppendLine("rules: " + prepareDie.ruleCount + " * " + Marshal.SizeOf<Behaviors.Rule>());
            //builder.AppendLine("behavior: " + Marshal.SizeOf<Behaviors.Behavior>());
            //Debug.Log(builder.ToString());
            //Debug.Log("Animation Data size: " + set.ComputeDataSetDataSize());

            var waitForMsg = new SendMessageAndWaitForResponseEnumerator<DieMessageTransferAnimSet, DieMessageTransferAnimSetAck>(this, prepareDie);
            yield return waitForMsg;

            string error = null;
            if (waitForMsg.IsSuccess)
            {
                if (waitForMsg.Message.result != 0)
                {
                    var setData = set.ToByteArray();
                    //StringBuilder hexdumpBuilder = new StringBuilder();
                    //for (int i = 0; i < setData.Length; ++i)
                    //{
                    //    if (i % 8 == 0)
                    //    {
                    //        hexdumpBuilder.AppendLine();
                    //    }
                    //    hexdumpBuilder.Append(setData[i].ToString("X02") + " ");
                    //}
                    //Debug.Log(hexdumpBuilder.ToString());

                    // Upload data
                    var hash = DataSet.ComputeHash(setData);
                    Debug.Log($"Die {name}: die is ready to receive dataset, byte array should be: {set.ComputeDataSetDataSize()} bytes and hash 0x{hash:X8}");
                    yield return InternalUploadDataSetAsync(DieMessageType.TransferAnimSetFinished, setData, err => error = err, onProgress);
                }
                else
                {
                    error = "Transfer refused, not enough memory";
                }
            }
            else
            {
                error = waitForMsg.Error;
            }

            if (error == null)
            {
                onResult?.Invoke(true, null);
            }
            else
            {
                Debug.LogError($"Die {name}: failed to upload data set, {error}");
                onResult?.Invoke(false, error);
            }
        }

        public IEnumerator PlayTestAnimationAsync(DataSet testAnimSet, DieOperationResultHandler<bool> onResult = null, DieOperationProgressHandler onProgress = null)
        {
            // Keep name locally in case our game object gets destroyed along the way
            string name = SafeName;

            // Prepare the die
            var prepareDie = new DieMessageTransferTestAnimSet
            {
                paletteSize = testAnimSet.animationBits.getPaletteSize(),
                rgbKeyFrameCount = testAnimSet.animationBits.getRGBKeyframeCount(),
                rgbTrackCount = testAnimSet.animationBits.getRGBTrackCount(),
                keyFrameCount = testAnimSet.animationBits.getKeyframeCount(),
                trackCount = testAnimSet.animationBits.getTrackCount(),
                animationSize = (ushort)Marshal.SizeOf(testAnimSet.animations[0].GetType()),
            };

            var setData = testAnimSet.ToTestAnimationByteArray();
            uint hash = DataSet.ComputeHash(setData);

            prepareDie.hash = hash;
            // Debug.Log("Animation Data to be sent:");
            // Debug.Log("palette: " + prepareDie.paletteSize * Marshal.SizeOf<byte>());
            // Debug.Log("rgb keyframes: " + prepareDie.rgbKeyFrameCount + " * " + Marshal.SizeOf<Animations.RGBKeyframe>());
            // Debug.Log("rgb tracks: " + prepareDie.rgbTrackCount + " * " + Marshal.SizeOf<Animations.RGBTrack>());
            // Debug.Log("keyframes: " + prepareDie.keyFrameCount + " * " + Marshal.SizeOf<Animations.Keyframe>());
            // Debug.Log("tracks: " + prepareDie.trackCount + " * " + Marshal.SizeOf<Animations.Track>());
            var waitForMsg = new SendMessageAndWaitForResponseEnumerator<DieMessageTransferTestAnimSet, DieMessageTransferTestAnimSetAck>(this, prepareDie);
            yield return waitForMsg;

            string error = null;
            if (waitForMsg.IsSuccess)
            {
                switch (waitForMsg.Message.ackType)
                {
                    case TransferTestAnimSetAckType.Download:
                        // Upload data
                        Debug.Log($"Die {name}: die is ready to receive test dataset, byte array should be: {setData.Length} bytes and hash 0x{hash:X8}");
                        yield return InternalUploadDataSetAsync(DieMessageType.TransferTestAnimSetFinished, setData, err => error = err, onProgress);
                        break;

                    case TransferTestAnimSetAckType.UpToDate:
                        // Nothing to do
                        Debug.Assert(error == null);
                        break;

                    default:
                        error = $"Got unknown ackType: {waitForMsg.Message.ackType}";
                        break;
                }
            }
            else
            {
                error = waitForMsg.Error;
            }

            if (error == null)
            {
                onResult?.Invoke(true, null);
            }
            else
            {
                Debug.LogError($"Die {name}: failed to play test animation, {error}");
                onResult?.Invoke(false, error);
            }
        }

        private IEnumerator InternalUploadDataSetAsync(DieMessageType transferDataFinished, byte[] data, System.Action<string> onResult, DieOperationProgressHandler onProgress = null)
        {
            // Keep name locally in case our game object gets destroyed along the way
            string name = SafeName;

            bool programmingFinished = false;
            void ProgrammingFinishedCallback(IDieMessage finishedMsg) => programmingFinished = true;

            AddMessageHandler(transferDataFinished, ProgrammingFinishedCallback);

            string error = null;
            try
            {
                bool success = false;
                yield return UploadBulkDataAsync(data, (res, err) => (success, error) = (res, err), onProgress);

                if (success)
                {
                    // We're done sending data, wait for the die to say its finished programming it!
                    Debug.Log($"Die {name}: done sending dataset, waiting for die to finish programming");
                    float timeout = Time.realtimeSinceStartup + AckMessageTimeout;
                    yield return new WaitUntil(() => programmingFinished || (Time.realtimeSinceStartup > timeout));

                    if (programmingFinished)
                    {
                        Debug.Assert(error == null);
                        Debug.Log($"Die {name}: programming done");
                    }
                    else
                    {
                        error = "Timeout waiting on die to confirm programming";
                    }
                }
            }
            finally
            {
                RemoveMessageHandler(transferDataFinished, ProgrammingFinishedCallback);
            }

            onResult(error);
        }

        //public IEnumerator UploadSettingsAsync(DieSettings settings, DieOperationResultHandler<bool> onResult = null, DieOperationProgressHandler onProgress = null)
        //{
        //    // Prepare the die
        //    var waitForMsg = new SendMessageAndWaitForResponseEnumerator<DieMessageTransferSettings, DieMessageTransferSettingsAck>(this);
        //    yield return waitForMsg;

        //    if (waitForMsg.IsSuccess)
        //    {
        //        // Die is ready, perform bulk transfer of the settings
        //        byte[] settingsBytes = DieSettings.ToByteArray(settings);
        //        yield return UploadBulkDataAsync(settingsBytes, onResult, onProgress);
        //    }
        //    else
        //    {
        //        onResult?.Invoke(false, waitForMsg.Error);
        //    }
        //}

        //public IEnumerator DownloadSettingsAsync(DieOperationResultHandler<DieSettings> onResult = null, DieOperationProgressHandler onProgress = null)
        //{
        //    // Request the settings from the die
        //    var waitForMsg = new SendMessageAndWaitForResponseEnumerator<DieMessageRequestSettings, DieMessageTransferSettings>(this);
        //    yield return waitForMsg;

        //    if (waitForMsg.IsSuccess)
        //    {
        //        // Got the message, acknowledge it
        //        PostMessage(new DieMessageTransferSettingsAck());

        //        string error = null;
        //        byte[] settingsBytes = null;
        //        yield return DownloadBulkDataAsync((buf, err) => (settingsBytes, error) = (buf, err));

        //        if (settingsBytes != null)
        //        {
        //            Debug.Assert(error == null);
        //            var newSettings = DieSettings.FromByteArray(settingsBytes);

        //            // We've read the settings
        //            onResult?.Invoke(newSettings, null);
        //        }
        //        else
        //        {
        //            onResult?.Invoke(null, error);
        //        }
        //    }
        //    else
        //    {
        //        onResult?.Invoke(null, waitForMsg.Error);
        //    }
        //}
    }
}
