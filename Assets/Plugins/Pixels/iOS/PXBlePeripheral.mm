#import "PXBlePeripheral.h"


static NSString *getRequestTypeString(PXBleRequestType type)
{
    switch (type)
    {
        case PXBleRequestTypeConnect: return @"Connect";
        case PXBleRequestTypeDisconnect: return @"Disconnect";
        case PXBleRequestTypeReadRssi: return @"ReadRssi";
        case PXBleRequestTypeReadValue: return @"ReadValue";
        case PXBleRequestTypeWriteValue: return @"WriteValue";
        case PXBleRequestTypeSetNotifyValue: return @"SetNotifyValue";
        default: return @"Unknwown";
    }
}

@implementation PXBleRequest

- (PXBleRequestType)type
{
    return _type;
}

- (instancetype)initWithRequestType:(PXBleRequestType)requestType executeHandler:(PXBleRequestExecuteHandler)executeHandler  completionHandler:(PXBleRequestCompletionHandler)completionHandler
{
    if (self = [super init])
    {
        if (!executeHandler)
        {
            return nil;
        }

        _type = requestType;
        _executeHandler = executeHandler;
        _completionHandler = completionHandler;
    }
    return self;
}

- (NSError *)execute
{
    return _executeHandler();
}

- (void)notifyResult:(NSError *)error
{
    _completionHandler(error);
}

@end

static NSError *invalidCallError = [NSError errorWithDomain:pxBleGetErrorDomain()
                                                       code:PXBlePeripheralRequestErrorInvalidCall
                                                   userInfo:@{ NSLocalizedDescriptionKey: @"Invalid call" }];

static NSError *disconnectedError = [NSError errorWithDomain:pxBleGetErrorDomain()
                                                        code:PXBlePeripheralRequestErrorDisconnected
                                                    userInfo:@{ NSLocalizedDescriptionKey: @"Disconnected" }];

static NSError *invalidParametersError = [NSError errorWithDomain:pxBleGetErrorDomain()
                                                             code:PXBlePeripheralRequestErrorInvalidParameters
                                                         userInfo:@{ NSLocalizedDescriptionKey: @"Invalid parameters" }];

static NSError *canceledError = [NSError errorWithDomain:pxBleGetErrorDomain()
                                                    code:PXBlePeripheralRequestErrorCanceled
                                                userInfo:@{ NSLocalizedDescriptionKey: @"Canceled" }];

