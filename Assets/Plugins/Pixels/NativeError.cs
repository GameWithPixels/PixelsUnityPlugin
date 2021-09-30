
namespace Systemic.Pixels.Unity.BluetoothLE
{
    //enum AndroidBluetoothError
    //{
    //    REASON_DEVICE_DISCONNECTED = -1,
    //    REASON_DEVICE_NOT_SUPPORTED = -2,
    //    REASON_NULL_ATTRIBUTE = -3,
    //    REASON_REQUEST_FAILED = -4,
    //    REASON_TIMEOUT = -5,
    //    REASON_VALIDATION = -6,
    //    REASON_CANCELLED = -7,
    //    REASON_BLUETOOTH_DISABLED = -100,
    //}

    //enum AppleBluetoothError
    //{
    //    CBErrorUnknown = 0, // An unknown error occurred.
    //    CBErrorInvalidParameters = 1, // The specified parameters are invalid.
    //    CBErrorInvalidHandle = 2, // The specified attribute handle is invalid.
    //    CBErrorNotConnected = 3, // The device isn?t currently connected.
    //    CBErrorOutOfSpace = 4, // The device has run out of space to complete the intended operation.
    //    CBErrorOperationCancelled = 5, // The error represents a canceled operation.
    //    CBErrorConnectionTimeout = 6, // The connection timed out.
    //    CBErrorPeripheralDisconnected = 7, // The peripheral disconnected.
    //    CBErrorUUIDNotAllowed = 8, // The specified UUID isn?t permitted.
    //    CBErrorAlreadyAdvertising = 9, // The peripheral is already advertising.
    //    CBErrorConnectionFailed = 10, // The connection failed.
    //    CBErrorConnectionLimitReached = 11, // The device already has the maximum number of connections.
    //    CBErrorUnknownDevice = 12, // The device is unknown.
    //    CBErrorOperationNotSupported = 13, // The operation isn?t supported.

    //    CBATTErrorSuccess = 0x00, // The ATT command or request successfully completed.
    //    CBATTErrorInvalidHandle = 0x01, // The attribute handle is invalid on this peripheral.
    //    CBATTErrorReadNotPermitted = 0x02, // The permissions prohibit reading the attribute?s value.
    //    CBATTErrorWriteNotPermitted = 0x03, // The permissions prohibit writing the attribute?s value.
    //    CBATTErrorInvalidPdu = 0x04, // The attribute Protocol Data Unit (PDU) is invalid.
    //    CBATTErrorInsufficientAuthentication = 0x05, // Reading or writing the attribute?s value failed for lack of authentication.
    //    CBATTErrorRequestNotSupported = 0x06, // The attribute server doesn?t support the request received from the client.
    //    CBATTErrorInvalidOffset = 0x07, // The specified offset value was past the end of the attribute?s value.
    //    CBATTErrorInsufficientAuthorization = 0x08, // Reading or writing the attribute?s value failed for lack of authorization.
    //    CBATTErrorPrepareQueueFull = 0x09, // The prepare queue is full, as a result of there being too many write requests in the queue.
    //    CBATTErrorAttributeNotFound = 0x0A, // The attribute wasn?t found within the specified attribute handle range.
    //    CBATTErrorAttributeNotLong = 0x0B, // The ATT read blob request can?t read or write the attribute.
    //    CBATTErrorInsufficientEncryptionKeySize = 0x0C, // The encryption key size used for encrypting this link is insufficient.
    //    CBATTErrorInvalidAttributeValueLength = 0x0D, // The length of the attribute?s value is invalid for the intended operation.
    //    CBATTErrorUnlikelyError = 0x0E, // The ATT request encountered an unlikely error and wasn?t completed.
    //    CBATTErrorInsufficientEncryption = 0x0F, // Reading or writing the attribute?s value failed for lack of encryption.
    //    CBATTErrorUnsupportedGroupType = 0x10, // The attribute type isn?t a supported grouping attribute as defined by a higher-layer specification.
    //    CBATTErrorInsufficientResources = 0x11, // Resources are insufficient to complete the ATT request.
    //}

    //private final static String ERROR_CONNECTION_STATE_CHANGE = "Error on connection state change";
    //private final static String ERROR_DISCOVERY_SERVICE = "Error on discovering services";
    //private final static String ERROR_AUTH_ERROR_WHILE_BONDED = "Phone has lost bonding information";
    //private final static String ERROR_READ_CHARACTERISTIC = "Error on reading characteristic";
    //private final static String ERROR_WRITE_CHARACTERISTIC = "Error on writing characteristic";
    //private final static String ERROR_READ_DESCRIPTOR = "Error on reading descriptor";
    //private final static String ERROR_WRITE_DESCRIPTOR = "Error on writing descriptor";
    //private final static String ERROR_MTU_REQUEST = "Error on mtu request";
    //private final static String ERROR_CONNECTION_PRIORITY_REQUEST = "Error on connection priority request";
    //private final static String ERROR_READ_RSSI = "Error on RSSI read";
    //private final static String ERROR_READ_PHY = "Error on PHY read";
    //private final static String ERROR_PHY_UPDATE = "Error on PHY update";
    //private final static String ERROR_RELIABLE_WRITE = "Error on Execute Reliable Write";
    //private final static String ERROR_NOTIFY = "Error on sending notification/indication";

    //TODO need more errors, see above
    public enum Error
    {
        None = 0,
        NotSupported = 0xCCCCCC,
        Unknown = 0xDDDDDD,
    }

    public struct NativeError
    {
        public NativeError(int code, string message = null)
            => (Code, Message) = (code, message);

        public bool IsEmpty => Code == 0;

        public int Code { get; }

        public string Message { get; }

        public static readonly NativeError Empty = new NativeError();
    }
}
