#include "pch.h"
#include "../../include/cble.h"
#include "Systemic/BluetoothLE/BluetoothLE.h"
#include "Systemic/BluetoothLE/Scanner.h"
#include "Systemic/BluetoothLE/ScannedPeripheral.h"
#include "Systemic/BluetoothLE/Peripheral.h"
#include "Systemic/BluetoothLE/Service.h"
#include "Systemic/BluetoothLE/Characteristic.h"
#include "Systemic/ComHelper.h"

#include <sstream>
#include <mutex>

using namespace winrt::Windows::Devices::Bluetooth;
using namespace Systemic;
using namespace Systemic::BluetoothLE;

// Always use lock to access the scanner
static std::mutex _scannerMutex{};
static std::shared_ptr<Scanner> _scanner;

// Always use lock to access the map
static std::mutex _peripheralsMutex{};
static std::map<bluetooth_address_t, std::shared_ptr<Peripheral>> _peripherals{};

// Anonymous namespace for our local functions
namespace
{
    auto findPeripheral(bluetooth_address_t address)
    {
        std::shared_ptr<Peripheral> peripheral{};
        {
            std::lock_guard lock{ _peripheralsMutex };
            auto it = _peripherals.find(address);
            if (it != _peripherals.end())
            {
                peripheral = it->second;
            }
        }
        return peripheral;
    }

    auto findCharacteristic(
        bluetooth_address_t address,
        const char* serviceUuid,
        const char* characteristicUuid,
        characteristic_index_t instanceIndex)
    {
        std::shared_ptr<Characteristic> characteristic{};
        if (auto peripheral = findPeripheral(address))
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
        bluetooth_address_t address,
        RequestStatusCallback onRequestStatus,
        const std::function<void(std::shared_ptr<Peripheral>)>& action)
    {
        assert(action);

        if (auto peripheral = findPeripheral(address))
        {
            action(peripheral);
        }
        else if (onRequestStatus)
        {
            onRequestStatus(BleRequestStatus::InvalidPeripheral);
        }
    }

    template <typename T>
    T runForPeripheral(
        bluetooth_address_t address,
        RequestStatusCallback onRequestStatus,
        const std::function<T(std::shared_ptr<Peripheral>)>& func)
    {
        assert(func);
        T result{};

        if (auto peripheral = findPeripheral(address))
        {
            result = func(peripheral);
        }
        else if (onRequestStatus)
        {
            onRequestStatus(BleRequestStatus::InvalidPeripheral);
        }

        return result;
    }

    template <typename T>
    T runForCharacteristic(
        bluetooth_address_t address,
        const char* serviceUuid,
        const char* characteristicUuid,
        characteristic_index_t instanceIndex,
        RequestStatusCallback onRequestStatus,
        const std::function<T(std::shared_ptr<Characteristic>)>& func)
    {
        assert(func);
        T result{};

        if (auto characteristic = findCharacteristic(address, serviceUuid, characteristicUuid, instanceIndex))
        {
            result = func(characteristic);
        }
        else if (onRequestStatus)
        {
            onRequestStatus(BleRequestStatus::InvalidParameters);
        }

        return result;
    }

    void runForPeripheralAsync(
        bluetooth_address_t address,
        RequestStatusCallback onRequestStatus,
        const std::function<std::future<void>(std::shared_ptr<Peripheral>)>& actionFuture)
    {
        assert(actionFuture);

        if (auto peripheral = findPeripheral(address))
        {
            // Run the future (which continues executing on another thread)
            actionFuture(peripheral);
        }
        else if (onRequestStatus)
        {
            onRequestStatus(BleRequestStatus::InvalidPeripheral);
        }
    }

