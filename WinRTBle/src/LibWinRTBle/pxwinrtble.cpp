#include "pch.h"
#include "../../include/pxwinrtble.h"
#include "CoreBluetoothLE/Scanner.h"
#include "CoreBluetoothLE/DiscoveredPeripheral.h"
#include "CoreBluetoothLE/Peripheral.h"
#include "CoreBluetoothLE/Service.h"
#include "CoreBluetoothLE/Characteristic.h"
#include "DotNetMarshalling.h"

#include <sstream>
#include <mutex>

using namespace Pixels::CoreBluetoothLE;

static std::shared_ptr<Scanner> _scanner;

// Always use lock to access the map
static std::mutex _peripheralsMutex{};
static std::map<peripheral_id_t, std::shared_ptr<Peripheral>> _peripherals{};

// Anonymous namespace for our local functions
namespace
{
    auto findPeripheral(peripheral_id_t peripheralId)
    {
        std::shared_ptr<Peripheral> peripheral{};
        {
            std::lock_guard lock{ _peripheralsMutex };
            auto it = _peripherals.find(peripheralId);
            if (it != _peripherals.end())
            {
                peripheral = it->second;
            }
        }
        return peripheral;
    }

    auto findCharacteristic(
        peripheral_id_t peripheralId,
        const char* serviceUuid,
        const char* characteristicUuid,
        characteristic_index_t instanceIndex)
    {
        std::shared_ptr<Characteristic> characteristic{};
        if (auto peripheral = findPeripheral(peripheralId))
        {
            auto service = peripheral->getDiscoveredService(winrt::guid{ serviceUuid });
            if (service)
            {
                if (!instanceIndex)
                {
                    characteristic = service->getCharacteristic(winrt::guid{ characteristicUuid });
                }
                else
                {
                    auto characteristics = service->getCharacteristics(winrt::guid{ characteristicUuid });
                    if (instanceIndex < characteristics.size())
                    {
                        characteristic = characteristics[instanceIndex];
                    }
                }
            }
        }
        return characteristic;
    }

    void runForPeripheral(
        peripheral_id_t peripheralId,
        RequestStatusCallback onRequestStatus,
        std::function<void(std::shared_ptr<Peripheral>)> action)
    {
        assert(action);

        if (auto peripheral = findPeripheral(peripheralId))
        {
            action(peripheral);
        }
        else if (onRequestStatus)
        {
            onRequestStatus((int)BleRequestStatus::InvalidParameters);
        }
    }

    template <typename T>
    T runForPeripheral(
        peripheral_id_t peripheralId,
        RequestStatusCallback onRequestStatus,
        std::function<T(std::shared_ptr<Peripheral>)> func)
    {
        assert(func);
        T result{};

        if (auto peripheral = findPeripheral(peripheralId))
        {
            result = func(peripheral);
        }
        else if (onRequestStatus)
        {
            onRequestStatus((int)BleRequestStatus::InvalidParameters);
        }

        return result;
    }

    template <typename T>
    T runForCharacteristic(
        peripheral_id_t peripheralId,
        const char* serviceUuid,
        const char* characteristicUuid,
        characteristic_index_t instanceIndex,
        RequestStatusCallback onRequestStatus,
        std::function<T(std::shared_ptr<Characteristic>)> func)
    {
        assert(func);
        T result{};

        if (auto characteristic = findCharacteristic(peripheralId, serviceUuid, characteristicUuid, instanceIndex))
        {
            result = func(characteristic);
        }
        else if (onRequestStatus)
        {
            onRequestStatus((int)BleRequestStatus::InvalidParameters);
        }

        return result;
    }

    void runForPeripheralAsync(
        peripheral_id_t peripheralId,
        RequestStatusCallback onRequestStatus,
        std::function<std::future<void>(std::shared_ptr<Peripheral>)> actionFuture)
    {
        assert(actionFuture);

        if (auto peripheral = findPeripheral(peripheralId))
        {
            // Run future (which will continue on another thread)
            actionFuture(peripheral);
        }
        else if (onRequestStatus)
        {
            onRequestStatus((int)BleRequestStatus::InvalidParameters);
        }
    }

