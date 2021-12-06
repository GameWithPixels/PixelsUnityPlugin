/**
 * @file
 * @brief Definition of the Peripheral class.
 */

#pragma once

#include <mutex>

// Common BLE types
#include "../../../include/bletypes.h"

namespace Systemic::BluetoothLE
{
    class Service;

    /**
     * @brief Represents a Bluetooth Low Energy (BLE) peripheral.
     *
     * The most common BLE operations are supported, such as reading the peripheral name,
     * MTU, RSSI, listing services and characteristics.
     *
     * Once created, a peripheral must be connected before most its methods may be used,
     * and it must be ready before accessing the services.
     * The peripheral becomes ready once all the required services have been discovered.
     *
     * The connection method connectAsync() is asynchronous and returns a \c std::future.
     *
     * A specific Service may be retrieved by its UUID with getDiscoveredService().
     * A service contains characteristics for which data may be read or written.
     *
     * The Peripheral class internally stores a WinRT's \c BluetoothLEDevice and \c GattSession objects.
     * @see https://docs.microsoft.com/en-us/uwp/api/windows.devices.bluetooth.bluetoothledevice
     */
    class Peripheral : public std::enable_shared_from_this<Peripheral>
    {
        using BluetoothLEDevice = winrt::Windows::Devices::Bluetooth::BluetoothLEDevice;
        using GattSession = winrt::Windows::Devices::Bluetooth::GenericAttributeProfile::GattSession;

        // The Bluetooth address
        const bluetooth_address_t _address{};

        // The user callback for connection events
        const std::function<void(ConnectionEvent, ConnectionEventReason)> _onConnectionEvent{};

        // Device and session
        BluetoothLEDevice _device{ nullptr };
        GattSession _session{ nullptr };
        winrt::event_token _connectionStatusChangedToken{};

        // Services
        std::unordered_map<winrt::guid, std::shared_ptr<Service>> _services{};

        // The ready state
        volatile bool _isReady{};

        // Connection
        mutable std::recursive_mutex _connectOpMtx{};   // Connection mutex
        volatile size_t _connectCounter{};              // Incremented every time connect or disconnect is called
        volatile bool _connecting{};                    // Whether we are trying to connect

        // Queue of connection events to notify to user
        std::vector<std::tuple<ConnectionEvent, ConnectionEventReason>> _connectionEventsQueue{};

    public:
        //! \name Constructor and destructor
        //! @{

        /**
         * @brief Initializes a new instance of Peripheral with the given Bluetooth address
         *        and a callback for notifying of the peripheral connection events.
         *
         * @param bluetoothAddress The Bluetooth address of the BLE peripheral.
         * @param onConnectionEvent Called when the connection status of the peripheral changes.
         */
        Peripheral(bluetooth_address_t bluetoothAddress, std::function<void(ConnectionEvent, ConnectionEventReason)> onConnectionEvent)
            : _address{ bluetoothAddress }, _onConnectionEvent{ onConnectionEvent }
        {
            assert(bluetoothAddress); //TODO check args
            assert(onConnectionEvent);
            _connectionEventsQueue.reserve(16);
        }

        /**
         * @brief Disconnects and destroys the Peripheral instance.
         */
        ~Peripheral()
        {
            disconnect();
        }

        //! @}
        //! \name Connection and disconnection
        //! @{

        /**
         * @brief Connects to the BLE peripheral.
         *
         * This request timeouts after 7 to 8 seconds, as of Windows 10 21H1.
         *
         * @param requiredServices List of services UUIDs that the peripheral should support, may be empty.
         * @param maintainConnection Whether to automatically reconnect after an unexpected disconnection
         *                           (i.e. not requested by a call to disconnect()).
         * @return A future with the resulting request status.
         */
        std::future<BleRequestStatus> connectAsync(
            std::vector<winrt::guid> requiredServices = std::vector<winrt::guid>{},
            bool maintainConnection = false);

        /**
         * @brief Immediately disconnects the peripheral.
         *
         * As a consequence, any on-going request either fails or is canceled, including connection requests.
         */
        void disconnect()
        {
            internalDisconnect(ConnectionEventReason::Success);
            notifyQueuedConnectionEvents(); //TODO not getting those events!
        }

