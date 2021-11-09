#include "pch.h"
#include "Systemic/ComHelper.h"

#include <strsafe.h>
#include <objbase.h>

#pragma comment(lib,"ole32.lib")

namespace Systemic::ComHelper
{
    const char* copyToComBuffer(const char* str)
    {
        // Get string length
        int strLen = str ? lstrlenA(str) : 0;
        if (strLen == 0)
        {
            // We got an empty string, return a buffer with just a null terminator
            char* utf8Str = (char*)::CoTaskMemAlloc(1);
            if (utf8Str)
            {
                utf8Str[0] = '\0';
            }
            return utf8Str;
        }
        else
        {
            // Take the null terminator in account
            ++strLen;

            // Allocate a COM buffer
            char* utf8Str = (char*)::CoTaskMemAlloc(strLen);
            if (utf8Str)
            {
                ::memcpy(utf8Str, str, strLen);
            }
            return utf8Str;
        }
    }

    const char* copyToComBuffer(const wchar_t* str)
    {
        // Get string length
        int strLen = str ? lstrlenW(str) : 0;
        if (strLen == 0)
        {
            // We got an empty string, return a buffer with just a null terminator
            char* utf8Str = (char*)::CoTaskMemAlloc(1);
            if (utf8Str)
            {
                utf8Str[0] = '\0';
            }
            return utf8Str;
        }
        else
        {
            // Take the null terminator in account
            ++strLen;

            // Allocate a COM buffer
            int utf8Len = WideCharToMultiByte(CP_UTF8, 0, str, strLen, 0, 0, NULL, NULL);
            char* utf8Str = (char*)::CoTaskMemAlloc(utf8Len);

            if (utf8Str)
            {
                // Copy and convert the string to UTF-8
                if (!WideCharToMultiByte(CP_UTF8, 0, str, strLen, utf8Str, utf8Len, NULL, NULL))
                {
                    utf8Str[0] = '\0';
                }
            }
            return utf8Str;
        }
    }

    void freeComBuffer(void* buffer)
    {
        ::CoTaskMemFree(buffer);
    }
}
