/**
 * @file
 * @brief Internal types for representing a BLE request.
 */

#import <Foundation/Foundation.h>

// We can't find any reliable information about the thread safety of CoreBluetooth APIs
// so we're going to assume that CBCentralManager, CBPeripheral, CBCharacteristic and CBDescriptor
// achieve any required synchronization with the given queue
// When no queue is given to the central manager, it defaults to the main thread serial queue.
// We create a serial queue as well so we don't have to worry about synchronization when we use
// the queue ourselves (and obviously CoreBluetooth works well with a serial queue).
// We're not concerned about performance by using a serial queue as Bluetooth LE operations
// are "low" frequency by design anyways.

typedef NS_ENUM(NSInteger, SGBleRequestType)
{
    SGBleRequestTypeUnknown = 0,
    SGBleRequestTypeConnect,
    SGBleRequestTypeDisconnect,
    SGBleRequestTypeReadRssi,
    SGBleRequestTypeReadValue,
    SGBleRequestTypeWriteValue,
    SGBleRequestTypeSetNotifyValue,
};

typedef NSError * (^SGBleRequestExecuteHandler)();
typedef void (^SGBleRequestCompletionHandler)(NSError *error);

@interface SGBleRequest : NSObject
{
    SGBleRequestType _type;
    SGBleRequestExecuteHandler _executeHandler;
    SGBleRequestCompletionHandler _completionHandler;
}

@property(readonly, getter=type) SGBleRequestType type;
- (SGBleRequestType)type;

// executeHandler returns an error if it has failed immediately
// completionHandler can be nil
- (instancetype)initWithRequestType:(SGBleRequestType)requestType executeHandler:(SGBleRequestExecuteHandler)executeHandler completionHandler:(SGBleRequestCompletionHandler)completionHandler;

- (NSError *)execute;

- (void)notifyResult:(NSError *)error;

+ (NSString *)getRequestTypeString(SGBleRequestType type);

@end

//! @endcond
