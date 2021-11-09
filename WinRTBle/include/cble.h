/**
 * @file
 * @brief C library for discovering, connecting to, and interacting with Bluetooth Low Energy
 * (BLE) peripherals on Windows 10 and above.
 *
 * WinRT is used to access bluetooth and allows accessing devices without needing to first
 * add them in Windows' Bluetooth devices manager.
 * Requires at least Windows 10 version 1709 (Fall Creators Update).
 *
 * Those functions are thread safe.
 */

#pragma once

#include <cstdint>
#include "bletypes.h"

/** @cond */
#ifdef LIBWINRTBLE_EXPORTS
#define DLL_DECLSPEC __declspec(dllexport)
#else
#define DLL_DECLSPEC __declspec(dllimport)
#endif
/** @endcond */

/// Type for a Bluetooth address.
using bluetooth_address_t = Systemic::BluetoothLE::bluetooth_address_t;

/// Enumeration for peripheral requests statuses.
using BleRequestStatus = Systemic::BluetoothLE::BleRequestStatus;

/// Peripheral connection events.
using ConnectionEvent = Systemic::BluetoothLE::ConnectionEvent;

/// Peripheral connection event reasons.
using ConnectionEventReason = Systemic::BluetoothLE::ConnectionEventReason;

/// Characteristic properties (standard BLE values).
using CharacteristicProperties = Systemic::BluetoothLE::CharacteristicProperties;

/// Type for the index of a characteristic instance in a service.
using characteristic_index_t = std::uint32_t;

/// Callback notifying of a change of the host device Bluetooth state, for example radio turned on or off.
typedef void (*BluetoothStateUpdateCallback)(bool available);

/// Callback notifying of the discovery of a BLE peripheral, with its advertisement data as a JSON string.
typedef void (*DiscoveredPeripheralCallback)(const char* advertisementDataJson);

/// Callback notifying of the status of a BLE request.
typedef void (*RequestStatusCallback)(BleRequestStatus status);

/// Callback notifying of a change connection state of a peripheral, with the reason for the change.
typedef void (*PeripheralConnectionEventCallback)(bluetooth_address_t address, ConnectionEvent connectionEvent, ConnectionEventReason reason);

/// Callback notifying of the value read from a peripheral's characteristic.
typedef void (*ValueReadCallback)(const void* _data, size_t length, BleRequestStatus status);

/// Callback notifying of a value change for a peripheral's characteristic.
typedef void (*ValueChangedCallback)(const void* _data, size_t length);

