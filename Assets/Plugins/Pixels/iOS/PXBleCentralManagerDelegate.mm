#import "PXBleCentralManagerDelegate.h"


@implementation PXBleCentralManagerDelegate

@synthesize peripheralDiscoveryHandler;

//
// Getters
//

- (CBCentralManager *)centralManager
{
    return _centralManager;
}

- (NSArray<CBPeripheral *> *)peripherals
{
    @synchronized(_peripherals)
    {
        return _peripherals.allValues;
    }
}

- (bool)isBluetoothOn
{
    return _centralManager.state == CBManagerStatePoweredOn;
}

//
// Public methods
//

- (instancetype)initWithStateUpdateHandler:(void (^)(CBManagerState state))stateUpdateHandler
{
    if (self = [super init])
    {
        _startScanSync = [NSObject new];
        _stateUpdateHandler = stateUpdateHandler;
        _centralManager = [[CBCentralManager alloc] initWithDelegate:self queue:pxBleGetSerialQueue()];
        _peripherals = [NSMutableDictionary<NSUUID *,CBPeripheral *> new];
        _peripheralsConnectionEventHandlers = [NSMutableDictionary<CBPeripheral *, PXBlePeripheralConnectionEventHandler> new];
    }
    return self;
}

#if DEBUG
- (void)dealloc
{
    NSLog(@"PXBleCentralManagerDelegate dealloc");
}
#endif

- (void)clearPeripherals
{
    [_peripherals removeAllObjects];
}

// Returns nil if not found
- (CBPeripheral *)peripheralForIdentifier:(NSUUID *)identifier;
{
    @synchronized(_peripherals)
    {
        return _peripherals[identifier];
    }
}

- (void)setConnectionEventHandler:(PXBlePeripheralConnectionEventHandler)peripheralConnectionEventHandler
                    forPeripheral:(CBPeripheral *)peripheral
{
    @synchronized (_peripheralsConnectionEventHandlers)
    {
        _peripheralsConnectionEventHandlers[peripheral] = peripheralConnectionEventHandler;
    }
}

//
// Private methods
//

- (void)raiseConnectionEventForPeripheral:(CBPeripheral *)peripheral
                          connectionEvent:(PXBlePeripheralConnectionEvent)connectionEvent
                                    error:(NSError *)error
{
    PXBlePeripheralConnectionEventHandler handler;
    @synchronized (_peripheralsConnectionEventHandlers)
    {
        handler = _peripheralsConnectionEventHandlers[peripheral];
    }
    if (handler)
    {
        handler(peripheral, connectionEvent, error);
    }
}

//
// CBCentralManagerDelegate implementation
//

- (void)centralManagerDidUpdateState:(CBCentralManager *)central
{
    if (_stateUpdateHandler)
    {
        _stateUpdateHandler(central.state);
    }
}

- (void)centralManager:(CBCentralManager *)central
 didDiscoverPeripheral:(CBPeripheral *)peripheral
     advertisementData:(NSDictionary<NSString *,id> *)advertisementData
                  RSSI:(NSNumber *)RSSI
{
    // Need to keep a reference to peripheral so the system doesn't deallocate it
    @synchronized(_peripherals)
    {
        _peripherals[peripheral.identifier] = peripheral;
    }
    PXBlePeripheralDiscoveryHandler handler = self.peripheralDiscoveryHandler;
    if (handler)
    {
        handler(peripheral, advertisementData, RSSI);
    }
}

- (void)centralManager:(CBCentralManager *)central didConnectPeripheral:(CBPeripheral *)peripheral
{
    [self raiseConnectionEventForPeripheral:peripheral connectionEvent:PXBlePeripheralConnectionEventConnected error:nil];
}

- (void)centralManager:(CBCentralManager *)central didDisconnectPeripheral:(CBPeripheral *)peripheral error:(NSError *)error
{
    [self raiseConnectionEventForPeripheral:peripheral connectionEvent:PXBlePeripheralConnectionEventDisconnected error:error];
}

- (void)centralManager:(CBCentralManager *)central didFailToConnectPeripheral:(CBPeripheral *)peripheral error:(NSError *)error
{
    [self raiseConnectionEventForPeripheral:peripheral connectionEvent:PXBlePeripheralConnectionEventFailedToConnect error:error];
}

// - (void)centralManager:(CBCentralManager *)central connectionEventDidOccur:(CBConnectionEvent)event forPeripheral:(CBPeripheral *)peripheral
// {
// }

@end
