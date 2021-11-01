#pragma once

namespace Pixels::CoreBluetoothLE
{
    using bluetooth_address_t = std::uint64_t;

    enum class BleRequestStatus
    {
        Success = 0,
        Error, // Generic error
        InProgress,
        Canceled,
        Disconnected, //TODO
        InvalidPeripheral,
        InvalidCall,
        InvalidParameters,
        NotSupported,
        ProtocolError,
        AccessDenied,
        AdpaterOff, //TODO
        Timeout,
    };

    enum class ConnectionEvent
    {
        Connecting,
        Connected,
        FailedToConnect, // + reason
        Ready,
        Disconnecting,
        Disconnected, // + reason
    };

    enum class ConnectionEventReason
    {
        Unknown = -1,
        Success = 0,
        Canceled,
        NotSupported,
        Timeout,
        LinkLoss,
        AdpaterOff,
        Peripheral, // Not supported in WinRT implementation
    };

    // Gatt errors
    //GattProtocolError.InvalidHandle = 1
    //GattProtocolError.ReadNotPermitted = 2
    //GattProtocolError.WriteNotPermitted = 3
    //GattProtocolError.InvalidPdu = 4
    //GattProtocolError.InsufficientAuthentication = 5
    //GattProtocolError.RequestNotSupported = 6
    //GattProtocolError.InvalidOffset = 7
    //GattProtocolError.InsufficientAuthorization = 8
    //GattProtocolError.PrepareQueueFull = 9
    //GattProtocolError.AttributeNotFound = 10
    //GattProtocolError.AttributeNotLong = 11
    //GattProtocolError.InsufficientEncryptionKeySize = 12
    //GattProtocolError.InvalidAttributeValueLength = 13
    //GattProtocolError.UnlikelyError = 14
    //GattProtocolError.InsufficientEncryption = 15
    //GattProtocolError.UnsupportedGroupType = 16
    //GattProtocolError.InsufficientResources = 17
}
