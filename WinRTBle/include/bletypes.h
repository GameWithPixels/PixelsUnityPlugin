/**
 * @file
 * @brief Some types used across the Systemic::BluetoothLE library.
 */

#pragma once

 //! \defgroup WinRT_Cpp
 //! @brief Collection of C++ classes that provide a simplified access to Bluetooth Low Energy peripherals.

/**
 * @brief Collection of C++ classes that provide a simplified access to Bluetooth Low Energy peripherals.
 * @ingroup WinRT_Cpp
 */
namespace Systemic::BluetoothLE
{
    /// Type for a Bluetooth address.
    using bluetooth_address_t = std::uint64_t;

    /// Peripheral requests statuses.
    enum class BleRequestStatus
    {
        /// The request completed successfully.
        Success,

        /// The request completed with a non-specific error.
        Error,

        /// The request is still in progress.
        InProgress,

        /// The request was canceled.
        Canceled,

        /// The request was aborted because the peripheral got disconnected.
        Disconnected, //TODO

        /// The request did not run because the given peripheral is not valid.
        InvalidPeripheral,

        /// The request did not run because the operation is not valid or permitted.
        InvalidCall,

        /// The request did not run because some of its parameters are invalid.
        InvalidParameters,

        /// The request failed because of the operation is not supported by the peripheral.
        NotSupported,

        /// The request failed because of BLE protocol error.
        ProtocolError,

        /// The request failed because it was denied access.
        AccessDenied,

        /// The request failed because the Bluetooth radio is off.
        AdpaterOff, //TODO

        /// The request did not succeed after the timeout period.
        Timeout,
    };

    /// Peripheral connection events.
    enum class ConnectionEvent
    {
        /// Raised at the beginning of the connect sequence and is followed either by Connected or FailedToConnect.
        Connecting,

        /// Raised once the peripheral is connected, at which point service discovery is triggered.
        Connected,

        /// Raised when the peripheral fails to connect, the reason of failure is also given.
        FailedToConnect,

        /// Raised after a Connected event, once the required services have been discovered.
        Ready,

        /// Raised at the beginning of a user initiated disconnect.
        Disconnecting,

        /// Raised when the peripheral is disconnected, the reason for the disconnection is also given.
        Disconnected,
    };

    /// Peripheral connection event reasons.
    enum class ConnectionEventReason
    {
        /// The disconnect happened for an unknown reason.
        Unknown = -1,

        /// The disconnect was initiated by user.
        Success = 0,

        /// Connection attempt canceled by user.
        Canceled,

        /// Peripheral does not have all required services.
        NotSupported,

        /// Peripheral didn't responded in time.
        Timeout,

        /// Peripheral was disconnected while in "auto connect" mode.
        LinkLoss,

        /// The local device Bluetooth adapter is off.
        AdpaterOff,

        /// Disconnection was initiated by peripheral.
        Peripheral,
    };

    /// Standard BLE values for characteristic properties.
    enum class CharacteristicProperties
    {
        None = 0,

        /// Characteristic is broadcastable.
        Broadcast = 0x001,

        /// Characteristic is readable.
        Read = 0x002,

        /// Characteristic can be written without response.
        WriteWithoutResponse = 0x004,

        /// Characteristic can be written.
        Write = 0x008,

        /// Characteristic supports notification.
        Notify = 0x010,

        /// Characteristic supports indication.
        Indicate = 0x020,

        /// Characteristic supports write with signature.
        SignedWrite = 0x040,

        /// Characteristic has extended properties.
        ExtendedProperties = 0x080,

        /// Characteristic notification uses encryption.
        NotifyEncryptionRequired = 0x100,

        /// Characteristic indication uses encryption.
        IndicateEncryptionRequired = 0x200,
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
