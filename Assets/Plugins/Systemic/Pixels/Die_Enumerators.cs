using System.Collections;
using UnityEngine;

namespace Dice
{
    partial class Die
    {
        protected interface IOperationEnumerator : IEnumerator
        {
            bool IsDone { get; }

            bool IsTimeout { get; }

            bool IsSuccess { get; }

            string Error { get; }
        }

        protected class WaitForMessageEnumerator<T> : IOperationEnumerator
            where T : IDieMessage, new()
        {
            readonly DieMessageType _msgType;
            readonly float _timeout;
            bool _isStarted;

            public bool IsDone => IsSuccess || (Error != null);

            public bool IsSuccess => Message != null;

            public bool IsTimeout { get; protected set; }

            public string Error { get; protected set; }

            public T Message { get; private set; }

            public object Current => null;

            protected Die Die { get; }

            protected bool IsDisposed { get; private set; }

            public WaitForMessageEnumerator(Die die, float timeoutSec = AckMessageTimeout)
            {
                if (timeoutSec <= 0) throw new System.ArgumentException("Timeout value must be greater than zero", nameof(timeoutSec));
                if (die == null) throw new System.ArgumentNullException(nameof(die));

                Die = die;
                _timeout = Time.realtimeSinceStartup + timeoutSec;
                _msgType = DieMessages.GetMessageType<T>();
            }

            public virtual bool MoveNext()
            {
                if (IsDisposed) throw new System.ObjectDisposedException(nameof(WaitForMessageEnumerator<T>));

                // Subscribe to our response message on first call
                if (!_isStarted)
                {
                    _isStarted = true;
                    Die.AddMessageHandler(_msgType, OnMessage);
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
                    Die.RemoveMessageHandler(_msgType, OnMessage);

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

            void OnMessage(IDieMessage msg)
            {
                Debug.Assert(msg is T);
                Message = (T)msg;
                Die.RemoveMessageHandler(_msgType, OnMessage);
            }
        }

        protected class SendMessageAndWaitForResponseEnumerator<TMsg, TResp> : WaitForMessageEnumerator<TResp>
            where TMsg : IDieMessage, new()
            where TResp : IDieMessage, new()
        {
            IOperationEnumerator _sendMessage;
            readonly System.Type _msgType;

            public SendMessageAndWaitForResponseEnumerator(Die die, TMsg message, float timeoutSec = AckMessageTimeout)
                : base(die, timeoutSec)
            {
                if (message == null) throw new System.ArgumentNullException(nameof(message));

                _msgType = message.GetType();
                _sendMessage = Die.WriteDataAsync(DieMessages.ToByteArray(message), timeoutSec);
            }

            public SendMessageAndWaitForResponseEnumerator(Die die, float timeoutSec = AckMessageTimeout)
                : this(die, new TMsg(), timeoutSec)
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
            where TMsg : IDieMessage, new()
            where TResp : IDieMessage, new()
        {
            System.Action<TResp> _onResponse;

            public SendMessageAndProcessResponseEnumerator(Die die, TMsg message, System.Action<TResp> onResponse, float timeoutSec = AckMessageTimeout)
               : base(die, message, timeoutSec)
            {
                _onResponse = onResponse ?? throw new System.ArgumentNullException(nameof(onResponse));
            }

            public SendMessageAndProcessResponseEnumerator(Die die, System.Action<TResp> onResponse, float timeoutSec = AckMessageTimeout)
               : this(die, new TMsg(), onResponse, timeoutSec)
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
            where TMsg : IDieMessage, new()
            where TResp : IDieMessage, new()
        {
            System.Func<TResp, TValue> _onResponse;

            public TValue Value { get; private set; }

            public SendMessageAndProcessResponseWithValue(Die die, TMsg message, System.Func<TResp, TValue> onResponse, float timeoutSec = AckMessageTimeout)
               : base(die, message, timeoutSec)
            {
                _onResponse = onResponse ?? throw new System.ArgumentNullException(nameof(onResponse)); ;
            }

            public SendMessageAndProcessResponseWithValue(Die die, System.Func<TResp, TValue> onResponse, float timeoutSec = AckMessageTimeout)
                : this(die, new TMsg(), onResponse, timeoutSec)
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
