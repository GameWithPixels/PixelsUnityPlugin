#pragma once

#include <algorithm>
#include "../../../include/bletypes.h"

namespace Systemic::BluetoothLE
{
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
