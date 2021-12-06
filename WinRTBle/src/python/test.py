import time
import asyncio

from ble import *
from pixel import *


# Thread safe print
# printlock = threading.Lock()
# sysprint = print
# def print(*args, **kwargs) -> None:
# 	with printlock:
# 		sysprint(*args, **kwargs)


async def test():

    libpath = os.path.dirname(os.path.abspath(__file__)) + \
        os.path.sep + r"..\..\bin\x64\Release\LibWinRTBle.dll"
    initialize(libpath)

    # Print process id for attaching debugger
    # import os
    # print(str(os.getpid()))
    # input("Press Enter to start...\n")

    startscan(PixelUuids.service)

    while not len(scannedPeripherals()):
        time.sleep(0.1)

    stopscan()

    # Get first scanned Pixel
    pixel = list(scannedPeripherals().values())[0]
    print("Found pixel " + pixel.name)

    def on_connection_event(connection_ev: ConnectionEvent, reason: ConnectionEventReason):
        print(f"==> Connection event: {str(connection_ev)}, {str(reason)}")

    if create_peripheral(pixel.address, on_connection_event):

        addr = pixel.address

        await connect_peripheral(addr, PixelUuids.service)

        print(f"name: {get_peripheral_name(addr)}")
        print(f"mtu: {get_peripheral_mtu(addr)}")
        for service in get_discovered_services(addr):
            print(f"services: {service}")
            for characteristic in get_service_characteristics(addr, service):
                props = get_characteristic_properties(
                    addr, service, characteristic, 0)
                print(f"           * {characteristic} => {props}")

        def on_value_changed(data: bytes):
            print("==> " + msg_to_str(data))

        # await read_characteristic(addr, service, notify, 0, on_value_changed)

        await subscribe_characteristic(addr, PixelUuids.service, PixelUuids.notify, 0, on_value_changed)

        await write_characteristic(addr, PixelUuids.service, PixelUuids.write, 0, bytes([int(MessageType.WHO_ARE_YOU)]))
        await write_characteristic(addr, PixelUuids.service, PixelUuids.write, 0, bytes([int(MessageType.REQUEST_BATTERY_LEVEL)]))
        await write_characteristic(addr, PixelUuids.service, PixelUuids.write, 0, bytes([int(MessageType.REQUEST_RSSI)]))
        await write_characteristic(addr, PixelUuids.service, PixelUuids.write, 0, bytes([int(MessageType.REQUEST_ROLL_STATE)]))

        #input("Press Enter to quit...\n")
        await asyncio.sleep(1)

        release_peripheral(addr)

    shutdown()


asyncio.run(test())
