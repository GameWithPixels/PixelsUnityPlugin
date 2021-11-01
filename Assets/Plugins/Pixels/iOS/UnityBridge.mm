#import "PXBleCentralManagerDelegate.h"
#import "PXBlePeripheral.h"

#include <cstdint>
#include <cstring>
#include <limits>

using peripheral_id_t = const char*;
using request_index_t = std::uint32_t;
using characteristic_index_t = std::uint32_t;
using characteristic_property_t = std::uint64_t;

typedef void (*CentralStateUpdateCallback)(bool available);
typedef void (*DiscoveredPeripheralCallback)(const char *advertisementDataJson);
typedef void (*RequestStatusCallback)(request_index_t requestIndex, int errorCode);
typedef void (*PeripheralConnectionEventCallback)(request_index_t requestIndex, peripheral_id_t peripheralId, int connectionEvent, int reason);
typedef void (*RssiReadCallback)(request_index_t requestIndex, int rssi, int errorCode);
typedef void (*ValueChangedCallback)(request_index_t requestIndex, const void* data, size_t length, int errorCode);

static PXBleCentralManagerDelegate *_central = nil;
static NSMutableDictionary<CBPeripheral *, PXBlePeripheral *> *_peripherals = nil;


static const int otherErrorsMask = 0x80000000;
static const int unexpectedError = otherErrorsMask;
static const int invalidPeripheralIdErrorCode = otherErrorsMask | 1;

int toErrorCode(NSError *error)
{
    if (!error)
    {
        return 0;
    }
    else if (error.domain == CBErrorDomain)
    {
        // CoreBluetooth error (zero is CBErrorUnknown)
        return -1 - (int)error.code;
    }
    else if (error.domain == CBATTErrorDomain)
    {
        // Protocol error (zero is success)
        return (int)error.code;
    }
    else if (error.domain == pxBleGetErrorDomain())
    {
        // One of our own error
        return otherErrorsMask | (0x100 + (int)error.code);
    }
    else
    {
        // Any other error
        return unexpectedError;
    }
}

typedef void (^CompletionHandler)(NSError *error);
CompletionHandler toCompletionHandler(RequestStatusCallback onRequestStatus, request_index_t requestIndex)
{
    return ^(NSError *error){
        if (onRequestStatus)
            onRequestStatus(requestIndex, toErrorCode(error));
    };
}

typedef void (^ValueChangedHandler)(CBCharacteristic *characteristic, NSError *error);
ValueChangedHandler toValueChangedHandler(ValueChangedCallback onValueChanged, request_index_t requestIndex)
{
    void (^handler)(CBCharacteristic *, NSError *error) = nil;
    if (onValueChanged)
    {
        handler = ^(CBCharacteristic *characteristic, NSError *error){
            NSData *data = characteristic.value;
            onValueChanged(requestIndex, data.bytes, data.length, toErrorCode(error));
            
        };
    }
    return handler;
}

// Convert c-string to array of CBUUID
NSArray<CBUUID *> *toCBUUIDArray(const char *serviceUuids)
{
    NSMutableArray<CBUUID *> *arr = nil;
    if (serviceUuids)
    {
        NSArray<NSString *> *servicesList = [[NSString stringWithUTF8String:serviceUuids] componentsSeparatedByString:@","];
        if (servicesList.count > 0)
        {
            arr = [NSMutableArray<CBUUID *> arrayWithCapacity: servicesList.count];
            for (NSString *uuidStr in servicesList)
            {
                CBUUID *uuid = [CBUUID UUIDWithString:uuidStr];
                if (uuid != nil)
                {
                    [arr addObject:uuid];
                }
                //else TODO error
            }
        }
    }
    return  arr;
}

NSString *toUuidsString(NSArray<CBAttribute *> *attributes)
{
    NSMutableString *uuids = [[NSMutableString alloc] initWithCapacity:36 * attributes.count]; // A UUID has 36 characters including the dashes
    for (CBService *attr in attributes)
    {
        if (uuids.length > 0)
        {
            [uuids appendString:@","];
        }
        [uuids appendString:attr.UUID.UUIDString.lowercaseString];
    }
    return uuids;
}

