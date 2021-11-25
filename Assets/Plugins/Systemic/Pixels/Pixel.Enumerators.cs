using System.Collections;
using Systemic.Unity.Pixels.Messages;
using UnityEngine;

namespace Systemic.Unity.Pixels
{
    partial class Pixel
    {
        protected interface IOperationEnumerator : IEnumerator
        {
            bool IsDone { get; }

            bool IsTimeout { get; }

            bool IsSuccess { get; }

            string Error { get; }
        }

        protected class WaitForMessageEnumerator<T> : IOperationEnumerator
            where T : IPixelMessage, new()
        {
            readonly MessageType _msgType;
            readonly float _timeout;
            bool _isStarted;

            public bool IsDone => IsSuccess || (Error != null);

            public bool IsSuccess => Message != null;

            public bool IsTimeout { get; protected set; }

            public string Error { get; protected set; }

            public T Message { get; private set; }

            public object Current => null;

            protected Pixel Pixel { get; }

            protected bool IsDisposed { get; private set; }

            public WaitForMessageEnumerator(Pixel pixel, float timeoutSec = AckMessageTimeout)
            {
                if (timeoutSec <= 0) throw new System.ArgumentException("Timeout value must be greater than zero", nameof(timeoutSec));
                if (pixel == null) throw new System.ArgumentNullException(nameof(pixel));

                Pixel = pixel;
                _timeout = Time.realtimeSinceStartup + timeoutSec;
                _msgType = PixelMessageMarshaling.GetMessageType<T>();
            }

            public virtual bool MoveNext()
            {
                if (IsDisposed) throw new System.ObjectDisposedException(nameof(WaitForMessageEnumerator<T>));

                // Subscribe to our response message on first call
                if (!_isStarted)
                {
                    _isStarted = true;
                    Pixel.AddMessageHandler(_msgType, OnMessage);
                }

                if ((!IsSuccess) && (_timeout > 0) && (Error == null))
                {
                    // Update timeout
                    if (IsTimeout = (Time.realtimeSinceStartupAsDouble > _timeout))
                    {
                        Error = $"Timeout while waiting for message of type {typeof(T)}";
                    }
                }

                // Error might be set by child class
                bool done = IsSuccess || IsTimeout || (Error != null);
                if (done)
                {
                    // Unsubscribe from message notifications
                    Pixel.RemoveMessageHandler(_msgType, OnMessage);

                    if (IsSuccess)
                    {
                        if (Error != null)
                        {
                            // Some error occurred, we might have got an old message
                            // Forget message, this will make IsSuccess return false
                            Message = default;
                        }
                    }
                    else if (Error == null)
                    {
                        // Operation failed
                        Error = $"Unknown error while waiting for message of type {typeof(T)}";
                    }
                }
                return !done;
            }

            public void Reset()
            {
                // Not supported
            }

            void OnMessage(IPixelMessage msg)
            {
                Debug.Assert(msg is T);
                Message = (T)msg;
                Pixel.RemoveMessageHandler(_msgType, OnMessage);
            }
        }

        protected class SendMessageAndWaitForResponseEnumerator<TMsg, TResp> : WaitForMessageEnumerator<TResp>
            where TMsg : IPixelMessage, new()
            where TResp : IPixelMessage, new()
        {
            IOperationEnumerator _sendMessage;
            readonly System.Type _msgType;

            public SendMessageAndWaitForResponseEnumerator(Pixel pixel, TMsg message, float timeoutSec = AckMessageTimeout)
                : base(pixel, timeoutSec)
            {
                if (message == null) throw new System.ArgumentNullException(nameof(message));

                _msgType = message.GetType();
                _sendMessage = Pixel.WriteDataAsync(PixelMessageMarshaling.ToByteArray(message), timeoutSec);
            }

            public SendMessageAndWaitForResponseEnumerator(Pixel pixel, float timeoutSec = AckMessageTimeout)
                : this(pixel, new TMsg(), timeoutSec)
            {
            }

            public override bool MoveNext()
            {
                if (IsDisposed) throw new System.ObjectDisposedException(nameof(SendMessageAndWaitForResponseEnumerator<TMsg, TResp>));

                if ((_sendMessage != null) && (!_sendMessage.MoveNext()))
                {
                    if (!_sendMessage.IsSuccess)
                    {
                        if (_sendMessage.IsTimeout)
                        {
                            IsTimeout = true;
                            Error = $"Timeout while sending for message of type {typeof(TMsg)}";
                        }
                        else
                        {
                            // Done sending message
                            Error = $"Failed to send message of type {typeof(TMsg)}, {_sendMessage.Error}";
                        }
                    }
                    _sendMessage = null;
                }

                return base.MoveNext();
            }
        }

        class SendMessageAndProcessResponseEnumerator<TMsg, TResp> : SendMessageAndWaitForResponseEnumerator<TMsg, TResp>
            where TMsg : IPixelMessage, new()
            where TResp : IPixelMessage, new()
        {
            System.Action<TResp> _onResponse;

            public SendMessageAndProcessResponseEnumerator(Pixel pixel, TMsg message, System.Action<TResp> onResponse, float timeoutSec = AckMessageTimeout)
               : base(pixel, message, timeoutSec)
            {
                _onResponse = onResponse ?? throw new System.ArgumentNullException(nameof(onResponse));
            }

            public SendMessageAndProcessResponseEnumerator(Pixel pixel, System.Action<TResp> onResponse, float timeoutSec = AckMessageTimeout)
               : this(pixel, new TMsg(), onResponse, timeoutSec)
            {
            }

            public override bool MoveNext()
            {
                bool result = base.MoveNext();
                if (IsSuccess)
                {
                    _onResponse(Message);
                }
                return result;
            }
        }

        class SendMessageAndProcessResponseWithValue<TMsg, TResp, TValue> : SendMessageAndWaitForResponseEnumerator<TMsg, TResp>
            where TMsg : IPixelMessage, new()
            where TResp : IPixelMessage, new()
        {
            System.Func<TResp, TValue> _onResponse;

            public TValue Value { get; private set; }

            public SendMessageAndProcessResponseWithValue(Pixel pixel, TMsg message, System.Func<TResp, TValue> onResponse, float timeoutSec = AckMessageTimeout)
               : base(pixel, message, timeoutSec)
            {
                _onResponse = onResponse ?? throw new System.ArgumentNullException(nameof(onResponse)); ;
            }

            public SendMessageAndProcessResponseWithValue(Pixel pixel, System.Func<TResp, TValue> onResponse, float timeoutSec = AckMessageTimeout)
                : this(pixel, new TMsg(), onResponse, timeoutSec)
            {
            }

            public override bool MoveNext()
            {
                bool result = base.MoveNext();
                if (IsSuccess)
                {
                    Value = _onResponse(Message);
                }
                return result;
            }
        }
    }
}
