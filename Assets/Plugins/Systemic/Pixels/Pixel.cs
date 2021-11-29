using System.Collections.Generic;
using Systemic.Unity.Pixels.Messages;
using UnityEngine;

/// <summary>
/// A collection of C# classes for the Unity game engine that enables communications with Pixels dice.
/// </summary>
//! @ingroup Unity_CSharp
namespace Systemic.Unity.Pixels
{
    /// <summary>
    /// Delegate for Pixel connection state events.
    /// </summary>
    /// <param name="pixel">The source of the event.</param>
    /// <param name="previousState">The previous connection state.</param>
    /// <param name="state">The current connection sate.</param>
    public delegate void ConnectionStateChangedEventHandler(Pixel pixel, PixelConnectionState previousState, PixelConnectionState state);

    /// <summary>
    /// Delegate for Pixel communication error events.
    /// </summary>
    /// <param name="pixel">The source of the event.</param>
    /// <param name="error">The type of error.</param>
    public delegate void ErrorRaisedEventHandler(Pixel pixel, PixelError error);

    /// <summary>
    /// Delegate for Pixel appearance setting changes.
    /// </summary>
    /// <param name="pixel">The source of the event.</param>
    /// <param name="faceCount"></param>
    /// <param name="design"></param>
    public delegate void AppearanceChangedEventHandler(Pixel pixel, int faceCount, PixelDesignAndColor design);

    /// <summary>
    /// Delegate for Pixel roll events.
    /// </summary>
    /// <param name="pixel">The source of the event.</param>
    /// <param name="rollState">The roll state.</param>
    /// <param name="face">The face index, when applicable (face number is index + 1).</param>
    public delegate void RollStateChangedEventHandler(Pixel pixel, PixelRollState rollState, int face);

    /// <summary>
    /// Delegate for Pixel battery level changes.
    /// </summary>
    /// <param name="pixel">The source of the event.</param>
    /// <param name="level">The latest battery level reported by the die, normalized between 0 and 1 included.</param>
    /// <param name="charging">Whether or not the battery is reported as charging.</param>
    public delegate void BatteryLevelChangedEventHandler(Pixel pixel, float batteryLevel, bool isCharging);

    /// <summary>
    /// Delegate for Pixel RSSI changes.
    /// </summary>
    /// <param name="pixel">The source of the event.</param>
    /// <param name="rssi">The latest RSSI reported by the die.</param>
    public delegate void RssiChangedEventHandler(Pixel pixel, int rssi);

    /// <summary>
    /// Delegate for Pixel telemetry events.
    /// </summary>
    /// <param name="pixel">The source of the event.</param>
    /// <param name="frame">The latest acceleration data reported by the die.</param>
    public delegate void TelemetryEventHandler(Pixel pixel, AccelFrame frame);

    /// <summary>
    /// Delegate for Pixel requests to notify user of some message, with the option to cancel the operation.
    /// </summary>
    /// <param name="pixel">The source of the event.</param>
    /// <param name="text">The text to display to the user.</param>
    /// <param name="canCancel">Whether the user may cancel the operation.</param>
    /// <param name="userActionCallback">The callback to run once the user has acknowledged the message.
    ///                                  False may be passed to cancel the operation when applicable.</param>
    public delegate void NotifyUserCallback(Pixel pixel, string text, bool canCancel, System.Action<bool> userActionCallback);

    /// <summary>
    /// Delegate for Pixel requests to play an audio clip.
    /// </summary>
    /// <param name="pixel">The source of the event.</param>
    /// <param name="clipId">The audio clip id to play.</param>
    public delegate void PlayAudioClipCallback(Pixel pixel, uint clipId);

    /// <summary>
    /// Represents a Pixel die.
    ///
    /// This abstract class does not implement a specific communication protocol with the dice, leaving the door
    /// open to have multiple implementations including a virtual die.
    /// Currently only Bluetooth communications are supported, see <see cref="DiceBag"/> to connect to and communicate
    /// with Bluetooth Low Energy Pixel dice.
    /// </summary>
    /// <remarks>
    /// The Pixel name is given by the parent class <see cref="MonoBehaviour"/> name property.
    /// </remarks>
    public abstract partial class Pixel : MonoBehaviour
    {
        //TODO upper case fields?

        // Use property to change value so it may properly raise the corresponding event
        PixelConnectionState _connectionState = PixelConnectionState.Invalid;

        TelemetryEventHandler _telemetryReceived;
        NotifyUserCallback _notifyUser;
        PlayAudioClipCallback _playAudioClip;

        #region Public properties

        /// <summary>
        /// Gets the connection state to the Pixel die.
        /// </summary>
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

        /// <summary>
        /// Indicates whether the connection state is set to available, meaning the Pixel die can be connected to.
        /// </summary>
        public bool isAvailable => _connectionState == PixelConnectionState.Available;

        /// <summary>
        /// Indicates whether the connection state is set to ready, meaning the Pixel is connected and ready to communicate.
        /// </summary>
        public bool isReady => _connectionState == PixelConnectionState.Ready;

        /// <summary>
        /// Get the last error that happened during communications with the Pixel.
        /// </summary>
        public PixelError lastError { get; protected set; }

        /// <summary>
        /// Gets the unique system id assigned to the Pixel. This value is platform specific and may change over long periods of time.
        /// </summary>
        public string systemId { get; protected set; }

        /// <summary>
        /// Gets the number of faces for the Pixel.
        /// </summary>
        public int faceCount { get; protected set; }

