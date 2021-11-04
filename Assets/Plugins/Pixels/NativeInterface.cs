using System;
using System.Collections.Generic;
using System.Linq;
using Systemic.Unity.BluetoothLE.Internal;

namespace Systemic.Unity.BluetoothLE
{
    /// <summary>
    /// Opaque and read only class storing the native peripheral handle as used
    /// by the platform specific <see cref="INativeInterfaceImpl"/> implementation.
    /// </summary>
    public struct NativePeripheralHandle
    {
        /// <summary>
        /// Initializes an instance with the given native peripheral handle.
        /// </summary>
        /// <param name="client"></param>
        internal NativePeripheralHandle(INativePeripheralHandleImpl client) => NativePeripheral = client;

        /// <summary>
        /// The native peripheral handle.
        /// </summary>
        internal INativePeripheralHandleImpl NativePeripheral { get; }

        /// <summary>
        /// Indicates whether the peripheral handle is valid.
        /// </summary>
        public bool IsValid => NativePeripheral?.IsValid ?? false;
    }

    /// <summary>
    /// A static class that abstracts each platform specific BLE support and offers a unified interface
    /// to the Unity programmer.
    /// 
    /// Each platform (Windows, iOS, Android) has specific APIs for managing Bluetooth Low Energy (BLE)
    /// peripherals and requires using their native language (respectively C++, Objective-C, Java)
    /// through Unity plugins.
    ///
    /// This static class selects the appropriate native implementation at runtime based on the platform
    /// it is running on. It abstracts away the marshaling specificities of the different platforms.
    ///
    /// Each native implementation wraps the platform specific BLE APIs around a unified architecture
    /// so they can be used in a similar manner. However differences, sometimes subtle, will always exist
    /// between those implementations.
    ///
    /// See the <see cref="Central"/> class for a higher level access to BLE peripherals.
    /// </summary>
    /// <remarks>
    /// In this context, the word "native" refers to the platform specific code and data for managing
    /// BLE peripherals.
    /// </remarks>
    public static class NativeInterface
    {
        #region The underlying INativeInterfaceImpl implementation

        static INativeInterfaceImpl _impl =
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            new Internal.Windows.WinRTNativeInterfaceImpl();
#elif UNITY_EDITOR_OSX || UNITY_IOS || UNITY_STANDALONE_OSX
            new Internal.Apple.AppleNativeInterfaceImpl();
#elif UNITY_ANDROID
            new Internal.Android.AndroidNativeInterfaceImpl();
#else
            null;
#endif
        #endregion

        // Indicate if we are successfully initialized
        static bool _isInitialized;

        /// <summary>
        /// The lowest Maximum Transmission Unit (MTU) value allowed by the BLE standard.
        /// </summary>
        public const int MinMtu = 23;

        /// <summary>
        /// The highest Maximum Transmission Unit (MTU) value allowed by the BLE standard.
        /// </summary>
        public const int MaxMtu = 517;

        /*! \name Static class life cycle */
        //! @{

        /// <summary>
        /// Initializes the underlying platform native implementation.
        /// </summary>
        /// <param name="onBluetoothEvent"></param>
        /// <returns></returns>
        public static bool Initialize(NativeBluetoothEventHandler onBluetoothEvent)
        {
            if (onBluetoothEvent == null) throw new ArgumentNullException(nameof(onBluetoothEvent));

            return _isInitialized = _impl.Initialize(onBluetoothEvent);
        }

        /// <summary>
        /// Shuts down the underlying platform native implementation.
        /// </summary>
        public static void Shutdown()
        {
            _isInitialized = false;
            _impl.Shutdown();
        }

        //! @}
        /*! \name Peripherals scanning */
        //! @{

