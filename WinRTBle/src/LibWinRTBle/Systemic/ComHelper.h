#pragma once

namespace Systemic::ComHelper
{
    const char* copyToComBuffer(const char* str);
    const char* copyToComBuffer(const wchar_t* str);
    void freeComBuffer(void* buffer);
}
