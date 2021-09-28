#import "BleUtils.h"


dispatch_queue_t GetBleSerialQueue()
{
    static dispatch_queue_t queue =
        dispatch_queue_create("com.systemic.pixels.ble", DISPATCH_QUEUE_SERIAL);
    return queue;
}
