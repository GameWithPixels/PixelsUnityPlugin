#pragma once

#include "BleCommon.h"
#include <mutex>

namespace Pixels::CoreBluetoothLE
{
    using namespace winrt::Windows::Foundation;
    using namespace winrt::Windows::Devices::Bluetooth;
    using namespace winrt::Windows::Devices::Bluetooth::GenericAttributeProfile;

    class Service;

    enum class ConnectionEvent
    {
        Connecting,
        Connected,
        FailedToConnect, // + reason
        Ready,
        Disconnecting,
        Disconnected, // + reason
    };

    enum class ConnectionEventReason
    {
        Unknown = -1,
        Success = 0,
        _Unused1,
        _Unused2,
        Unreachable,
        NotSupported,
        _Unused3,
        ProtocolError,
        AccessDenied,
    };

    //TODO see BleRequestStatus
    enum class ConnectResult
    {
        Success, InvalidParameters, Error, Canceled
    };

    class Peripheral : public std::enable_shared_from_this<Peripheral>
    {
        const bluetooth_address_t _address{};
        BluetoothLEDevice _device{ nullptr };
        GattSession _session{ nullptr };
        winrt::event_token _connectionStatusChangedToken{};
        std::function<void(ConnectionEvent, ConnectionEventReason)> _onConnectionEvent{};
        std::unordered_map<winrt::guid, std::shared_ptr<Service>> _services{};
        volatile size_t _connectCounter{};
        volatile bool _connecting{};
        std::recursive_mutex _connectOpMtx{};
        std::vector<std::tuple<ConnectionEvent, ConnectionEventReason>> _connectionEventsQueue{};

    public:
        bool hasDevice() const { return _device != nullptr; }

        bool isConnected() const { auto d = _device; return d ? (d.ConnectionStatus() == BluetoothConnectionStatus::Connected) : false; }

        const wchar_t* deviceId() const { auto d = _device; return d ? d.DeviceId().data() : nullptr; }

        bluetooth_address_t address() const { return _address; }

        const wchar_t* name() const { auto d = _device; return d ? d.Name().data() : nullptr; }

        uint16_t mtu() const { auto s = _session; return s ? s.MaxPduSize() : 0; }

        std::vector<std::shared_ptr<Service>> discoveredServices()
        {
            std::lock_guard lock{ _connectOpMtx };
            std::vector<std::shared_ptr<Service>> ret{};
            for (auto& [_,s] : _services) ret.push_back(s);
            return ret;
        }

        Peripheral(bluetooth_address_t bluetoothAddress, std::function<void(ConnectionEvent, ConnectionEventReason)> onConnectionEvent)
            : _address{ bluetoothAddress }, _onConnectionEvent{ onConnectionEvent }
        {
            assert(bluetoothAddress); //TODO check args
            assert(onConnectionEvent);
            _connectionEventsQueue.reserve(16);
        }

        //TODO return error code
        std::future<BleRequestStatus> connectAsync(
            std::vector<winrt::guid> requiredServices = std::vector<winrt::guid>{},
            bool maintainConnection = false);

        void disconnect()
        {
            internalDisconnect(ConnectionEventReason::Success);
        }

        std::shared_ptr<Service> getDiscoveredService(const winrt::guid& uuid)
        {
            std::lock_guard lock{ _connectOpMtx };
            auto it = _services.find(uuid);
            return it != _services.end() ? it->second : nullptr;
        }

        ~Peripheral()
        {
            disconnect();
        }

    private:
        void internalDisconnect(ConnectionEventReason reason, bool fromDeviceEvent = false);

        void notifyQueuedConnectionEvents()
        {
            //TODO optimize for case with 1 or 2 elements in queue
            decltype(_connectionEventsQueue) queue{};
            {
                std::lock_guard lock{ _connectOpMtx };
                queue = _connectionEventsQueue;
                _connectionEventsQueue.clear();
            }
            if (_onConnectionEvent)
            {
                for (auto& [ev, reason] : queue)
                {
                    _onConnectionEvent(ev, reason);
                }
            }
        }

        void onDeviceConnectionStatusChanged(BluetoothLEDevice _, IInspectable __)
        {
            bool connected = false;
            {
                std::lock_guard lock{ _connectOpMtx };
                connected = isConnected();
                if (connected)
                {
                    _connectionEventsQueue.emplace_back(ConnectionEvent::Connected, ConnectionEventReason::Success);
                }
            }

            if (connected)
            {
                notifyQueuedConnectionEvents();
            }
            else
            {
                internalDisconnect(ConnectionEventReason::Unreachable, true);
            }
        }
    };
}
