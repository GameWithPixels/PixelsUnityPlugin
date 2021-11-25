using System.Collections.Generic;
using Systemic.Unity.Pixels.Messages;
using UnityEngine;

/// <summary>
/// A collection of C# classes for the Unity game engine that enables communications with Pixels dice.
/// </summary>
//! @ingroup Unity_CSharp
namespace Systemic.Unity.Pixels
{
    public abstract partial class Pixel
        : MonoBehaviour
    {
        PixelConnectionState _connectionState = PixelConnectionState.Invalid; // Use property to change value
        public PixelConnectionState connectionState
        {
            get => _connectionState;
            protected set
            {
                EnsureRunningOnMainThread();

                Debug.Assert(System.Threading.Thread.CurrentThread.ManagedThreadId == 1);
                if (value != _connectionState)
                {
                    Debug.Log($"Pixel {SafeName}: Connection state change, {_connectionState} => {value}");
                    var oldState = _connectionState;
                    _connectionState = value;
                    ConnectionStateChanged?.Invoke(this, oldState, value);
                }
            }
        }

        public bool isConnectingOrReady => (_connectionState == PixelConnectionState.Connecting)
                || (_connectionState == PixelConnectionState.Identifying)
                || (_connectionState == PixelConnectionState.Ready);

        public PixelLastError lastError { get; protected set; } = PixelLastError.None;

        // name is stored on the gameObject itself

        public string systemId { get; protected set; }

        public int faceCount { get; protected set; } = 0;

        public PixelDesignAndColor designAndColor { get; protected set; } = PixelDesignAndColor.Unknown;

        public string firmwareVersionId { get; protected set; } = "Unknown";

        public uint dataSetHash { get; protected set; } = 0;

        public uint flashSize { get; protected set; } = 0;

        public PixelRollState state { get; protected set; } = PixelRollState.Unknown;

        public int face { get; protected set; } = -1;

        public float? batteryLevel { get; protected set; } = null;

        public bool? charging { get; protected set; } = null;

        public int? rssi { get; protected set; } = null;

        public delegate void TelemetryEvent(Pixel pixel, AccelFrame frame);

        public TelemetryEvent _TelemetryReceived;
        public event TelemetryEvent TelemetryReceived
        {
            add
            {
                if (_TelemetryReceived == null)
                {
                    // The first time around, we make sure to request telemetry from the Pixel
                    RequestTelemetry(true);
                }
                _TelemetryReceived += value;
            }
            remove
            {
                _TelemetryReceived -= value;
                if (_TelemetryReceived == null || _TelemetryReceived.GetInvocationList().Length == 0)
                {
                    if (connectionState == PixelConnectionState.Ready)
                    {
                        // Unregister from the Pixel telemetry
                        RequestTelemetry(false);
                    }
                    // Otherwise we can't send bluetooth packets to the Pixel, can we?
                }
            }
        }

        public delegate void StateChangedEvent(Pixel pixel, PixelRollState newState, int newFace);
        public StateChangedEvent StateChanged;

        public delegate void ConnectionStateChangedEvent(Pixel pixel, PixelConnectionState oldState, PixelConnectionState newState);
        public ConnectionStateChangedEvent ConnectionStateChanged;

        public delegate void ErrorEvent(Pixel pixel, PixelLastError error);
        public ErrorEvent GotError;

        public delegate void AppearanceChangedEvent(Pixel pixel, int newFaceCount, PixelDesignAndColor newDesign);
        public AppearanceChangedEvent AppearanceChanged;

        public delegate void BatteryLevelChangedEvent(Pixel pixel, float? level, bool? charging);
        public BatteryLevelChangedEvent BatteryLevelChanged;

        public delegate void RssiChangedEvent(Pixel pixel, int? rssi);
        public RssiChangedEvent RssiChanged;

        public delegate void NotifyUserReceivedEvent(Pixel pixel, bool cancel, string text, System.Action<bool> ackCallback);
        public NotifyUserReceivedEvent NotifyUserReceived;

        public delegate void PlayAudioClipReceivedEvent(Pixel pixel, uint clipId);
        public PlayAudioClipReceivedEvent PlayAudioClipReceived;

        // Internal delegate per message type
        protected delegate void MessageReceivedEvent(IPixelMessage msg);
        protected Dictionary<MessageType, MessageReceivedEvent> messageDelegates;

        void Awake()
        {
            messageDelegates = new Dictionary<MessageType, MessageReceivedEvent>();

            // Setup delegates for face and telemetry
            messageDelegates.Add(MessageType.IAmADie, OnIAmADieMessage);
            messageDelegates.Add(MessageType.RollState, OnRollStateMessage);
            messageDelegates.Add(MessageType.Telemetry, OnTelemetryMessage);
            messageDelegates.Add(MessageType.DebugLog, OnDebugLogMessage);
            messageDelegates.Add(MessageType.NotifyUser, OnNotifyUserMessage);
            messageDelegates.Add(MessageType.PlaySound, OnPlayAudioClip);
        }

        protected abstract IOperationEnumerator WriteDataAsync(byte[] bytes, float timeout = 0);

        protected void EnsureRunningOnMainThread()
        {
            if (System.Threading.Thread.CurrentThread.ManagedThreadId != 1)
            {
                throw new System.InvalidOperationException($"Methods of type {GetType()} can only be called from the main thread");
            }
        }
    }
}