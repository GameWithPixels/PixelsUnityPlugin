import asyncio
import os
import ctypes
from enum import Enum, Flag, IntEnum, auto
from uuid import UUID
from typing import Optional, Tuple, Union
from collections.abc import Iterable



# Peripheral connection events.
class ConnectionEvent(IntEnum):
    # Raised at the beginning of the connect sequence and is followed either by Connected or FailedToConnect.
    CONNECTING = 0

    # Raised once the peripheral is connected, just before services are being discovered.
    CONNECTED = 1

    # Raised when the peripheral fails to connect, the reason of failure is also given.
    FAILED_TO_CONNECT = 2

    # Raised after a Connected event, once the required services have been discovered.
    READY = 3

    # Raised at the beginning of a user initiated disconnect.
    DISCONNECTING = 4

    # Raised when the peripheral is disconnected, the reason for the disconnection is also given.
    DISCONNECTED = 5


# Peripheral connection event reasons.
class ConnectionEventReason(IntEnum):
    # The disconnect happened for an unknown reason.
    UNKNOWN = -1

    # The disconnect was initiated by user.
    SUCCESS = 0

    # Connection attempt canceled by user.
    CANCELED = 1

    # Peripheral doesn't have all required services.
    NOT_SUPPORTED = 2

    # Peripheral didn't responded in time.
    TIMEOUT = 3

    # Peripheral was disconnected while in "auto connect" mode.
    LINK_LOSS = 4

    # The local device Bluetooth adapter is off.
    ADPATER_OFF = 5

    # Disconnection was initiated by peripheral.
    PERIPHERAL = 6


# Peripheral requests statuses.
class BleRequestStatus(Enum):
    # The request completed successfully.
    Success = 0

    # The request completed with a non-specific error.
    ERROR = auto()

    # The request is still in progress.
    IN_PROGRESS = auto()

    # The request was canceled.
    CANCELED = auto()

    # The request was aborted because the peripheral got disconnected.
    DISCONNECTED = auto()

    # The request did not run because the given peripheral is not valid.
    INVALID_PERIPHERAL = auto()

    # The request did not run because the operation is not valid or permitted.
    INVALID_CALL = auto()

    # The request did not run because some of its parameters are invalid.
    INVALID_PARAMETERS = auto()

    # The request failed because of the operation is not supported by the peripheral.
    NOT_SUPPORTED = auto()

    # The request failed because of BLE protocol error.
    PROTOCOL_ERROR = auto()

    # The request failed because it was denied access.
    ACCESS_DENIED = auto()

    # The request failed because the Bluetooth radio is off.
    ADAPTER_OFF = auto()

    # The request did not succeed after the timeout period.
    TIMEOUT = auto()


# Standard BLE values for characteristic properties, those are flags that can be combined.
class CharacteristicProperties(Flag):
    NONE = 0

    # Characteristic is broadcastable.
    BROADCAST = 0x001

    # Characteristic is readable.
    READ = 0x002

    # Characteristic can be written without response.
    WRITE_WITHOUT_RESPONSE = 0x004

    # Characteristic can be written.
    WRITE = 0x008

    # Characteristic supports notification.
    NOTIFY = 0x010

    # Characteristic supports indication.
    INDICATE = 0x020

    # Characteristic supports write with signature.
    SIGNED_WRITE = 0x040

    # Characteristic has extended properties.
    EXTENDED_PROPERTIES = 0x080

    # Characteristic notification uses encryption.
    NOTIFY_ENCRYPTION_REQUIRED = 0x100

    # Characteristic indication uses encryption.
    INDICATE_ENCRYPTION_REQUIRED = 0x200


class ScannedPeripheral:
    def __init__(self, system_id: str, address: str, name: str) -> None:
        self._system_id = system_id
        self._address = address
        self._name = name

    @property
    def system_id(self) -> str:
        return self._system_id

    @property
    def address(self) -> str:
        return self._address

    @property
    def name(self) -> str:
        return self._name


_scanned = dict()
_blelib = None
_reqstatus_ev = None


def _uuids_to_c_char_p(uuids: Union[UUID, Iterable[UUID]] = None):
    bytes = None
    if uuids:
        if not isinstance(uuids, Iterable) and not isinstance(uuids, str):
            uuids = [uuids]
        uuids_str = [str(uuid) for uuid in uuids if uuid]
        if len(uuids_str):
            bytes = ','.join(uuids_str).encode('utf-8')
    return ctypes.c_char_p(bytes)