const char *allocateCStr(NSString *str)
{
    char* cStr = NULL;
    if (str)
    {
        const char* utf8CStr = [str UTF8String];
        cStr = (char*)malloc(strlen(utf8CStr) + 1);
        std::strcpy(cStr, utf8CStr);
    }
    return cStr;
}

NSString *toJsonStr(CBUUID *uuid)
{
    return uuid.UUIDString.lowercaseString;
}

void appendToJsonStr(NSMutableString *jsonStr, NSArray<CBUUID *> *uuids)
{
    [jsonStr appendString:@"["];
    NSUInteger len = uuids.count;
    for (NSUInteger i = 0; i < len; i++)
    {
        if (i) [jsonStr appendString:@","];
        [jsonStr appendFormat:@"\"%@\"", toJsonStr(uuids[i])];
    }
    [jsonStr appendString:@"]"];
}

void appendToJsonStr(NSMutableString *jsonStr, NSData *data)
{
    [jsonStr appendString:@"["];
    std::uint8_t *bytes = (std::uint8_t *)data.bytes;
    NSUInteger len = data.length;
    for (NSUInteger i = 0; i < len; i++)
    {
        if (i) [jsonStr appendString:@","];
        [jsonStr appendFormat:@"%d", bytes[i]];
    }
    [jsonStr appendString:@"]"];
}

NSString *advertisementDataToJsonString(const char *systemId, NSDictionary<NSString *,id> *advertisementData, NSNumber *RSSI)
{
    NSData *manufacturerData = advertisementData[CBAdvertisementDataManufacturerDataKey];
    NSString *localName = advertisementData[CBAdvertisementDataLocalNameKey];
    NSDictionary<CBUUID *, NSData *> *servicesData = advertisementData[CBAdvertisementDataServiceDataKey];
    NSArray<CBUUID *> *serviceUUIDs = advertisementData[CBAdvertisementDataServiceUUIDsKey];
    NSArray<CBUUID *> *overflowServiceUUIDs = advertisementData[CBAdvertisementDataOverflowServiceUUIDsKey];
    NSNumber *txPowerLevel = advertisementData[CBAdvertisementDataTxPowerLevelKey];
    NSNumber *isConnectable = advertisementData[CBAdvertisementDataIsConnectable];
    NSArray<CBUUID *> *solicitedServiceUUIDs = advertisementData[CBAdvertisementDataSolicitedServiceUUIDsKey];
    
    NSMutableString *jsonStr = [NSMutableString new];
    [jsonStr appendFormat:@"{\"systemId\":\"%s\",", systemId];
    if (manufacturerData)
    {
        [jsonStr appendString:@"\"manufacturerData\":"];
        appendToJsonStr(jsonStr, manufacturerData);
        [jsonStr appendString:@","];
    }
    if (localName)
    {
        [jsonStr appendFormat:@"\"name\":\"%@\",", localName];
    }
    if (servicesData)
    {
        [jsonStr appendString:@"\"serviceData\":{"];
        bool first = true;
        for (CBUUID *uuid in servicesData)
        {
            if (!first) [jsonStr appendString:@","];
            first = false;
            [jsonStr appendFormat:@"\"%@\":", toJsonStr(uuid)];
            appendToJsonStr(jsonStr, [servicesData objectForKey:uuid]);
        }
        [jsonStr appendString:@"},"];
    }
    if (serviceUUIDs)
    {
        [jsonStr appendString:@"\"services\":"];
        appendToJsonStr(jsonStr, serviceUUIDs);
        [jsonStr appendString:@","];
    }
    if (overflowServiceUUIDs)
    {
        [jsonStr appendString:@"\"overflowServiceUUIDs\":"];
        appendToJsonStr(jsonStr, overflowServiceUUIDs);
        [jsonStr appendString:@","];
    }
    if (txPowerLevel)
    {
        [jsonStr appendFormat:@"\"txPowerLevel\":\"%@\",", txPowerLevel];
    }
    if (isConnectable.boolValue)
    {
        [jsonStr appendString:@"\"isConnectable\":true,"];
    }
    if (solicitedServiceUUIDs)
    {
        [jsonStr appendString:@"\"solicitedServiceUUIDs\":"];
        appendToJsonStr(jsonStr, solicitedServiceUUIDs);
        [jsonStr appendString:@","];
    }
    [jsonStr appendFormat:@"\"rssi\":%@", RSSI];
    [jsonStr appendString:@"}"];
    return jsonStr;
}

