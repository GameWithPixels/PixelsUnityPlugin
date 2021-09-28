#import "PXBlePeripheral.h"


@implementation PXBlePeripheral

// https://github.com/NordicSemiconductor/Android-BLE-Library/blob/master/ble/src/main/java/no/nordicsemi/android/ble/callback/FailCallback.java
//int REASON_DEVICE_DISCONNECTED = -1;
//int REASON_DEVICE_NOT_SUPPORTED = -2;
//int REASON_NULL_ATTRIBUTE = -3;
//int REASON_REQUEST_FAILED = -4;
//int REASON_TIMEOUT = -5;
//int REASON_VALIDATION = -6;
//int REASON_CANCELLED = -7;
//int REASON_BLUETOOTH_DISABLED = -100;

static NSError *notConnectedError = [NSError errorWithDomain:[NSString stringWithFormat:@"%@.errorDomain", [[NSBundle mainBundle] bundleIdentifier]]
                                                        code:-1 // Same value as Nordic's Android BLE library
                                                    userInfo:@{ NSLocalizedDescriptionKey: @"Not connected" }];

// static NSError *deviceNotSupportedError = [NSError errorWithDomain:[NSString stringWithFormat:@"%@.errorDomain", [[NSBundle mainBundle] bundleIdentifier]]
//                                                               code:-2 // Same value as Nordic's Android BLE library
//                                                           userInfo:@{ NSLocalizedDescriptionKey: @"Device not supported" }];

static NSError *nullAttributeError = [NSError errorWithDomain:[NSString stringWithFormat:@"%@.errorDomain", [[NSBundle mainBundle] bundleIdentifier]]
                                                         code:-3 // Same value as Nordic's Android BLE library
                                                     userInfo:@{ NSLocalizedDescriptionKey: @"Null attribute" }];

// static NSError *discoveryError = [NSError errorWithDomain:[NSString stringWithFormat:@"%@.errorDomain", [[NSBundle mainBundle] bundleIdentifier]]
//                                                      code:-8
//                                                  userInfo:@{ NSLocalizedDescriptionKey: @"Discover error" }];

static NSError *bluetoothDisabledError = [NSError errorWithDomain:[NSString stringWithFormat:@"%@.errorDomain", [[NSBundle mainBundle] bundleIdentifier]]
                                                             code:-100 // Same value as Nordic's Android BLE library
                                                         userInfo:@{ NSLocalizedDescriptionKey: @"Bluetooth disabled" }];

//
// Getters
//

- (NSUUID *)identifier
{
    return _peripheral.identifier;
}

- (bool)isConnected
{
    return _peripheral.state == CBPeripheralStateConnected;
}

- (int)rssi
{
    return _rssi;
}

//
// Public methods
//

