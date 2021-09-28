#pragma once

#include "BleCommon.h"

namespace Pixels::CoreBluetoothLE
{
    using namespace winrt::Windows::Foundation;
    using namespace winrt::Windows::Devices::Bluetooth;
    using namespace winrt::Windows::Devices::Bluetooth::GenericAttributeProfile;
    using namespace winrt::Windows::Storage::Streams;

    class Peripheral;

    class Characteristic
    {
        GattCharacteristic _characteristic{ nullptr };
        std::function<void(const std::vector<std::uint8_t>&)> _onValueChanged{};
        winrt::event_token _valueChangedToken{};
        std::recursive_mutex _subscribeMtx{};

    public:
        std::uint16_t handle() const { return _characteristic.AttributeHandle(); }

        winrt::guid uuid() const { return _characteristic.Uuid(); }

        GattCharacteristicProperties properties() const { return _characteristic.CharacteristicProperties(); }

        bool canWrite() const { return (properties() & GattCharacteristicProperties::Write) == GattCharacteristicProperties::Write; }

        bool canRead() const { return (properties() & GattCharacteristicProperties::Read) == GattCharacteristicProperties::Read; }

        bool canNotify() const { return (properties() & GattCharacteristicProperties::Notify) == GattCharacteristicProperties::Notify; }

        //TODO return error code
        std::future<std::vector<std::uint8_t>> readValueAsync()
        {
            auto result = co_await _characteristic.ReadValueAsync();
            co_return toVector(result.Value());
        }

        //TODO use std::span
        // Buffer may be empty but not null
        std::future<BleRequestStatus> writeAsync(const std::vector<std::uint8_t>& value, bool withoutResponse = false)
        {
            //InMemoryRandomAccessStream stream;
            DataWriter dataWriter{}; // { stream };
            dataWriter.ByteOrder(ByteOrder::LittleEndian);
            dataWriter.WriteBytes(value);

            auto options = withoutResponse ? GattWriteOption::WriteWithoutResponse : GattWriteOption::WriteWithResponse;
            auto result = co_await _characteristic.WriteValueAsync(dataWriter.DetachBuffer(), options);

            co_return result == GattCommunicationStatus::Success ? BleRequestStatus::Success : BleRequestStatus::Error;
        }

        std::future<BleRequestStatus> subscribeAsync(std::function<void(const std::vector<std::uint8_t>&)> onValueChanged)
        {
            if (!onValueChanged) co_return BleRequestStatus::InvalidParameters;
            if (!canNotify()) co_return BleRequestStatus::NotSupported;

            {
                std::lock_guard lock{ _subscribeMtx };
                if (_onValueChanged != nullptr)
                {
                    co_return BleRequestStatus::Busy;
                }
                _onValueChanged = onValueChanged;
                _valueChangedToken = _characteristic.ValueChanged({ this, &Characteristic::onValueChanged });
            }

            auto result = co_await _characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                GattClientCharacteristicConfigurationDescriptorValue::Notify);

            co_return result == GattCommunicationStatus::Success ? BleRequestStatus::Success : BleRequestStatus::Error;
        }

        std::future<BleRequestStatus> unsubscribeAsync()
        {
            {
                std::lock_guard lock{ _subscribeMtx };
                if (!_onValueChanged)
                {
                    co_return BleRequestStatus::Success;
                }
                _onValueChanged = nullptr;
            }

            _characteristic.ValueChanged(_valueChangedToken);
            auto result = co_await _characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                GattClientCharacteristicConfigurationDescriptorValue::None);

            co_return result == GattCommunicationStatus::Success ? BleRequestStatus::Success : BleRequestStatus::Error;
        }

        ~Characteristic()
        {
            if (_characteristic)
            {
                _characteristic.ValueChanged(_valueChangedToken);
                _characteristic = nullptr;
            }
        }

    private:
        friend Peripheral;

        Characteristic(GattCharacteristic characteristic)
            : _characteristic{ characteristic }
        {
        }

        void onValueChanged(GattCharacteristic _, GattValueChangedEventArgs args)
        {
            if (_onValueChanged) _onValueChanged(toVector(args.CharacteristicValue()));
        }

        static std::vector<std::uint8_t> toVector(IBuffer buffer)
        {
            std::vector<std::uint8_t> bytes{};
            bytes.resize(buffer.Length());
            auto dataReader = DataReader::FromBuffer(buffer);
            dataReader.ReadBytes(bytes);
            return bytes;
        }
    };
}