const char *getPeripheralId(CBPeripheral *peripheral)
{
    return [[peripheral.identifier UUIDString] UTF8String];
}

CBPeripheral *getCBPeripheral(const char *peripheralId)
{
    CBPeripheral *peripheral = nil;
    if (peripheralId)
    {
        NSUUID *uuid = [[NSUUID alloc] initWithUUIDString:[NSString stringWithUTF8String:peripheralId]];
        peripheral = [_central peripheralForIdentifier:uuid];
    }
    return peripheral;
}

const char *getPeripheralId(PXBlePeripheral *peripheral)
{
    return [[peripheral.identifier UUIDString] UTF8String];
}

PXBlePeripheral *getPxBlePeripheral(const char *peripheralId)
{
    return [_peripherals objectForKey:getCBPeripheral(peripheralId)];
}

PXBlePeripheral *getPxBlePeripheral(const char *peripheralId, RequestStatusCallback onRequestStatus, request_index_t requestIndex)
{
    PXBlePeripheral *pxPeripheral = getPxBlePeripheral(peripheralId);
    if (!pxPeripheral && onRequestStatus)
    {
        onRequestStatus(requestIndex, invalidPeripheralIdErrorCode);
    }
    return pxPeripheral;
}

PXBlePeripheral *getPxBlePeripheral(const char *peripheralId, RssiReadCallback onRssiRead, request_index_t requestIndex)
{
    PXBlePeripheral *pxPeripheral = getPxBlePeripheral(peripheralId);
    if (!pxPeripheral && onRssiRead)
    {
        onRssiRead(std::numeric_limits<int>::min(), requestIndex, invalidPeripheralIdErrorCode);
    }
    return pxPeripheral;
}

CBService *getService(const char *peripheralId, const char *serviceUuidStr)
{
    if (peripheralId && serviceUuidStr)
    {
        CBUUID *serviceUuid = [CBUUID UUIDWithString:[NSString stringWithUTF8String:serviceUuidStr]];
        CBPeripheral *peripheral = getCBPeripheral(peripheralId);
        for (CBService *service in peripheral.services)
        {
            if ([serviceUuid isEqual:service.UUID])
            {
                return service;
            }
        }
    }
    return nil;
}

CBCharacteristic *getCharacteristic(const char *peripheralId, const char *serviceUuidStr, const char *characteristicUuidStr, characteristic_index_t instanceIndex)
{
    CBService *service = getService(peripheralId, serviceUuidStr);
    if (service && characteristicUuidStr)
    {
        CBUUID *characteristicUuid = [CBUUID UUIDWithString:[NSString stringWithUTF8String:characteristicUuidStr]];
        for (CBCharacteristic *characteristic in service.characteristics)
        {
            if ([characteristicUuid isEqual:characteristic.UUID])
            {
                if (instanceIndex == 0)
                {
                    return characteristic;
                }
                else
                {
                    --instanceIndex;
                }
            }
        }
    }
    return nil;
}


//////////////////////////////////////////////////////////////////////////////////////////
//
// Exported C interface
//
//////////////////////////////////////////////////////////////////////////////////////////


