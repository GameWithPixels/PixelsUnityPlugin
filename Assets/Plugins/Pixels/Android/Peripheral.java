/**
 * \package com.systemic.bluetoothle
 * \brief Android package for simplified access to Bluetooth Low Energy peripherals.
 *
 * Contents:
 * - Peripheral class
 * - Scanner class
 */
 package com.systemic.bluetoothle;

import java.util.ArrayList;
import java.util.List;
import java.util.UUID;
import java.util.Objects;
import android.os.ParcelUuid;
import android.util.Log;
import android.bluetooth.*;
import android.content.Context;
import android.os.Handler;

import no.nordicsemi.android.ble.*;
import no.nordicsemi.android.ble.callback.*;
import no.nordicsemi.android.ble.data.Data;
import no.nordicsemi.android.ble.observer.ConnectionObserver;
import no.nordicsemi.android.ble.annotation.WriteType;

import com.unity3d.player.UnityPlayer;

/**
 * @brief Represents a Bluetooth Low Energy (BLE) peripheral.
 *
 * @note This class was designed to work as a Unity plugin, and thus its methods take
 * and return strings rather than ParcelUuid object for a simplified marshaling with
 * the .NET platform.
 *
 * Implements the most common BLE operations such as reading the peripheral name,
 * MTU, RSSI, listing services and characteristics. As well as  reading, writing
 * and subscribing to characteristics.
 *
 * A new instance takes an Android BluetoothDevice which can be retrieved either
 * from its Bluetooth address or from a scan using the Scanner class. 
 *
 * It relies on Nordic's Android-BLE-Library library for most of the work.
 * @see https://github.com/NordicSemiconductor/Android-BLE-Library
 */
public class Peripheral
{
	private static final String TAG = "systemic";

    /**
     * @brief Interface for most BLE request callbacks.
     */
	public interface RequestCallback extends SuccessCallback, FailCallback, InvalidRequestCallback {}

    /**
     * @brief Interface for MTU change request callbacks.
     */
	public interface MtuRequestCallback extends MtuCallback, FailCallback, InvalidRequestCallback {}

    /**
     * @brief Interface for RSSI reading request callbacks.
     */
	public interface ReadRssiRequestCallback extends RssiCallback, FailCallback, InvalidRequestCallback {}

    /**
     * @brief Interface for characteristic value reading request callbacks.
     */
	public interface ReadValueRequestCallback extends RssiCallback, FailCallback, InvalidRequestCallback {}

    //public enum ConnectionStatus
    //{ 
    //    Disconnected(0), Connected(1);
    //    private final int value;
    //    private ConnectionStatus(int value) { this.value = value; }
    //    public int getValue() { return value; }
    //};

    /**
     * @brief Implements Nordic's BleManager class.
     */
    private class ClientManager extends BleManager
    {
        /**
         * @brief Implements Nordic's BleManagerGattCallback class.
         */
        private class GattCallback extends BleManagerGattCallback
        {
            private BluetoothGatt _gatt;
            
            public GattCallback()
            {
            }

            public BluetoothGattService getService(final UUID serviceUuid)
            {
                return _gatt == null ? null : _gatt.getService(serviceUuid);
            }
            
            public List<BluetoothGattService> getServices()
            {
                return _gatt == null ? null : _gatt.getServices();
            }

            @Override
            protected boolean isRequiredServiceSupported(final BluetoothGatt gatt)
            {
                Log.v(TAG, "==> GattCallback::isRequiredServiceSupported");

                UUID[] servicesUuids = Peripheral.this._requiredServices;
                if (servicesUuids != null)
                {
                    for (UUID uuid : servicesUuids)
                    {
                        boolean found = false;
                        for (BluetoothGattService service : gatt.getServices())
                        {
                            Log.v(TAG, "service " + service.getUuid());
                            if (service.getUuid().equals(uuid))
                            {
                                found = true;
                                break;
                            }
                        }
                        if (!found)
                        {
                            return false;
                        }
                    }
                }

                _gatt = gatt;
                return true;
            }

