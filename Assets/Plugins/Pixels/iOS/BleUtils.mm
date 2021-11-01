#import "BleUtils.h"

NSErrorDomain pxBleGetErrorDomain()
{
    static NSErrorDomain pxBleErrorDomain = [NSString stringWithFormat:@"%@.pxBLE.errorDomain", [[NSBundle mainBundle] bundleIdentifier]];
    return pxBleErrorDomain;;
}


dispatch_queue_t pxBleGetSerialQueue()
{
    static dispatch_queue_t queue =
        dispatch_queue_create("com.systemic.pixels.ble", DISPATCH_QUEUE_SERIAL);
    return queue;
}
