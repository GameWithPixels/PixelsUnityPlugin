#pragma once

#include <cstdint>
#include "bletypes.h"

#ifdef LIBWINRTBLE_EXPORTS
#define DLL_DECLSPEC __declspec(dllexport)
#else
#define DLL_DECLSPEC __declspec(dllimport)
#endif

using bluetooth_address_t = Systemic::BluetoothLE::bluetooth_address_t;
using BleRequestStatus = Systemic::BluetoothLE::BleRequestStatus;
using ConnectionEvent = Systemic::BluetoothLE::ConnectionEvent;
using ConnectionEventReason = Systemic::BluetoothLE::ConnectionEventReason;
using characteristic_index_t = std::uint32_t;
using characteristic_property_t = std::uint64_t;

typedef void (*CentralStateUpdateCallback)(bool available);
typedef void (*DiscoveredPeripheralCallback)(const char* advertisementDataJson);
typedef void (*RequestStatusCallback)(BleRequestStatus status);
typedef void (*PeripheralConnectionStatusChangedCallback)(bluetooth_address_t address, ConnectionEvent connectionEvent, ConnectionEventReason reason);
typedef void (*ValueChangedCallback)(const void* _data, size_t length, BleRequestStatus status);

extern "C"
{
    // None of the sgBle* methods are thread safe!
    DLL_DECLSPEC bool sgBleInitialize(bool apartmentSingleThreaded, CentralStateUpdateCallback onCentralStateUpdate);

    DLL_DECLSPEC void sgBleShutdown();

    // requiredServicesUuids is a comma separated list of UUIDs, it can be null
    DLL_DECLSPEC bool sgBleStartScan(
        const char* requiredServicesUuids,
        DiscoveredPeripheralCallback onDiscoveredPeripheral);

    DLL_DECLSPEC void sgBleStopScan();

    // discoverServicesUuids is a comma separated list of UUIDs, it can be null
    DLL_DECLSPEC bool sgBleCreatePeripheral(
        bluetooth_address_t address,
        PeripheralConnectionStatusChangedCallback onPeripheralStatusChanged);

    DLL_DECLSPEC void sgBleReleasePeripheral(bluetooth_address_t address);

    DLL_DECLSPEC void sgBleConnectPeripheral(bluetooth_address_t address,
        const char* requiredServicesUuids,
        bool autoConnect,
        RequestStatusCallback onRequestStatus);

    DLL_DECLSPEC void sgBleDisconnectPeripheral(
        bluetooth_address_t address,
        RequestStatusCallback onRequestStatus);

    DLL_DECLSPEC int sgBleGetPeripheralMtu(bluetooth_address_t address);

    // caller should free string with CoTaskMemFree() or sgBleFreeString() (.NET marshaling takes care of it)
    DLL_DECLSPEC const char* sgBleGetPeripheralName(bluetooth_address_t address);

    // returns a comma separated list of UUIDs
    // caller should free string with CoTaskMemFree() or sgBleFreeString() (.NET marshaling takes care of it)
    DLL_DECLSPEC const char* sgBleGetPeripheralDiscoveredServices(bluetooth_address_t address);

    // returns a comma separated list of UUIDs
    // caller should free string with CoTaskMemFree() or sgBleFreeString() (.NET marshaling takes care of it)
    DLL_DECLSPEC const char* sgBleGetPeripheralServiceCharacteristics(
        bluetooth_address_t address,
        const char* serviceUuid);

    // https://developer.apple.com/documentation/corebluetooth/cbcharacteristicproperties?language=objc
    DLL_DECLSPEC characteristic_property_t sgBleGetCharacteristicProperties(
        bluetooth_address_t address,
        const char* serviceUuid,
        const char* characteristicUuid,
        characteristic_index_t instanceIndex);

    DLL_DECLSPEC void sgBleReadCharacteristicValue(
        bluetooth_address_t address,
        const char* serviceUuid,
        const char* characteristicUuid,
        characteristic_index_t instanceIndex,
        ValueChangedCallback onValueChanged,
        RequestStatusCallback onRequestStatus);

    DLL_DECLSPEC void sgBleWriteCharacteristicValue(
        bluetooth_address_t address,
        const char* serviceUuid,
        const char* characteristicUuid,
        characteristic_index_t instanceIndex,
        const void* _data,
        const size_t length,
        bool withoutResponse,
        RequestStatusCallback onRequestStatus);

    DLL_DECLSPEC void sgBleSetNotifyCharacteristic(
        bluetooth_address_t address,
        const char* serviceUuid,
        const char* characteristicUuid,
        characteristic_index_t instanceIndex,
        ValueChangedCallback onValueChanged,
        RequestStatusCallback onRequestStatus);
    
    DLL_DECLSPEC void sgFreeString(char* str);

} // extern "C"
