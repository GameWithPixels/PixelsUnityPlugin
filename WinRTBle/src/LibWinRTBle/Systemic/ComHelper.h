/**
 * @file
 * @brief Helper methods for COM string marshaling.
 */

#pragma once

/**
 * @brief Helper methods for COM string marshaling.
 */
namespace Systemic::ComHelper
{
    /**
     * @brief Copy the UTF-8 C string into a COM buffer.
     * 
     * @param The UTF-8 C string. 
     * @return The COM buffer containing a copy of the string.
     */
    const char* copyToComBuffer(const char* str);

    /**
     * @brief Convert the UTF-16 C string to UTF-8 and copy the result into a COM buffer.
     *
     * @param The UTF-16 C string.
     * @return The COM buffer containing a copy of the string once converted to UTF-8.
     */
    const char* copyToComBuffer(const wchar_t* str);

    /**
     * @brief Free a COM buffer.
     * 
     * @param The buffer to free.
     */
    void freeComBuffer(void* buffer);
}
