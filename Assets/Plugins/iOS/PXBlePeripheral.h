#import <Foundation/Foundation.h>
#import <CoreBluetooth/CoreBluetooth.h>
#import "PXBleCentralManagerDelegate.h"
#import "BleUtils.h"


typedef NS_ENUM(NSInteger, PXBlePeripheralConnectionEventReason)
{
    PXBlePeripheralConnectionEventReasonUnknown = -1,
    PXBlePeripheralConnectionEventReasonSuccess,
    PXBlePeripheralConnectionEventReasonUnused1,
    PXBlePeripheralConnectionEventReasonUnused2,
    PXBlePeripheralConnectionEventReasonUnreachable,
    PXBlePeripheralConnectionEventReasonNotSupported,
    PXBlePeripheralConnectionEventReasonCancelled,
};

// We can't find any reliable information about the thread safety of CoreBluetooth APIs
// so we're going to assume that CBCentralManager, CBPeripheral, CBCharacteristic and CBDescriptor
// achieve any required synchronization with the given queue
// When no queue is given to the central manager, it defaults to the main thread serial queue.
// We create a serial queue as well so we don't have to worry about synchronization when we use
// the queue ourselves (and obviously CoreBluetooth works well with a serial queue).
// We're not concerned about performance by using a serial queue as Bluetooth LE operations
// are "low" frequency by design anyways.

// This queues BLE operations (requests)
// Note that disconnect request is also queued, and that request keep a strong reference to the instance
// so the instance won't be de-allocated until the queue is empty
// On being deallocated, instance will cancel connection to peripheral
@interface PXBlePeripheral : NSObject<CBPeripheralDelegate>
{
    dispatch_queue_t _queue;
    PXBleCentralManagerDelegate *_centralDelegate;
    CBPeripheral *_peripheral;
    void (^_connectionEventHandler)(PXBlePeripheralConnectionEvent connectionEvent, PXBlePeripheralConnectionEventReason reason);
    NSArray<CBUUID *> *_requiredServices;
    NSUInteger _discoveringServicesCounter;
    PXBlePeripheralConnectionEventReason _discoveryDisconnectReason;
    int _rssi;
    NSMutableArray<bool (^)()> *_pendingRequests;
    NSMutableArray<void (^)(NSError *error)> *_completionHandlers;
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

@end