            @Override
            protected void onServicesInvalidated()
            {
                Log.v(TAG, "==> GattCallback::onServicesInvalidated");

                _gatt = null;
            }
        }
    
        private GattCallback _callback;

        public ClientManager(ConnectionObserver connectionObserver)
        {
            // Use main thread looper (dispatcher)
            super(UnityPlayer.currentActivity.getApplicationContext());
            setConnectionObserver(connectionObserver);
        }

        public BluetoothGattService getService(final UUID serviceUuid)
        {
            return _callback.getService(serviceUuid);
        }
        
        public List<BluetoothGattService> getServices()
        {
            return _callback.getServices();
        }

        public MtuRequest requestMtu(final int mtu)
        {
            return super.requestMtu(mtu);
        }

	    public final int getMtu()
        {
		    return super.getMtu();
	    }

        public ReadRssiRequest readRssi()
        {
		    return super.readRssi();
        }

        public ReadRequest readCharacteristic(final BluetoothGattCharacteristic characteristic)
        {
		    return super.readCharacteristic(characteristic);
        }

        public WriteRequest writeCharacteristic(final BluetoothGattCharacteristic characteristic, final byte[] data, int writeType)
        {
		    return super.writeCharacteristic(characteristic, data, writeType);
        }

	    public ValueChangedCallback setNotificationCallback(final BluetoothGattCharacteristic characteristic)
        {
		    return super.setNotificationCallback(characteristic);
        }

	    public void removeNotificationCallback(final BluetoothGattCharacteristic characteristic)
        {
		    super.removeNotificationCallback(characteristic);
        }

	    public WriteRequest disableNotifications(final BluetoothGattCharacteristic characteristic)
        {
		    return super.disableNotifications(characteristic);
        }

        public WriteRequest enableNotifications(final BluetoothGattCharacteristic characteristic)
        {
		    return super.enableNotifications(characteristic);
        }

        public void cancelOperations()
        {
            super.cancelQueue();
        }

        @Override
        public void log(final int priority, final String message)
        {
            Log.println(priority, TAG, message);
        }

        @Override
        protected BleManagerGattCallback getGattCallback()
        {
            return _callback = new GattCallback();
        }
    }

    private BluetoothDevice _device;
    private ClientManager _client;
    private UUID[] _requiredServices;

    /**
     * @brief Returns the BluetoothDevice object for the given Bluetooth address.
     *
     * @param bluetoothAddress The address of a Bluetooth device.
     * @return A BluetoothDevice or null if there is none for the given address.
     */
    public static BluetoothDevice getDeviceFromAddress(final long bluetoothAddress)
    {
        // Get the Bluetooth Manager
        Context currentActivity
            = com.unity3d.player.UnityPlayer.currentActivity.getApplicationContext();
        BluetoothManager bluetoothManager
            = (BluetoothManager)currentActivity.getSystemService(Context.BLUETOOTH_SERVICE);

        // Convert the Bluetooth address to a string
        StringBuilder sb = new StringBuilder();
        for (int shift = 40; shift >= 0; shift -= 8)
        {
            if (sb.length() > 0) sb.append(":");
            sb.append(String.format("%02X", (bluetoothAddress >> shift) & 0xFF));
        }

        // Returns the Bluetooth device
        return bluetoothManager.getAdapter().getRemoteDevice(sb.toString());
    }

    /*! \name Constructor */
    //! @{

    /**
     * @brief Initializes a peripheral for the given Bluetooth device
     *        and with a connection observer.
     *
     * @param device The Android Bluetooth device object.
     * @param connectionObserver The connection observer to use for notifying connection events.
     */
    public Peripheral(final BluetoothDevice device, final ConnectionObserver connectionObserver)
    {
        Log.v(TAG, "==> createPeripheral");

        // Check arguments
        Objects.requireNonNull(device);
        Objects.requireNonNull(connectionObserver);

        // Store device
        _device = device;

        // Create client manager
        _client = new ClientManager();
    }

    //! @}
    /*! \name Connection and disconnection */
    //! @{

