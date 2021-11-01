package com.systemic.pixels;

import java.lang.StringBuilder;
import java.util.ArrayList;
import java.util.List;

import android.os.ParcelUuid;
import android.util.Log;
import android.bluetooth.BluetoothDevice;

import no.nordicsemi.android.support.v18.scanner.*;

public final class Scanner
{
    public interface ScannerCallback
    {
        public void onScanResult(BluetoothDevice device, String advertisementDataJson);
        public void onScanFailed(String error);
    }

	private static String TAG = "systemic";
    private static ScanCallback _scanCallback;
    private static Object _scanSync = new Object();

    // requiredServicesUuids is a comma separated list of UUIDs, it can be null
    public static void startScan(final String requiredServicesUuids, final ScannerCallback callback)
    {
        Log.v(TAG, "==> startScan");

        if (callback == null)
        {
            throw new IllegalArgumentException("callback is null");
        }

        ScanSettings settings = new ScanSettings.Builder()
            .setLegacy(false) // Default is true for compatibility with older apps, but we all type of advertisements, not just legacy
            .setScanMode(ScanSettings.SCAN_MODE_LOW_LATENCY) // Default is low power which is good for long scans, in our use case we do short scans and we prefer having quick results
            .build(); // Other defaults are great for us

        List<ScanFilter> filters = null;
        if (requiredServicesUuids != null)
        {
            filters = new ArrayList<>();
            for (String uuidStr : requiredServicesUuids.split(","))
            {
                try
                {
                    ParcelUuid uuid = ParcelUuid.fromString(uuidStr);
                    filters.add(new ScanFilter.Builder().setServiceUuid(uuid).build());
                }
                catch (IllegalArgumentException e)
                {
                    throw new IllegalArgumentException("requiredServicesUuids must be either null, an empty string or a comma separated list of UUIDs");
                }
            }
        }

        synchronized (_scanSync)
        {
            // Only one scan at a time
            if (_scanCallback != null)
            {
    	        BluetoothLeScannerCompat.getScanner().stopScan(_scanCallback);
            }

            // Start scan
            _scanCallback = createCallback(callback);
            BluetoothLeScannerCompat.getScanner().startScan(filters, settings, _scanCallback);
        }
    }

	public static void stopScan()
    {
        Log.v(TAG, "==> stopScan");

        synchronized (_scanSync)
        {
            if (_scanCallback != null)
            {
    	        BluetoothLeScannerCompat.getScanner().stopScan(_scanCallback);
                _scanCallback = null;
            }
        }
    }

    private static ScanCallback createCallback(final ScannerCallback callback)
    {
        return new ScanCallback()
        {
            @Override
            public void onScanResult(final int callbackType, final ScanResult result)
            {
                NotifyScanResult(result);
            }

            @Override
            public void onBatchScanResults(final List<ScanResult> results)
            {
                for (ScanResult scan : results)
                {
                    NotifyScanResult(scan);
                }
            }

            @Override
        	public void onScanFailed(final int errorCode)
            {
                callback.onScanFailed(errorToString(errorCode));
            }

            private String errorToString(final int errorCode)
            {
                switch (errorCode)
                {
                    case ScanCallback.SCAN_FAILED_ALREADY_STARTED:
                        return "Already started";
                    case ScanCallback.SCAN_FAILED_APPLICATION_REGISTRATION_FAILED:
                        return "Application registration failed";
                    case ScanCallback.SCAN_FAILED_INTERNAL_ERROR:
                        return "Internal error";
                    case ScanCallback.SCAN_FAILED_FEATURE_UNSUPPORTED:
                        return "Feature unsupported";
                    case ScanCallback.SCAN_FAILED_OUT_OF_HARDWARE_RESOURCES:
                        return "Out of hardware resources";
                }
                return "Unknown error";
            }

            private void NotifyScanResult(final ScanResult scanResult)
            {
                BluetoothDevice device = scanResult.getDevice();
                if (device != null)
                {
                    long address = 0, shift = 0;
                    String[] octets = device.getAddress().split(":");
                    for (int i = octets.length - 1; i >= 0; --i)
                    {
                        address += (long)Integer.parseInt(octets[i], 16) << shift;
                        shift += 8;
                    }

                    StringBuilder sb = new StringBuilder();
                    sb.append("{\"systemId\":\"");
                    sb.append(device.hashCode());
                    sb.append("\",\"address\":\"");
                    sb.append(address);
                    sb.append("\",\"name\":\"");
                    sb.append(device.getName());
                    sb.append("\",\"rssi\":");
                    sb.append(scanResult.getRssi());

                    sb.append(",\"services\":[");

                    ScanRecord scanRecord = scanResult.getScanRecord();
                    if (scanRecord != null)
                    {
                        if (scanRecord.getServiceUuids() != null)
                        {
                            boolean first = true;
                            for (ParcelUuid service : scanRecord.getServiceUuids())
                            {
                                if (!first) sb.append(",");
                                first = false;

                                sb.append("\"");
                                sb.append(service);
                                sb.append("\"");
                            }
                        }
                    }
                    sb.append("]}");

                    callback.onScanResult(device, sb.toString());
                }
            }
        };
    }
}
