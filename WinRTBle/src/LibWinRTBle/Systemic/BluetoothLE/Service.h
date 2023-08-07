/**
 * @file
 * @brief Definition of the Service class.
 */

#pragma once

namespace Systemic::BluetoothLE
{
    class Peripheral;
    class Characteristic;

    /**
     * @brief Represents a primary service on a Bluetooth Low Energy (BLE) peripheral.
     *
     * A specific Characteristic may be retrieved by its UUID with getCharacteristic().
     *
     * The Service class internally stores a WinRT's \c GattDeviceService object.
     */
    class Service
    {
        using GattDeviceService = winrt::Windows::Devices::Bluetooth::GenericAttributeProfile::GattDeviceService;

        // Keep a weak pointer to the peripheral so it can be accessed
        std::weak_ptr<Peripheral> _peripheral;

        // Service
        GattDeviceService _service{ nullptr };

        // Characteristics (there may be more than one characteristic instance for a given UUID)
        std::unordered_map<winrt::guid, std::vector<std::shared_ptr<Characteristic>>> _characteristics{};

    public:
        //! \name Destructor
        //! @{

        /**
         * @brief Closes and destroys the Service instance.
         */
        ~Service()
        {
            _characteristics.clear();

            if (_service)
            {
                _service.Close();
                _service = nullptr;
            }
        }

        //! @}
        //! @name Getters
        //! @{

        /**
         * @brief Gets the 16 bits handle of the BLE service.
         *
         * @return The 16 bits handle of the service.
         */
        std::uint16_t handle() const
        {
            return _service.AttributeHandle();
        }

        /**
         * @brief Gets the UUID of the service.
         *
         * @return The UUID of the service.
         */
        winrt::guid uuid() const
        {
            return _service.Uuid();
        }

        /**
         * @brief Gets the peripheral to which the service belongs.
         *
         * @return The peripheral for the service.
         */
        std::shared_ptr<const Peripheral> peripheral() const
        {
            return _peripheral.lock();
        }

        /**
         * @brief Gets the peripheral to which the service belongs.
         *
         * @return The peripheral for the service.
         */
        std::shared_ptr<Peripheral> peripheral()
        {
            return _peripheral.lock();
        }

        //! @}
        //! @name Characteristics access
        //! @{

        /**
         * @brief Gets the first Characteristic instance with the given UUID.
         *
         * @param uuid The UUID of the Characteristic.
         * @return The Characteristic instance.
         */
        std::shared_ptr<Characteristic> getCharacteristic(const winrt::guid& uuid)
        {
            auto it = _characteristics.find(uuid);
            bool found = (it != _characteristics.end()) && (!it->second.empty());
            return found ? it->second[0] : nullptr;
        }

        /**
         * @brief Gets the all the Characteristic instances with the given UUID.
         *
         * Characteristics are always returned in the same order.
         *
         * @param uuid The UUID of the Characteristic.
         * @return The Characteristic instances.
         */
        const std::vector<std::shared_ptr<Characteristic>> getCharacteristics(const winrt::guid& uuid)
        {
            return _characteristics.find(uuid)->second;
        }

        /**
         * @brief Copy the discovered characteristics to the given std::vector.
         *
         * @param outCharacteristics A std::vector to which the discovered characteristics are copied (appended).
         */
        void copyCharacteristics(std::vector<std::shared_ptr<Characteristic>>& outCharacteristics)
        {
            size_t _characteristicsCount = 0;
            for (auto& [_, instances] : _characteristics)
            {
                _characteristicsCount += instances.size();
            }
            outCharacteristics.reserve(outCharacteristics.size() + _characteristicsCount);
            for (auto& [_, instances] : _characteristics)
            {
                for (auto& c : instances)
                {
                    outCharacteristics.emplace_back(c);
                }
            }
        }

        //! @}

    private:
        friend Peripheral;

        // Initializes a new instance of Service for a Peripheral and GattDeviceService,
        // and with a list of characteristics.
        Service(
            std::weak_ptr<Peripheral> peripheral,
            GattDeviceService service,
            std::unordered_map<winrt::guid, std::vector<std::shared_ptr<Characteristic>>> characteristics)
            :
            _peripheral{ peripheral },
            _service{ service },
            _characteristics{ characteristics }
        {
        }
    };
}
