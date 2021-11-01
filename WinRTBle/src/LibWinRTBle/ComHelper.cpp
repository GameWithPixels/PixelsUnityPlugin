#include "pch.h"
#include "Systemic/ComHelper.h"

#include <strsafe.h>
#include <objbase.h>

#pragma comment(lib,"ole32.lib")

namespace Systemic::ComHelper
{
    const char* copyToComBuffer(const char* str)
    {
        int strLen = str ? lstrlenA(str) : 0;
        if (strLen == 0)
        {
            char* utf8Str = (char*)::CoTaskMemAlloc(1);
            if (utf8Str)
            {
                utf8Str[0] = '\0';
            }
            return utf8Str;
        }
        else
        {
            ++strLen; // To include null-terminating character that we want copied in the utf8 string
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
        int strLen = str ? lstrlenW(str) : 0;
        if (strLen == 0)
        {
            char* utf8Str = (char*)::CoTaskMemAlloc(1);
            if (utf8Str)
            {
                utf8Str[0] = '\0';
            }
            return utf8Str;
        }
        else
        {
            ++strLen; // To include null-terminating character that we want copied in the utf8 string
            int utf8Len = WideCharToMultiByte(CP_UTF8, 0, str, strLen, 0, 0, NULL, NULL);
            char* utf8Str = (char*)::CoTaskMemAlloc(utf8Len);
            if (utf8Str)
            {
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
