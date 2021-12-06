from enum import Enum, IntEnum, auto
from uuid import UUID


class PixelUuids:
    service = UUID('{6e400001-b5a3-f393-e0a9-e50e24dcca9e}')
    notify = UUID('{6e400001-b5a3-f393-e0a9-e50e24dcca9e}')
    write = UUID('{6e400002-b5a3-f393-e0a9-e50e24dcca9e}')


# Lists all the Pixel dice message types.
# The value is used for the first byte of data in a Pixel message to identify it's type.
class MessageType(IntEnum):
    NONE = 0,
    WHO_ARE_YOU = auto()
    I_AM_A_DIE = auto()
    ROLL_STATE = auto()
    TELEMETRY = auto()
    BULK_SETUP = auto()
    BULK_SETUP_ACK = auto()
    BULK_DATA = auto()
    BULK_DATA_ACK = auto()
    TRANSFER_ANIMATION_SET = auto()
    TRANSFER_ANIMATION_SET_ACK = auto()
    TRANSFER_ANIMATION_SET_FINISHED = auto()
    TRANSFER_SETTINGS = auto()
    TRANSFER_SETTINGS_ACK = auto()
    TRANSFER_SETTINGS_FINISHED = auto()
    TRANSFER_TEST_ANIMATION_SET = auto()
    TRANSFER_TEST_ANIMATION_SET_ACK = auto()
    TRANSFER_TEST_ANIMATION_SET_FINISHED = auto()
    DEBUG_LOG = auto()
    PLAY_ANIMATION = auto()
    PLAY_ANIMATION_EVENT = auto()
    STOP_ANIMATION = auto()
    PLAY_SOUND = auto()
    REQUEST_ROLL_STATE = auto()
    REQUEST_ANIMATION_SET = auto()
    REQUEST_SETTINGS = auto()
    REQUEST_TELEMETRY = auto()
    PROGRAM_DEFAULT_ANIMATION_SET = auto()
    PROGRAM_DEFAULT_ANIMATION_SET_FINISHED = auto()
    BLINK = auto()
    BLINK_FINISHED = auto()
    REQUEST_DEFAULT_ANIMATION_SET_COLOR = auto()
    DEFAULT_ANIMATION_SET_COLOR = auto()
    REQUEST_BATTERY_LEVEL = auto()
    BATTERY_LEVEL = auto()
    REQUEST_RSSI = auto()
    RSSI = auto()
    CALIBRATE = auto()
    CALIBRATE_FACE = auto()
    NOTIFY_USER = auto()
    NOTIFY_USER_ACK = auto()
    TEST_HARDWARE = auto()
    SET_STANDARD_STATE = auto()
    SET_LEDANIMATION_STATE = auto()
    SET_BATTLE_STATE = auto()
    PROGRAM_DEFAULT_PARAMETERS = auto()
    PROGRAM_DEFAULT_PARAMETERS_FINISHED = auto()
    SET_DESIGN_AND_COLOR = auto()
    SET_DESIGN_AND_COLOR_ACK = auto()
    SET_CURRENT_BEHAVIOR = auto()
    SET_CURRENT_BEHAVIOR_ACK = auto()
    SET_NAME = auto()
    SET_NAME_ACK = auto()
    # Testing
    TEST_BULK_SEND = auto()
    TEST_BULK_RECEIVE = auto()
    SET_ALL_LEDS_TO_COLOR = auto()
    ATTRACT_MODE = auto()
    PRINT_NORMALS = auto()
    PRINT_A2D_READINGS = auto()
    LIGHT_UP_FACE = auto()
    SET_LED_TO_COLOR = auto()
    DEBUG_ANIMATION_CONTROLLER = auto()


# Available combinations of Pixel designs and colors.
class PixelDesignAndColor(Enum):
    UNKNOWN = 0
    GENERIC = auto()
    V3_ORANGE = auto()
    V4_BLACK_CLEAR = auto()
    V4_WHITE_CLEAR = auto()
    V5_GREY = auto()
    V5_WHITE = auto()
    V5_BLACK = auto()
    V5_GOLD = auto()
    ONYX_BACK = auto()
    HEMATITE_GREY = auto()
    MIDNIGHT_GALAXY = auto()
    AURORA_SKY = auto()


# Pixel roll states.
class PixelRollState(Enum):

    # The Pixel roll state could not be determined.
    UNKNOWN = 0

    # The Pixel is resting in a position with a face up.
    ON_FACE = 1

    # The Pixel is being handled.
    HANDLING = 2

    # The Pixel is rolling.
    ROLLING = 3

    # The Pixel is resting in a crooked position.
    CROOKED = 4


def msg_to_str(data: bytes) -> None:
    t = MessageType(data[0])
    if t == MessageType.I_AM_A_DIE:
        return f"Face count: {data[1]}, design & color: {PixelDesignAndColor(data[2])}"
    elif t == MessageType.ROLL_STATE:
        return f"State: {PixelRollState(data[1])}, face up: {data[2] + 1}"
    elif t == MessageType.BATTERY_LEVEL:
        import struct
        level = round(100 * struct.unpack('f', data[1:5])[0])
        voltage = struct.unpack('f', data[5:9])[0]
        charging = "charging" if data[9] else "not charging"
        return f"Battery: {level}%, {voltage:.2f}V, {charging}"
    elif t == MessageType.RSSI:
        rssi = data[0] + (data[1] << 8)
        return f"RSSI: {rssi}"
    else:
        return str(MessageType(t))