    void runForCharacteristicAsync(
        peripheral_id_t peripheralId,
        const char* serviceUuid,
        const char* characteristicUuid,
        characteristic_index_t instanceIndex,
        RequestStatusCallback onRequestStatus,
        std::function<std::future<void>(std::shared_ptr<Characteristic>)> actionFuture)
    {
        assert(actionFuture);

        if (auto characteristic = findCharacteristic(peripheralId, serviceUuid, characteristicUuid, instanceIndex))
        {
            actionFuture(characteristic);
        }
        else if (onRequestStatus)
        {
            onRequestStatus((int)BleRequestStatus::InvalidParameters);
        }
    }

    std::vector<winrt::guid> toGuids(const char* uuids)
    {
        std::vector<winrt::guid> guids{};
        if (uuids && uuids[0])
        {
            std::istringstream istream{ uuids };
            std::string token{};
            while (std::getline(istream, token, ','))
            {
                guids.emplace_back(token); //TODO check if valid
            }
        }
        return guids;
    }

    void notifyValueChange(ValueChangedCallback onValueChanged, const std::vector<std::uint8_t>& data)
    {
        if (onValueChanged)
        {
            if (data.empty())
            {
                onValueChanged(nullptr, 0, (int)BleRequestStatus::Error);
            }
            else
            {
                onValueChanged(data.data(), data.size(), (int)BleRequestStatus::Success);
            }
        }
    }

    std::string toStr(const winrt::guid& guid)
    {
        auto s = winrt::to_string(winrt::to_hstring(guid)); // Returns a lower case UUID surrounded by curvy brackets
        return s.substr(1, s.size() - 2);
    }

    std::string toJsonStr(const std::shared_ptr<DiscoveredPeripheral>& peripheral)
    {
        std::stringstream str{};
        str << "{\"systemId\":\"" << peripheral->address() << "\"";
        //str << ",\"timestamp\":\"" << peripheral->timestamp() << "\";
        str << ",\"address\":" << peripheral->address();
        str << ",\"isConnectable\":\"" << (peripheral->isConnectable() ? "true" : "false") << "\"";
        str << ",\"rssi\":" << peripheral->rssi();
        str << ",\"name\":\"" << winrt::to_string(peripheral->name()) << "\"";
        if (!peripheral->services().empty())
        {
            str << ",\"services\":[";
            bool first = true;
            for (auto& uuid : peripheral->services())
            {
                if (!first) str << ",";
                first = false;
                str << "\"" << toStr(uuid) << "\"";
            }
            str << "]";
        }
        if (!peripheral->manufacturerData().empty())
        {
            str << ",\"manufacturerData\":[";
            bool first = true;
            for (auto b : peripheral->manufacturerData()[0].data()) //TODO company id, multiple sets
            {
                if (!first) str << ",";
                first = false;
                str << (int)b;
            }
            str << "]";
        }
        str << "}";
        return str.str();
    }
}


//////////////////////////////////////////////////////////////////////////////////////////
//
// Exported C interface
//
//////////////////////////////////////////////////////////////////////////////////////////


// None of the pxBle* methods are thread safe!
bool pxBleInitialize(bool apartmentSingleThreaded, CentralStateUpdateCallback onCentralStateUpdate)
{
    try
    {
        winrt::init_apartment(apartmentSingleThreaded ? winrt::apartment_type::single_threaded : winrt::apartment_type::multi_threaded);
        return true;
    }
    catch (const winrt::hresult_error&)
    {
        return false;
    }
}

void pxBleShutdown()
{
    _scanner.reset();
    {
        std::vector<std::shared_ptr<Peripheral>> copy{ _peripherals.size() };
        {
            std::lock_guard lock{ _peripheralsMutex };
            for (auto& [_, p] : _peripherals)
            {
                copy.emplace_back(p);
            }
            _peripherals.clear();
        }
        // Peripherals are destroyed here (it's important that _peripherals is already empty
    }

    winrt::clear_factory_cache();
    winrt::uninit_apartment();
}