        /// <summary>
        /// Start scanning for BLE peripherals. If there is already a scan running, it will be replaced by this one.
        /// 
        /// Specifying one more service required for the peripherals will save battery on mobile devices.
        /// </summary>
        /// <param name="requiredServices">List of services that the peripheral should advertise, may be empty or null.</param>
        /// <param name="onScannedPeripheral">Called every time an advertisement packet with the required services is received.</param>
        /// <returns></returns>
        public static bool StartScan(IEnumerable<Guid> requiredServices, Action<ScannedPeripheral> onScannedPeripheral)
        {
            if (onScannedPeripheral == null) throw new ArgumentNullException(nameof(onScannedPeripheral));

            SanityCheck();

            return _impl.StartScan(UuidsToString(requiredServices),
                (device, advData) => onScannedPeripheral(new ScannedPeripheral(device, advData)));
        }

        /// <summary>
        /// Stops any on-going BLE scan.
        /// </summary>
        public static void StopScan()
        {
            _impl.StopScan();
        }

        //! @}
        /*! \name Peripherals life cycle */
        //! @{

        /// <summary>
        /// Requests the native implementation to create an object for the BLE peripheral at the given address.
        ///
        /// This method doesn't initiate a connection.
        /// </summary>
        /// <param name="bluetoothAddress">The BLE peripheral address.</param>
        /// <param name="onConnectionEventChanged">Invoked when the peripheral connection state changes.</param>
        /// <returns>
        /// Handle to the native object for the BLE peripheral.
        /// Returns <c>null</c> if an object was already returned for this peripheral, and not yet released.
        /// </returns>
        public static NativePeripheralHandle CreatePeripheral(ulong bluetoothAddress, NativeConnectionEventHandler onConnectionEventChanged)
        {
            if (bluetoothAddress == 0) throw new ArgumentException("Empty bluetooth address", nameof(bluetoothAddress));
            if (onConnectionEventChanged == null) throw new ArgumentNullException(nameof(onConnectionEventChanged));

            SanityCheck();

            return new NativePeripheralHandle(
                _impl.CreatePeripheral(bluetoothAddress, onConnectionEventChanged));
        }

        /// <summary>
        /// Requests the native implementation to create an object for the BLE peripheral associated
        /// with the given advertisement data passed with the <paramref name="scannedPeripheral"/> parameter.
        ///
        /// This method doesn't initiate a connection.
        /// </summary>
        /// <param name="scannedPeripheral">Some advertisement data for the BLE peripheral.</param>
        /// <param name="onConnectionEvent">Invoked when the peripheral connection state changes.</param>
        /// <returns>
        /// Handle to the native object for the BLE peripheral.
        /// Returns <c>null</c> if an object was already returned for this peripheral, and not yet released.
        /// </returns>
        public static NativePeripheralHandle CreatePeripheral(ScannedPeripheral scannedPeripheral, NativeConnectionEventHandler onConnectionEvent)
        {
            if (scannedPeripheral == null) throw new ArgumentNullException(nameof(scannedPeripheral));
            if (scannedPeripheral.NativeDevice == null) throw new ArgumentException("Invalid ScannedPeripheral", nameof(scannedPeripheral));
            if (onConnectionEvent == null) throw new ArgumentNullException(nameof(onConnectionEvent));

            SanityCheck();

            return new NativePeripheralHandle(
                _impl.CreatePeripheral(scannedPeripheral.NativeDevice, onConnectionEvent));
        }

        /// <summary>
        /// Requests the native implementation to release the underlying native object for the BLE peripheral.
        /// </summary>
        /// <param name="nativePeripheralHandle">Handle to the native object for the BLE peripheral.</param>
        public static void ReleasePeripheral(NativePeripheralHandle nativePeripheralHandle)
        {
            SanityCheck();

            if (nativePeripheralHandle.IsValid)
            {
                _impl.ReleasePeripheral(nativePeripheralHandle.NativePeripheral);
            }
        }

        //! @}
        /*! \name Peripheral connection and disconnection */
        //! @{

