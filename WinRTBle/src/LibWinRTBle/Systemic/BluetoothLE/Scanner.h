/**
 * @file
 * @brief Definition of the Scanner class.
 */

#pragma once

#include <mutex>

// Common BLE types
#include "../../../include/bletypes.h"
#include "../Internal/Utils.h"
#include "ScannedPeripheral.h"

namespace Systemic::BluetoothLE
{
    class ScannedPeripheral;

    /**
     * @brief Implements scanning of Bluetooth Low Energy (BLE) peripherals.
     *        It stores and notifies of discovered peripherals with ScannedPeripheral objects.
     * 
     * The Scanner class internally stores a WinRT's \c BluetoothLEAdvertisementWatcher object.
     * @see https://docs.microsoft.com/en-us/uwp/api/windows.devices.bluetooth.advertisement.bluetoothleadvertisementwatcher
     */
    class Scanner final
    {
        using BluetoothLEAdvertisementWatcher = winrt::Windows::Devices::Bluetooth::Advertisement::BluetoothLEAdvertisementWatcher;
        using BluetoothLEAdvertisementReceivedEventArgs = winrt::Windows::Devices::Bluetooth::Advertisement::BluetoothLEAdvertisementReceivedEventArgs;
        using BluetoothLEAdvertisementWatcherStoppedEventArgs = winrt::Windows::Devices::Bluetooth::Advertisement::BluetoothLEAdvertisementWatcherStoppedEventArgs;

        // Watcher and its event tokens
        BluetoothLEAdvertisementWatcher _watcher{ nullptr };
        winrt::event_token _receivedToken{};
        winrt::event_token _stoppedToken{};

        // List of user required services
        std::vector<winrt::guid> _requestedServices{};

        // Discovered peripherals
        std::mutex _peripheralsMtx{};
        std::map<bluetooth_address_t, std::shared_ptr<ScannedPeripheral>> _peripherals{};

        // User callback for discovered peripherals
        std::function<void(std::shared_ptr<ScannedPeripheral>)> _onPeripheralDiscovered{};

    public:
        /**
         * @brief Initializes a new instance of Scanner and immediately starts scanning
         *        for BLE peripherals.
         * 
         * @param peripheralDiscovered Called for each received advertisement packet. <br>
         *                             When receiving a scan response, the advertisement data is combined with
         *                             the data from the last DiscoveredPeripheral instance created for the same
         *                             peripheral before being passed to this callback.
         * @param services List of services UUIDs that the peripheral should advertise, may be empty.
         */
        Scanner(
            std::function<void(std::shared_ptr<ScannedPeripheral>)> peripheralDiscovered,
            std::vector<winrt::guid> services = std::vector<winrt::guid>{})
            :
            _watcher{},
            _requestedServices{ services },
            _onPeripheralDiscovered{ peripheralDiscovered }
        {
            using namespace winrt::Windows::Devices::Bluetooth::Advertisement;

            // We want to receive all advertisement packets
            _watcher.AllowExtendedAdvertisements(true);

            // Send scan requests packets
            _watcher.ScanningMode(BluetoothLEScanningMode::Active);

            // We must subscribe to both Received and Stopped for the watcher to work
            _receivedToken = _watcher.Received({ this, &Scanner::onReceived });
            _stoppedToken = _watcher.Stopped({ this, &Scanner::onStopped });

            // Starts scanning
            _watcher.Start();
        }

        /**
         * @brief Copy the discovered peripherals to the given std::vector.
         *
         * Peripherals are copied in no particular order, which may vary from one call to another.
         *
         * @param outDiscoveredPeripherals A std::vector to which the discovered peripherals are copied (appended).
         */
        void copyDiscoveredPeripherals(std::vector<std::shared_ptr<ScannedPeripheral>>& outDiscoveredPeripherals)
        {
            std::lock_guard lock{ _peripheralsMtx };
            outDiscoveredPeripherals.reserve(outDiscoveredPeripherals.size() + _peripherals.size());
            for (auto& [_, p] : _peripherals)
            {
                outDiscoveredPeripherals.emplace_back(p);
            }
        }

        /**
         * @brief Stops the scan and destroys the Scanner instance.
         */
        ~Scanner()
        {
            if (_watcher)
            {
                _watcher.Received(_receivedToken);
                _watcher.Stopped(_stoppedToken);
                _watcher.Stop();
                _watcher = nullptr;
            }
        }