def _to_c(address: int, service: Optional[UUID] = None, characteristic: Optional[UUID] = None, index: Optional[int] = 0) -> Union[ctypes.c_int64, Tuple[ctypes.c_int64, ctypes.c_char_p], Tuple[ctypes.c_int64, ctypes.c_char_p, ctypes.c_char_p, ctypes.c_uint32]]:
    if not service:
        return ctypes.c_int64(address)
    elif not characteristic:
        return (ctypes.c_int64(address), ctypes.c_char_p(str(service).encode('utf-8')))
    else:
        return (ctypes.c_int64(address),
                ctypes.c_char_p(str(service).encode('utf-8')),
                ctypes.c_char_p(str(characteristic).encode('utf-8')),
                ctypes.c_uint32(0 if index is None else index))


def _retstr_from_c(str_ptr: ctypes.POINTER(ctypes.c_char)) -> Optional[str]:
    try:
        if str_ptr:
            return ctypes.cast(str_ptr, ctypes.c_char_p).value.decode('utf-8')
    finally:
        _blelib.sgFreeString(str_ptr)


def _retuuids_from_c(str_ptr: ctypes.POINTER(ctypes.c_char)) -> Optional[list[UUID]]:
    str = _retstr_from_c(str_ptr)
    if str:
        return [UUID(uuid) for uuid in str.split(',') if uuid]


def initialize(libpath: str) -> bool:

    global _reqstatus_ev
    _reqstatus_ev = asyncio.Event()

    global _blelib
    _blelib = ctypes.CDLL(libpath)

    # We run in multi-threaded COM apartment, WinRT coroutines
    # didn't worked properly during tests in single threaded apartment
    return _blelib.sgBleInitialize(ctypes.c_bool(False), ctypes.c_void_p(None))


def shutdown() -> None:
    _blelib.sgBleShutdown()


def _onDiscoveredPeripheral(advertisement_data_json: bytes) -> None:
    jsonstr = advertisement_data_json.decode('utf-8')
    #print("==> _onDiscoveredPeripheral: " + jsonstr)
    import json

    def object_hook(dct):
        return ScannedPeripheral(
            dct['systemId'], dct['address'], dct['name']
        ) if 'systemId' in dct else dct
    obj = json.loads(jsonstr, object_hook=object_hook)
    if isinstance(obj, ScannedPeripheral):
        _scanned[obj.system_id] = obj


_DiscoveredPeripheralCallback = ctypes.CFUNCTYPE(None, ctypes.c_char_p)
_discovery_cb = _DiscoveredPeripheralCallback(_onDiscoveredPeripheral)


def startscan(required_services: Union[UUID, Iterable[UUID]] = None) -> bool:
    return _blelib.sgBleStartScan(
        _uuids_to_c_char_p(required_services),
        _discovery_cb)


def stopscan() -> None:
    _blelib.sgBleStopScan()


def scannedPeripherals() -> dict[str, ScannedPeripheral]:
    return _scanned


_on_connection_event_handler = None


def _onPeripheralConnectionEvent(address: int, connection_ev: int, reason: int):
    if _on_connection_event_handler:
        _on_connection_event_handler(ConnectionEvent(connection_ev), ConnectionEventReason(reason))


_PeripheralConnectionEventCallback = ctypes.CFUNCTYPE(
    None, ctypes.c_int64, ctypes.c_int, ctypes.c_int)
_connevent_cb = _PeripheralConnectionEventCallback(
    _onPeripheralConnectionEvent)


def create_peripheral(address: int, on_connection_event) -> bool:
    #on_connection_event: callable[ConnectionEvent, ConnectionEventReason]
    global _on_connection_event_handler
    _on_connection_event_handler = on_connection_event
    return _blelib.sgBleCreatePeripheral(_to_c(address), _connevent_cb)


def release_peripheral(address: int) -> None:
    return _blelib.sgBleReleasePeripheral(_to_c(address))


def onRequestStatus(status: int) -> None:
    def doit():
        if status:
            print(f"==> onRequestStatus {BleRequestStatus(status)}")
        _reqstatus_ev.set()  # Must be on loop thread to set event
    _reqstatus_ev._loop.call_soon_threadsafe(doit)


_RequestStatusCallback = ctypes.CFUNCTYPE(None, ctypes.c_int)
_reqstatus_cb = _RequestStatusCallback(onRequestStatus)


async def connect_peripheral(address: int, required_services: Union[UUID, Iterable[UUID]] = None) -> bool:
    _reqstatus_ev.clear()
    _blelib.sgBleConnectPeripheral(
        _to_c(address),
        _uuids_to_c_char_p(required_services),
        ctypes.c_bool(False),
        _reqstatus_cb)
    await _reqstatus_ev.wait()