        /// <summary>
        /// Requests the native implementation to connect to the given peripheral.
        /// </summary>
        /// <param name="nativePeripheralHandle">Handle to the native object for the BLE peripheral.</param>
        /// <param name="requiredServices">List of services that the peripheral should support, may be empty or null.</param>
        /// <param name="autoConnect">Whether the native implementation should attempt to automatically reconnect
        /// after an unexpected disconnection (i.e. not triggered by the software).</param>
        /// <param name="onResult">Invoked when the request has finished (successfully or not).</param>
        public static void ConnectPeripheral(NativePeripheralHandle nativePeripheralHandle, IEnumerable<Guid> requiredServices, bool autoConnect, NativeRequestResultHandler onResult)
        {
            if (!nativePeripheralHandle.IsValid) throw new ArgumentException("Invalid NativePeripheralHandle", nameof(nativePeripheralHandle));
            if (onResult == null) throw new ArgumentNullException(nameof(onResult));

            SanityCheck();

            _impl.ConnectPeripheral(
                nativePeripheralHandle.NativePeripheral,
                UuidsToString(requiredServices),
                autoConnect,
                onResult);
        }

        /// <summary>
        /// Requests the native implementation to disconnect the given peripheral.
        /// </summary>
        /// <param name="nativePeripheralHandle">Handle to the native object for the BLE peripheral.</param>
        /// <param name="onResult">Invoked when the request has finished (successfully or not).</param>
        public static void DisconnectPeripheral(NativePeripheralHandle nativePeripheralHandle, NativeRequestResultHandler onResult)
        {
            if (!nativePeripheralHandle.IsValid) throw new ArgumentException("Invalid NativePeripheralHandle", nameof(nativePeripheralHandle));
            if (onResult == null) throw new ArgumentNullException(nameof(onResult));

            SanityCheck();

            _impl.DisconnectPeripheral(nativePeripheralHandle.NativePeripheral, onResult);
        }

        //! @}
        /*! \name Peripheral operations
         *  Only valid once a peripheral is connected. */
        //! @{

        /// <summary>
        /// Returns the name of the given peripheral.
        /// </summary>
        /// <param name="nativePeripheralHandle">Handle to the native object for the BLE peripheral.</param>
        /// <returns>The peripheral name.</returns>
        /// <remarks>The peripheral must be connected.</remarks>
        public static string GetPeripheralName(NativePeripheralHandle nativePeripheralHandle)
        {
            if (!nativePeripheralHandle.IsValid) throw new ArgumentException("Invalid NativePeripheralHandle", nameof(nativePeripheralHandle));

            SanityCheck();

            return _impl.GetPeripheralName(nativePeripheralHandle.NativePeripheral);
        }

        /// <summary>
        /// Returns the MTU for the given peripheral.
        /// </summary>
        /// <param name="nativePeripheralHandle">Handle to the native object for the BLE peripheral.</param>
        /// <returns>The peripheral MTU.</returns>
        /// <remarks>The peripheral must be connected.</remarks>
        public static int GetPeripheralMtu(NativePeripheralHandle nativePeripheralHandle)
        {
            if (!nativePeripheralHandle.IsValid) throw new ArgumentException("Invalid NativePeripheralHandle", nameof(nativePeripheralHandle));

            SanityCheck();

            return _impl.GetPeripheralMtu(nativePeripheralHandle.NativePeripheral);
        }

        /// <summary>
        /// Have the native implementation to request the given peripheral to change its MTU (Android only).
        /// </summary>
        /// <param name="nativePeripheralHandle">Handle to the native object for the BLE peripheral.</param>
        /// <param name="mtu">The requested MTU, see <see cref="MinMtu"/> and <see cref="MaxMtu"/> for the legal range of values.</param>
        /// <param name="onMtuResult">Invoked when the request has finished (successfully or not), with the updated MTU value.</param>
        /// <remarks>The peripheral must be connected.</remarks>
        public static void RequestPeripheralMtu(NativePeripheralHandle nativePeripheralHandle, int mtu, NativeValueRequestResultHandler<int> onMtuResult)
        {
            if (!nativePeripheralHandle.IsValid) throw new ArgumentException("Invalid NativePeripheralHandle", nameof(nativePeripheralHandle));
            if ((mtu < MinMtu) || (mtu > MaxMtu)) throw new ArgumentException($"MTU must be between {MinMtu} and {MaxMtu}", nameof(mtu));
            if (onMtuResult == null) throw new ArgumentNullException(nameof(onMtuResult));

            SanityCheck();

            _impl.RequestPeripheralMtu(nativePeripheralHandle.NativePeripheral, mtu, onMtuResult);
        }

