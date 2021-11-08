/**
 * @file
 * @brief A few internal types and functions.
 */

#pragma once

#include <algorithm> // sort, includes

// Include common BLE types
#include "../../../include/bletypes.h"

namespace Systemic::BluetoothLE::Internal
{
    /**
     * @brief Check whether all elements of \c subset are contained in \c superset.
     * 
     * @tparam T The value type of the containers.
     * @param subset The subset of elements. 
     * @param superset The superset of elements.
     * @return Whether we have a subset. 
     */
    template <typename T>
    bool isSubset(std::vector<T> subset, std::vector<T> superset)
    {
        std::sort(subset.begin(), subset.end());
        std::sort(superset.begin(), superset.end());
        return std::includes(superset.begin(), superset.end(), subset.begin(), subset.end());
    }

    /**
     * @brief Converts a WinRT data buffer to a std::vector<uint8_t>.
     * 
     * @param data 
     * @return std::vector<uint8_t> 
     */
    inline
    std::vector<uint8_t> dataBufferToVector(winrt::Windows::Storage::Streams::IBuffer data)
    {
        std::vector<uint8_t> dst;
        dst.resize(data.Length());
        auto reader = winrt::Windows::Storage::Streams::DataReader::FromBuffer(data);
        reader.ReadBytes(dst);
        return dst;
    }
}