extern "C"
{

// None of the pxBle* methods are thread safe!
bool pxBleInitialize(CentralStateUpdateCallback onCentralStateUpdate)
{
    if (!_peripherals)
    {
        // Allocate just once
        _peripherals = [NSMutableDictionary<CBPeripheral *, PXBlePeripheral *> new];
    }
    if (!_central)
    {
        // Allocate everytime (set to nil in shutdown)
        _central = [[PXBleCentralManagerDelegate alloc] initWithStateUpdateHandler:^(CBManagerState state) {
            if (onCentralStateUpdate) onCentralStateUpdate(state >= CBManagerStatePoweredOn);
        }];
    }
    
    return _peripherals && _central;
}

void pxBleShutdown()
{
    if (_central)
    {
        [_peripherals removeAllObjects];
        [_central clearPeripherals];
        _central = nil;
    }
}

// requiredServicesUuids is a comma separated list of UUIDs, it can be null
bool pxBleStartScan(const char *requiredServicesUuids,
                    bool allowDuplicates,
                    DiscoveredPeripheralCallback onDiscoveredPeripheral)
{
    if (!onDiscoveredPeripheral)
    {
        return false;
    }
    
    _central.peripheralDiscoveryHandler = ^(CBPeripheral *peripheral, NSDictionary<NSString *,id> *advertisementData, NSNumber *RSSI){
        onDiscoveredPeripheral([advertisementDataToJsonString(getPeripheralId(peripheral), advertisementData, RSSI) UTF8String]);
    };
    
    // If already scanning, it will update the previous scan
    [_central.centralManager scanForPeripheralsWithServices:toCBUUIDArray(requiredServicesUuids)
                                                    options:allowDuplicates ? @{ CBCentralManagerScanOptionAllowDuplicatesKey: @YES } : nil];
    
    return _central != nil;
}

void pxBleStopScan()
{
    [_central.centralManager stopScan];
}

// discoverServicesUuids is a comma separated list of UUIDs, it can be null
bool pxBleCreatePeripheral(peripheral_id_t peripheralId,
                           PeripheralConnectionEventCallback onPeripheralConnectionEvent,
                           request_index_t requestIndex)
{
    if (getPxBlePeripheral(peripheralId))
    {
        // Already created
        return false;
    }
    
    CBPeripheral *cbPeripheral = getCBPeripheral(peripheralId);
    if (!cbPeripheral)
    {
        // No known peripheral for this id
        return false;
    }
    
    PXBlePeripheral *pxPeripheral = [[PXBlePeripheral alloc] initWithPeripheral:cbPeripheral
                                                         centralManagerDelegate:_central
                                                 connectionStatusChangedHandler:^(PXBlePeripheralConnectionEvent connectionEvent, PXBlePeripheralConnectionEventReason reason){
        if (onPeripheralConnectionEvent)
            onPeripheralConnectionEvent(requestIndex,
                                        getPeripheralId(cbPeripheral),
                                        (int)connectionEvent,
                                        (int)reason);
    }];
    if (pxPeripheral)
    {
        [_peripherals setObject:pxPeripheral forKey:cbPeripheral];
    }
    return pxPeripheral != nil;
}

void pxBleReleasePeripheral(peripheral_id_t peripheralId)
{
    CBPeripheral *cbPeripheral = getCBPeripheral(peripheralId);
    [_peripherals removeObjectForKey:cbPeripheral];
}

void pxBleConnectPeripheral(peripheral_id_t peripheralId,
                            const char* requiredServicesUuids,
                            RequestStatusCallback onRequestStatus,
                            request_index_t requestIndex)
{
    PXBlePeripheral *pxPeripheral = getPxBlePeripheral(peripheralId, onRequestStatus, requestIndex);
    [pxPeripheral queueConnectWithServices:toCBUUIDArray(requiredServicesUuids)
                         completionHandler:toCompletionHandler(onRequestStatus, requestIndex)];
}

void pxBleDisconnectPeripheral(peripheral_id_t peripheralId,
                               RequestStatusCallback onRequestStatus,
                               request_index_t requestIndex)
{
    PXBlePeripheral *peripheral = getPxBlePeripheral(peripheralId, onRequestStatus, requestIndex);
    [peripheral cancelQueue];
    [peripheral queueDisconnect:toCompletionHandler(onRequestStatus, requestIndex)];
}

int pxBleGetPeripheralMtu(peripheral_id_t peripheralId)
{
    CBPeripheral *cbPeripheral = getCBPeripheral(peripheralId);
    // Return the smallest MTU since we don't differentiate the 2 values
    int mtu1 = (int)[cbPeripheral maximumWriteValueLengthForType:CBCharacteristicWriteWithResponse];
    int mtu2 = (int)[cbPeripheral maximumWriteValueLengthForType:CBCharacteristicWriteWithoutResponse];
    return mtu1 <= mtu2 ? mtu1 : mtu2;
}

// caller should free string (Unity marshaling takes care of it)
const char* pxBleGetPeripheralName(peripheral_id_t peripheralId)
{
    CBPeripheral *cbPeripheral = getCBPeripheral(peripheralId);
    return allocateCStr(cbPeripheral.name);
}

void pxBleReadPeripheralRssi(peripheral_id_t peripheralId,
                             RssiReadCallback onRssiRead,
                             request_index_t requestIndex)
{
    PXBlePeripheral *peripheral = getPxBlePeripheral(peripheralId, onRssiRead, requestIndex);
    [peripheral queueReadRssi:^(NSError *error) {
        if (onRssiRead)
            onRssiRead(requestIndex, error ? 0 : peripheral.rssi, toErrorCode(error));
    }];
}

// returns a comma separated list of UUIDs
// caller should free string (Unity marshaling takes care of it)
const char *pxBleGetPeripheralDiscoveredServices(peripheral_id_t peripheralId)
{
    CBPeripheral *peripheral = getCBPeripheral(peripheralId);
    return allocateCStr(toUuidsString(peripheral.services));
}

// returns a comma separated list of UUIDs
// caller should free string (Unity marshaling takes care of it)
const char *pxBleGetPeripheralServiceCharacteristics(peripheral_id_t peripheralId,
                                                     const char* serviceUuid)
{
    CBService *service = getService(peripheralId, serviceUuid);
    return allocateCStr(toUuidsString(service.characteristics));
}

// https://developer.apple.com/documentation/corebluetooth/cbcharacteristicproperties?language=objc
characteristic_property_t pxBleGetCharacteristicProperties(peripheral_id_t peripheralId,
                                                           const char *serviceUuid,
                                                           const char *characteristicUuid,
                                                           characteristic_index_t instanceIndex)
{
    CBCharacteristic *characteristic = getCharacteristic(peripheralId, serviceUuid, characteristicUuid, instanceIndex);
    return characteristic.properties;
}

void pxBleReadCharacteristicValue(peripheral_id_t peripheralId,
                                  const char *serviceUuid,
                                  const char *characteristicUuid,
                                  characteristic_index_t instanceIndex,
                                  ValueChangedCallback onValueChanged,
                                  RequestStatusCallback onRequestStatus,
                                  request_index_t requestIndex)
{
    PXBlePeripheral *peripheral = getPxBlePeripheral(peripheralId, onRequestStatus, requestIndex);
    [peripheral queueReadValueForCharacteristic:getCharacteristic(peripheralId, serviceUuid, characteristicUuid, instanceIndex)
                            valueChangedHandler:toValueChangedHandler(onValueChanged, requestIndex)
                              completionHandler:toCompletionHandler(onRequestStatus, requestIndex)];
}

void pxBleWriteCharacteristicValue(peripheral_id_t peripheralId,
                                   const char *serviceUuid,
                                   const char *characteristicUuid,
                                   characteristic_index_t instanceIndex,
                                   const void *data,
                                   const size_t length,
                                   bool withoutResponse,
                                   RequestStatusCallback onRequestStatus,
                                   request_index_t requestIndex)
{
    if (data && length)
    {
        PXBlePeripheral *peripheral = getPxBlePeripheral(peripheralId, onRequestStatus, requestIndex);
        [peripheral queueWriteValue:[NSData dataWithBytes:data length:length]
                  forCharacteristic:getCharacteristic(peripheralId, serviceUuid, characteristicUuid, instanceIndex)
                               type:withoutResponse ? CBCharacteristicWriteWithoutResponse : CBCharacteristicWriteWithResponse
                  completionHandler:toCompletionHandler(onRequestStatus, requestIndex)];
    }
}

void pxBleSetNotifyCharacteristic(peripheral_id_t peripheralId,
                                  const char *serviceUuid,
                                  const char *characteristicUuid,
                                  characteristic_index_t instanceIndex,
                                  ValueChangedCallback onValueChanged,
                                  RequestStatusCallback onRequestStatus,
                                  request_index_t requestIndex)
{
    PXBlePeripheral *peripheral = getPxBlePeripheral(peripheralId, onRequestStatus, requestIndex);
    [peripheral queueSetNotifyValueForCharacteristic:getCharacteristic(peripheralId, serviceUuid, characteristicUuid, instanceIndex)
                                 valueChangedHandler:toValueChangedHandler(onValueChanged, requestIndex)
                                   completionHandler:toCompletionHandler(onRequestStatus, requestIndex)];
}

} // extern "C"
