#import <Foundation/Foundation.h>
#import <CoreBluetooth/CoreBluetooth.h>
#import "PXBleCentralManagerDelegate.h"
#import "BleUtils.h"


typedef NS_ENUM(NSInteger, PXBlePeripheralConnectionEventReason)
{
    PXBlePeripheralConnectionEventReasonUnknown = -1,
    PXBlePeripheralConnectionEventReasonSuccess = 0,
    PXBlePeripheralConnectionEventReasonCanceled,
    PXBlePeripheralConnectionEventReasonNotSupported,
    PXBlePeripheralConnectionEventReasonTimeout,
    PXBlePeripheralConnectionEventReasonLinkLoss,
    PXBlePeripheralConnectionEventReasonAdpaterOff,
};

typedef NS_ENUM(NSInteger, PXBlePeripheralRequestError)
{
    PXBlePeripheralRequestErrorDisconnected,
    PXBlePeripheralRequestErrorInvalidCall,
    PXBlePeripheralRequestErrorInvalidParameters,
    PXBlePeripheralRequestErrorCanceled,
};

// We can't find any reliable information about the thread safety of CoreBluetooth APIs
// so we're going to assume that CBCentralManager, CBPeripheral, CBCharacteristic and CBDescriptor
// achieve any required synchronization with the given queue
// When no queue is given to the central manager, it defaults to the main thread serial queue.
// We create a serial queue as well so we don't have to worry about synchronization when we use
// the queue ourselves (and obviously CoreBluetooth works well with a serial queue).
// We're not concerned about performance by using a serial queue as Bluetooth LE operations
// are "low" frequency by design anyways.

typedef NS_ENUM(NSInteger, PXBleRequestType)
{
    PXBleRequestTypeUnknown = 0,
    PXBleRequestTypeConnect,
    PXBleRequestTypeDisconnect,
    PXBleRequestTypeReadRssi,
    PXBleRequestTypeReadValue,
    PXBleRequestTypeWriteValue,
    PXBleRequestTypeSetNotifyValue,
};

typedef NSError *(^PXBleRequestExecuteHandler)();
typedef void (^PXBleRequestCompletionHandler)(NSError *error);

@interface PXBleRequest : NSObject
{
    PXBleRequestType _type;
    PXBleRequestExecuteHandler _executeHandler;
    PXBleRequestCompletionHandler _completionHandler;
}

@property(readonly, getter=type) PXBleRequestType type;
- (PXBleRequestType)type;

// executeHandler returns an error if it has failed immediatly
// completionHandler can be nil
- (instancetype)initWithRequestType:(PXBleRequestType)requestType executeHandler:(PXBleRequestExecuteHandler)executeHandler  completionHandler:(PXBleRequestCompletionHandler)completionHandler;

- (NSError *)execute;

- (void)notifyResult:(NSError *)error;

@end

// This queues BLE operations (requests)
// The connect request will wait indefinitely until the connection is made, and will block
// any further request until connection. To cancel a blocking connection call cancelQueue().
// A request keep a strong reference to the instance so the later won't be de-allocated until the queue is empty.
// On being de-allocated, instance will cancel connection to peripheral
// Handlers (such as request completion handler) are called on the shared BLE queue,
// user code for those handlers should return as quickly as possible to avoid blocking/delaying any other BLE event.
@interface PXBlePeripheral : NSObject<CBPeripheralDelegate>
{
    dispatch_queue_t _queue; // Run all peripheral requests
    PXBleCentralManagerDelegate *_centralDelegate;
    CBPeripheral *_peripheral;
    void (^_connectionEventHandler)(PXBlePeripheralConnectionEvent connectionEvent, PXBlePeripheralConnectionEventReason reason);
    NSArray<CBUUID *> *_requiredServices;
    NSUInteger _discoveringServicesCounter;
    PXBlePeripheralConnectionEventReason _disconnectReason;
    int _rssi;
    PXBleRequest *_runningRequest; // Accessed only from queue
    NSMutableArray<PXBleRequest *> *_pendingRequests; // Always synchronize access to this list
    NSMapTable<CBCharacteristic *, void (^)(CBCharacteristic *characteristic, NSError *error)> *_valueChangedHandlers;
}

@property(nonatomic, readonly, getter=systemId) NSUUID *identifier;
- (NSUUID *)identifier;

@property(readonly, getter=isConnected) bool isConnected;
- (bool)isConnected;

@property(readonly, getter=rssi) int rssi;
- (int)rssi;

- (instancetype)initWithPeripheral:(CBPeripheral *)peripheral
            centralManagerDelegate:(PXBleCentralManagerDelegate *)centralManagerDelegate
    connectionStatusChangedHandler:(void (^)(PXBlePeripheralConnectionEvent connectionEvent, PXBlePeripheralConnectionEventReason reason))connectionEventHandler;

- (void)queueConnectWithServices:(NSArray<CBUUID *> *)services
               completionHandler:(void (^)(NSError *error))completionHandler;

- (void)queueDisconnect:(void (^)(NSError *error))completionHandler;

- (void)queueReadRssi:(void (^)(NSError *error))completionHandler;

- (void)queueReadValueForCharacteristic:(CBCharacteristic *)characteristic
                    valueChangedHandler:(void (^)(CBCharacteristic *characteristic, NSError *error))valueChangedHandler
                      completionHandler:(void (^)(NSError *error))completionHandler;

- (void)queueWriteValue:(NSData *)data
      forCharacteristic:(CBCharacteristic *)characteristic
                   type:(CBCharacteristicWriteType)type
      completionHandler:(void (^)(NSError *error))completionHandler;

- (void)queueSetNotifyValueForCharacteristic:(CBCharacteristic *)characteristic
                         valueChangedHandler:(void (^)(CBCharacteristic *characteristic, NSError *error))valueChangedHandler
                           completionHandler:(void (^)(NSError *error))completionHandler;

- (void)cancelQueue;

@end