    /**
     * @brief Connects to the BLE peripheral.
     *
     * This request timeouts after 30 seconds.
     *
     * @param requiredServicesUuids Comma separated list of services UUIDs that the peripheral
     *                              should support, may be null or empty.
     * @param autoReconnect Whether to automatically reconnect after an unexpected disconnection
     *                      (i.e. not triggered by a call to disconnect()).
     * @param requestCallback The object implementing the request callbacks.
     */
    public void connect(final String requiredServicesUuids, final boolean autoReconnect, final RequestCallback requestCallback)
    {
        Log.v(TAG, "==> connect");

        // Convert the comma separated list of UUIDs
        UUID[] requiredServices = null;
        if (requiredServicesUuids != null)
        {
            String[] uuids = requiredServicesUuids.split(",");
            if (uuids.length > 0)
            {
                requiredServices = new UUID[uuids.length];
                for (int i = 0; i < uuids.length; ++i)
                {
                    try
                    {
                        requiredServices[i] = UUID.fromString(uuids[i]);
                    }
                    catch (IllegalArgumentException e)
                    {
                        throw new IllegalArgumentException("requiredServicesUuids must be either null, an empty string or a comma separated list of UUIDs");
                    }
                }
            }
        }

        // Store required services for later retrieval (once we know more about the peripheral)
        _requiredServices = requiredServices;

        // Connect
        _client.connect(_device)
            .useAutoConnect(autoConnect)
            .timeout(0) // Actually it will timeout after 30s
            .done(requestCallback).fail(requestCallback).invalid(requestCallback)
            .enqueue();
    }

    /**
     * @brief Immediately disconnects the given peripheral.
     *
     * Any on-going request will either failed or be canceled, including connection requests.
     * Any pending request is dropped.
     *
     * @param requestCallback The object implementing the request callbacks.
     */
    public void disconnect(final RequestCallback requestCallback)
    {
        Log.v(TAG, "==> disconnect");

        // Cancel all on-going operations so the disconnect can happen immediately
        _client.cancelOperations();

        // Disconnect (request will be ignored we are disconnecting)
        if (_client.getConnectionState() != BluetoothProfile.STATE_DISCONNECTING)
        {
            _client.disconnect()
                .done(requestCallback).fail(requestCallback).invalid(requestCallback)
                .enqueue();
        }
        else if (requestCallback != null)
        {
            //TODO this will happen if device was connecting, we should return a success once disconnected!
            // Immediately Notify invalid request
            requestCallback.onInvalidRequest();
        }
    }

    //! @}
    /*! \name Peripheral operations
     *  Valid only for connected peripherals. */
    //! @{

    /**
     * @brief Returns the name of the peripheral.
     *
     * @return The name, or null if the call failed.
     */
    public String getName()
    {
        Log.v(TAG, "==> getName");

        return _device.getName();
    }

    /**
     * @brief Returns the Maximum Transmission Unit (MTU).
     *
     * @return The MTU, or zero if the call failed.
     */
    public int getMtu()
    {
        Log.v(TAG, "==> getMtu");

        return _client.getMtu();
    }

    /**
     * @brief Request the peripheral to change its MTU to the given value.
     *
     * @param mtu The requested MTU, must be between 23 and 517 included.
     * @param mtuChangedCallback The object implementing the MTU request callbacks.
     */
    public void requestMtu(int mtu, final MtuRequestCallback mtuChangedCallback)
    {
        Log.v(TAG, "==> requestMtu " + mtu);

        _client.requestMtu(mtu)
            .with(mtuChangedCallback).fail(mtuChangedCallback).invalid(mtuChangedCallback)
            .enqueue();
    }

    /**
     * @brief Reads the current Received Signal Strength Indicator (RSSI).
     *
     * @param rssiReadCallback The object implementing the read RSSI request callbacks.
     */
    public void readRssi(final ReadRssiRequestCallback rssiReadCallback)
    {
        Log.v(TAG, "==> readRssi");

        _client.readRssi()
            .with(rssiReadCallback).fail(rssiReadCallback).invalid(rssiReadCallback)
            .enqueue();
    }

