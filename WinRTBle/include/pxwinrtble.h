#pragma once

#include <cstdint>
#include "bletypes.h"

#ifdef LIBWINRTBLE_EXPORTS
#define DLL_DECLSPEC __declspec(dllexport)
#else
#define DLL_DECLSPEC __declspec(dllimport)
#endif

using bluetooth_address_t = Pixels::CoreBluetoothLE::bluetooth_address_t;
using BleRequestStatus = Pixels::CoreBluetoothLE::BleRequestStatus;
using ConnectionEvent = Pixels::CoreBluetoothLE::ConnectionEvent;
using ConnectionEventReason = Pixels::CoreBluetoothLE::ConnectionEventReason;
using characteristic_index_t = std::uint32_t;
using characteristic_property_t = std::uint64_t;

typedef void (*CentralStateUpdateCallback)(bool available);
typedef void (*DiscoveredPeripheralCallback)(const char* advertisementDataJson);
typedef void (*RequestStatusCallback)(BleRequestStatus status);
typedef void (*PeripheralConnectionStatusChangedCallback)(bluetooth_address_t address, ConnectionEvent connectionEvent, ConnectionEventReason reason);
typedef void (*ValueChangedCallback)(const void* _data, size_t length, BleRequestStatus status);

extern "C"
{
    // None of the pxBle* methods are thread safe!
    DLL_DECLSPEC bool pxBleInitialize(bool apartmentSingleThreaded, CentralStateUpdateCallback onCentralStateUpdate);

    DLL_DECLSPEC void pxBleShutdown();

    // requiredServicesUuids is a comma separated list of UUIDs, it can be null
    DLL_DECLSPEC bool pxBleStartScan(
        const char* requiredServicesUuids,
        DiscoveredPeripheralCallback onDiscoveredPeripheral);

    DLL_DECLSPEC void pxBleStopScan();

    // discoverServicesUuids is a comma separated list of UUIDs, it can be null
    DLL_DECLSPEC bool pxBleCreatePeripheral(
        bluetooth_address_t address,
        PeripheralConnectionStatusChangedCallback onPeripheralStatusChanged);

    DLL_DECLSPEC void pxBleReleasePeripheral(bluetooth_address_t address);

    DLL_DECLSPEC void pxBleConnectPeripheral(bluetooth_address_t address,
        const char* requiredServicesUuids,
        bool autoConnect,
        RequestStatusCallback onRequestStatus);

    DLL_DECLSPEC void pxBleDisconnectPeripheral(
        bluetooth_address_t address,
        RequestStatusCallback onRequestStatus);

    DLL_DECLSPEC int pxBleGetPeripheralMtu(bluetooth_address_t address);

    // caller should free string with CoTaskMemFree() or pxBleFreeString() (.NET marshaling takes care of it)
    DLL_DECLSPEC const char* pxBleGetPeripheralName(bluetooth_address_t address);

    // returns a comma separated list of UUIDs
    // caller should free string with CoTaskMemFree() or pxBleFreeString() (.NET marshaling takes care of it)
    DLL_DECLSPEC const char* pxBleGetPeripheralDiscoveredServices(bluetooth_address_t address);

    // returns a comma separated list of UUIDs
    // caller should free string with CoTaskMemFree() or pxBleFreeString() (.NET marshaling takes care of it)
    DLL_DECLSPEC const char* pxBleGetPeripheralServiceCharacteristics(
        bluetooth_address_t address,
        const char* serviceUuid);

    // https://developer.apple.com/documentation/corebluetooth/cbcharacteristicproperties?language=objc
    DLL_DECLSPEC characteristic_property_t pxBleGetCharacteristicProperties(
        bluetooth_address_t address,
        const char* serviceUuid,
        const char* characteristicUuid,
        characteristic_index_t instanceIndex);

    DLL_DECLSPEC void pxBleReadCharacteristicValue(
        bluetooth_address_t address,
        const char* serviceUuid,
        const char* characteristicUuid,
        characteristic_index_t instanceIndex,
        ValueChangedCallback onValueChanged,
        RequestStatusCallback onRequestStatus);

    DLL_DECLSPEC void pxBleWriteCharacteristicValue(
        bluetooth_address_t address,
        const char* serviceUuid,
        const char* characteristicUuid,
        characteristic_index_t instanceIndex,
        const void* _data,
        const size_t length,
        bool withoutResponse,
        RequestStatusCallback onRequestStatus);

    DLL_DECLSPEC void pxBleSetNotifyCharacteristic(
        bluetooth_address_t address,
        const char* serviceUuid,
        const char* characteristicUuid,
        characteristic_index_t instanceIndex,
        ValueChangedCallback onValueChanged,
        RequestStatusCallback onRequestStatus);
    
    DLL_DECLSPEC void pxBleFreeString(char* str);

} // extern "C"