// requiredServicesUuids is a comma separated list of UUIDs, it can be null
bool pxBleStartScan(
    const char* requiredServicesUuids,
    DiscoveredPeripheralCallback onDiscoveredPeripheral)
{
    if (!onDiscoveredPeripheral)
    {
        return false;
    }

    try
    {
        _scanner.reset(new Scanner(
            [onDiscoveredPeripheral](std::shared_ptr<DiscoveredPeripheral> p)
            {
                onDiscoveredPeripheral(toJsonStr(p).data());
            },
            toGuids(requiredServicesUuids)));
        return true;
    }
    catch (const winrt::hresult_error&)
    {
        //TODO Bluetooth not enabled
        return false;
    }
}

void pxBleStopScan()
{
    _scanner.reset();
}

// discoverServicesUuids is a comma separated list of UUIDs, it can be null
bool pxBleCreatePeripheral(
    peripheral_id_t bluetoothAddress,
    PeripheralConnectionStatusChangedCallback onPeripheralStatusChanged)
{
    if (!bluetoothAddress)
    {
        // Invalid parameter
        return false;
    }

    std::lock_guard lock{ _peripheralsMutex };
    if (_peripherals.find(bluetoothAddress) != _peripherals.end())
    {
        // Already created
        return false;
    }

    auto peripheral = std::make_shared<Peripheral>(
        bluetoothAddress,
        [onPeripheralStatusChanged, bluetoothAddress](ConnectionEvent ev, ConnectionEventReason reason)
        {
            if (onPeripheralStatusChanged) onPeripheralStatusChanged(bluetoothAddress, (int)ev, (int)reason);
        }
    );

    if (peripheral)
    {
        assert(bluetoothAddress == peripheral->address());
        _peripherals[bluetoothAddress] = peripheral;
    }

    return (peripheral != nullptr);
}

void pxBleReleasePeripheral(peripheral_id_t peripheralId)
{
    std::shared_ptr<Peripheral> peripheral{};
    {
        std::lock_guard lock{ _peripheralsMutex };
        auto it = _peripherals.find(peripheralId);
        if (it != _peripherals.end())
        {
            peripheral = it->second;
            _peripherals.erase(it);
        }
    }
    // Peripheral is destroyed here
}

void pxBleConnectPeripheral(
    peripheral_id_t peripheralId,
    const char* requiredServicesUuids,
    RequestStatusCallback onRequestStatus)
{
    auto services = toGuids(requiredServicesUuids);
    runForPeripheralAsync(peripheralId, onRequestStatus,
        [services, onRequestStatus](std::shared_ptr<Peripheral> p)->std::future<void>
        {
            auto o = onRequestStatus;
            auto result = co_await p->connectAsync(services);
            if (o) o((int)result);
        }
    );
}

void pxBleDisconnectPeripheral(
    peripheral_id_t peripheralId,
    RequestStatusCallback onRequestStatus)
{
    runForPeripheral(peripheralId, onRequestStatus,
        [onRequestStatus](auto p)
        {
            p->disconnect();
            if (onRequestStatus) onRequestStatus((int)BleRequestStatus::Success);
        }
    );
}

int pxBleGetPeripheralMtu(peripheral_id_t peripheralId)
{
    return runForPeripheral<int>(peripheralId, nullptr,
        [](auto p) { return (int)p->mtu(); });
}

const char* pxBleGetPeripheralName(peripheral_id_t peripheralId)
{
    return runForPeripheral<const char*>(peripheralId, nullptr,
        [](auto p) { return marshalForReturningStrToDotNet(p->name()); });
}

// returns a comma separated list of UUIDs
// caller should free string (Unity marshaling takes care of it)
const char* pxBleGetPeripheralDiscoveredServices(peripheral_id_t peripheralId)
{
    return runForPeripheral<const char*>(peripheralId, nullptr,
        [](auto p)
        {
            std::stringstream str{};
            bool first = true;
            for (auto& s : p->discoveredServices())
            {
                if (!first) str << ",";
                first = false;
                str << toStr(s->uuid());
            }
            return marshalForReturningStrToDotNet(str.str().data());
        }
    );
}

