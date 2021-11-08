#pragma once

#include "BleCommon.h"

namespace Systemic::BluetoothLE
{
    class Scanner;

    class ManufacturerData
    {
        uint16_t _companyId{};
        std::vector<uint8_t> _data{};

    public:
        uint16_t companyId() const { return _companyId; }

        const std::vector<uint8_t>& data() const { return _data; }

    private:
        friend Scanner;

        ManufacturerData(uint16_t companyId, winrt::Windows::Storage::Streams::IBuffer data)
            : _companyId{ companyId }, _data{ Internal::dataBufferToVector(data) } {}

    };

    class AdvertisementData
    {
        uint8_t _dataType{};
        std::vector<uint8_t> _data{};

    public:
        uint8_t dataType() const { return _dataType; }

        const std::vector<uint8_t>& data() const { return _data; }

    private:
        friend Scanner;

        // Initializes a new instance of Advertisement Data from the advertisement data type and a WinRT data buffer
        AdvertisementData(uint8_t dataType, winrt::Windows::Storage::Streams::IBuffer data)
            : _dataType{ dataType }, _data{ Internal::dataBufferToVector(data) } {}
    };

    class DiscoveredPeripheral
    {
        using DateTime = winrt::Windows::Foundation::DateTime;

        DateTime _timestamp{};
        bluetooth_address_t _address{};
        bool _isConnectable{};
        int _rssi{};
        int _txPowerLevel{};
        std::wstring _name{};
        std::vector<winrt::guid> _services{};
        std::vector<ManufacturerData> _manufacturerData{};
        std::vector<AdvertisementData> _advertisingData{};

    public:
        bluetooth_address_t address() const{ return _address; }

        bool isConnectable() const { return _isConnectable; }

        int rssi() const { return _rssi; }

        int txPowerLevel() const { return _txPowerLevel; }

        const std::wstring& name() const { return _name; }

        const std::vector<winrt::guid>& services() const { return _services; }

        const std::vector<ManufacturerData>& manufacturerData() const { return _manufacturerData; }

        const std::vector<AdvertisementData>& advertisingData() const { return _advertisingData; }

    private:
        friend Scanner;

        DiscoveredPeripheral(
            const DateTime& timestamp,
            bluetooth_address_t address,
            bool isConnectable,
            int rssi,
            int txPowerLevel,
            const std::wstring& name,
            const std::vector<winrt::guid>& services,
            const std::vector<ManufacturerData>& manufacturerData,
            const std::vector<AdvertisementData>& advertisingData)
            :
            _timestamp{ timestamp },
            _address{ address },
            _isConnectable{ isConnectable },
            _rssi{ rssi },
            _txPowerLevel{ txPowerLevel },
            _name{ name },
            _services{ services },
            _manufacturerData{ manufacturerData },
            _advertisingData{ advertisingData } {}

        DiscoveredPeripheral(
            const DateTime& timestamp,
            const DiscoveredPeripheral& peripheral,
            int rssi,
            int txPowerLevel,
            std::wstring& name,
            const std::vector<winrt::guid>& services,
            const std::vector<ManufacturerData>& manufacturerData,
            const std::vector<AdvertisementData>& advertisingData)
            :
            _timestamp{ timestamp },
            _address{ peripheral.address() },
            _isConnectable{ peripheral.isConnectable() },
            _rssi{ rssi },
            _txPowerLevel{ txPowerLevel },
            _name{ name.empty() ? peripheral._name : name },
            _services{ concat(peripheral._services, services) },
            _manufacturerData{ concat(peripheral._manufacturerData, manufacturerData) },
            _advertisingData{ concat(peripheral._advertisingData, advertisingData) } {}

        template <typename T>
        std::vector<T> concat(const std::vector<T> a, const std::vector<T>& b)
        {
            std::vector<T> out{ a };
            out.insert(out.end(), b.begin(), b.end());
            return out;
        }
    };
}
