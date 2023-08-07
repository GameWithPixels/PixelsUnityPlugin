/**
 * @file
 * @brief Definition of the ManufacturerData, AdvertisementData and DiscoveredPeripheral classes.
 */

#pragma once

namespace Systemic::BluetoothLE
{
    class Scanner;

    /**
     * @brief Stores a company id and it's associated binary data.
     *
     * @note This is a read only class.
     */
    class ManufacturerData
    {
        uint16_t _companyId{};
        std::vector<uint8_t> _data{};

    public:
        /**
         * @brief Gets the company id.
         *
         * @return The company id.
         */
        uint16_t companyId() const { return _companyId; }

        /**
         * @brief Gets the binary data associated with the company id.
         *
         * @return The binary data. 
         */
        const std::vector<uint8_t>& data() const { return _data; }

    private:
        friend Scanner;

        // Initializes a new instance of ManufacturerData with the company id and a WinRT data buffer
        ManufacturerData(uint16_t companyId, winrt::Windows::Storage::Streams::IBuffer data)
            : _companyId{ companyId }, _data{ Internal::dataBufferToBytesVector(data) } {}
    };

    /**
     * @brief Stores a company id and it's associated binary data.
     *
     * @note This is a read only class.
     */
    class ServiceData
    {
        uint16_t _shortUuid{};
        std::vector<uint8_t> _data{};

    public:
        /**
         * @brief Gets the service short id (16 bits).
         *
         * @return The service short id.
         */
        uint16_t shortUuid() const { return _shortUuid; }

        /**
         * @brief Gets the binary data associated with the service.
         *
         * @return The binary data. 
         */
        const std::vector<uint8_t>& data() const { return _data; }

    private:
        friend Scanner;

        // Initializes a new instance of ServiceData with a WinRT data buffer first containing the service short UUID
        ServiceData(winrt::Windows::Storage::Streams::IBuffer data)
            : _shortUuid(0), _data{ Internal::dataBufferToBytesVector(data) }
        {
            if (_data.size() >= 2)
            {
                _shortUuid = _data[0] | ((uint16_t)_data[1] << 8);
                _data.erase(_data.begin(), _data.begin() + 2);
            }
        }
    };

    /**
     * @brief Stores an advertisement packet data type and it's associated binary data.
     *
     * @note This is a read only class.
     */
    class AdvertisementData
    {
        uint8_t _dataType{};
        std::vector<uint8_t> _data{};

    public:
        /**
         * @brief Gets this advertisement packet data type.
         *
         * @return The advertisement packet data type. 
         */
        uint8_t dataType() const { return _dataType; }

        /**
         * @brief Gets this advertisement packet binary data.
         *
         * @return The advertisement packet binary data.
         */
        const std::vector<uint8_t>& data() const { return _data; }

    private:
        friend Scanner;

        // Initializes a new instance of AdvertisementData with the advertisement data type and a WinRT data buffer
        AdvertisementData(uint8_t dataType, winrt::Windows::Storage::Streams::IBuffer data)
            : _dataType{ dataType }, _data{ Internal::dataBufferToBytesVector(data) } {}
    };

    /**
     * @brief Holds the information from advertisement packet(s) received from a peripheral.
     *
     * The data includes the peripheral's Bluetooth address, name, services
     * manufacturer data, etc.
     *
     * The data may come from several advertisement packet as the data from
     * scan responses is combined with the data from an existing DiscoveredPeripheral instance
     * for the same peripheral.
     * 
     * @note This is a read only class.
     */
    class ScannedPeripheral
    {
        using DateTime = winrt::Windows::Foundation::DateTime;

        DateTime _timestamp{};
        bluetooth_address_t _address{};
        std::wstring _name{};
        bool _isConnectable{};
        int _rssi{};
        int _txPowerLevel{};
        std::vector<winrt::guid> _services{};
        std::vector<ManufacturerData> _manufacturersData{};
        std::vector<ServiceData> _servicesData{};
        std::vector<AdvertisementData> _advertisingData{};

    public:
        /**
         * @brief Gets the time at which the last advertisement packet used
         *        for initializing this instance was received.
         *
         * @return The time at which the advertisement packet was received.
         */
        const DateTime& timestamp() const { return _timestamp; }

        /**
         * @brief Gets the Bluetooth address of the peripheral that send the advertisement packet(s).
         *
         * If multiple advertisement packets were used to initialize this instance,
         * the returned value is the from the last received packet.
         *
         * @return The Bluetooth address of the peripheral.
         */
        bluetooth_address_t address() const{ return _address; }

