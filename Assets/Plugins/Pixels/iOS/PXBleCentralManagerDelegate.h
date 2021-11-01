#import <Foundation/Foundation.h>
#import <CoreBluetooth/CoreBluetooth.h>
#import "BleUtils.h"


typedef NS_ENUM(NSInteger, SGBlePeripheralConnectionEvent)
{
    SGBlePeripheralConnectionEventConnecting,
    SGBlePeripheralConnectionEventConnected,
    SGBlePeripheralConnectionEventFailedToConnect, // + reason
    SGBlePeripheralConnectionEventReady,
    SGBlePeripheralConnectionEventDisconnecting,
    SGBlePeripheralConnectionEventDisconnected, // + reason
};


typedef void (^SGBlePeripheralDiscoveryHandler)(CBPeripheral *peripheral, NSDictionary<NSString *,id> *advertisementData, NSNumber *RSSI);
typedef void (^SGBlePeripheralConnectionEventHandler)(CBPeripheral *peripheral, SGBlePeripheralConnectionEvent connectionEvent, NSError *error);


@interface SGBleCentralManagerDelegate : NSObject<CBCentralManagerDelegate>
{
    NSObject *_startScanSync;
    void (^_stateUpdateHandler)(CBManagerState state);
    CBCentralManager *_centralManager;
    NSMutableDictionary<NSUUID *,CBPeripheral *> *_peripherals;
    NSMutableDictionary<CBPeripheral *, SGBlePeripheralConnectionEventHandler> *_peripheralsConnectionEventHandlers;
}

@property(readonly, getter=centralManager) CBCentralManager *centralManager;
- (CBCentralManager *)centralManager;

@property(strong) SGBlePeripheralDiscoveryHandler peripheralDiscoveryHandler;

@property(readonly, getter=peripherals) NSArray<CBPeripheral *> *peripherals;
- (NSArray<CBPeripheral *> *)peripherals;

@property(nonatomic, readonly, getter=isBluetoothOn) bool isBluetoothOn;
- (bool)isBluetoothOn;

- (instancetype)initWithStateUpdateHandler:(void (^)(CBManagerState state))stateUpdateHandler;

- (void)clearPeripherals;

- (CBPeripheral *)peripheralForIdentifier:(NSUUID *)identifier;

- (void)setConnectionEventHandler:(SGBlePeripheralConnectionEventHandler)peripheralConnectionEventHandler
                    forPeripheral:(CBPeripheral *)peripheral;

@end
