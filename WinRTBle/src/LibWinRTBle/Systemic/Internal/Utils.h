/**
 * @file
 * @brief A few internal functions.
 */

#pragma once

#include <algorithm> // sort, includes

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

    template <typename T>
    bool isOverlapping(const std::vector<T>& v0, const std::vector<T>& v1)
    {
        return std::find_first_of(v0.begin(), v0.end(), v1.begin(), v1.end()) != v0.end();
    }

    /**
     * @brief Converts a WinRT IBuffer to a std::vector<uint8_t>.
     * 
     * @param buffer The WinRT IBuffer.
     * @return A std::vector<uint8_t> with a copy of the data from the buffer.
     */
    inline
    std::vector<uint8_t> dataBufferToBytesVector(winrt::Windows::Storage::Streams::IBuffer buffer)
    {
        using namespace winrt::Windows::Storage::Streams;

        std::vector<uint8_t> outData;
        outData.resize(buffer.Length());
        auto reader = DataReader::FromBuffer(buffer);
        reader.ReadBytes(outData);
        return outData;
    }

    /**
     * @brief Converts a std::vector<uint8_t> to a WinRT IBuffer.
     *
     * @param buffer The vector of bytes.
     * @return A WinRT IBuffer with a copy of the data from the vector.
     */
    inline
    winrt::Windows::Storage::Streams::IBuffer bytesVectorToDataBuffer(const std::vector<std::uint8_t>& data)
    {
        using namespace winrt::Windows::Storage::Streams;

        //InMemoryRandomAccessStream stream{};
        DataWriter dataWriter{}; // { stream };
        dataWriter.ByteOrder(ByteOrder::LittleEndian);
        dataWriter.WriteBytes(data);
        return dataWriter.DetachBuffer();
    }
}