        /**
         * @brief The name of the peripheral as advertised.
         *
         * If multiple advertisement packets were used to initialize this instance,
         * the returned value is the from the last received packet that contained
         * a value for the name.
         *
         * @return The name of the peripheral.
         */
        const std::wstring& name() const { return _name; }

        /**
         * @brief Indicates whether the received advertisement is connectable.
         *
         * If multiple advertisement packets were used to initialize this instance,
         * the returned value is the from the last received packet.
         *
         * @return Whether the received advertisement is connectable.
         */
        bool isConnectable() const { return _isConnectable; }

        /**
         * @brief Gets the received signal strength indicator (RSSI) value, in dBm,
         *        of the advertisement packet.
         *
         * If multiple advertisement packets were used to initialize this instance,
         * the returned value is the from the last received packet.
         *
         * @return The RSSI value of the advertisement packet.
         */
        int rssi() const { return _rssi; }

        /**
         * @brief Gets the received transmit power of the advertisement packet.
         *
         * If multiple advertisement packets were used to initialize this instance,
         * the returned value is the from the last received packet that contained
         * a value for the received transmit power.
         *
         * @return The received transmit power of the advertisement packet.
         */
        int txPowerLevel() const { return _txPowerLevel; }

        /**
         * @brief Gets the list of services contained in the advertisement packet(s).
         *
         * If multiple advertisement packets were used to initialize this instance,
         * the returned value is the concatenation of data from all the packets.
         *
         * @return The list of advertised services of the peripheral.
         */
        const std::vector<winrt::guid>& services() const { return _services; }

        /**
         * @brief Gets the list of manufacturer data contained in the advertisement packet(s).
         *
         * If multiple advertisement packets were used to initialize this instance,
         * the returned value is the concatenation of data from all the packets.
         *
         * @return The list of manufacturer data of the peripheral.
         */
        const std::vector<ManufacturerData>& manufacturersData() const { return _manufacturersData; }

        /**
         * @brief Gets the list of service data contained in the advertisement packet(s).
         *
         * If multiple advertisement packets were used to initialize this instance,
         * the returned value is the concatenation of data from all the packets.
         *
         * @return The list of service data of the peripheral.
         */
        const std::vector<ServiceData>& servicesData() const { return _servicesData; }

        /**
         * @brief Gets the list of binary advertisement data contained in the advertisement packet(s).
         *
         * If multiple advertisement packets were used to initialize this instance,
         * the returned value is the concatenation of data from all the packets.
         *
         * @return The list of binary advertisement data of the peripheral.
         */
        const std::vector<AdvertisementData>& advertisingData() const { return _advertisingData; }

    private:
        friend Scanner;

        ScannedPeripheral(
            const DateTime& timestamp,
            bluetooth_address_t address,
            const std::wstring& name,
            bool isConnectable,
            int rssi,
            int txPowerLevel,
            const std::vector<winrt::guid>& services,
            const std::vector<ManufacturerData>& manufacturersData,
            const std::vector<ServiceData>& servicesData,
            const std::vector<AdvertisementData>& advertisingData)
            :
            _timestamp{ timestamp },
            _address{ address },
            _name{ name },
            _isConnectable{ isConnectable },
            _rssi{ rssi },
            _txPowerLevel{ txPowerLevel },
            _services{ services },
            _manufacturersData{ manufacturersData },
            _servicesData{ servicesData },
            _advertisingData{ advertisingData } {}

        ScannedPeripheral(
            const DateTime& timestamp,
            const ScannedPeripheral& peripheral,
            std::wstring& name,
            int rssi,
            int txPowerLevel,
            const std::vector<winrt::guid>& services,
            const std::vector<ManufacturerData>& manufacturersData,
            const std::vector<ServiceData>& servicesData,
            const std::vector<AdvertisementData>& advertisingData)
            :
            _timestamp{ timestamp },
            _address{ peripheral.address() },
            _name{ name.empty() ? peripheral._name : name },
            _isConnectable{ peripheral.isConnectable() },
            _rssi{ rssi },
            _txPowerLevel{ txPowerLevel },
            _services{ concat(peripheral._services, services) },
            _manufacturersData{ concat(peripheral._manufacturersData, manufacturersData) },
            _servicesData{ concat(peripheral._servicesData, servicesData) },
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
