#pragma once

#include "BleCommon.h"

namespace Pixels::CoreBluetoothLE
{
    using namespace winrt::Windows::Foundation;
    using namespace winrt::Windows::Storage::Streams;

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

        ManufacturerData(uint16_t companyId, IBuffer data)
            : _companyId{ companyId }, _data{ toVector(data) } {}

        std::vector<uint8_t> toVector(IBuffer data)
        {
            std::vector<uint8_t> dst;
            dst.resize(data.Length());
            auto reader = DataReader::FromBuffer(data);
            reader.ReadBytes(dst);
            return dst;
        }
    };

    class AdvertisingData
    {
        uint8_t _dataType{};
        std::vector<uint8_t> _data{};

    public:
        uint8_t dataType() const { return _dataType; }

        const std::vector<uint8_t>& data() const { return _data; }

    private:
        friend Scanner;

        AdvertisingData(uint8_t dataType, IBuffer data)
            : _dataType{ dataType }, _data{ toVector(data) } {}

        std::vector<uint8_t> toVector(IBuffer data)
        {
            std::vector<uint8_t> dst;
            dst.resize(data.Length());
            auto reader = DataReader::FromBuffer(data);
            reader.ReadBytes(dst);
            return dst;
        }
    };

    class DiscoveredPeripheral
    {
        DateTime _timestamp{};
        bluetooth_address_t _address{};
        bool _isConnectable{};
        int _rssi{};
        std::wstring _name{};
        std::vector<winrt::guid> _services{};
        std::vector<ManufacturerData> _manufacturerData{};
        std::vector<AdvertisingData> _advertisingData{};

    public:
        const DateTime& timestamp() const{ return _timestamp; }

        bluetooth_address_t address() const{ return _address; }

        bool isConnectable() const { return _isConnectable; }

        int rssi() const { return _rssi; }
        
        const std::wstring& name() const { return _name; }
        
        const std::vector<winrt::guid>& services() const { return _services; }

        const std::vector<ManufacturerData>& manufacturerData() const { return _manufacturerData; }

        const std::vector<AdvertisingData>& advertisingData() const { return _advertisingData; }

    private:
        friend Scanner;

        DiscoveredPeripheral(
            const DateTime& timestamp,
            bluetooth_address_t address,
            bool isConnectable,
            int rssi,
            const std::wstring& name,
            const std::vector<winrt::guid>& services,
            const std::vector<ManufacturerData>& manufacturerData,
            const std::vector<AdvertisingData>& advertisingData)
            :
            _timestamp{ timestamp },
            _address{ address },
            _isConnectable{ isConnectable },
            _rssi{ rssi },
            _name{ name },
            _services{ services },
            _manufacturerData{ manufacturerData },
            _advertisingData{ advertisingData } {}

        DiscoveredPeripheral(
            const DateTime& timestamp,
            const DiscoveredPeripheral& peripheral,
            int rssi,
            std::wstring& name,
            const std::vector<winrt::guid>& services,
            const std::vector<ManufacturerData>& manufacturerData,
            const std::vector<AdvertisingData>& advertisingData)
            :
            _timestamp{ timestamp },
            _address{ peripheral.address() },
            _isConnectable{ peripheral.isConnectable() },
            _rssi{ rssi },
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
