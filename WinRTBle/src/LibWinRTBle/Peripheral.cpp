#include "pch.h"
#include "Systemic/Internal/Utils.h"
#include "Systemic/BluetoothLE/Peripheral.h"
#include "Systemic/BluetoothLE/Service.h"
#include "Systemic/BluetoothLE/Characteristic.h"

using namespace winrt::Windows::Devices::Bluetooth;
using namespace winrt::Windows::Devices::Bluetooth::GenericAttributeProfile;

namespace Systemic::BluetoothLE
{
    namespace
    {
        // Converts a WinRT GattCommunicationStatus to a ConnectionEventReason
        inline ConnectionEventReason toReason(GattCommunicationStatus gattStatus)
        {
            switch (gattStatus)
            {
            case GattCommunicationStatus::Success:
                return ConnectionEventReason::Success;
            case GattCommunicationStatus::Unreachable:
                return ConnectionEventReason::Timeout;
            default:
                assert(false);
                return ConnectionEventReason::Unknown;
            }
        }

        // Converts a ConnectionEventReason to a BleRequestStatus
        inline BleRequestStatus toRequestStatus(ConnectionEventReason reason)
        {
            switch (reason)
            {
            case ConnectionEventReason::Success:
                return BleRequestStatus::Success;
            case ConnectionEventReason::NotSupported:
                return BleRequestStatus::NotSupported;
            case ConnectionEventReason::Timeout:
                return BleRequestStatus::Timeout;
            case ConnectionEventReason::Canceled:
                return BleRequestStatus::Canceled;
            default:
                return BleRequestStatus::Error;
            }
        }
    }