        /// <summary>
        /// Reads the current RSSI for the given peripheral.
        /// </summary>
        /// <param name="nativePeripheralHandle">Handle to the native object for the BLE peripheral.</param>
        /// <param name="onRssiResult">Invoked when the request has finished (successfully or not), with the current RSSI value.</param>
        /// <remarks>The peripheral must be connected.</remarks>
        public static void ReadPeripheralRssi(NativePeripheralHandle nativePeripheralHandle, NativeValueRequestResultHandler<int> onRssiResult)
        {
            if (!nativePeripheralHandle.IsValid) throw new ArgumentException("Invalid NativePeripheralHandle", nameof(nativePeripheralHandle));
            if (onRssiResult == null) throw new ArgumentNullException(nameof(onRssiResult));

            SanityCheck();

            _impl.ReadPeripheralRssi(nativePeripheralHandle.NativePeripheral, onRssiResult);
        }

        /// <summary>
        /// Returns the list of discovered services for the given peripheral.
        /// </summary>
        /// <param name="nativePeripheralHandle">Handle to the native object for the BLE peripheral.</param>
        /// <returns>The list of discovered services.</returns>
        /// <remarks>The peripheral must be connected.</remarks>
        public static Guid[] GetPeripheralDiscoveredServices(NativePeripheralHandle nativePeripheralHandle)
        {
            if (!nativePeripheralHandle.IsValid) throw new ArgumentException("Invalid NativePeripheralHandle", nameof(nativePeripheralHandle));

            return StringToUuids(
                _impl.GetPeripheralDiscoveredServices(nativePeripheralHandle.NativePeripheral));
        }

        /// <summary>
        /// Returns the list of discovered characteristics of the given peripheral's service.
        /// 
        /// The same characteristic may be listed several times according to the peripheral's configuration.
        /// </summary>
        /// <param name="nativePeripheralHandle">Handle to the native object for the BLE peripheral.</param>
        /// <param name="serviceUuid">A service UUID.</param>
        /// <returns>The list of discovered characteristics of a service.</returns>
        /// <remarks>The peripheral must be connected.</remarks>
        public static Guid[] GetPeripheralServiceCharacteristics(NativePeripheralHandle nativePeripheralHandle, Guid serviceUuid)
        {
            if (!nativePeripheralHandle.IsValid) throw new ArgumentException("Invalid NativePeripheralHandle", nameof(nativePeripheralHandle));
            if (serviceUuid == Guid.Empty) throw new ArgumentException("Empty service UUID", nameof(serviceUuid));

            return StringToUuids(
                _impl.GetPeripheralServiceCharacteristics(nativePeripheralHandle.NativePeripheral, serviceUuid.ToString()));
        }

        /// <summary>
        /// Returns the BLE properties of the specified service's characteristic for the given peripheral.
        /// </summary>
        /// <param name="nativePeripheralHandle">Handle to the native object for the BLE peripheral.</param>
        /// <param name="serviceUuid">A service UUID.</param>
        /// <param name="characteristicUuid">A characteristic UUID.</param>
        /// <param name="instanceIndex">The instance index of the characteristic if listed more than once for the service, default is zero.</param>
        /// <returns>The BLE properties of a service's characteristic.</returns>
        /// <remarks>The peripheral must be connected.</remarks>
        public static CharacteristicProperties GetCharacteristicProperties(NativePeripheralHandle nativePeripheralHandle, Guid serviceUuid, Guid characteristicUuid, uint instanceIndex)
        {
            if (!nativePeripheralHandle.IsValid) throw new ArgumentException("Invalid NativePeripheralHandle", nameof(nativePeripheralHandle));
            if (serviceUuid == Guid.Empty) throw new ArgumentException("Empty service UUID", nameof(serviceUuid));
            if (characteristicUuid == Guid.Empty) throw new ArgumentException("Empty characteristic UUID", nameof(characteristicUuid));

            SanityCheck();

            return _impl.GetCharacteristicProperties(
                nativePeripheralHandle.NativePeripheral,
                serviceUuid.ToString(),
                characteristicUuid.ToString(),
                instanceIndex);
        }