    /**
     * @brief Returns the list of discovered services.
     *
     * @return A comma separated list of services UUIDs, or null if the call failed.
     */
    public String getDiscoveredServices()
    {
        Log.v(TAG, "==> getDiscoveredServices");

        // Get services
        List<BluetoothGattService> services = _client.getServices();
        if (services == null)
        {
            return null;
        }
        else
        {
            // Convert to a comma separated list
            StringBuilder sb = new StringBuilder();
            for (BluetoothGattService serv : services)
            {
                if (sb.length() > 0) sb.append(",");
                sb.append(serv.getUuid());
            }
            return sb.toString();
        }
    }

    /**
     * @brief Returns the list of discovered characteristics for the given service.
     *
     * The same characteristic may be listed several times according to the peripheral's configuration.
     *
     * @param serviceUuid The service UUID for which to retrieve the characteristics.
     * @return A comma separated list of characteristics UUIDs, or null if the call failed.
     */
    public String getServiceCharacteristics(final String serviceUuid)
    {
        Log.v(TAG, "==> getServiceCharacteristics " + serviceUuid);

        // Get the service
        BluetoothGattService service = getService(serviceUuid);
        if (service != null)
        {
            // Get the list of characteristics
            List<BluetoothGattCharacteristic> characteristics = service.getCharacteristics();
            if (characteristics != null)
            {
                // Convert to a comma separated list
                StringBuilder sb = new StringBuilder();
                for (BluetoothGattCharacteristic charac : characteristics)
                {
                    if (sb.length() > 0) sb.append(",");
                    sb.append(charac.getUuid());
                }
                return sb.toString();
            }
        }
        return null;
    }

    /**
     * @brief Returns the standard BLE properties of the specified service's characteristic.
     *
     * @see https://developer.android.com/reference/android/bluetooth/BluetoothGattCharacteristic#PROPERTY_BROADCAST,
     * PROPERTY_READ, PROPERTY_NOTIFY, etc.
     *
     * @param serviceUuid The service UUID.
     * @param characteristicUuid The characteristic UUID.
     * @param instanceIndex The instance index of the characteristic if listed more than once
     *                      for the service, otherwise zero.
     * @return The standard BLE properties of a service's characteristic, or zero if the call failed.
     */
    public int getCharacteristicProperties(final String serviceUuid, final String characteristicUuid, final int instanceIndex)
    {
        Log.v(TAG, "==> getCharacteristicProperties " + characteristicUuid);

        BluetoothGattCharacteristic characteristic
            = getCharacteristic(serviceUuid, characteristicUuid, instanceIndex);

        return characteristic == null ? 0 : characteristic.getProperties();
    }

    /**
     * @brief Sends a request to read the value of the specified service's characteristic.
     *
     * @param serviceUuid The service UUID.
     * @param characteristicUuid The characteristic UUID.
     * @param instanceIndex The instance index of the characteristic if listed more than once
     *                      for the service, otherwise zero.
     * @param valueReadCallback The object implementing the read value request callbacks.
     */
    public void readCharacteristic(final String serviceUuid, final String characteristicUuid, final int instanceIndex, final ReadValueRequestCallback valueReadCallback)
    {
        Log.v(TAG, "==> readCharacteristic " + characteristicUuid);

        // Get the characteristic
        BluetoothGattCharacteristic characteristic
            = getCharacteristic(serviceUuid, characteristicUuid, instanceIndex);

        // Send the read request
        _client.readCharacteristic(characteristic)
            .with(valueReadCallback)
            .done(valueReadCallback).fail(valueReadCallback).invalid(valueReadCallback)
            .enqueue();
    }