// returns a comma separated list of UUIDs
// caller should free string (Unity marshaling takes care of it)
const char* pxBleGetPeripheralServiceCharacteristics(
    peripheral_id_t peripheralId,
    const char* serviceUuid)
{
    return runForPeripheral<const char*>(peripheralId, nullptr,
        [serviceUuid](auto p)
        {
            std::stringstream str{};
            auto service = p->getDiscoveredService(winrt::guid{ serviceUuid });
            if (service)
            {
                bool first = true;
                for (auto& c : service->characteristics())
                {
                    if (!first) str << ",";
                    first = false;
                    str << toStr(c->uuid());
                }
            }
            return marshalForReturningStrToDotNet(str.str().data());
        }
    );
}

// https://developer.apple.com/documentation/corebluetooth/cbcharacteristicproperties?language=objc
characteristic_property_t pxBleGetCharacteristicProperties(
    peripheral_id_t peripheralId,
    const char* serviceUuid,
    const char* characteristicUuid,
    characteristic_index_t instanceIndex)
{
    return runForCharacteristic<characteristic_property_t>(
        peripheralId, serviceUuid, characteristicUuid, instanceIndex, nullptr,
        [](auto c)
        {
            return (characteristic_property_t)c->properties();
        }
    );
}

void pxBleReadCharacteristicValue(
    peripheral_id_t peripheralId,
    const char* serviceUuid,
    const char* characteristicUuid,
    characteristic_index_t instanceIndex,
    ValueChangedCallback onValueChanged,
    RequestStatusCallback onRequestStatus)
{
    runForCharacteristicAsync(
        peripheralId, serviceUuid, characteristicUuid, instanceIndex, onRequestStatus,
        [onValueChanged, onRequestStatus](std::shared_ptr<Characteristic> c)->std::future<void>
        {
            auto o = onRequestStatus;
            auto v = onValueChanged;
            auto data = co_await c->readValueAsync();
            notifyValueChange(v, data);
            if (o) o(int(!data.empty() ? BleRequestStatus::Success : BleRequestStatus::Error));
        }
    );
}

void pxBleWriteCharacteristicValue(
    peripheral_id_t peripheralId,
    const char* serviceUuid,
    const char* characteristicUuid,
    characteristic_index_t instanceIndex,
    const void* data,
    const size_t length,
    bool withoutResponse,
    RequestStatusCallback onRequestStatus)
{
    auto ptr = (std::uint8_t*)data;
    std::vector<std::uint8_t> value{ ptr, ptr + length };
    runForCharacteristicAsync(
        peripheralId, serviceUuid, characteristicUuid, instanceIndex, onRequestStatus,
        [value, withoutResponse, onRequestStatus](std::shared_ptr<Characteristic> c)->std::future<void>
        {
            auto o = onRequestStatus;
            auto result = co_await c->writeAsync(value, withoutResponse);
            if (o) o((int)result);
        }
    );
}

void pxBleSetNotifyCharacteristic(
    peripheral_id_t peripheralId,
    const char* serviceUuid,
    const char* characteristicUuid,
    characteristic_index_t instanceIndex,
    ValueChangedCallback onValueChanged,
    RequestStatusCallback onRequestStatus)
{
    runForCharacteristicAsync(
        peripheralId, serviceUuid, characteristicUuid, instanceIndex, onRequestStatus,
        [onValueChanged, onRequestStatus](std::shared_ptr<Characteristic> c)->std::future<void>
        {
            auto o = onRequestStatus;
            auto v = onValueChanged;
            BleRequestStatus result;
            if (v)
            {
                result = co_await c->subscribeAsync([v](const std::vector<std::uint8_t>& data) {
                    notifyValueChange(v, data); });
            }
            else
            {
                result = co_await c->unsubscribeAsync();
            }
            if (o) o((int)result);
        }
    );
}
