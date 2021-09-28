#include "pch.h"
#include "CoreBluetoothLE/Peripheral.h"
#include "CoreBluetoothLE/Service.h"
#include "CoreBluetoothLE/Characteristic.h"

#include <chrono>
using namespace std::chrono;
using namespace ::winrt;

namespace Pixels::CoreBluetoothLE
{
    static inline ConnectionEventReason toReason(GattCommunicationStatus gattStatus)
    {
        switch (gattStatus)
        {
        case GattCommunicationStatus::Success: return ConnectionEventReason::Success;
        case GattCommunicationStatus::Unreachable: return ConnectionEventReason::Unreachable;
        case GattCommunicationStatus::ProtocolError: return ConnectionEventReason::ProtocolError;
        case GattCommunicationStatus::AccessDenied: return ConnectionEventReason::AccessDenied;
        default:
            assert(false);
            return ConnectionEventReason::Unknown;
        }
    }

    std::future<BleRequestStatus> Peripheral::connectAsync(
        std::vector<winrt::guid> requiredServices /*= std::vector<winrt::guid>{}*/,
        bool maintainConnection /*= false*/)
    {
        size_t connectCounter = 0;
        {
            std::lock_guard lock{ _connectOpMtx };
            if (_session || _connecting)
            {
                co_return BleRequestStatus::Busy;
            }

            // New connect session
            connectCounter = ++_connectCounter;

            // Flag ourselves as connecting
            _connecting = true;

            _connectionEventsQueue.emplace_back(ConnectionEvent::Connecting, ConnectionEventReason::Success);
        }

        BleRequestStatus result = BleRequestStatus::Error;
        try
        {
            // Move to background thread to avoid blocking itself on re-entrant calls
            // (i.e. when try to connect on getting a connection failure event from a previous connection attempt)
            co_await resume_background();

            notifyQueuedConnectionEvents();

            auto device = co_await BluetoothLEDevice::FromBluetoothAddressAsync(_address);
            GattSession session = nullptr;
            if ((connectCounter == _connectCounter) && device)
            {
                session = co_await GattSession::FromDeviceIdAsync(device.BluetoothDeviceId());
            }

            bool ownsSession = false;
            if (session)
            {
                std::lock_guard lock{ _connectOpMtx };

                if (connectCounter == _connectCounter)
                {
                    assert(!_device);
                    _device = device;
                    _session = session;
                    _connectionStatusChangedToken = _device.ConnectionStatusChanged({ this, &Peripheral::onDeviceConnectionStatusChanged });
                    //TODO event never raised if device already connected
                    ownsSession = true;
                }
            }

            if (ownsSession)
            {
                // We're connected and now need to retrieve the list of services
                std::vector<std::shared_ptr<Service>> services{};

                // If true, request to connect to the device immediately rather than doing it lazily
                session.MaintainConnection(maintainConnection);

                //TODO only get required services, cache mode
                bool missingServices = false;
                auto servicesResult = co_await device.GetGattServicesAsync(BluetoothCacheMode::Uncached);
                auto gattStatus = servicesResult.Status();

                if ((connectCounter == _connectCounter) && (gattStatus == GattCommunicationStatus::Success))
                {
                    auto gattServices = servicesResult.Services();

                    // Check required services
                    if (requiredServices.size())
                    {
                        std::vector<winrt::guid> servicesUuids{};
                        servicesUuids.reserve(gattServices.Size());
                        for (auto service : gattServices)
                        {
                            servicesUuids.emplace_back(service.Uuid());
                        }
                        missingServices = !Internal::isSubset(requiredServices, std::move(servicesUuids));
                    }

                    if (!missingServices)
                    {
                        std::unordered_map<winrt::guid, std::vector<std::shared_ptr<Characteristic>>> characteristics{};
                        for (auto service : gattServices)
                        {
                            //TODO cache mode
                            auto characteristicsResult = co_await service.GetCharacteristicsAsync(BluetoothCacheMode::Uncached);
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

                auto reason = ConnectionEventReason::Success;
                {
                    std::lock_guard lock{ _connectOpMtx };

                    if (connectCounter == _connectCounter)
                    {
                        reason = missingServices ? ConnectionEventReason::NotSupported : toReason(gattStatus);
                        if (reason == ConnectionEventReason::Success)
                        {
                            assert(_device);
                            // Note: we can't be disconnecting while having the lock
                            _services.reserve(services.size());
                            for (auto s : services)
                            {
                                _services.emplace(s->uuid(), s);
                            }

                            _connectionEventsQueue.emplace_back(ConnectionEvent::Ready, ConnectionEventReason::Success);

                            result = BleRequestStatus::Success;
                        }
                        else if (reason == ConnectionEventReason::Unreachable)
                        {
                            result = BleRequestStatus::Unreachable;
                        }
                        else if (reason == ConnectionEventReason::ProtocolError)
                        {
                            result = BleRequestStatus::GattError;
                        }
                    }
                    else
                    {
                        result = BleRequestStatus::Canceled;
                    }

                    _connecting = false;
                }

                if (reason != ConnectionEventReason::Success)
                {
                    internalDisconnect(reason);
                }
            }
            else
            {
                std::lock_guard lock{ _connectOpMtx };

                if (connectCounter == _connectCounter)
                {
                    _connectionEventsQueue.emplace_back(ConnectionEvent::FailedToConnect, ConnectionEventReason::Unknown);
                }

                _connecting = false;
            }
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
    
    void Peripheral::internalDisconnect(ConnectionEventReason reason, bool fromDeviceEvent)
    {
        {
            BluetoothLEDevice device{ nullptr };
            GattSession session{ nullptr };
            std::vector<std::shared_ptr<Service>> services{};

            std::lock_guard lock{ _connectOpMtx };
            bool wasConnected = false;

            if (!fromDeviceEvent)
            {
                // Cancel any on-going connect operation
                ++_connectCounter;

                wasConnected = isConnected();
            }

            if (!_device) return; // Nothing to do
            if (fromDeviceEvent && (_session.MaintainConnection())) return; // Don't remove device as want it to reconnect automatically

            if (wasConnected)
            {
                _connectionEventsQueue.emplace_back(ConnectionEvent::Disconnecting, ConnectionEventReason::Success);
            }

            // Also immediately notify of disconnection as we won't get a WinRT event
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
            _services.clear();
            _session = nullptr;
            _device = nullptr;

            // Lock is released first
            // Actual destroy happens here, any callback will get an empty services list, no session and no device
        }

        notifyQueuedConnectionEvents();
    }
}
