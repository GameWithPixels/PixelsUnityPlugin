/**
 * @file
 * @brief Definition of the Characteristic class.
 */

#pragma once

#include <type_traits> // underlying_type

// Common BLE types
#include "../../../include/bletypes.h"
#include "../Internal/Utils.h"

namespace Systemic::BluetoothLE
{
    class Peripheral;

    /**
     * @brief Represents a service's characteristic of a Bluetooth Low Energy (BLE) peripheral.
     *
     * A characteristic may be queried for its properties, and its underlying value may be read and/or
     * written depending on the characteristic's capabilities.
     * A characteristic with the notifiable property may be subscribed to get notified when its value changes.
     *
     * Those operations are asynchronous and return a std::future.
     *
     * The Characteristic class internally stores a WinRT's \c GattCharacteristic object.
     */
    class Characteristic
    {
        using GattCharacteristic = winrt::Windows::Devices::Bluetooth::GenericAttributeProfile::GattCharacteristic;
        using GattValueChangedEventArgs = winrt::Windows::Devices::Bluetooth::GenericAttributeProfile::GattValueChangedEventArgs;

        // Characteristic
        GattCharacteristic _characteristic{ nullptr };

        // The user callback for value changes
        std::function<void(const std::vector<std::uint8_t>&)> _onValueChanged{};

        // Value change
        winrt::event_token _valueChangedToken{};
        std::recursive_mutex _subscribeMtx{};

    public:
        //! \name Destructor
        //! @{

        /**
         * @brief Destroys the Characteristic instance.
         */
        ~Characteristic()
        {
            if (_characteristic)
            {
                _characteristic.ValueChanged(_valueChangedToken);
                _characteristic = nullptr;
            }
        }

        //! @}
        //! @name Getters
        //! @{

        /**
         * @brief Gets the 16 bits handle of the BLE characteristic.
         *
         * @return The 16 bits handle of the characteristic.
         */
        std::uint16_t handle() const
        {
            return _characteristic.AttributeHandle();
        }

        /**
         * @brief Gets the UUID of the characteristic.
         *
         * @return The UUID of the characteristic.
         */
        winrt::guid uuid() const
        {
            return _characteristic.Uuid();
        }

        /**
         * @brief Gets the standard BLE properties of the characteristic.
         *
         * @return The properties of the characteristic. 
         *         See CharacteristicProperties for the different values (it may be a combination of them).
         */
        std::underlying_type<CharacteristicProperties>::type properties() const
        {
            return static_cast<std::underlying_type<CharacteristicProperties>::type>(
                _characteristic.CharacteristicProperties());
        }

        /**
         * @brief Indicates whether the characteristic can be written.
         *
         * @return Whether the characteristic can be written.
         */
        bool canWrite() const
        {
            return properties() & CharacteristicProperties::Write;
        }

        /**
         * @brief Indicates whether the characteristic can be read.
         *
         * @return Whether the characteristic can be read.
         */
        bool canRead() const
        {
            return properties() & CharacteristicProperties::Read;
        }

        /**
         * @brief Indicates whether the characteristic can notify its value changes.
         *
         * @return Whether the characteristic can notify.
         */
        bool canNotify() const
        {
            return properties() & CharacteristicProperties::Notify;
        }

        //! @}
        //! @name Characteristic operations
        //! @{

        /**
         * @brief Reads the value from the characteristic.
         *
         * The call fails if the characteristic is not readable.
         *
         * @return A future with the read value as a vector of bytes.
         */
        std::future<std::vector<std::uint8_t>> readValueAsync()
        {
            //TODO return error code

            // Read from characteristic
            auto result = co_await _characteristic.ReadValueAsync();
            co_return Internal::dataBufferToBytesVector(result.Value());
        }

        /**
         * @brief Writes the given data to the value of the characteristic.
         *
         * The call fails if the characteristic is not writable.
         *
         * @param data The data to write to the characteristic (may be empty).
         * @param withoutResponse Whether to wait for the peripheral to respond.
         * @return A future with the resulting request status.
         */
        std::future<BleRequestStatus> writeAsync(const std::vector<std::uint8_t>& data, bool withoutResponse = false)
        {
            //TODO use std::span, test with empty buffer
            using namespace winrt::Windows::Devices::Bluetooth::GenericAttributeProfile;

            // Write to characteristic
            auto options = withoutResponse ? GattWriteOption::WriteWithoutResponse : GattWriteOption::WriteWithResponse;
            auto result = co_await _characteristic.WriteValueAsync(Internal::bytesVectorToDataBuffer(data), options);

            co_return result == GattCommunicationStatus::Success ? BleRequestStatus::Success : BleRequestStatus::Error;
        }

        /**
         * @brief Subscribes for value changes of the characteristic.
         *
         * Replaces a previously registered value change callback.
         * The call fails if the characteristic doesn't support notifications.
         *
         * @param onValueChanged Called when the value of the characteristic changes.
         * @return A future with the resulting request status.
         */
        std::future<BleRequestStatus> subscribeAsync(const std::function<void(const std::vector<std::uint8_t>&)>& onValueChanged)
        {
            using namespace winrt::Windows::Devices::Bluetooth::GenericAttributeProfile;

            // Check parameters
            if (!onValueChanged) co_return BleRequestStatus::InvalidParameters;
            if (!canNotify()) co_return BleRequestStatus::NotSupported;

            {
                std::lock_guard lock{ _subscribeMtx };

                // Store the callback and subscribe
                _onValueChanged = onValueChanged;
                if (!_valueChangedToken)
                {
                    _valueChangedToken = _characteristic.ValueChanged({ this, &Characteristic::onValueChanged });
                }
            }

            // Update characteristic configuration
            auto result = co_await _characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                GattClientCharacteristicConfigurationDescriptorValue::Notify);

            co_return result == GattCommunicationStatus::Success ? BleRequestStatus::Success : BleRequestStatus::Error;
        }

        /**
         * @brief Unsubscribes from value changes of the characteristic.
         *
         * @return A future with the resulting request status.
         */
        std::future<BleRequestStatus> unsubscribeAsync()
        {
            using namespace winrt::Windows::Devices::Bluetooth::GenericAttributeProfile;

            {
                // Check if subscribed
                std::lock_guard lock{ _subscribeMtx };
                if (!_onValueChanged)
                {
                    co_return BleRequestStatus::Success;
                }

                // Forget the callback
                _onValueChanged = nullptr;
            }

            // Unsubscribe
            _characteristic.ValueChanged(_valueChangedToken);
            _valueChangedToken = winrt::event_token{};

            // Update characteristic configuration
            auto result = co_await _characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                GattClientCharacteristicConfigurationDescriptorValue::None);

            co_return result == GattCommunicationStatus::Success ? BleRequestStatus::Success : BleRequestStatus::Error;
        }

    private:
        friend Peripheral;

        // Initialize a new instance with a GattCharacteristic object
        Characteristic(GattCharacteristic characteristic)
            : _characteristic{ characteristic }
        {
        }

        // Called when subscribed to the characteristic and its value changes
        void onValueChanged(GattCharacteristic _, GattValueChangedEventArgs args)
        {
            // Safely get the callback
            std::function<void(const std::vector<std::uint8_t>&)> callback{};
            {
                std::lock_guard lock{ _subscribeMtx };
                callback = _onValueChanged;
            }

            if (callback)
            {
                callback(Internal::dataBufferToBytesVector(args.CharacteristicValue()));
            }
        }
    };
}