        //! @}
        //! \name Getters
        //! Always valid.
        //! @{

        /**
         * @brief Gets the Bluetooth address of the peripheral.
         *
         * @return The Bluetooth address of the peripheral.
         */
        bluetooth_address_t address() const
        {
            return _address;
        }

        /**
         * @brief Indicates whether the peripheral is connected.
         *
         * Services may not have been discovered yet.
         *
         * @return Whether the peripheral is connected.
         */
        bool isConnected() const
        {
            using namespace winrt::Windows::Devices::Bluetooth;

            auto dev = safeGetDevice();
            return dev ? (dev.ConnectionStatus() == BluetoothConnectionStatus::Connected) : false;
        }

        /**
         * @brief Indicates whether the peripheral is ready.
         *
         * The peripheral is ready once it has successfully connected and
         * discovered the required services.
         *
         * @return Whether the peripheral is ready.
         */
        bool isReady() const
        {
            return _isReady;
        }

        //! @}
        //! \name Connected getters
        //! Valid only for connected peripherals.
        //! @{

        /**
         * @brief Gets the device id assigned by the system for the peripheral.
         *
         * @return The device id assigned by the system.
         */
        const wchar_t* deviceId() const
        {
            auto dev = safeGetDevice();
            return dev ? dev.DeviceId().data() : nullptr;
        }

        /**
         * @brief Gets the name of the peripheral.
         *
         * @return The name of the peripheral, or null if it doesn't have a valid device.
         */
        const wchar_t* name() const
        {
            auto dev = safeGetDevice();
            return dev ? dev.Name().data() : nullptr;
        }

        /**
         * @brief Gets the Maximum Transmission Unit (MTU).
         *
         * @return The MTU, or zero if it doesn't have a valid device.
         */
        uint16_t mtu() const
        {
            //TODO is the lock needed?
            std::lock_guard lock{ _connectOpMtx };
            return _session ? _session.MaxPduSize() : 0;
        }

        //! @}
        //! \name Services access
        //! Valid only for peripherals in ready state.
        //! @{

        /**
         * @brief Gets the Service instance with the given UUID.
         *
         * @param uuid The UUID of the service.
         * @return The Service instance.
         */
        std::shared_ptr<Service> getDiscoveredService(const winrt::guid& uuid) const
        {
            std::lock_guard lock{ _connectOpMtx };
            auto it = _services.find(uuid);
            return it != _services.end() ? it->second : nullptr;
        }

        /**
         * @brief Copy the discovered services to the given std::vector.
         *
         * @param outServices The std::vector to which the discovered services are copied (appended).
         */
        void copyDiscoveredServices(std::vector<std::shared_ptr<Service>>& outServices) const
        {
            std::lock_guard lock{ _connectOpMtx };
            outServices.reserve(outServices.size() + _services.size());
            for (auto& [_, s] : _services)
            {
                outServices.emplace_back(s);
            }
        }

        //! @}

    private:
        // Get the device object in a thread safe manner
        BluetoothLEDevice safeGetDevice() const
        {
            //TODO is the lock needed?
            std::lock_guard lock{ _connectOpMtx };
            return _device;
        }

        // Take the lock and release device and session, be sure to call notifyQueuedConnectionEvents() afterwards
        void internalDisconnect(ConnectionEventReason reason, bool fromDevice = false);

        // Notify user code with pending connection event
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

        // Called by the device when the connection status changes
        void onDeviceConnectionStatusChanged(BluetoothLEDevice device, winrt::Windows::Foundation::IInspectable __)
        {
            using namespace winrt::Windows::Devices::Bluetooth;

            if (device.ConnectionStatus() == BluetoothConnectionStatus::Disconnected)
            {
                bool linkLoss = (_session != nullptr) && _session.MaintainConnection();
                internalDisconnect(linkLoss ? ConnectionEventReason::LinkLoss : ConnectionEventReason::Timeout, true);
                notifyQueuedConnectionEvents();
            }
            else
            {
                // Connected event is raised in connectAsync() after it has successfully retrieved the services
            }
        }
    };
}
