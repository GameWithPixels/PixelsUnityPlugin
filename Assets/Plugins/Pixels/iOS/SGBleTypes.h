/**
 * @file
 * @brief Definition of SGBleConnectionEvent and SGBleConnectionEventReason enumerations.
 */

/**
 * @defgroup Apple_Objective-C
 * @brief Collection of Objective-C classes that provide a simplified access to Bluetooth
 *        Low Energy peripherals.
 *
 * The Systemic BluetoohLE library for Apple device provides classes for scanning
 * Bluetooth Low Energy (BLE) peripherals, connecting to and communicating with them.
 * Those classes are entirely based on the Apple's Core Bluetooth framework and
 * are here to provide a simplified access to BLE peripherals.
 * 
 * CBCentralManagerDelegate: implementation of the SGBleCentralManagerDelegate protocol
 * that stores and notifies of discovered peripherals, notifies of host device Bluetooth
 * state changes and of peripherals connection events.
 * 
 * SGBlePeripheral: implementation of the CBPeripheralDelegate protocol that queues BLE
 *                  operations to perform on a peripheral.
 *
 * SGBleConnectionEvent Enumeration of peripheral connection events.
 *
 * SGBleConnectionEventReason Enumeration of peripheral connection event reasons.
 */

#import <Foundation/Foundation.h>

/**
 * @brief Peripheral connection events.
 * @ingroup Apple_Objective-C
 */
typedef NS_ENUM(NSInteger, SGBleConnectionEvent)
{
    /// Raised at the beginning of the connect sequence and is followed either by Connected or FailedToConnect.
    SGBleConnectionEventConnecting,

    /// Raised once the peripheral is connected, at which point service discovery is triggered.
    SGBleConnectionEventConnected,

    /// Raised when the peripheral fails to connect, the reason of failure is also given.
    SGBleConnectionEventFailedToConnect,

    /// Raised after a Connected event, once the required services have been discovered.
    SGBleConnectionEventReady,

    /// Raised at the beginning of a user initiated disconnect.
    SGBleConnectionEventDisconnecting,

    /// Raised when the peripheral is disconnected, the reason for the disconnection is also given.
    SGBleConnectionEventDisconnected,
};

/**
 * @brief Peripheral connection events reasons.
 * @ingroup Apple_Objective-C
 */
typedef NS_ENUM(NSInteger, SGBleConnectionEventReason) {
    /// The disconnect happened for an unknown reason.
    SGBleConnectionEventReasonUnknown = -1,

    /// The disconnect was initiated by user.
    SGBleConnectionEventReasonSuccess = 0,

    /// Connection attempt canceled by user.
    SGBleConnectionEventReasonCanceled,

    /// Peripheral does not have all required services.
    SGBleConnectionEventReasonNotSupported,

    /// Peripheral didn't responded in time.
    SGBleConnectionEventReasonTimeout,

    /// Peripheral was disconnected while in "auto connect" mode.
    SGBleConnectionEventReasonLinkLoss,

    /// The local device Bluetooth adapter is off.
    SGBleConnectionEventReasonAdpaterOff,
};

/**
 * @brief Peripheral discovery handler.
 * @ingroup Apple_Objective-C
 */
typedef void (^SGBlePeripheralDiscoveryHandler)(CBPeripheral *peripheral, NSDictionary<NSString *,id> *advertisementData, NSNumber *RSSI);

/**
 * @brief Peripheral connection event handler.
 * @ingroup Apple_Objective-C
 */
typedef void (^SGBleConnectionEventHandler)(CBPeripheral *peripheral, SGBleConnectionEvent connectionEvent, NSError *error);