    /**
     * @brief Sends a request to write to the specified service's characteristic
     *        for the given peripheral.
     *
     * @param serviceUuid The service UUID.
     * @param characteristicUuid The characteristic UUID.
     * @param instanceIndex The instance index of the characteristic if listed more than once
     *                      for the service, otherwise zero.
     * @param data The data to write to the characteristic.
     * @param withoutResponse Whether to wait for the peripheral to respond.
     * @param requestCallback The object implementing the request callbacks.
     */
    public void writeCharacteristic(final String serviceUuid, final String characteristicUuid, final int instanceIndex, final byte[] data, boolean withoutResponse, final RequestCallback requestCallback)
    {
        Log.v(TAG, "==> writeCharacteristic " + characteristicUuid);

        // Get the characteristic
        BluetoothGattCharacteristic characteristic
            = getCharacteristic(serviceUuid, characteristicUuid, instanceIndex);
        int writeType = withoutResponse
            ? BluetoothGattCharacteristic.WRITE_TYPE_NO_RESPONSE
            : BluetoothGattCharacteristic.WRITE_TYPE_DEFAULT;

        // Send the write request
        _client.writeCharacteristic(characteristic, data, writeType)
            .done(requestCallback).fail(requestCallback).invalid(requestCallback)
            .enqueue();
    }

    /**
     * @brief Subscribes for value changes of the specified service's characteristic.
     *
     * @param serviceUuid The service UUID.
     * @param characteristicUuid The characteristic UUID.
     * @param instanceIndex The instance index of the characteristic if listed more than once
     *                      for the service, otherwise zero.
     * @param valueChangedCallback The object implementing the callback for characteristic's value changes.
     * @param requestCallback The object implementing the request callbacks.
     */
    public void subscribeCharacteristic(final String serviceUuid, final String characteristicUuid, final int instanceIndex, final DataReceivedCallback valueChangedCallback, final RequestCallback requestCallback)
    {
        Log.v(TAG, "==> subscribeCharacteristic" + characteristicUuid);

        // Get the characteristic
        BluetoothGattCharacteristic characteristic
            = getCharacteristic(serviceUuid, characteristicUuid, instanceIndex);

        // Subscribe to notifications
        _client.setNotificationCallback(characteristic)
            .with(valueChangedCallback);

        // And turn them on
        _client.enableNotifications(characteristic)
            .done(requestCallback).fail(requestCallback).invalid(requestCallback)
            .enqueue();
    }

    /**
     * @brief Unsubscribes from the specified service's characteristic.
     *
     * @param serviceUuid The service UUID.
     * @param characteristicUuid The characteristic UUID.
     * @param instanceIndex The instance index of the characteristic if listed more than once
     *                      for the service, otherwise zero.
     * @param requestCallback The object implementing the request callbacks.
     */
    public void unsubscribeCharacteristic(final String serviceUuid, final String characteristicUuid, final int instanceIndex, final RequestCallback requestCallback)
    {
        Log.v(TAG, "==> unsubscribeCharacteristic" + characteristicUuid);

        // Get the characteristic
        BluetoothGattCharacteristic characteristic
            = getCharacteristic(serviceUuid, characteristicUuid, instanceIndex);

        // Unsubscribe from notifications
        _client.removeNotificationCallback(characteristic);

        // And turn them of
        _client.disableNotifications(characteristic)
            .done(requestCallback).fail(requestCallback).invalid(requestCallback)
            .enqueue();
    }

    //! @}

    /**
     * @brief Returns the Android gatt service object for the given service UUID.
     */
    private BluetoothGattService getService(final String serviceUuid)
    {
        return _client.getService(UUID.fromString(serviceUuid));
    }

    /**
     * @brief Returns the Android gatt characteristic object for the given characteristic UUID.
     */
    private BluetoothGattCharacteristic getCharacteristic(final String serviceUuid, final String characteristicUuid, final int instanceIndex)
    {
        // Get the service
        BluetoothGattService service = getService(serviceUuid);
        if (service != null)
        {
            // Get the list of characteristics
            List<BluetoothGattCharacteristic> characteristics = service.getCharacteristics();
            if (characteristics != null)
            {
                // Look-up for the characteristic with the specified index
                UUID uuid = UUID.fromString(characteristicUuid);
                int counter = 0;
                for (BluetoothGattCharacteristic charac : characteristics)
                {
                    if (charac.getUuid().equals(uuid))
                    {
                        if (counter == instanceIndex)
                        {
                            return charac;
                        }
                        else
                        {
                            ++counter;
                        }
                    }
                }
            }
        }
        return null;
    }
}