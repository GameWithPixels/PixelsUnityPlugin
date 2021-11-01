using UnityEngine;

namespace Systemic.Pixels.Unity.BluetoothLE.Internal.Android
{
	internal sealed class ConnectionObserver : AndroidJavaProxy
	{
		NativePeripheralConnectionEventHandler _connectionEventHandler;

		public ConnectionObserver(NativePeripheralConnectionEventHandler onConnectionEventHandler)
			: base("no.nordicsemi.android.ble.observer.ConnectionObserver")
			=> _connectionEventHandler = onConnectionEventHandler;

		/**
		 * Called when the Android device started connecting to given device.
		 * The {@link #onDeviceConnected(AndroidJavaObject)} will be called when the device is connected,
		 * or {@link #onDeviceFailedToConnect(AndroidJavaObject, int)} if connection will fail.
		 *
		 * @param device the device that got connected.
		 */
		void onDeviceConnecting(AndroidJavaObject device)
        {
			Debug.Log("ConnectionObserver ==> onDeviceConnecting");
			_connectionEventHandler?.Invoke(ConnectionEvent.Connecting, ConnectionEventReason.Success);
		}

		/**
		 * Called when the device has been connected. This does not mean that the application may start
		 * communication. Service discovery will be handled automatically after this call.
		 *
		 * @param device the device that got connected.
		 */
		void onDeviceConnected(AndroidJavaObject device)
        {
			Debug.Log("ConnectionObserver ==> onDeviceConnected");
			_connectionEventHandler?.Invoke(ConnectionEvent.Connected, ConnectionEventReason.Success);
		}

		/**
		 * Called when the device failed to connect.
		 * @param device the device that failed to connect.
		 * @param reason the reason of failure.
		 */
		void onDeviceFailedToConnect(AndroidJavaObject device, int reason)
        {
			Debug.Log($"ConnectionObserver ==> onDeviceFailedToConnect: {(AndroidConnectionEventReason)reason}");
			_connectionEventHandler?.Invoke(ConnectionEvent.FailedToConnect, AndroidNativeInterfaceImpl.ToConnectionEventReason(reason));
		}

		/**
		 * Method called when all initialization requests has been completed.
		 *
		 * @param device the device that get ready.
		 */
		void onDeviceReady(AndroidJavaObject device)
        {
			Debug.Log("ConnectionObserver ==> onDeviceReady");
			_connectionEventHandler?.Invoke(ConnectionEvent.Ready, ConnectionEventReason.Success);
		}

		/**
		 * Called when user initialized disconnection.
		 *
		 * @param device the device that gets disconnecting.
		 */
		void onDeviceDisconnecting(AndroidJavaObject device)
        {
			Debug.Log("ConnectionObserver ==> onDeviceDisconnecting");
			_connectionEventHandler?.Invoke(ConnectionEvent.Disconnecting, ConnectionEventReason.Success);
		}

		/**
		 * Called when the device has disconnected (when the callback returned
		 * {@link BluetoothGattCallback#onConnectionStateChange(BluetoothGatt, int, int)} with state
		 * DISCONNECTED).
		 *
		 * @param device the device that got disconnected.
		 * @param reason of the disconnect (mapped from the status code reported by the GATT
		 *               callback to the library specific status codes).
		 */
		void onDeviceDisconnected(AndroidJavaObject device, int reason)
        {
			Debug.Log($"ConnectionObserver ==> onDeviceDisconnected: {(AndroidConnectionEventReason)reason}");
			_connectionEventHandler?.Invoke(ConnectionEvent.Disconnected, AndroidNativeInterfaceImpl.ToConnectionEventReason(reason));
		}
	}
}
