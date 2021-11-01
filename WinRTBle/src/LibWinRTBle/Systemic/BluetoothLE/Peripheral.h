#pragma once

#include "BleCommon.h"
#include <mutex>

namespace Systemic::BluetoothLE
{
    using namespace winrt::Windows::Foundation;
    using namespace winrt::Windows::Devices::Bluetooth;
    using namespace winrt::Windows::Devices::Bluetooth::GenericAttributeProfile;

    class Service;

    class Peripheral : public std::enable_shared_from_this<Peripheral>
    {
        const bluetooth_address_t _address{};
        const std::function<void(ConnectionEvent, ConnectionEventReason)> _onConnectionEvent{};
        BluetoothLEDevice _device{ nullptr };
        GattSession _session{ nullptr };
        winrt::event_token _connectionStatusChangedToken{};
        std::unordered_map<winrt::guid, std::shared_ptr<Service>> _services{};
        volatile size_t _connectCounter{};
        volatile bool _connecting{};
        mutable std::recursive_mutex _connectOpMtx{};
        std::vector<std::tuple<ConnectionEvent, ConnectionEventReason>> _connectionEventsQueue{};
        volatile bool _isReady{};

    public:
        bool hasDevice() const
        {
            return _device != nullptr;
        }

        bool isConnected() const
        {
            auto dev = safeGetDevice();
            return dev ? (dev.ConnectionStatus() == BluetoothConnectionStatus::Connected) : false;
        }

        bool isReady() const
        {
            return _isReady;
        }

        const wchar_t* deviceId() const
        {
            auto dev = safeGetDevice();
            return dev ? dev.DeviceId().data() : nullptr;
        }

        bluetooth_address_t address() const
        {
            return _address;
        }

        const wchar_t* name() const
        {
            auto dev = safeGetDevice();
            return dev ? dev.Name().data() : nullptr;
        }

        uint16_t mtu() const { auto s = _session; return s ? s.MaxPduSize() : 0; }

        void copyDiscoveredServices(std::vector<std::shared_ptr<Service>>& outServices)
        {
            std::lock_guard lock{ _connectOpMtx };
            for (auto& [_, s] : _services)
            {
                outServices.emplace_back(s);
            }
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
            notifyQueuedConnectionEvents(); //TODO not getting those events!
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
        BluetoothLEDevice safeGetDevice() const
        {
            std::lock_guard lock{ _connectOpMtx };
            {
                return _device;
            }
        }

        // Take the lock and release device and session, be sure to call notifyQueuedConnectionEvents() afterwards
        void internalDisconnect(ConnectionEventReason reason, bool triggeredByDevice = false);

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

        void onDeviceConnectionStatusChanged(BluetoothLEDevice device, IInspectable __)
        {
            if (device.ConnectionStatus() == BluetoothConnectionStatus::Disconnected)
            {
                auto reason = (_session != nullptr) && (_session.MaintainConnection()) ? ConnectionEventReason::LinkLoss : ConnectionEventReason::Timeout;
                internalDisconnect(reason, true);
                notifyQueuedConnectionEvents();
            }
            else
            {
                // Connected event is raised in connectAsync() after it has successfully retrieved the services
            }
        }
    };
}
