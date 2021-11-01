#pragma once

#include "BleCommon.h"

namespace Pixels::CoreBluetoothLE
{
    using namespace winrt::Windows::Foundation;
    using namespace winrt::Windows::Devices::Bluetooth;
    using namespace winrt::Windows::Devices::Bluetooth::GenericAttributeProfile;

    class Peripheral;
    class Characteristic;

    class Service
    {
        std::weak_ptr<Peripheral> _peripheral;
        GattDeviceService _service{ nullptr };
        std::unordered_map<winrt::guid, std::vector<std::shared_ptr<Characteristic>>> _characteristics{};

    public:
        std::uint16_t handle() const
        {
            return _service.AttributeHandle();
        }

        winrt::guid uuid() const
        {
            return _service.Uuid();
        }

        std::shared_ptr<const Peripheral> peripheral() const
        {
            return _peripheral.lock();
        }
        
        std::shared_ptr<Peripheral> peripheral()
        {
            return _peripheral.lock();
        }

        void copyCharacteristics(std::vector<std::shared_ptr<Characteristic>>& outCharacteristics)
        {
            for (auto& [_, v] : _characteristics)
            {
                for (auto& c : v)
                {
                    outCharacteristics.emplace_back(c);
                }
            }
        }

        std::shared_ptr<Characteristic> getCharacteristic(const winrt::guid& uuid)
        {
            auto it = _characteristics.find(uuid);
            bool found = (it != _characteristics.end()) && (!it->second.empty());
            return found ? it->second[0] : nullptr;
        }

        const std::vector<std::shared_ptr<Characteristic>> getCharacteristics(const winrt::guid& uuid)
        {
            return _characteristics.find(uuid)->second;
        }

        ~Service()
        {
            _characteristics.clear();

            if (_service)
            {
                _service.Close();
                _service = nullptr;
            }
        }

    private:
        friend Peripheral;

        Service(
            std::weak_ptr<Peripheral> peripheral,
            GattDeviceService service,
            std::unordered_map<winrt::guid, std::vector<std::shared_ptr<Characteristic>>> characteristics)
            :
            _peripheral{ peripheral }, _service{ service }, _characteristics{ characteristics }
        {
        }
    };
}