        /// <summary>
        /// Requests the native implementation to read the value of the specified service's characteristic for the given peripheral.
        /// </summary>
        /// <param name="nativePeripheralHandle">Handle to the native object for the BLE peripheral.</param>
        /// <param name="serviceUuid">A service UUID.</param>
        /// <param name="characteristicUuid">A characteristic UUID.</param>
        /// <param name="instanceIndex">The instance index of the characteristic if listed more than once for the service, default is zero.</param>
        /// <param name="onResult">Invoked when the request has finished (successfully or not) and with the characteristic's current value on success.</param>
        /// <remarks>The peripheral must be connected.</remarks>
        public static void ReadCharacteristic(NativePeripheralHandle nativePeripheralHandle, Guid serviceUuid, Guid characteristicUuid, uint instanceIndex, NativeValueRequestResultHandler<byte[]> onResult)
        {
            if (!nativePeripheralHandle.IsValid) throw new ArgumentException("Invalid NativePeripheralHandle", nameof(nativePeripheralHandle));
            if (serviceUuid == Guid.Empty) throw new ArgumentException("Empty service UUID", nameof(serviceUuid));
            if (characteristicUuid == Guid.Empty) throw new ArgumentException("Empty characteristic UUID", nameof(characteristicUuid));
            if (onResult == null) throw new ArgumentNullException(nameof(onResult));

            SanityCheck();

            //TODO _impl.ReadCharacteristic(nativePeripheralHandle.NativePeripheral, serviceUuid.ToString(), characteristicUuid.ToString(), instanceIndex, onValueChanged, onResult);
        }

        /// <summary>
        /// Requests the native implementation to write to the specified service's characteristic for the given peripheral.
        /// </summary>
        /// <param name="nativePeripheralHandle">Handle to the native object for the BLE peripheral.</param>
        /// <param name="serviceUuid">A service UUID.</param>
        /// <param name="characteristicUuid">A characteristic UUID.</param>
        /// <param name="instanceIndex">The instance index of the characteristic if listed more than once for the service, default is zero.</param>
        /// <param name="data">The data to write to the characteristic.</param>
        /// <param name="withoutResponse">Whether to wait for the peripheral to respond.</param>
        /// <param name="onResult">Invoked when the request has finished (successfully or not).</param>
        /// <remarks>The peripheral must be connected.</remarks>
        public static void WriteCharacteristic(NativePeripheralHandle nativePeripheralHandle, Guid serviceUuid, Guid characteristicUuid, uint instanceIndex, byte[] data, bool withoutResponse, NativeRequestResultHandler onResult)
        {
            if (!nativePeripheralHandle.IsValid) throw new ArgumentException("Invalid NativePeripheralHandle", nameof(nativePeripheralHandle));
            if (serviceUuid == Guid.Empty) throw new ArgumentException("Empty service UUID", nameof(serviceUuid));
            if (characteristicUuid == Guid.Empty) throw new ArgumentException("Empty characteristic UUID", nameof(characteristicUuid));
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Length == 0) throw new ArgumentException("Empty data set", nameof(data));
            if (onResult == null) throw new ArgumentNullException(nameof(onResult));

            SanityCheck();

            _impl.WriteCharacteristic(
                nativePeripheralHandle.NativePeripheral,
                serviceUuid.ToString(),
                characteristicUuid.ToString(),
                instanceIndex,
                data,
                withoutResponse,
                onResult);
        }