        /// <summary>
        /// Gets the Pixel combination of design and color.
        /// </summary>
        public PixelDesignAndColor designAndColor { get; protected set; } = PixelDesignAndColor.Unknown;

        /// <summary>
        /// Get the version id of the firmware running on the Pixel.
        /// </summary>
        public string firmwareVersionId { get; protected set; } = "Unknown";

        /// <summary>
        /// Get the hash value of the animation data loaded on the Pixel.
        /// </summary>
        public uint dataSetHash { get; protected set; }

        /// <summary>
        /// Get the size of memory that can be used to store animation data on the Pixel.
        /// </summary>
        public uint flashSize { get; protected set; }

        /// <summary>
        /// Gets the Pixel current roll state.
        /// </summary>
        public PixelRollState rollState { get; protected set; } = PixelRollState.Unknown;

        /// <summary>
        /// Gets Pixel the current face that is up.
        /// </summary>
        public int face { get; protected set; } = -1; //TODO change to face number rather than index

        /// <summary>
        /// Gets the Pixel last read battery level.
        /// The value is normalized between 0 and 1 included.
        /// </summary>
        public float batteryLevel { get; protected set; }

        /// <summary>
        /// Indicates whether or not the Pixel was last reported as charging.
        /// </summary>
        public bool isCharging { get; protected set; }

        /// <summary>
        /// Gets the Pixel last read RSSI value.
        /// </summary>
        public int rssi { get; protected set; }

        #endregion

        #region Public methods

        /// <summary>
        /// Subscribes to user notifications request send by the Pixel.
        /// 
        /// Replaces the callback passed in a previous call to this method.
        /// </summary>
        /// <param name="notifyUserCallback">The callback to run, pass null to unsubscribe.</param>
        public void SubscribeToUserNotifications(NotifyUserCallback notifyUserCallback)
        {
            _notifyUser = notifyUserCallback;
        }

        /// <summary>
        /// Subscribes to audio request send by the Pixel.
        /// 
        /// Replaces the callback passed in a previous call to this method.
        /// </summary>
        /// <param name="playAudioClipCallback">The callback to run, pass null to unsubscribe.</param>
        public void SubscribeToPlayAudioClip(PlayAudioClipCallback playAudioClipCallback)
        {
            _playAudioClip = playAudioClipCallback;
        }

        #endregion

        #region Public events

        /// <summary>
        /// Event raised when the Pixel connection state changes.
        /// </summary>
        public ConnectionStateChangedEventHandler ConnectionStateChanged;

        /// <summary>
        /// Event raised when communications with the Pixel encountered an error.
        /// </summary>
        public ErrorRaisedEventHandler ErrorRaised;

        /// <summary>
        /// Event raised when the Pixel appearance setting is changed.
        /// </summary>
        public AppearanceChangedEventHandler AppearanceChanged;

        /// <summary>
        /// Event raised when the Pixel roll state changes.
        /// </summary>
        public RollStateChangedEventHandler RollStateChanged;

        /// <summary>
        /// Event raised when the battery level reported by the Pixel changes.
        /// </summary>
        public BatteryLevelChangedEventHandler BatteryLevelChanged;

        /// <summary>
        /// Event raised when the RSSI value reported by the Pixel changes.
        /// </summary>
        public RssiChangedEventHandler RssiChanged;

        /// <summary>
        /// Event raised when telemetry data is received.
        /// </summary>
        public event TelemetryEventHandler TelemetryReceived
        {
            add
            {
                if (_telemetryReceived == null)
                {
                    // The first time around, we make sure to request telemetry from the Pixel
                    RequestTelemetry(true);
                }
                _telemetryReceived += value;
            }
            remove
            {
                _telemetryReceived -= value;
                if (_telemetryReceived == null || _telemetryReceived.GetInvocationList().Length == 0)
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

        #endregion

        #region Protected members

        /// <summary>
        /// Internal delegate per message type.
        /// </summary>
        /// <param name="message">The message object.</param>
        protected delegate void MessageReceivedEventHandler(IPixelMessage message);

        /// <summary>
        /// Maps a message type to the corresponding event handler.
        /// </summary>
        protected Dictionary<MessageType, MessageReceivedEventHandler> _messageDelegates;

        /// <summary>
        /// Abstract method to send a message to the Pixel.
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        protected abstract IOperationEnumerator SendMessageAsync(byte[] bytes, float timeout = 0);

        /// <summary>
        /// Helper to method to check if we are running on the main thread. Throws an exception if running on another thread.
        /// </summary>
        protected void EnsureRunningOnMainThread()
        {
            if (System.Threading.Thread.CurrentThread.ManagedThreadId != 1)
            {
                throw new System.InvalidOperationException($"Methods of type {GetType()} can only be called from the main thread");
            }
        }

        #endregion

        // Awake is called when the script instance is being loaded
        void Awake()
        {
            _messageDelegates = new Dictionary<MessageType, MessageReceivedEventHandler>();

            // Setup delegates for face and telemetry
            _messageDelegates.Add(MessageType.IAmADie, OnIAmADieMessage);
            _messageDelegates.Add(MessageType.RollState, OnRollStateMessage);
            _messageDelegates.Add(MessageType.Telemetry, OnTelemetryMessage);
            _messageDelegates.Add(MessageType.DebugLog, OnDebugLogMessage);
            _messageDelegates.Add(MessageType.NotifyUser, OnNotifyUserMessage);
            _messageDelegates.Add(MessageType.PlaySound, OnPlayAudioClip);
        }
    }
}