- (instancetype)initWithPeripheral:(CBPeripheral *)peripheral
            centralManagerDelegate:(PXBleCentralManagerDelegate *)centralManagerDelegate
    connectionStatusChangedHandler:(void (^)(PXBlePeripheralConnectionEvent connectionEvent, PXBlePeripheralConnectionEventReason reason))connectionEventHandler;
{
    if (self = [super init])
    {
        if (!peripheral || !centralManagerDelegate)
        {
            return nil;
        }
        
        _queue = GetBleSerialQueue();
        _centralDelegate = centralManagerDelegate;
        _peripheral = peripheral;
        _peripheral.delegate = self;
        _connectionEventHandler = connectionEventHandler;
        _rssi = 0;
        _pendingRequests = [NSMutableArray<bool (^)()> new];
        _completionHandlers = [NSMutableArray<void (^)(NSError *error)> new];
        _valueChangedHandlers = [NSMapTable<CBCharacteristic *, void (^)(CBCharacteristic *characteristic, NSError *error)> strongToStrongObjectsMapTable];
        
        __weak PXBlePeripheral *weakSelf = self;
        PXBlePeripheralConnectionEventHandler handler =
        ^(CBPeripheral *peripheral, PXBlePeripheralConnectionEvent connectionEvent, NSError *error)
        {
            // Be sure to not use self directly (or implictly by referencing a property)
            // otherwise it creates a strong reference to itself and prevents the instance's deallocation
            PXBlePeripheral *strongSelf = weakSelf;
            if (strongSelf != nil)
            {
                switch (connectionEvent)
                {
                    case PXBlePeripheralConnectionEventConnected:
                        NSLog(@">> PeripheralConnectionEvent = connected");
                        // We must discover services and characteristics before we can use them
                        [peripheral discoverServices:strongSelf->_requiredServices];
                        break;
                        
                    case PXBlePeripheralConnectionEventDisconnected:
                    {
                        NSLog(@">> PeripheralConnectionEvent = disconnected with error %@", error);
                        PXBlePeripheralConnectionEventReason reason = PXBlePeripheralConnectionEventReasonUnreachable;
                        if (strongSelf->_discoveryDisconnectReason != PXBlePeripheralConnectionEventReasonSuccess)
                        {
                            reason = strongSelf->_discoveryDisconnectReason;
                            strongSelf->_discoveryDisconnectReason = PXBlePeripheralConnectionEventReasonSuccess;
                        }

                        // We're now disconnected, notify error and clear any pending request
                        // Note: we could skip clear when there is no error (meaning that the disconnect was intentional)
                        //       but we're doing the same as in Nordic's Android BLE library
                        [strongSelf reportRequestResult:error clearPendingRequests:true];

                        if (connectionEventHandler)
                        {
                            connectionEventHandler(PXBlePeripheralConnectionEventDisconnected, reason);
                        }
                        break;
                    }
                        
                    case PXBlePeripheralConnectionEventFailedToConnect:
                        NSLog(@">> PeripheralConnectionEvent = failed with error %@", error);
                        [strongSelf reportRequestResult:error];
                        break;
                        
                    default:
                        NSLog(@">> PeripheralConnectionEvent = ???"); //TODO
                        break;
                }
            }
        };
        [_centralDelegate setConnectionEventHandler:handler
                                      forPeripheral:_peripheral];
    }
    return self;
}

- (void)dealloc
{
    // No need to call super dealloc a ARC is enabled
    [_centralDelegate.centralManager cancelPeripheralConnection:_peripheral];
    NSLog(@"PXBlePeripheral dealloc");
}

- (void)queueConnectWithServices:(NSArray<CBUUID *> *)services
               completionHandler:(void (^)(NSError *error))completionHandler
{
    NSLog(@">> queueConnect");
    
    NSArray<CBUUID *> *requiredServices = [services copy];
    [self queueRequest:^{
        NSLog(@">> Connect");
        _requiredServices = requiredServices;
        [_centralDelegate.centralManager connectPeripheral:self->_peripheral options:nil];
        return true;
    }
 withCompletionHandler:completionHandler];
}

- (void)queueDisconnect:(void (^)(NSError *error))completionHandler
{
    NSLog(@">> queueDisconnect");
    
    [self queueRequest:^{
        NSLog(@">> Disconnect");
        [_centralDelegate.centralManager cancelPeripheralConnection:self->_peripheral];
        return true;
    }
 withCompletionHandler:completionHandler];
}

- (void)queueReadRssi:(void (^)(NSError *error))completionHandler
{
    NSLog(@">> queueReadRsssi");
    
    [self queueRequest:^{
        NSLog(@">> ReadRSSI");
        [self->_peripheral readRSSI];
        return true;
    }
 withCompletionHandler:completionHandler];
}

- (void)queueReadValueForCharacteristic:(CBCharacteristic *)characteristic
                    valueChangedHandler:(void (^)(CBCharacteristic *characteristic, NSError *error))valueChangedHandler
                      completionHandler:(void (^)(NSError *error))completionHandler
{
    NSLog(@">> queueReadValueForCharacteristic");
    
    [self queueRequest:^{
        if (!characteristic || !valueChangedHandler || !self.isConnected)
        {
            NSLog(@">> ReadValueForCharacteristic -> invalid call");
            return false;
        }
        
        NSLog(@">> ReadValueForCharacteristic");
        [self->_valueChangedHandlers setObject:valueChangedHandler forKey:characteristic];
        [self->_peripheral readValueForCharacteristic:characteristic];
        return true;
    }
 withCompletionHandler:completionHandler];
}

