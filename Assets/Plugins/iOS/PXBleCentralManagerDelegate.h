#import <Foundation/Foundation.h>
#import <CoreBluetooth/CoreBluetooth.h>
#import "BleUtils.h"


typedef NS_ENUM(NSInteger, PXBlePeripheralConnectionEvent)
{
    PXBlePeripheralConnectionEventConnecting,
    PXBlePeripheralConnectionEventConnected,
    PXBlePeripheralConnectionEventFailedToConnect, // + reason
    PXBlePeripheralConnectionEventReady,
    PXBlePeripheralConnectionEventDisconnecting,
    PXBlePeripheralConnectionEventDisconnected, // + reason
};


typedef void (^PXBlePeripheralDiscoveryHandler)(CBPeripheral *peripheral, NSDictionary<NSString *,id> *advertisementData, NSNumber *RSSI);
typedef void (^PXBlePeripheralConnectionEventHandler)(CBPeripheral *peripheral, PXBlePeripheralConnectionEvent connectionEvent, NSError *error);


@interface PXBleCentralManagerDelegate : NSObject<CBCentralManagerDelegate>
{
    NSObject *_startScanSync;
    void (^_stateUpdateHandler)(CBManagerState state);
    CBCentralManager *_centralManager;
    NSMutableDictionary<NSUUID *,CBPeripheral *> *_peripherals;
    NSMutableDictionary<CBPeripheral *, PXBlePeripheralConnectionEventHandler> *_peripheralsConnectionEventHandlers;
}

@property(readonly, getter=centralManager) CBCentralManager *centralManager;
- (CBCentralManager *)centralManager;

@property(strong) PXBlePeripheralDiscoveryHandler peripheralDiscoveryHandler;

@property(readonly, getter=peripherals) NSArray<CBPeripheral *> *peripherals;
- (NSArray<CBPeripheral *> *)peripherals;

@property(nonatomic, readonly, getter=isBluetoothOn) bool isBluetoothOn;
- (bool)isBluetoothOn;

- (instancetype)initWithStateUpdateHandler:(void (^)(CBManagerState state))stateUpdateHandler;

- (void)clearPeripherals;

- (CBPeripheral *)peripheralForIdentifier:(NSUUID *)identifier;

- (void)setConnectionEventHandler:(PXBlePeripheralConnectionEventHandler)peripheralConnectionEventHandler
                    forPeripheral:(CBPeripheral *)peripheral;

@end
