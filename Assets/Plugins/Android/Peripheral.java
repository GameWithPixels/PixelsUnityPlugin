package com.systemic.pixels;

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

public class Peripheral
{
	private static final String TAG = "systemic";

	public interface RequestCallback extends SuccessCallback, FailCallback, InvalidRequestCallback {}
	public interface MtuRequestCallback extends MtuCallback, FailCallback, InvalidRequestCallback {}
	public interface RssiRequestCallback extends RssiCallback, FailCallback, InvalidRequestCallback {}

    //public enum ConnectionStatus
    //{ 
    //    Disconnected(0), Connected(1);
    //    private final int value;
    //    private ConnectionStatus(int value) { this.value = value; }
    //    public int getValue() { return value; }
    //};

    private class ClientManager extends BleManager
    {
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

        public ClientManager()
        {
            // Use main thread looper (dispatcher)
            super(UnityPlayer.currentActivity.getApplicationContext());
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

    public static BluetoothDevice getDeviceFromAddress(final long address)
    {
        Context currentActivity = com.unity3d.player.UnityPlayer.currentActivity.getApplicationContext();
        BluetoothManager bluetoothManager = (BluetoothManager)currentActivity.getSystemService(Context.BLUETOOTH_SERVICE);

        StringBuilder sb = new StringBuilder();
        for (int shift = 40; shift >= 0; shift -= 8)
        {
            if (sb.length() > 0) sb.append(":");
            sb.append(String.format("%02X", (address >> shift) & 0xFF));
        }
        return bluetoothManager.getAdapter().getRemoteDevice(sb.toString());
    }

    public Peripheral(final BluetoothDevice device, final ConnectionObserver connectionObserver)
    {
        Log.v(TAG, "==> createPeripheral");

        Objects.requireNonNull(device);
        Objects.requireNonNull(connectionObserver);

        _device = device;
        _client = new ClientManager();
        _client.setConnectionObserver(connectionObserver);
    }

    public void connect(final String requiredServicesUuids, final RequestCallback callback)
    {
        Log.v(TAG, "==> connect");

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

        _requiredServices = requiredServices;

        _client.connect(_device)//.useAutoConnect(true)
            .timeout(0)
            .done(new SuccessCallback()
            {
                public void onRequestCompleted(final BluetoothDevice device)
                {
                    callback.onRequestCompleted(device);
                }
            })
            .fail(callback).invalid(callback)
            .enqueue();
    }

    public void disconnect(final RequestCallback callback)
    {
        Log.v(TAG, "==> disconnect");

        _client.disconnect()
            //.timeout(0) TODO
            .done(callback).fail(callback).invalid(callback)
            .enqueue();
    }

    public String getName()
    {
        Log.v(TAG, "==> getName");

        return _device.getName();
    }

    public int getMtu()
    {
        Log.v(TAG, "==> getMtu");

        return _client.getMtu();
    }

    public void requestMtu(int mtu, final MtuRequestCallback callback)
    {
        Log.v(TAG, "==> requestMtu " + mtu);

        _client.requestMtu(mtu)
            .with(callback).fail(callback).invalid(callback)
            .enqueue();
    }

    public void readRssi(final RssiRequestCallback callback)
    {
        Log.v(TAG, "==> readRssi");

        _client.readRssi()
            .with(callback).fail(callback).invalid(callback)
            .enqueue();
    }

    public String getDiscoveredServices()
    {
        Log.v(TAG, "==> getDiscoveredServices");

        List<BluetoothGattService> services = _client.getServices();
        if (services == null)
        {
            return null;
        }
        else
        {
            StringBuilder sb = new StringBuilder();
            for (BluetoothGattService serv : services)
            {
                if (sb.length() > 0) sb.append(",");
                sb.append(serv.getUuid());
            }
            return sb.toString();
        }
    }

    // See https://developer.android.com/reference/android/bluetooth/BluetoothGattCharacteristic#getInstanceId()
    public String getServiceCharacteristics(final String serviceUuid)
    {
        Log.v(TAG, "==> getServiceCharacteristics " + serviceUuid);

        BluetoothGattService service = getService(serviceUuid);
        if (service != null)
        {
            List<BluetoothGattCharacteristic> characteristics = service.getCharacteristics();
            if (characteristics != null)
            {
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

    // https://developer.android.com/reference/android/bluetooth/BluetoothGattCharacteristic#PROPERTY_BROADCAST
    // BluetoothGattCharacteristic.PROPERTY_READ
    // BluetoothGattCharacteristic.PROPERTY_NOTIFY
    // etc.
    public int getCharacteristicProperties(final String serviceUuid, final String characteristicUuid, final int instanceIndex)
    {
        Log.v(TAG, "==> getCharacteristicProperties " + characteristicUuid);

        BluetoothGattCharacteristic characteristic = getCharacteristic(serviceUuid, characteristicUuid, instanceIndex);

        return characteristic == null ? 0 : characteristic.getProperties();
    }

    public void readCharacteristic(final String serviceUuid, final String characteristicUuid, final int instanceIndex, final DataReceivedCallback dataReceivedCallback, final RequestCallback callback)
    {
        Log.v(TAG, "==> readCharacteristic " + characteristicUuid);

        BluetoothGattCharacteristic characteristic = getCharacteristic(serviceUuid, characteristicUuid, instanceIndex);

        _client.readCharacteristic(characteristic)
            .with(dataReceivedCallback)
            .done(callback).fail(callback).invalid(callback)
            .enqueue();
    }

    public void writeCharacteristic(final String serviceUuid, final String characteristicUuid, final int instanceIndex, final byte[] data, boolean withoutResponse, final RequestCallback callback)
    {
        Log.v(TAG, "==> writeCharacteristic " + characteristicUuid);

        BluetoothGattCharacteristic characteristic = getCharacteristic(serviceUuid, characteristicUuid, instanceIndex);
        int writeType = withoutResponse ? BluetoothGattCharacteristic.WRITE_TYPE_NO_RESPONSE : BluetoothGattCharacteristic.WRITE_TYPE_DEFAULT;

        _client.writeCharacteristic(characteristic, data, writeType)
            .done(callback).fail(callback).invalid(callback)
            .enqueue();
    }

    public void subscribeCharacteristic(final String serviceUuid, final String characteristicUuid, final int instanceIndex, final DataReceivedCallback dataReceivedCallback, final RequestCallback callback)
    {
        Log.v(TAG, "==> subscribeCharacteristic" + characteristicUuid);

        BluetoothGattCharacteristic characteristic = getCharacteristic(serviceUuid, characteristicUuid, instanceIndex);

        // Subscribe to notifications
        _client.setNotificationCallback(characteristic).with(dataReceivedCallback);

        // And turn them on
        _client.enableNotifications(characteristic)
            .done(callback).fail(callback).invalid(callback)
            .enqueue();
    }

    public void unsubscribeCharacteristic(final String serviceUuid, final String characteristicUuid, final int instanceIndex, final RequestCallback callback)
    {
        Log.v(TAG, "==> unsubscribeCharacteristic" + characteristicUuid);

        BluetoothGattCharacteristic characteristic = getCharacteristic(serviceUuid, characteristicUuid, instanceIndex);

        // Unsubscribe from notifications
        _client.removeNotificationCallback(characteristic);

        // And turn them of
        _client.disableNotifications(characteristic)
            .done(callback).fail(callback).invalid(callback)
            .enqueue();
    }

    private BluetoothGattService getService(final String serviceUuid)
    {
        return _client.getService(UUID.fromString(serviceUuid));
    }

    private BluetoothGattCharacteristic getCharacteristic(final String serviceUuid, final String characteristicUuid, final int instanceIndex)
    {
        BluetoothGattService service = getService(serviceUuid);
        if (service != null)
        {
            List<BluetoothGattCharacteristic> characteristics = service.getCharacteristics();
            if (characteristics != null)
            {
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