    BleRequestStatus runForCharacteristicAsync(
        bluetooth_address_t address,
        const char* serviceUuid,
        const char* characteristicUuid,
        characteristic_index_t instanceIndex,
        RequestStatusCallback onRequestStatus,
        const std::function<std::future<void>(std::shared_ptr<Characteristic>)>& actionFuture)
    {
        assert(actionFuture);

        auto status = BleRequestStatus::Success;

        if (auto characteristic = findCharacteristic(address, serviceUuid, characteristicUuid, instanceIndex))
        {
            // Run the future (which continues executing on another thread)
            actionFuture(characteristic);
            return BleRequestStatus::Success;
        }
        else
        {
            status = BleRequestStatus::InvalidParameters;
            if (onRequestStatus)
            {
                onRequestStatus(status);
            }
        }
        return status;
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

    std::string toStr(const winrt::guid& guid)
    {
        auto s = winrt::to_string(winrt::to_hstring(guid)); // Returns a lower case UUID surrounded by curvy brackets
        return s.substr(1, s.size() - 2);
    }

    std::string toJsonStr(const std::shared_ptr<ScannedPeripheral>& peripheral)
    {
        std::stringstream str{};
        str << "{\"systemId\":\"" << peripheral->address() << "\"";
        //str << ",\"timestamp\":\"" << peripheral->timestamp() << "\";
        str << ",\"address\":" << peripheral->address();
        str << ",\"name\":\"" << winrt::to_string(peripheral->name()) << "\"";
        str << ",\"isConnectable\":" << (peripheral->isConnectable() ? "true" : "false");
        str << ",\"rssi\":" << peripheral->rssi();
        str << ",\"txPowerLevel\":" << peripheral->txPowerLevel();

        // Services
        if (!peripheral->services().empty())
        {
            str << ",\"services\":[";
            auto& services = peripheral->services();
            for (size_t i = 0, iMax = services.size(); i < iMax; ++i)
            {
                if (i > 0) str << ",";
                str << "\"" << toStr(services[i]) << "\"";
            }
            str << "]";
        }

        // Manufacturers data
        if (!peripheral->manufacturersData().empty())
        {
            str << ",\"manufacturersData\":[";
            auto& manufData = peripheral->manufacturersData();
            for (size_t i = 0, iMax = manufData.size(); i < iMax; ++i)
            {
                if (i > 0) str << ",";
                str << "{\"companyId\":" << manufData[i].companyId();
                str << ",\"data\":[";
                auto& data = manufData[i].data();
                for (size_t j = 0, jMax = data.size(); j < jMax; ++j)
                {
                    if (j > 0) str << ",";
                    str << (int)data[j];
                }
                str << "]}";
            }
            str << "]";
        }

        // Services data
        if (!peripheral->servicesData().empty())
        {
            str << ",\"servicesData\":[";
            auto& servData = peripheral->servicesData();
            for (size_t i = 0, iMax = servData.size(); i < iMax; ++i)
            {
                if (i > 0) str << ",";
                const auto uuid = BluetoothUuidHelper::FromShortId(servData[i].shortUuid());
                str << "{\"uuid\":\"" << toStr(uuid);
                str << "\",\"data\":[";
                auto& data = servData[i].data();
                for (size_t j = 0, jMax = data.size(); j < jMax; ++j)
                {
                    if (j > 0) str << ",";
                    str << (int)data[j];
                }
                str << "]}";
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

bool sgBleInitialize(bool apartmentSingleThreaded, BluetoothStateUpdateCallback onBluetoothStateUpdate)
{
    try
    {
        winrt::init_apartment(apartmentSingleThreaded ? winrt::apartment_type::single_threaded : winrt::apartment_type::multi_threaded);
        if (onBluetoothStateUpdate)
        {
            // This future will continue executing on another thread
            auto notify = [](BluetoothStateUpdateCallback onBluetoothStateUpdate)->std::future<void>
            {
                auto state = co_await getAdapterStateAsync();
                // TODO check if we haven't shutdown
                onBluetoothStateUpdate(state);
            }(onBluetoothStateUpdate);

            // Watch for adapter state changes
            subscribeAdapterStateChangedAsync(onBluetoothStateUpdate);
        }
        return true;
    }
    catch (const winrt::hresult_error&)
    {
        return false;
    }
}

void sgBleShutdown()
{
    // Unsubscribe from adapter state changes
    subscribeAdapterStateChangedAsync(nullptr);

    // Destroy scanner instance
    {
        std::lock_guard lock{ _scannerMutex };
        _scanner.reset();
    }

    // Destroy all peripherals
    {
        std::vector<std::shared_ptr<Peripheral>> copy{ _peripherals.size() };
        {
            std::lock_guard lock{ _peripheralsMutex };
            for (auto& [_, p] : _peripherals)
            {
                copy.emplace_back(p);
            }
            _peripherals.clear();
            //TODO remove all subscriptions callbacks so their events are not raised after this point
        }
        // Peripherals are destroyed here
        // We want _peripherals to be empty in case a callback to user code tries
        // to access to a peripheral (wile it's being destroyed)
    }

    winrt::clear_factory_cache();
    winrt::uninit_apartment();

    //TODO prevent callbacks to native code after shutdown? But this as implications on the managed code which expect all requests to return a result
}

// requiredServicesUuids is a comma separated list of UUIDs, it can be null
bool sgBleStartScan(
    const char* requiredServicesUuids,
    DiscoveredPeripheralCallback onDiscoveredPeripheral)
{
    if (!onDiscoveredPeripheral)
    {
        return false;
    }

    try
    {
        std::lock_guard lock{ _scannerMutex };
        _scanner.reset(new Scanner(
            [onDiscoveredPeripheral](std::shared_ptr<ScannedPeripheral> p)
            {
                onDiscoveredPeripheral(toJsonStr(p).data());
            },
            toGuids(requiredServicesUuids)));
        return true;
    }
    catch (const winrt::hresult_error&)
    {
        return false;
    }
}

void sgBleStopScan()
{
    std::lock_guard lock{ _scannerMutex };
    _scanner.reset();
}

// discoverServicesUuids is a comma separated list of UUIDs, it can be null
bool sgBleCreatePeripheral(
    bluetooth_address_t bluetoothAddress,
    PeripheralConnectionEventCallback onPeripheralConnectionEvent)
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
        [onPeripheralConnectionEvent, bluetoothAddress](ConnectionEvent ev, ConnectionEventReason reason)
        {
            //TODO check valid peripheral
            if (onPeripheralConnectionEvent) onPeripheralConnectionEvent(bluetoothAddress, ev, reason);
        }
    );

    if (peripheral)
    {
        assert(bluetoothAddress == peripheral->address());
        _peripherals[bluetoothAddress] = peripheral;
    }

    return (peripheral != nullptr);
}

void sgBleReleasePeripheral(bluetooth_address_t address)
{
    std::shared_ptr<Peripheral> peripheral{};
    {
        std::lock_guard lock{ _peripheralsMutex };
        auto it = _peripherals.find(address);
        if (it != _peripherals.end())
        {
            peripheral = it->second;
            _peripherals.erase(it);
        }
    }
    // Peripheral is destroyed here
}

void sgBleConnectPeripheral(
    bluetooth_address_t address,
    const char* requiredServicesUuids,
    bool autoReconnect,
    RequestStatusCallback onRequestStatus)
{
    auto services = toGuids(requiredServicesUuids);
    runForPeripheralAsync(address, onRequestStatus,
        [services, autoReconnect, onRequestStatus](std::shared_ptr<Peripheral> p)->std::future<void>
        {
            auto o = onRequestStatus;
            auto result = co_await p->connectAsync(services, autoReconnect);
            if (o) o(result);
        }
    );
}

//TODO return BleRequestStatus as out parameter
void sgBleDisconnectPeripheral(
    bluetooth_address_t address,
    RequestStatusCallback onRequestStatus)
{
    runForPeripheral(address, onRequestStatus,
        [onRequestStatus](auto p)
        {
            p->disconnect();
            if (onRequestStatus) onRequestStatus(BleRequestStatus::Success);
        }
    );
}

//TODO return BleRequestStatus as out parameter
// caller should free string with CoTaskMemFree() or sgFreeString() (.NET marshaling takes care of it)
const char* sgBleGetPeripheralName(bluetooth_address_t address)
{
    return runForPeripheral<const char*>(address, nullptr,
        [](auto p) { return ComHelper::copyToComBuffer(p->name()); });
}

//TODO return BleRequestStatus as out parameter
int sgBleGetPeripheralMtu(bluetooth_address_t address)
{
    return runForPeripheral<int>(address, nullptr,
        [](auto p) { return (int)p->mtu(); });
}

// returns a comma separated list of UUIDs
// caller should free string with CoTaskMemFree() or sgFreeString() (.NET marshaling takes care of it)
//TODO return BleRequestStatus as out parameter
const char* sgBleGetDiscoveredServices(bluetooth_address_t address)
{
    return runForPeripheral<const char*>(address, nullptr,
        [](auto p)
        {
            std::stringstream str{};
            std::vector<std::shared_ptr<Service>> services{};
            p->copyDiscoveredServices(services);
            for (size_t i = 0, iMax = services.size(); i < iMax; ++i)
            {
                if (i > 0) str << ",";
                str << toStr(services[i]->uuid());
            }
            return ComHelper::copyToComBuffer(str.str().data());
        }
    );
}

// returns a comma separated list of UUIDs
// caller should free string with CoTaskMemFree() or sgFreeString() (.NET marshaling takes care of it)
//TODO return BleRequestStatus as out parameter
const char* sgBleGetServiceCharacteristics(
    bluetooth_address_t address,
    const char* serviceUuid)
{
    return runForPeripheral<const char*>(address, nullptr,
        [serviceUuid](auto p)
        {
            std::stringstream str{};
            auto service = p->getDiscoveredService(winrt::guid{ serviceUuid });
            if (service)
            {
                std::vector<std::shared_ptr<Characteristic>> characteristics{};
                service->copyCharacteristics(characteristics);
                for (size_t i = 0, iMax = characteristics.size(); i < iMax; ++i)
                {
                    if (i > 0) str << ",";
                    str << toStr(characteristics[i]->uuid());
                }
            }
            return ComHelper::copyToComBuffer(str.str().data());
        }
    );
}

//TODO return BleRequestStatus as out parameter
int sgBleGetCharacteristicProperties(
    bluetooth_address_t address,
    const char* serviceUuid,
    const char* characteristicUuid,
    characteristic_index_t instanceIndex)
{
    return runForCharacteristic<int>(
        address, serviceUuid, characteristicUuid, instanceIndex, nullptr,
        [](auto c)
        {
            return static_cast<int>(c->properties());
        }
    );
}

void sgBleReadCharacteristic(
    bluetooth_address_t address,
    const char* serviceUuid,
    const char* characteristicUuid,
    characteristic_index_t instanceIndex,
    ValueReadCallback onValueRead)
{
    auto status = runForCharacteristicAsync(
        address, serviceUuid, characteristicUuid, instanceIndex, nullptr,
        [onValueRead](std::shared_ptr<Characteristic> c)->std::future<void>
        {
            auto v = onValueRead;
            auto data = co_await c->readValueAsync();
            if (v)
            {
                if (data.empty())
                {
                    v(nullptr, 0, BleRequestStatus::Error); //TODO more error codes
                }
                else
                {
                    v(data.data(), data.size(), BleRequestStatus::Success);
                }
            }
        }
    );
    if (status != BleRequestStatus::Success)
    {
        onValueRead(nullptr, 0, status);
    }
}

void sgBleWriteCharacteristic(
    bluetooth_address_t address,
    const char* serviceUuid,
    const char* characteristicUuid,
    characteristic_index_t instanceIndex,
    const void* data,
    const size_t length,
    bool withoutResponse,
    RequestStatusCallback onRequestStatus)
{
    if (data || (length == 0))
    {
        auto ptr = (std::uint8_t*)data;
        std::vector<std::uint8_t> value{ ptr, ptr + length };
        runForCharacteristicAsync(
            address, serviceUuid, characteristicUuid, instanceIndex, onRequestStatus,
            [value, withoutResponse, onRequestStatus](std::shared_ptr<Characteristic> c)->std::future<void>
            {
                auto o = onRequestStatus;
                auto result = co_await c->writeAsync(value, withoutResponse);
                if (o) o(result);
            }
        );
    }
    else if (onRequestStatus)
    {
        onRequestStatus(BleRequestStatus::InvalidParameters);
    }
}

void sgBleSetNotifyCharacteristic(
    bluetooth_address_t address,
    const char* serviceUuid,
    const char* characteristicUuid,
    characteristic_index_t instanceIndex,
    ValueChangedCallback onValueChanged,
    RequestStatusCallback onRequestStatus)
{
    runForCharacteristicAsync(
        address, serviceUuid, characteristicUuid, instanceIndex, onRequestStatus,
        [onValueChanged, onRequestStatus](std::shared_ptr<Characteristic> c)->std::future<void>
        {
            auto o = onRequestStatus;
            auto v = onValueChanged;
            BleRequestStatus result;
            if (v)
            {
                result = co_await c->subscribeAsync(
                    [v](const std::vector<std::uint8_t>& data)
                    {
                        v(data.empty() ? nullptr : data.data(), data.size());
                    });
            }
            else
            {
                result = co_await c->unsubscribeAsync();
            }
            if (o) o(result);
        }
    );
}

void sgFreeString(char* str)
{
    ComHelper::freeComBuffer(str);
}