- (void)queueWriteValue:(NSData *)data
      forCharacteristic:(CBCharacteristic *)characteristic
                   type:(CBCharacteristicWriteType)type
      completionHandler:(void (^)(NSError *error))completionHandler
{
    NSLog(@">> queueWriteValue");
    
    [self queueRequest:^{
        if (!characteristic || !self.isConnected)
        {
            NSLog(@">> WriteValue -> invalid call");
            return false;
        }
        
        NSLog(@">> WriteValue");
        [self->_peripheral writeValue:data forCharacteristic:characteristic type:type];
        if (type == CBCharacteristicWriteWithoutResponse)
        {
            [self reportRequestResult:nil];
        }
        return true;
    }
 withCompletionHandler:completionHandler];
}

- (void)queueSetNotifyValueForCharacteristic:(CBCharacteristic *)characteristic
                         valueChangedHandler:(void (^)(CBCharacteristic *characteristic, NSError *error))valueChangedHandler
                           completionHandler:(void (^)(NSError *error))completionHandler
{
    NSLog(@">> queueSetNotifyValueForCharacteristic");
    
    [self queueRequest:^{
        if (!characteristic || !valueChangedHandler || !self.isConnected)
        {
            NSLog(@">> SetNotifyValueForCharacteristic -> invalid call");
            return false;
        }
        
        NSLog(@">> SetNotifyValueForCharacteristic");
        [self->_valueChangedHandlers setObject:valueChangedHandler forKey:characteristic];
        [self->_peripheral setNotifyValue:valueChangedHandler != nil forCharacteristic:characteristic];
        return true;
    }
 withCompletionHandler:completionHandler];
}

//
// Private methods
//

// completionHandler can be nil
- (void)queueRequest:(bool (^)())requestBlock withCompletionHandler:(void (^)(NSError *error))completionHandler
{
    NSAssert(requestBlock != nil, @"Nil operation block");
    
    dispatch_async(_queue, ^{
        [self->_pendingRequests addObject:requestBlock];
        [self->_completionHandlers addObject:completionHandler];
        
        // Run operation if queue was empty
        if (self->_completionHandlers.count == 1)
        {
            [self runNextRequest];
        }
    });
}

- (void)runNextRequest
{
    if (_pendingRequests.count > 0)
    {
        NSLog(@">> runNextRequest");
        
        bool (^block)() = _pendingRequests[0];
        [_pendingRequests removeObjectAtIndex:0];
        
        if (!block()) // Block can't be nil
        {
            NSError *error;
            if (self.isConnected)
            {
                error = nullAttributeError;
            }
            else if (_centralDelegate.isBluetoothOn)
            {
                error = notConnectedError;
            }
            else
            {
                error = bluetoothDisabledError;
            }
            [self reportRequestResult:error];
        }
    }
}

// Should always be called on the queue
- (void)reportRequestResult:(NSError *)error
{
    [self reportRequestResult:error clearPendingRequests:false];
}

// Should always be called on the queue
- (void)reportRequestResult:(NSError *)error clearPendingRequests:(bool)clearPendingRequests
{
    NSAssert(clearPendingRequests || (_completionHandlers.count > 0), @"Empty _completionHandlers");
    if (_completionHandlers.count > 0)
    {
        NSLog(@">> reportRequestResult %@", [error localizedDescription]);
        
        void (^handler)(NSError *error) = _completionHandlers[0];
        if (clearPendingRequests)
        {
            [_pendingRequests removeAllObjects];
            [_completionHandlers removeAllObjects];
        }
        else
        {
            [_completionHandlers removeObjectAtIndex:0];
        }
        
        // Handler can be nil
        if (handler)
        {
            handler(error);
        }
        
        [self runNextRequest];
    }
    else if (clearPendingRequests)
    {
        NSLog(@">> _pendingRequests not empty!!");
        [_pendingRequests removeAllObjects]; // Should be empty anyways
    }
    else
    {
        NSLog(@">> _completionHandlers empty!!");
    }
}