    private:
        // Called by the watcher for each received advertisement packet 
        void onReceived(
            BluetoothLEAdvertisementWatcher const& _watcher,
            BluetoothLEAdvertisementReceivedEventArgs const& args)
        {
            using namespace winrt::Windows::Devices::Bluetooth::Advertisement;

            std::wstring name{};
            std::vector<winrt::guid> services{};
            std::vector<ManufacturerData> manufacturersData{};
            std::vector<ServiceData> servicesData{};
            std::vector<AdvertisementData> advertisingData{};

            auto advertisement = args.Advertisement();
            if (advertisement)
            {
                // Get name
                name = advertisement.LocalName();

                // Get services
                auto serv = advertisement.ServiceUuids();
                if (serv)
                {
                    services.reserve(serv.Size());
                    for (const auto& uuid : serv)
                    {
                        services.push_back(uuid);
                    }
                }

                // Get manufacturer-specific data sections
                auto manufDataList = advertisement.ManufacturerData();
                if (manufDataList)
                {
                    manufacturersData.reserve(manufDataList.Size());
                    for (const auto& manuf : manufDataList)
                    {
                        auto size = manuf.Data().Length();
                        manufacturersData.emplace_back(ManufacturerData{ manuf.CompanyId(), manuf.Data() });
                    }
                }

                // Get raw data sections
                auto advDataList = advertisement.DataSections();
                if (advDataList)
                {
                    advertisingData.reserve(advDataList.Size());
                    for (const auto& adv : advDataList)
                    {
                        auto size = adv.Data().Length();
                        advertisingData.emplace_back(AdvertisementData{ adv.DataType(), adv.Data() });

                        // Check if it's a service data
                        if (adv.DataType() == 0x16) // Service Data - 16-bit UUID
                        {
                            servicesData.emplace_back(ServiceData{ adv.Data() });
                        }
                    }
                }
            }

            // Get tx power
            int txPower = 0;
            if (args.TransmitPowerLevelInDBm())
            {
                txPower = args.TransmitPowerLevelInDBm().Value();
            }

            switch (args.AdvertisementType())
            {
            case BluetoothLEAdvertisementType::ConnectableUndirected:
            case BluetoothLEAdvertisementType::ConnectableDirected:
            case BluetoothLEAdvertisementType::ScannableUndirected:
            case BluetoothLEAdvertisementType::NonConnectableUndirected:
            {
                // We got a fresh advertisement packet, create a new DiscoveredPeripheral
                std::shared_ptr<ScannedPeripheral> peripheral{
                    new ScannedPeripheral(
                        args.Timestamp(),
                        args.BluetoothAddress(),
                        name,
                        args.IsConnectable(),
                        args.RawSignalStrengthInDBm(),
                        txPower,
                        services,
                        manufacturersData,
                        servicesData,
                        advertisingData) };

                {
                    std::lock_guard lock{ _peripheralsMtx };
                    _peripherals[peripheral->address()] = peripheral;
                }
                notify(peripheral);
            }
            break;

            case BluetoothLEAdvertisementType::ScanResponse:
            {
                std::shared_ptr<ScannedPeripheral> updatedPeripheral{};
                {
                    std::lock_guard lock{ _peripheralsMtx };
                    auto it = _peripherals.find(args.BluetoothAddress());
                    if (it != _peripherals.end())
                    {
                        // We got an advertisement packet in response to a scan request send after receiving
                        // an initial advertisement packet, update the existing DiscoveredPeripheral
                        updatedPeripheral.reset(
                            new ScannedPeripheral(
                                args.Timestamp(),
                                *it->second,
                                name,
                                args.RawSignalStrengthInDBm(),
                                txPower,
                                services,
                                manufacturersData,
                                servicesData,
                                advertisingData));

                        _peripherals[updatedPeripheral->address()] = updatedPeripheral;
                    }
                }
                if (updatedPeripheral)
                {
                    notify(updatedPeripheral);
                }
            }
            break;
            }
        }

        // Notify user code if peripheral advertise required services
        void notify(const std::shared_ptr<ScannedPeripheral>& peripheral)
        {
            if (_onPeripheralDiscovered &&
                (_requestedServices.empty() || Internal::isSubset(_requestedServices, peripheral->services())))
            {
                _onPeripheralDiscovered(peripheral);
            }
        }

        // Called by the watcher when scanning is stopped
        void onStopped(
            BluetoothLEAdvertisementWatcher const& _watcher,
            BluetoothLEAdvertisementWatcherStoppedEventArgs const& args)
        {
        }
    };
}