extern "C"
{
    //! \name Library life cycle
    //! @{

    /**
     * @brief Initializes the library for accessing BLE peripherals.
     * 
     * @param apartmentSingleThreaded Whether to initialize COM apartment as single thread
     *                                or multi-threaded
     * @param onBluetoothEvent Called when the host device Bluetooth state changes.
     * @return Whether the initialization was successful.
     */
    /** @cond */ DLL_DECLSPEC /** @endcond */
    bool sgBleInitialize(bool apartmentSingleThreaded, BluetoothStateUpdateCallback onCentralStateUpdate);

    /**
     * @brief Shuts down the library.
     *
     * Scanning is stopped and all peripherals are disconnected and removed.
     */
    /** @cond */ DLL_DECLSPEC /** @endcond */
    void sgBleShutdown();

    //! @}
    //! \name Peripherals scanning
    //! @{

    /**
     * @brief Starts scanning for BLE peripherals advertising the given list of services.
     *
     * If a scan is already running, it is updated to run with the new parameters.
     * 
     * @param requiredServicesUuids Comma separated list of services UUIDs that the peripheral
     *                              should advertise, may be null or empty.
     * @param onDiscoveredPeripheral Called every time an advertisement packet with the required
     *                               services is received. <br>
     *                               The advertisement data is passed as a JSON string.
     *                               The callback must stay valid until the scan is stopped.
     * @return Whether the scan was successfully started.
     */
    /** @cond */ DLL_DECLSPEC /** @endcond */
    bool sgBleStartScan(
        const char* requiredServicesUuids,
        DiscoveredPeripheralCallback onDiscoveredPeripheral);

    /**
     * @brief Stops an on-going BLE scan.
     */
    /** @cond */ DLL_DECLSPEC /** @endcond */
    void sgBleStopScan();

    //! @}
    //! \name Peripherals life cycle
    //! @{

    /**
     * @brief Creates a Peripheral for the BLE peripheral with the given Bluetooth address.
     *
     * The underlying object is not returned, instead the peripheral must be referenced by
     * its Bluetooth address. Call sgBleReleasePeripheral() to destroy the object.
     *
     * Usually a scan is run first to discover available peripherals through their advertisement data.
     * But if the BLE address is know in advance, the peripheral may be created without scanning for it.
     *
     * @param address The Bluetooth address of the peripheral.
     * @param onPeripheralConnectionEvent Called when the peripheral connection state changes. <br>
     *                                    The callback must stay valid until the peripheral is released.
     * @return Whether the peripheral object was successfully created.
     */
    /** @cond */ DLL_DECLSPEC /** @endcond */
    bool sgBleCreatePeripheral(
        bluetooth_address_t address,
        PeripheralConnectionEventCallback onPeripheralConnectionEvent);

    /**
     * @brief Releases the Peripheral object associated with the given Bluetooth address.
     *
     * @param address The Bluetooth address of the peripheral.
     */
    /** @cond */ DLL_DECLSPEC /** @endcond */
    void sgBleReleasePeripheral(bluetooth_address_t address);

    //! @}
    //! \name Peripheral connection and disconnection
    //! @{

    /**
     * @brief Connects to the given peripheral.
     *
     * This request timeouts after 7 to 8 seconds, as of Windows 10 21H1.
     *
     * @note sgBleCreatePeripheral() must be called first.
     * 
     * @param address The Bluetooth address of the peripheral.
     * @param requiredServicesUuids Comma separated list of services UUIDs that the peripheral
     *                              should support, may be null or empty.
     * @param autoReconnect Whether to automatically reconnect after an unexpected disconnection
     *                      (i.e. not triggered by a call to sgBleDisconnectPeripheral()).
     * @param onRequestStatus Called when the request has completed (successfully or not).
     */
    /** @cond */ DLL_DECLSPEC /** @endcond */
    void sgBleConnectPeripheral(bluetooth_address_t address,
        const char* requiredServicesUuids,
        bool autoReconnect,
        RequestStatusCallback onRequestStatus);

    /**
     * @brief Immediately disconnects the given peripheral.
     *
     * As a consequence, any on-going request either fails or is canceled, including connection requests.
     *
     * @param address The Bluetooth address of the peripheral.
     * @param onRequestStatus Called when the request has completed (successfully or not).
     */
    /** @cond */ DLL_DECLSPEC /** @endcond */
    void sgBleDisconnectPeripheral(
        bluetooth_address_t address,
        RequestStatusCallback onRequestStatus);

    //! @}
    //! \name Peripheral operations
    //! Valid only for connected peripherals.
    //! @{

    /**
     * @brief Returns the name of the given peripheral.
     * 
     * @param address The Bluetooth address of a connected peripheral.
     * @return The name of the peripheral, or null if the call failed.
     *
     * @remark The caller should free the returned string with either a call to sgBleFreeString()
     *         or CoTaskMemFree(). <br>
     *         .NET marshaling automatically takes care of it.
     */
    /** @cond */ DLL_DECLSPEC /** @endcond */
    const char* sgBleGetPeripheralName(bluetooth_address_t address);

    /**
     * @brief Returns the Maximum Transmission Unit (MTU) for the given peripheral.
     * 
     * @param address The Bluetooth address of a connected peripheral.
     * @return The MTU of the peripheral, or zero if the call failed.
     */
    /** @cond */ DLL_DECLSPEC /** @endcond */
    int sgBleGetPeripheralMtu(bluetooth_address_t address);

    //! @}
    //! \name Services operations
    //! Valid only for connected peripherals.
    //! @{

    /**
     * @brief Returns the list of discovered services for the given peripheral.
     *
     * @param address The Bluetooth address of a connected peripheral.
     * @return A comma separated list of services UUIDs, or null if the call failed.
     *
     * @remark The caller should free the returned string with either a call to sgBleFreeString()
     *         or CoTaskMemFree(). <br>
     *         .NET marshaling automatically takes care of it.
     */
    /** @cond */ DLL_DECLSPEC /** @endcond */
    const char* sgBleGetPeripheralDiscoveredServices(bluetooth_address_t address);

    /**
     * @brief Returns the list of discovered characteristics for the given peripheral's service.
     *
     * The same characteristic may be listed several times according to the peripheral's configuration.
     *
     * @param address The Bluetooth address of a connected peripheral.
     * @param serviceUuid The service UUID for which to retrieve the characteristics.
     * @return A comma separated list of characteristics UUIDs, or null if the call failed.
     * 
     * @remark The caller should free the returned string with either a call to sgBleFreeString()
     *         or CoTaskMemFree(). <br>
     *         .NET marshaling automatically takes care of it.
     */
    /** @cond */ DLL_DECLSPEC /** @endcond */
    const char* sgBleGetPeripheralServiceCharacteristics(
        bluetooth_address_t address,
        const char* serviceUuid);

    //! @}
    //! \name Characteristics operations
    //! Valid only for connected peripherals.
    //! @{

    /**
     * @brief Returns the standard BLE properties of the specified service's characteristic
     *        for the given peripheral.
     * 
     * @param address The Bluetooth address of a connected peripheral.
     * @param serviceUuid The service UUID.
     * @param characteristicUuid The characteristic UUID.
     * @param instanceIndex The instance index of the characteristic if listed more than once
     *                      for the service, otherwise zero.
     * @return The standard BLE properties of a service's characteristic, or zero if the call failed.
     */
    /** @cond */ DLL_DECLSPEC /** @endcond */
    CharacteristicProperties sgBleGetCharacteristicProperties(
        bluetooth_address_t address,
        const char* serviceUuid,
        const char* characteristicUuid,
        characteristic_index_t instanceIndex);

    /**
     * @brief Sends a request to read the value of the specified service's characteristic
     *        for the given peripheral.
     *
     * The call fails if the characteristic is not readable.
     * 
     * @param address The Bluetooth address of a connected peripheral.
     * @param serviceUuid The service UUID.
     * @param characteristicUuid The characteristic UUID.
     * @param instanceIndex The instance index of the characteristic if listed more than once
     *                      for the service, otherwise zero.
     * @param onValueRead Called when the request has completed (successfully or not)
     *                    and with the data read from the characteristic.
     * @return
     */
    /** @cond */ DLL_DECLSPEC /** @endcond */
    void sgBleReadCharacteristicValue(
        bluetooth_address_t address,
        const char* serviceUuid,
        const char* characteristicUuid,
        characteristic_index_t instanceIndex,
        ValueReadCallback onValueRead);

    /**
     * @brief Sends a request to write to the specified service's characteristic
     *        for the given peripheral.
     *
     * The call fails if the characteristic is not writable.
     *
     * @param address The Bluetooth address of a connected peripheral.
     * @param serviceUuid The service UUID.
     * @param characteristicUuid The characteristic UUID.
     * @param instanceIndex The instance index of the characteristic if listed more than once
     *                      for the service, otherwise zero.
     * @param data A pointer to the data to write to the characteristic.
     * @param length The size in bytes of the data.
     * @param withoutResponse Whether to wait for the peripheral to respond.
     * @param onRequestStatus Called when the request has completed (successfully or not).
     * @return
     */
    /** @cond */ DLL_DECLSPEC /** @endcond */
    void sgBleWriteCharacteristicValue(
        bluetooth_address_t address,
        const char* serviceUuid,
        const char* characteristicUuid,
        characteristic_index_t instanceIndex,
        const void* data, //TODO what if null or empty => doc
        const size_t length,
        bool withoutResponse,
        RequestStatusCallback onRequestStatus);

    /**
     * @brief Subscribes or unsubscribes for value changes of the specified service's characteristic
     *        for the given peripheral.
     *
     * The call fails if the characteristic doesn't support notification or if it is already subscribed.
     * 
     * @param address The Bluetooth address of a connected peripheral.
     * @param serviceUuid The service UUID.
     * @param characteristicUuid The characteristic UUID.
     * @param instanceIndex The instance index of the characteristic if listed more than once
     *                      for the service, otherwise zero.
     * @param onValueChanged Called when the value of the characteristic changes.
     *                       Pass null to unsubscribe. <br>
     *                       The callback must stay valid until the characteristic is unsubscribed
     *                       or the peripheral is released.
     * @param onRequestStatus Called when the request has completed (successfully or not).
     */
    /** @cond */ DLL_DECLSPEC /** @endcond */
    void sgBleSetNotifyCharacteristic(
        bluetooth_address_t address,
        const char* serviceUuid,
        const char* characteristicUuid,
        characteristic_index_t instanceIndex,
        ValueChangedCallback onValueChanged,
        RequestStatusCallback onRequestStatus);

    //! @}
    //! \name Miscellaneous
    //! @{

    /**
     * @brief Deallocates a string returned by any of the sgBle* methods.
     *
     * Unity's marshaling handles the deallocation automatically.
     * 
     * @param str A c-string pointer returned by any of the sgBle* methods.
     */
    /** @cond */ DLL_DECLSPEC /** @endcond */
    void sgFreeString(char* str);

    //! @}

} // extern "C"
