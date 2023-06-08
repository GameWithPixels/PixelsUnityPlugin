/**
 * @file
 * @brief Global functions for accessing Bluetooth adapter state.
 */

#pragma once

#include "../../../include/bletypes.h"

namespace Systemic::BluetoothLE
{
    /**
     * @brief Returns the default Bluetooth adapter state.
     *
     * @return The adapter state
     */
    std::future<BleAdapterState> getAdapterStateAsync();

    /**
     * @brief Subscribe to the default Bluetooth adapter radio state events.
     *
     * @param onStateChanged Called when the radio state changes
     * @return A future indicating whether the operation was successful or not.
     */
    std::future<bool> subscribeAdapterStateChangedAsync(const std::function<void(BleAdapterState)>& onStateChanged);
}