        /// <summary>
        /// Requests the native implementation to subscribe for value changes of the specified service's characteristic for the given peripheral.
        /// </summary>
        /// <param name="nativePeripheralHandle">Handle to the native object for the BLE peripheral.</param>
        /// <param name="serviceUuid">A service UUID.</param>
        /// <param name="characteristicUuid">A characteristic UUID.</param>
        /// <param name="instanceIndex">The instance index of the characteristic if listed more than once for the service, default is zero.</param>
        /// <param name="onValueChanged">The callback to be invoked when the characteristic value changes.</param>
        /// <param name="onResult">Invoked when the request has finished (successfully or not).</param>
        /// <remarks>The peripheral must be connected.</remarks>
        public static void SubscribeCharacteristic(NativePeripheralHandle nativePeripheralHandle, Guid serviceUuid, Guid characteristicUuid, uint instanceIndex, NativeValueRequestResultHandler<byte[]> onValueChanged, NativeRequestResultHandler onResult)
        {
            if (!nativePeripheralHandle.IsValid) throw new ArgumentException("Invalid NativePeripheralHandle", nameof(nativePeripheralHandle));
            if (serviceUuid == Guid.Empty) throw new ArgumentException("Empty service UUID", nameof(serviceUuid));
            if (characteristicUuid == Guid.Empty) throw new ArgumentException("Empty characteristic UUID", nameof(characteristicUuid));
            if (onValueChanged == null) throw new ArgumentNullException(nameof(onValueChanged));
            if (onResult == null) throw new ArgumentNullException(nameof(onResult));

            SanityCheck();

            _impl.SubscribeCharacteristic(
                nativePeripheralHandle.NativePeripheral,
                serviceUuid.ToString(),
                characteristicUuid.ToString(),
                instanceIndex,
                onValueChanged,
                onResult);
        }

        /// <summary>
        /// Requests the native implementation to unsubscribe from the specified service's characteristic for the given peripheral.
        /// </summary>
        /// <param name="nativePeripheralHandle">Handle to the native object for the BLE peripheral.</param>
        /// <param name="serviceUuid">A service UUID.</param>
        /// <param name="characteristicUuid">A characteristic UUID.</param>
        /// <param name="instanceIndex">The instance index of the characteristic if listed more than once for the service, default is zero.</param>
        /// <param name="onResult">Invoked when the request has finished (successfully or not).</param>
        /// <remarks>The peripheral must be connected.</remarks>
        public static void UnsubscribeCharacteristic(NativePeripheralHandle nativePeripheralHandle, Guid serviceUuid, Guid characteristicUuid, uint instanceIndex, NativeRequestResultHandler onResult)
        {
            if (!nativePeripheralHandle.IsValid) throw new ArgumentException("Invalid NativePeripheralHandle", nameof(nativePeripheralHandle));
            if (serviceUuid == Guid.Empty) throw new ArgumentException("Empty service UUID", nameof(serviceUuid));
            if (characteristicUuid == Guid.Empty) throw new ArgumentException("Empty characteristic UUID", nameof(characteristicUuid));
            if (onResult == null) throw new ArgumentNullException(nameof(onResult));

            SanityCheck();

            _impl.UnsubscribeCharacteristic(
                nativePeripheralHandle.NativePeripheral,
                serviceUuid.ToString(),
                characteristicUuid.ToString(),
                instanceIndex,
                onResult);
        }

        //! @}

        // Check the static class is a valid state to access BLE peripherals
        private static void SanityCheck()
        {
            if (_impl == null) throw new InvalidOperationException("Platform not supported: " + UnityEngine.Application.platform);
            if (!_isInitialized) throw new InvalidOperationException($"{nameof(NativeInterface)} not initialized");
        }

        // Converts a list of UUIDs to a string representation as expected by the native implementation
        private static string UuidsToString(IEnumerable<Guid> uuids)
        {
            string str = null;
            if (uuids != null)
            {
                str = string.Join(",", uuids.Select(s => s.ToString()));
                if (str.Length == 0)
                {
                    str = null;
                }
            }
            return str;
        }

        // The reverse of UuidsToString
        private static Guid[] StringToUuids(string uuids)
        {
            return uuids?.Split(',').Select(BleUuid.StringToGuid).ToArray() ?? Array.Empty<Guid>();
        }
    }
}