def disconnect_peripheral(address: int):
    _blelib.sgBleDisconnectPeripheral(
        _to_c(address),
        _reqstatus_cb)


def get_peripheral_name(address: int) -> Optional[str]:
    # can't use c_char_p because we'll need the pointer to free the memory
    _blelib.sgBleGetPeripheralName.restype = ctypes.POINTER(ctypes.c_char)
    return _retstr_from_c(_blelib.sgBleGetPeripheralName(_to_c(address)))


def get_peripheral_mtu(address: int) -> int:
    return _blelib.sgBleGetPeripheralMtu(_to_c(address))


def get_discovered_services(address: int) -> Optional[list[UUID]]:
    # can't use c_char_p because we'll need the pointer to free the memory
    _blelib.sgBleGetPeripheralDiscoveredServices.restype = ctypes.POINTER(
        ctypes.c_char)
    return _retuuids_from_c(_blelib.sgBleGetPeripheralDiscoveredServices(_to_c(address)))


def get_service_characteristics(address: int, service: UUID) -> Optional[list[UUID]]:
    # can't use c_char_p because we'll need the pointer to free the memory
    _blelib.sgBleGetPeripheralServiceCharacteristics.restype = ctypes.POINTER(
        ctypes.c_char)
    return _retuuids_from_c(_blelib.sgBleGetPeripheralServiceCharacteristics(
        *_to_c(address, service)))


def get_characteristic_properties(address: int, service: UUID, characteristic: UUID, index: int) -> CharacteristicProperties:
    prop = _blelib.sgBleGetCharacteristicProperties(
        *_to_c(address, service, characteristic, index))
    return CharacteristicProperties(prop)


def onValueRead(data: ctypes.POINTER(ctypes.c_ubyte), length: int, status: int) -> None:
    if status:
        print(f"==> onValueRead status: {BleRequestStatus(status)}")
    else:
        _onValueChanged(data, length)


_ValueReadCallback = ctypes.CFUNCTYPE(
    None, ctypes.POINTER(ctypes.c_ubyte), ctypes.c_size_t, ctypes.c_int)
_valueread_cb = _ValueReadCallback(onValueRead)


async def read_characteristic(address: int, service: UUID, characteristic: UUID, index: int, on_value_read) -> None:
    # on_value_read: callable[bytes]
    global _value_changed_handler
    _value_changed_handler = on_value_read
    _reqstatus_ev.clear()
    _blelib.sgBleReadCharacteristicValue(
        *_to_c(address, service, characteristic, index), _valueread_cb)
    await _reqstatus_ev.wait()


async def write_characteristic(address: int, service: UUID, characteristic: UUID, index: int, data: bytes, without_response: bool = False) -> None:
    _reqstatus_ev.clear()
    _blelib.sgBleWriteCharacteristicValue(
        *_to_c(address, service, characteristic, index),
        # cast(c_char_p(data), POINTER(c_ubyte)), len(data))
        ctypes.cast(data, ctypes.POINTER(ctypes.c_byte)),
        ctypes.c_size_t(len(data)),
        ctypes.c_bool(without_response),
        _reqstatus_cb)
    await _reqstatus_ev.wait()


_value_changed_handler = None


def _onValueChanged(data: ctypes.POINTER(ctypes.c_ubyte), length: int) -> None:
    c_ubyte_arr = ctypes.cast(data, ctypes.POINTER(ctypes.c_ubyte * length))
    _value_changed_handler(bytes(c_ubyte_arr.contents))


_ValueChangedCallback = ctypes.CFUNCTYPE(
    None, ctypes.POINTER(ctypes.c_ubyte), ctypes.c_size_t)
_valuechanged_cb = _ValueChangedCallback(_onValueChanged)


async def subscribe_characteristic(address: int, service: UUID, characteristic: UUID, index: int, on_value_changed) -> None:
    # on_value_changed: callable[bytes]
    global _value_changed_handler
    _value_changed_handler = on_value_changed
    _reqstatus_ev.clear()
    _blelib.sgBleSetNotifyCharacteristic(
        *_to_c(address, service, characteristic, index),
        _valuechanged_cb,
        _reqstatus_cb)
    await _reqstatus_ev.wait()


async def unsubscribe_characteristic(address: int, service: UUID, characteristic: UUID, index: int) -> None:
    _reqstatus_ev.clear()
    _blelib.sgBleSetNotifyCharacteristic(
        *_to_c(address, service, characteristic, index),
        _ValueChangedCallback(None),
        _reqstatus_cb)
    await _reqstatus_ev.wait()