@implementation PXBlePeripheral

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
        
        _queue = pxBleGetSerialQueue();
        _centralDelegate = centralManagerDelegate;
        _peripheral = peripheral;
        _peripheral.delegate = self;
        _connectionEventHandler = connectionEventHandler;
        _pendingRequests = [NSMutableArray<PXBleRequest *> new];
        _valueChangedHandlers = [NSMapTable<CBCharacteristic *, void (^)(CBCharacteristic *characteristic, NSError *error)> strongToStrongObjectsMapTable];
        
        __weak PXBlePeripheral *weakSelf = self;
        PXBlePeripheralConnectionEventHandler handler =
        ^(CBPeripheral *peripheral, PXBlePeripheralConnectionEvent connectionEvent, NSError *error)
        {
            // Be sure to not use self directly (or implictly by referencing a property)
            // otherwise it creates a strong reference to itself and prevents the instance's deallocation
            PXBlePeripheral *strongSelf = weakSelf;
            if (strongSelf)
            {
                bool connecting = strongSelf->_runningRequest.type == PXBleRequestTypeConnect;
                bool disconnecting = strongSelf->_runningRequest.type == PXBleRequestTypeDisconnect;
                NSLog(@">> PeripheralConnectionEvent: connecting=%i, disconnecting=%i", (int)connecting, (int)disconnecting);

                switch (connectionEvent)
                {
                    case PXBlePeripheralConnectionEventConnected:
                    {
                        if (connecting)
                        {
                            NSLog(@">> PeripheralConnectionEvent => connected, now discovering services");
                            // We must discover services and characteristics before we can use them
                            strongSelf->_disconnectReason = PXBlePeripheralConnectionEventReasonSuccess;
                            [peripheral discoverServices:strongSelf->_requiredServices];
                        }
                        else
                        {
                            // This shouldn't happen
                            NSLog(@">> PeripheralConnectionEvent => connected, but not running a connection request => disconnecting");
                            [strongSelf internalDisconnect:PXBlePeripheralConnectionEventReasonUnknown];
                        }
                        break;
                    }
                        
                    case PXBlePeripheralConnectionEventDisconnected:
                    {
                        NSLog(@">> PeripheralConnectionEvent => disconnected with error %@", error);
                        PXBlePeripheralConnectionEventReason reason = strongSelf->_disconnectReason;
                        if (reason != PXBlePeripheralConnectionEventReasonSuccess)
                        {
                            // Reset stored reason
                            strongSelf->_disconnectReason = PXBlePeripheralConnectionEventReasonSuccess;
                            if (reason == PXBlePeripheralConnectionEventReasonCanceled)
                            {
                                error = canceledError;
                            }
                        }
                        else if (!disconnecting)
                        {
                            // We got disconnected but not because we asked for it
                            reason = PXBlePeripheralConnectionEventReasonLinkLoss;
                        }
                        
                        // We were connecting, we need to have an error
                        if (connecting && !error)
                        {
                            error = disconnectedError;
                        }
                        
                        [strongSelf qNotifyConnectionEvent:PXBlePeripheralConnectionEventDisconnected reason:reason];

                        [strongSelf qReportRequestResult:error forRequestType:strongSelf->_runningRequest.type];
                        break;
                    }

                    // This case happens rarely, and is usually caused by a transient issue
                    // Because connection shouldn't time out, we attempt to connect again
                    case PXBlePeripheralConnectionEventFailedToConnect:
                    {
                        NSLog(@">> PeripheralConnectionEvent => failed with error %@", error);
                        if (connecting)
                        {
                            [strongSelf->_centralDelegate.centralManager connectPeripheral:strongSelf->_peripheral options:nil];
                        }
                        break;
                    }
                        
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
    // No need to call super dealloc when ARC is enabled
    [self internalDisconnect:PXBlePeripheralConnectionEventReasonSuccess];
    NSLog(@">> PXBlePeripheral dealloc");
}

- (void)queueConnectWithServices:(NSArray<CBUUID *> *)services
               completionHandler:(void (^)(NSError *error))completionHandler
{
    NSLog(@">> queueConnect");
    
    NSArray<CBUUID *> *requiredServices = [services copy];
    PXBleRequestExecuteHandler block = ^{
        NSLog(@">> Connect");
        self->_requiredServices = requiredServices;
        [self qNotifyConnectionEvent:PXBlePeripheralConnectionEventConnecting reason:PXBlePeripheralConnectionEventReasonSuccess];
        [self->_centralDelegate.centralManager connectPeripheral:self->_peripheral options:nil];
        return (NSError *)nil;
    };
    
    [self queueRequest:PXBleRequestTypeConnect
        executeHandler:block
     completionHandler:completionHandler];
}

- (void)queueDisconnect:(void (^)(NSError *error))completionHandler
{
    NSLog(@">> queueDisconnect");
    
    PXBleRequestExecuteHandler block = ^{
        NSLog(@">> Disconnect");
        [self qNotifyConnectionEvent:PXBlePeripheralConnectionEventDisconnecting reason:PXBlePeripheralConnectionEventReasonSuccess];
        [self internalDisconnect:PXBlePeripheralConnectionEventReasonSuccess];
        return (NSError *)nil;
    };
    
    [self queueRequest:PXBleRequestTypeDisconnect
        executeHandler:block
     completionHandler:completionHandler];
}

- (void)queueReadRssi:(void (^)(NSError *error))completionHandler
{
    NSLog(@">> queueReadRsssi");
    
    PXBleRequestExecuteHandler block = ^{
        NSLog(@">> ReadRSSI");
        [self->_peripheral readRSSI];
        return (NSError *)nil;
    };

    [self queueRequest:PXBleRequestTypeReadRssi
        executeHandler:block
     completionHandler:completionHandler];
}

- (void)queueReadValueForCharacteristic:(CBCharacteristic *)characteristic
                    valueChangedHandler:(void (^)(CBCharacteristic *characteristic, NSError *error))valueChangedHandler
                      completionHandler:(void (^)(NSError *error))completionHandler
{
    NSLog(@">> queueReadValueForCharacteristic");
    
    PXBleRequestExecuteHandler block = ^{
        if (!characteristic || !valueChangedHandler)
        {
            NSLog(@">> ReadValueForCharacteristic -> invalid call");
            return invalidParametersError;
        }
        
        NSLog(@">> ReadValueForCharacteristic");
        [self->_valueChangedHandlers setObject:valueChangedHandler forKey:characteristic];
        [self->_peripheral readValueForCharacteristic:characteristic];
        return (NSError *)nil;
    };

    [self queueRequest:PXBleRequestTypeReadValue
        executeHandler:block
     completionHandler:completionHandler];
}

- (void)queueWriteValue:(NSData *)data
      forCharacteristic:(CBCharacteristic *)characteristic
                   type:(CBCharacteristicWriteType)type
      completionHandler:(void (^)(NSError *error))completionHandler
{
    NSLog(@">> queueWriteValue");
    
    PXBleRequestExecuteHandler block = ^{
        if (!characteristic)
        {
            NSLog(@">> WriteValue -> invalid call");
            return invalidParametersError;
        }
        
        NSLog(@">> WriteValue");
        [self->_peripheral writeValue:data forCharacteristic:characteristic type:type];
        if (type == CBCharacteristicWriteWithoutResponse)
        {
            [self qReportRequestResult:nil forRequestType:PXBleRequestTypeWriteValue];
        }
        return (NSError *)nil;
    };

    [self queueRequest:PXBleRequestTypeWriteValue
        executeHandler:block
     completionHandler:completionHandler];
}

- (void)queueSetNotifyValueForCharacteristic:(CBCharacteristic *)characteristic
                         valueChangedHandler:(void (^)(CBCharacteristic *characteristic, NSError *error))valueChangedHandler
                           completionHandler:(void (^)(NSError *error))completionHandler
{
    NSLog(@">> queueSetNotifyValueForCharacteristic");
    
    PXBleRequestExecuteHandler block = ^{
        if (!characteristic || !valueChangedHandler)
        {
            NSLog(@">> SetNotifyValueForCharacteristic -> invalid call");
            return invalidParametersError;
        }
        
        NSLog(@">> SetNotifyValueForCharacteristic");
        [self->_valueChangedHandlers setObject:valueChangedHandler forKey:characteristic];
        [self->_peripheral setNotifyValue:valueChangedHandler != nil forCharacteristic:characteristic];
        return (NSError *)nil;
    };

    [self queueRequest:PXBleRequestTypeSetNotifyValue
        executeHandler:block
     completionHandler:completionHandler];
}

- (void)cancelQueue
{
    NSLog(@">> cancelQueue");
    
    @synchronized (_pendingRequests)
    {
        // Clear the queue (without notification)
        [_pendingRequests removeAllObjects];
    }
    
    dispatch_async(_queue, ^{
        if (_runningRequest)
        {
            PXBleRequestType requestType = _runningRequest.type;

            // Cancel the running request
            NSLog(@">> Queue canceled while running request of type %@", getRequestTypeString(requestType));
            [self qReportRequestResult:canceledError forRequestType:requestType];
        
            // If were trying to connect, cancel connection immediately
            if (requestType == PXBleRequestTypeConnect)
            {
                NSLog(@">> Queue canceled while connecting => cancelling connection");
                [self internalDisconnect:PXBlePeripheralConnectionEventReasonCanceled];
            }
        }
    });
}

//
// Private methods
//

- (void)internalDisconnect:(PXBlePeripheralConnectionEventReason)reason
{
    if ((reason == PXBlePeripheralConnectionEventReasonCanceled)
        || (_disconnectReason != PXBlePeripheralConnectionEventReasonCanceled))
    {
        _disconnectReason = reason;
    }
    [_centralDelegate.centralManager cancelPeripheralConnection:_peripheral];
}

// completionHandler can be nil
- (void)queueRequest:(PXBleRequestType)requestType
      executeHandler:(PXBleRequestExecuteHandler)executeHandler
   completionHandler:(PXBleRequestCompletionHandler)completionHandler
{
    bool runNow = false;
    @synchronized (_pendingRequests)
    {
        // Queue request and completion handler
        PXBleRequest *request = [[PXBleRequest alloc] initWithRequestType:requestType executeHandler:executeHandler completionHandler:completionHandler];
        [_pendingRequests addObject:request];
        
        // Process request immediately if this is the only request in the queue
        runNow =  _pendingRequests.count == 1;
        
        NSLog(@">> queueRequest size=%lu", (unsigned long)_pendingRequests.count);
    }
    
    if (runNow)
    {
        dispatch_async(_queue, ^{
            [self qRunNextRequest];
        });
    }
}

// Should always be called on the queue
- (void)qRunNextRequest
{
    PXBleRequest *request = nil;

    @synchronized (_pendingRequests)
    {
        if ((!_runningRequest) && (_pendingRequests.count > 0))
        {
            NSLog(@">> runNextRequest size=%lu", (unsigned long)_pendingRequests.count);
            
            request = _runningRequest = _pendingRequests[0];
            [_pendingRequests removeObjectAtIndex:0];
            NSAssert(request, @"Got a nil request from the queue");
        }
    }
    
    if (request)
    {
        CBPeripheralState state = _peripheral.state;
        bool connectState = (state == CBPeripheralStateConnecting) || (state == CBPeripheralStateConnected);
        bool disconnectState = (state == CBPeripheralStateDisconnecting) || (state == CBPeripheralStateDisconnected);
        if (((request.type == PXBleRequestTypeConnect) && connectState)
            || ((request.type == PXBleRequestTypeDisconnect) && disconnectState))
        {
            // Connect or disconnect return immediately a success if peripheral already
            // in desired state or transitionning to it
            [self qReportRequestResult:nil forRequestType:request.type];
        }
        else
        {
            // Any other request other than connect are only valid when peripheral is connected
            NSError *error = invalidCallError;
            if ((state == CBPeripheralStateConnected) || (request.type == PXBleRequestTypeConnect))
            {
                error = [request execute];
            }
            if (error)
            {
                [self qReportRequestResult:error forRequestType:request.type];
            }
        }
    }
}

// Should always be called on the queue
- (void)qReportRequestResult:(NSError *)error forRequestType:(PXBleRequestType)requestType
{
    PXBleRequest *request = nil;
    
    @synchronized (_pendingRequests)
    {
        request = _runningRequest;
        _runningRequest = nil;
    }
    
    if (request.type == requestType)
    {
        NSLog(@">> Notifying result for request of type %@, with error: %@",
              getRequestTypeString(request.type), error);
        [request notifyResult:error];
    }
    else if (requestType)
    {
        NSLog(@">> Got result for request of type %@ while running request of type %@, with error: %@",
              getRequestTypeString(requestType), getRequestTypeString(request.type), error);
    }
    
    [self qRunNextRequest];
}

// Should always be called on the queue
- (void)qNotifyConnectionEvent:(PXBlePeripheralConnectionEvent)connectionEvent
                        reason:(PXBlePeripheralConnectionEventReason)reason
{
    NSLog(@">> Notifying connection event: %ld, reason: %ld", (long)connectionEvent, (long)reason);
    if (_connectionEventHandler)
    {
        _connectionEventHandler(connectionEvent, reason);
    }
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
    NSLog(@">> peripheral:didDiscoverServices:error => %@", error);
    if (error)
    {
        [self internalDisconnect:PXBlePeripheralConnectionEventReasonUnknown];
    }
    else if (![self hasAllRequiredServices:peripheral.services])
    {
        [self internalDisconnect:PXBlePeripheralConnectionEventReasonNotSupported];
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
    NSLog(@">> peripheral:didDiscoverCharacteristicsForService:error => %@", error);
    if (error)
    {
        [self internalDisconnect:PXBlePeripheralConnectionEventReasonUnknown];
    }
    else
    {
        NSAssert(_discoveringServicesCounter > 0, @"Discovered characteristics for more services than expected");
        --_discoveringServicesCounter;
        if (_discoveringServicesCounter == 0)
        {
            // Notify connected when characteristics are discovered for all services
            // We must assume that each service will at least report one characteristic
            [self qReportRequestResult:error forRequestType:PXBleRequestTypeConnect];
            [self qNotifyConnectionEvent:PXBlePeripheralConnectionEventReady reason:PXBlePeripheralConnectionEventReasonSuccess];
        }
    }
}

// - (void)peripheral:(CBPeripheral *)peripheral didDiscoverDescriptorsForCharacteristic:(CBCharacteristic *)characteristic error:(NSError *)error
// {
// }

- (void)peripheral:(CBPeripheral *)peripheral didUpdateValueForCharacteristic:(CBCharacteristic *)characteristic error:(NSError *)error
{
    NSLog(@">> peripheral:didUpdateValueForCharacteristic:error => %@", error);
    void (^handler)(CBCharacteristic *characteristic, NSError *error) = [_valueChangedHandlers objectForKey:characteristic];
    if (handler)
    {
        handler(characteristic, error);
    }
}

// - (void)peripheral:(CBPeripheral *)peripheral didUpdateValueForDescriptor:(CBDescriptor *)descriptor error:(NSError *)error
// {
// }

- (void)peripheral:(CBPeripheral *)peripheral didWriteValueForCharacteristic:(CBCharacteristic *)characteristic error:(NSError *)error
{
    NSLog(@">> peripheral:didWriteValueForCharacteristic:error => %@", error);
    [self qReportRequestResult:error forRequestType:PXBleRequestTypeWriteValue];
}

// - (void)peripheral:(CBPeripheral *)peripheral didWriteValueForDescriptor:(CBDescriptor *)descriptor error:(NSError *)error
// {
// }

// - (void)peripheralIsReadyToSendWriteWithoutResponse:(CBPeripheral *)peripheral
// {
// }

- (void)peripheral:(CBPeripheral *)peripheral didUpdateNotificationStateForCharacteristic:(CBCharacteristic *)characteristic error:(NSError *)error
{
    NSLog(@">> peripheral:didUpdateNotificationStateForCharacteristic:error => %@", error);
    [self qReportRequestResult:error forRequestType:PXBleRequestTypeSetNotifyValue];
}

- (void)peripheral:(CBPeripheral *)peripheral didReadRSSI:(NSNumber *)RSSI error:(NSError *)error
{
    NSLog(@">> peripheral:didReadRSSI:error => %@", error);
    _rssi = RSSI.intValue;
    [self qReportRequestResult:error forRequestType:PXBleRequestTypeReadRssi];
}

// - (void)peripheralDidUpdateName:(CBPeripheral *)peripheral
// {
// }

@end