- (void)disconnectForDiscoveryError:(PXBlePeripheralConnectionEventReason)reason
{
    NSAssert(!_discoveryDisconnectReason, @"_discoveryDisconnectReason already set");
    _discoveryDisconnectReason = reason;
    [_centralDelegate.centralManager cancelPeripheralConnection:_peripheral];
}

- (bool)hasAllRequiredServices:(NSArray<CBService *> *)services
{
    for (CBUUID *uuid in _requiredServices)
    {
        bool found = false;
        for (CBService *service in services)
        {
            found = [service.UUID isEqual:uuid];
            if (found) break;
        }
        if (!found) return false;
    }
    return true;
}

//
// CBPeripheralDelegate implementation
//

- (void)peripheral:(CBPeripheral *)peripheral didDiscoverServices:(NSError *)error
{
    if (error)
    {
        [self disconnectForDiscoveryError:PXBlePeripheralConnectionEventReasonUnknown];
    }
    else if (![self hasAllRequiredServices:peripheral.services])
    {
        [self disconnectForDiscoveryError:PXBlePeripheralConnectionEventReasonNotSupported];
    }
    else
    {
        // Store number of services to discover, we'll consider to be fully connected
        // only all the services have been discovered
        _discoveringServicesCounter = peripheral.services.count;
        
        for (CBService *service in peripheral.services)
        {
            [peripheral discoverCharacteristics:nil forService:service];
        }
    }
}

- (void)peripheral:(CBPeripheral *)peripheral didDiscoverCharacteristicsForService:(CBService *)service error:(NSError *)error
{
    if (error)
    {
        [self disconnectForDiscoveryError:PXBlePeripheralConnectionEventReasonUnknown];
    }
    else
    {
        _discoveringServicesCounter -= 1;
        if (_discoveringServicesCounter == 0)
        {
            // Notify connected when characteristics are discovered for all services
            // We must assume that each service will at least report one characteristic
            [self reportRequestResult:error];
            if (_connectionEventHandler != nil)
            {
                _connectionEventHandler(PXBlePeripheralConnectionEventReady, PXBlePeripheralConnectionEventReasonSuccess);
            }
        }
    }
}

// - (void)peripheral:(CBPeripheral *)peripheral didDiscoverDescriptorsForCharacteristic:(CBCharacteristic *)characteristic error:(NSError *)error
// {
// }

- (void)peripheral:(CBPeripheral *)peripheral didUpdateValueForCharacteristic:(CBCharacteristic *)characteristic error:(NSError *)error
{
    NSLog(@">> didUpdateValueForCharacteristic with error %@", error);
    void (^handler)(CBCharacteristic *characteristic, NSError *error) = [_valueChangedHandlers objectForKey:characteristic];
    if (handler != nil)
    {
        handler(characteristic, error);
    }
}

// - (void)peripheral:(CBPeripheral *)peripheral didUpdateValueForDescriptor:(CBDescriptor *)descriptor error:(NSError *)error
// {
// }

- (void)peripheral:(CBPeripheral *)peripheral didWriteValueForCharacteristic:(CBCharacteristic *)characteristic error:(NSError *)error
{
    NSLog(@">> didWriteValueForCharacteristic with error %@", error);
    [self reportRequestResult:error];
}

// - (void)peripheral:(CBPeripheral *)peripheral didWriteValueForDescriptor:(CBDescriptor *)descriptor error:(NSError *)error
// {
// }

// - (void)peripheralIsReadyToSendWriteWithoutResponse:(CBPeripheral *)peripheral
// {
// }

- (void)peripheral:(CBPeripheral *)peripheral didUpdateNotificationStateForCharacteristic:(CBCharacteristic *)characteristic error:(NSError *)error
{
    NSLog(@">> didUpdateNotificationStateForCharacteristic with error %@", error);
    [self reportRequestResult:error];
}

- (void)peripheral:(CBPeripheral *)peripheral didReadRSSI:(NSNumber *)RSSI error:(NSError *)error
{
    NSLog(@">> didReadRSSI with error %@", error);
    _rssi = RSSI.intValue;
    [self reportRequestResult:error];
}

// - (void)peripheralDidUpdateName:(CBPeripheral *)peripheral
// {
// }

@end
