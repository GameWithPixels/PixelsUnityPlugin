#pragma once

#include <cstdint>

#ifdef LIBWINRTBLE_EXPORTS
#define DLL_DECLSPEC __declspec(dllexport)
#else
#define DLL_DECLSPEC __declspec(dllimport)
#endif

using peripheral_id_t = std::uint64_t;
using characteristic_index_t = std::uint32_t;
using characteristic_property_t = std::uint64_t;

typedef void (*CentralStateUpdateCallback)(bool available);
typedef void (*DiscoveredPeripheralCallback)(const char* advertisementDataJson);
typedef void (*RequestStatusCallback)(int errorCode);
typedef void (*PeripheralConnectionStatusChangedCallback)(peripheral_id_t peripheralId, int connectionStatus, int reason);
typedef void (*ValueChangedCallback)(const void* _data, size_t length, int errorCode);

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
        peripheral_id_t peripheralId,
        PeripheralConnectionStatusChangedCallback onPeripheralStatusChanged);

    DLL_DECLSPEC void pxBleReleasePeripheral(peripheral_id_t peripheralId);

    DLL_DECLSPEC void pxBleConnectPeripheral(peripheral_id_t peripheralId,
        const char* requiredServicesUuids,
        RequestStatusCallback onRequestStatus);

    DLL_DECLSPEC void pxBleDisconnectPeripheral(
        peripheral_id_t peripheralId,
        RequestStatusCallback onRequestStatus);

    DLL_DECLSPEC int pxBleGetPeripheralMtu(peripheral_id_t peripheralId);

    // caller should free string with CoTaskMemFree (.NET marshaling takes care of it)
    DLL_DECLSPEC const char* pxBleGetPeripheralName(peripheral_id_t peripheralId);

    // returns a comma separated list of UUIDs
    // caller should free string with CoTaskMemFree (.NET marshaling takes care of it)
    DLL_DECLSPEC const char* pxBleGetPeripheralDiscoveredServices(peripheral_id_t peripheralId);

    // returns a comma separated list of UUIDs
    // caller should free string with CoTaskMemFree (.NET marshaling takes care of it)
    DLL_DECLSPEC const char* pxBleGetPeripheralServiceCharacteristics(
        peripheral_id_t peripheralId,
        const char* serviceUuid);

    // https://developer.apple.com/documentation/corebluetooth/cbcharacteristicproperties?language=objc
    DLL_DECLSPEC characteristic_property_t pxBleGetCharacteristicProperties(
        peripheral_id_t peripheralId,
        const char* serviceUuid,
        const char* characteristicUuid,
        characteristic_index_t instanceIndex);

    DLL_DECLSPEC void pxBleReadCharacteristicValue(
        peripheral_id_t peripheralId,
        const char* serviceUuid,
        const char* characteristicUuid,
        characteristic_index_t instanceIndex,
        ValueChangedCallback onValueChanged,
        RequestStatusCallback onRequestStatus);

    DLL_DECLSPEC void pxBleWriteCharacteristicValue(
        peripheral_id_t peripheralId,
        const char* serviceUuid,
        const char* characteristicUuid,
        characteristic_index_t instanceIndex,
        const void* _data,
        const size_t length,
        bool withoutResponse,
        RequestStatusCallback onRequestStatus);

    DLL_DECLSPEC void pxBleSetNotifyCharacteristic(
        peripheral_id_t peripheralId,
        const char* serviceUuid,
        const char* characteristicUuid,
        characteristic_index_t instanceIndex,
        ValueChangedCallback onValueChanged,
        RequestStatusCallback onRequestStatus);

} // extern "C"