    std::future<BleRequestStatus> Peripheral::connectAsync(
        std::vector<winrt::guid> requiredServices /*= std::vector<winrt::guid>{}*/,
        bool maintainConnection /*= false*/)
    {
        //TODO return error code

        size_t connectCounter; // Initialized in the block bellow
        {
            std::lock_guard lock{ _connectOpMtx };
            if (_isReady)
            {
                co_return BleRequestStatus::Success;
            }
            if (_connecting)
            {
                //TODO instead wait for other connection call to succeed
                co_return BleRequestStatus::InvalidCall;
            }

            // New connect session
            connectCounter = ++_connectCounter;

            // Flag ourselves as connecting
            _connecting = true;

            // We're going to start connecting
            _connectionEventsQueue.emplace_back(ConnectionEvent::Connecting, ConnectionEventReason::Success);
        }

        BleRequestStatus result = BleRequestStatus::Canceled;
        try
        {
            // Move to background thread to avoid blocking itself on re-entrant calls
            // (i.e. when try to connect on getting a connection failure event from a previous connection attempt)
            co_await winrt::resume_background();

            // Notify "connecting" event
            notifyQueuedConnectionEvents();

            // Get the device and a session object
            // Those 2 requests will succeed as long as the device was previously scanned,
            // even if it's presently not reachable
            auto device = co_await BluetoothLEDevice::FromBluetoothAddressAsync(_address);
            GattSession session = nullptr;
            if ((connectCounter == _connectCounter) && device)
            {
                session = co_await GattSession::FromDeviceIdAsync(device.BluetoothDeviceId());
            }

            // Those variables track the state of the connection process
            GattCommunicationStatus gattStatus = GattCommunicationStatus::Success;
            bool ownsSession = false;
            bool missingServices = false;
            std::vector<std::shared_ptr<Service>> services{};

            if ((connectCounter == _connectCounter) && session)
            {
                // We're connected and now need to retrieve the list of services

                // This request might take a long time (up to 18 seconds) if the device is not reachable
                //TODO use GetGattServicesForUuidAsync() + cache mode
                auto servicesResult = co_await device.GetGattServicesAsync(BluetoothCacheMode::Uncached);
                gattStatus = servicesResult.Status();

                if ((connectCounter == _connectCounter) && (gattStatus == GattCommunicationStatus::Success))
                {
                    std::lock_guard lock{ _connectOpMtx };

                    // See if we can assign this device and session
                    if (connectCounter == _connectCounter)
                    {
                        assert(!_device);
                        _device = device;
                        _session = session;
                        _connectionStatusChangedToken = _device.ConnectionStatusChanged({ this, &Peripheral::onDeviceConnectionStatusChanged });
                        ownsSession = true;

                        _connectionEventsQueue.emplace_back(ConnectionEvent::Connected, ConnectionEventReason::Success);
                    }
                }

                if (ownsSession)
                {
                    // If true, it will auto-reconnect to a lost device as soon it's available again
                    session.MaintainConnection(maintainConnection);

                    // Check required services
                    if (requiredServices.size())
                    {
                        auto gattServices = servicesResult.Services();

                        // Iterate through services to make sure we have all the requested ones
                        std::vector<winrt::guid> servicesUuids{};
                        servicesUuids.reserve(gattServices.Size());
                        for (auto service : gattServices)
                        {
                            servicesUuids.emplace_back(service.Uuid());
                        }
                        missingServices = !Internal::isSubset(requiredServices, std::move(servicesUuids));

                        // If all services are accounted for, get their characteristics
                        if (!missingServices)
                        {
                            services.reserve(gattServices.Size());
                            std::unordered_map<winrt::guid, std::vector<std::shared_ptr<Characteristic>>> characteristics{};

                            for (auto service : gattServices)
                            {
                                //TODO cache mode
                                GattCharacteristicsResult characteristicsResult = nullptr;
                                try
                                {
                                    // Got an exception once, may be caused by having the device disconnected...
                                    characteristicsResult = co_await service.GetCharacteristicsAsync(BluetoothCacheMode::Uncached);
                                }
                                catch (const winrt::hresult_error&)
                                {
                                    gattStatus = GattCommunicationStatus::AccessDenied;
                                    break;
                                }

                                gattStatus = characteristicsResult.Status();
                                if ((connectCounter != _connectCounter) || (gattStatus != GattCommunicationStatus::Success))
                                {
                                    break;
                                }

                                for (auto characteristic : characteristicsResult.Characteristics())
                                {
                                    auto it = characteristics.try_emplace(characteristic.Uuid()); // , std::vector<std::shared_ptr<Characteristic>>{}
                                    it.first->second.emplace_back(new Characteristic(characteristic));
                                }

                                services.emplace_back(new Service{ shared_from_this(), service, characteristics });
                                characteristics.clear();
                            }
                        }
                    }
                }
            }

            //
            // Almost done, check current state and finalize
            //

            std::lock_guard lock{ _connectOpMtx };

            if (connectCounter == _connectCounter)
            {
                // If not canceled, the only cause of failures are a null session missing services or a GATT error
                auto reason = (session == nullptr) ? ConnectionEventReason::Unknown :
                    (missingServices ? ConnectionEventReason::NotSupported : toReason(gattStatus));

                if (reason == ConnectionEventReason::Success)
                {
                    assert(_device == device);

                    result = BleRequestStatus::Success;

                    _services.reserve(services.size());
                    for (auto& s : services)
                    {
                        _services.emplace(s->uuid(), s);
                    }

                    _isReady = true;

                    _connectionEventsQueue.emplace_back(ConnectionEvent::Ready, ConnectionEventReason::Success);
                }
                else
                {
                    //TODO check if BLE adapter is enabled
                    //if (!bleApdater.isOn())
                    //{
                    //    reason = ConnectionEventReason::AdapterOff;
                    //}

                    result = toRequestStatus(reason);
                    assert(result != BleRequestStatus::Success);

                    if (ownsSession)
                    {
                        // It's OK to call disconnect while holding the lock since we have a copy
                        // of the current device and session, so their destruction won't happen
                        // in internalDisconnect() but in this method when they go out of scope
                        // and after the lock has been released
                        internalDisconnect(reason);
                    }
                    else
                    {
                        _connectionEventsQueue.emplace_back(ConnectionEvent::FailedToConnect, reason);
                    }
                }
            }
            //else => don't do anything if canceled, as internalDisconnect() has already been called

            _connecting = false;

            // Lock is released first, then device and session are destroy here
            // if there were not assigned to member variables
        }
        catch (...)
        {
            std::lock_guard lock{ _connectOpMtx };
            _connecting = false;
            throw;
        }

        notifyQueuedConnectionEvents();

        co_return result;
    }
    
    void Peripheral::internalDisconnect(ConnectionEventReason reason, bool fromDevice)
    {
        BluetoothLEDevice device{ nullptr };
        GattSession session{ nullptr };
        std::vector<std::shared_ptr<Service>> services{};

        std::lock_guard lock{ _connectOpMtx };

        // Cancel any on-going connect operation
        ++_connectCounter;

        // Nothing to do if no device
        if (!_device) return;
        assert(_session != nullptr);

        // Don't remove device as want it to reconnect automatically
        if (fromDevice && (_session.MaintainConnection())) return;

        if (!fromDevice)
        {
            // Notify that we are disconnecting
            _connectionEventsQueue.emplace_back(ConnectionEvent::Disconnecting, ConnectionEventReason::Success);
        }

        // Also notify of disconnection as we won't get a WinRT event
        // since we have to destroy the device to force a disconnection
        _connectionEventsQueue.emplace_back(ConnectionEvent::Disconnected, reason);

        // Unhook from event before destroying device
        _device.ConnectionStatusChanged(_connectionStatusChangedToken);

        // Copy members
        device = _device;
        session = _session;
        services.reserve(_services.size());
        for (auto& [_, s] : _services)
        {
            services.emplace_back(s);
        }

        // Clear members
        _isReady = false;
        _services.clear();
        _session = nullptr;
        _device = nullptr;

        // Lock is released first, then device and session are destroy here
        // Any callback to user code will see an empty services list, no session and no device
        //TODO in some circumstances, destroying the session+device may block for 5 seconds => spawn a thread?
    }
}
