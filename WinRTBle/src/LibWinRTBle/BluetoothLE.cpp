#include "pch.h"
#include "Systemic/BluetoothLE/BluetoothLE.h"

using namespace winrt::Windows::Devices::Bluetooth;
using namespace winrt::Windows::Devices::Radios;

// Anonymous namespace for our local functions
namespace
{
    using namespace Systemic::BluetoothLE;

    BleAdapterState toAdapterState(RadioState state)
    {
        switch (state)
        {
        case RadioState::On:
            return BleAdapterState::Enabled;
        case RadioState::Off:
        case RadioState::Disabled:
            return BleAdapterState::Disabled;
        default:
            return BleAdapterState::Unavailable;
        }
    }

    class StateCallback
    {
        std::mutex _mutex{};
        winrt::event_token _stateChangedToken{};
        Radio _radio = nullptr;
        std::function<void(BleAdapterState)> _onStateChanged{};

    public:
        void set(Radio radio, const std::function<void(BleAdapterState)>& onStateChanged)
        {
            if (onStateChanged)
            {
                std::lock_guard lock{ _mutex };

                _onStateChanged = onStateChanged;
                _radio = radio;
                _stateChangedToken = radio.StateChanged({ this, &StateCallback::callback });
            }
        }

        void reset()
        {
            if (_onStateChanged)
            {
                std::lock_guard lock{ _mutex };

                _onStateChanged = nullptr;
                if (_stateChangedToken)
                {
                    _radio.StateChanged(_stateChangedToken);
                    _stateChangedToken = {};
                }
            }
        }

        void callback(Radio radio, winrt::Windows::Foundation::IInspectable const& object)
        {
            auto onStateChanged = _onStateChanged;
            if (onStateChanged)
            {
                onStateChanged(toAdapterState(radio.State()));
            }
        }
    };

    static StateCallback _callback{};
}

namespace Systemic::BluetoothLE
{
    std::future<BleAdapterState> getAdapterStateAsync()
    {
        auto adapter = co_await BluetoothAdapter::GetDefaultAsync();
        auto state = BleAdapterState::Enabled;
        if (!adapter || !adapter.IsCentralRoleSupported() || !adapter.IsLowEnergySupported())
        {
            state = BleAdapterState::Unsupported;
        }
        else
        {
            auto radio = co_await adapter.GetRadioAsync();
            // Radio is returned only when the application is natively compiled for the target architecture
            if (radio)
            {
                state = toAdapterState(radio.State());
            }
        }
        co_return state;
    }

    std::future<bool> subscribeAdapterStateChangedAsync(const std::function<void(BleAdapterState)>& onStateChanged)
    {
        _callback.reset();

        bool success = !onStateChanged;
        if (!success)
        {
            const auto handler = onStateChanged; // Keep a copy because we may get suspended
            auto adapter = co_await BluetoothAdapter::GetDefaultAsync();
            if (adapter)
            {
                auto radio = co_await adapter.GetRadioAsync();
                if (radio)
                {
                    _callback.set(radio, handler);
                    success = true;
                }
            }
        }
        co_return success;
    }
}
