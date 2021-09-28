#pragma once

#include <algorithm>

namespace Pixels::CoreBluetoothLE
{
    using bluetooth_address_t = std::uint64_t;

    enum class BleRequestStatus
    {
        Success,
        InvalidParameters,
        NotSupported,
        Busy,
        Unreachable,
        GattError,
        Error,
        Canceled,
    };

    namespace Internal
    {
        template <typename T>
        bool isSubset(std::vector<T> elements, std::vector<T> superset)
        {
            std::sort(elements.begin(), elements.end());
            std::sort(superset.begin(), superset.end());
            return std::includes(superset.begin(), superset.end(), elements.begin(), elements.end());
        }
    }
}
