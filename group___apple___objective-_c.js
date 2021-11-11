var group___apple___objective__c =
[
    [ "SGBleCentralManagerDelegate", "interface_s_g_ble_central_manager_delegate.html", [
      [ "clearPeripherals", "interface_s_g_ble_central_manager_delegate.html#aa93bf7d3373eb28ddc76ff6b8dbfa8a4", null ],
      [ "initWithStateUpdateHandler:", "interface_s_g_ble_central_manager_delegate.html#aebf051d3a3146ac4467365eadcf86fba", null ],
      [ "peripheralForIdentifier:", "interface_s_g_ble_central_manager_delegate.html#a061b77a0e0f212cc22198abc2965d295", null ],
      [ "setConnectionEventHandler:forPeripheral:", "interface_s_g_ble_central_manager_delegate.html#a77cb474294a0bb1171c1ec1efb05bc15", null ],
      [ "centralManager", "interface_s_g_ble_central_manager_delegate.html#acb768dc4f13c71dffac2d43fef148d7d", null ],
      [ "isBluetoothOn", "interface_s_g_ble_central_manager_delegate.html#af51b0acad2c4cd30206338d8414139dd", null ],
      [ "peripheralDiscoveryHandler", "interface_s_g_ble_central_manager_delegate.html#a1b850ef9c7e9a982ec6567bcfdd9c830", null ],
      [ "peripherals", "interface_s_g_ble_central_manager_delegate.html#a772237c71b7bffe478488a1a47676df5", null ]
    ] ],
    [ "SGBlePeripheral", "interface_s_g_ble_peripheral.html", [
      [ "cancelQueue", "interface_s_g_ble_peripheral.html#a48223a85a31f38db8d1f87bfdbe87f06", null ],
      [ "initWithPeripheral:centralManagerDelegate:connectionEventHandler:", "interface_s_g_ble_peripheral.html#a180b6a6d517204d23a9448c2554536f6", null ],
      [ "queueConnectWithServices:completionHandler:", "interface_s_g_ble_peripheral.html#ad3d02e8c13d1ddf68d24b3c5393bc40b", null ],
      [ "queueDisconnect:", "interface_s_g_ble_peripheral.html#a3990cb4503452b3c77f7b5ef9a83cd31", null ],
      [ "queueReadRssi:", "interface_s_g_ble_peripheral.html#ad8ee3977c2bd518a7894e924f5481bbd", null ],
      [ "queueReadValueForCharacteristic:valueReadHandler:", "interface_s_g_ble_peripheral.html#ae321bff8b3cfdc75dac9aa5cb258b9b6", null ],
      [ "queueSetNotifyValueForCharacteristic:valueChangedHandler:completionHandler:", "interface_s_g_ble_peripheral.html#a0f125e4d3ce68588f7b6624f9a01552a", null ],
      [ "queueWriteValue:forCharacteristic:type:completionHandler:", "interface_s_g_ble_peripheral.html#a773c50ea682f7091bee1c58a3b1a366f", null ],
      [ "identifier", "interface_s_g_ble_peripheral.html#a7cc7a643e783d61a5981598fede77809", null ],
      [ "isConnected", "interface_s_g_ble_peripheral.html#a2e65f1bf44e44ef09e4e175f21ea8b7a", null ],
      [ "rssi", "interface_s_g_ble_peripheral.html#a8903b2ffd6d462044e0c2bd80ea2d3de", null ]
    ] ],
    [ "SGBleConnectionEventHandler", "group___apple___objective-_c.html#gae5d9676c0c2204f460acd039b6da9ba6", null ],
    [ "SGBlePeripheralDiscoveryHandler", "group___apple___objective-_c.html#ga662b4b1ab3054584449b801ddb83d3fd", [
      [ "SGBleConnectionEventConnecting", "group___apple___objective-_c.html#gga99fb83031ce9923c84392b4e92f956b5a673eb38d75dfd0ed1ecebbc9ccd8517c", null ],
      [ "SGBleConnectionEventConnected", "group___apple___objective-_c.html#gga99fb83031ce9923c84392b4e92f956b5aae6c83567c94e9ff792cdf428c62678e", null ],
      [ "SGBleConnectionEventFailedToConnect", "group___apple___objective-_c.html#gga99fb83031ce9923c84392b4e92f956b5a916e62d0298a5715f0fa50e3730a8491", null ],
      [ "SGBleConnectionEventReady", "group___apple___objective-_c.html#gga99fb83031ce9923c84392b4e92f956b5acec85e9e9c63ca56e39300ab1ed5629c", null ],
      [ "SGBleConnectionEventDisconnecting", "group___apple___objective-_c.html#gga99fb83031ce9923c84392b4e92f956b5a6ff50324c1b41a252f7c54b3307580e3", null ],
      [ "SGBleConnectionEventDisconnected", "group___apple___objective-_c.html#gga99fb83031ce9923c84392b4e92f956b5a58536bb1bc9a399939941ba98dccfe12", null ],
      [ "SGBleConnectionEventReasonUnknown", "group___apple___objective-_c.html#ggabc6126af1d45847bc59afa0aa3216b04a96aafed235ff9f6f276e06d9d06e54f2", null ],
      [ "SGBleConnectionEventReasonSuccess", "group___apple___objective-_c.html#ggabc6126af1d45847bc59afa0aa3216b04a6e3580469f3f3ad504d3d745a5026563", null ],
      [ "SGBleConnectionEventReasonCanceled", "group___apple___objective-_c.html#ggabc6126af1d45847bc59afa0aa3216b04affeb4734877e11a6fdac50ee1fda3832", null ],
      [ "SGBleConnectionEventReasonNotSupported", "group___apple___objective-_c.html#ggabc6126af1d45847bc59afa0aa3216b04a70a5b369e1950d5840b7b53e0a8761c0", null ],
      [ "SGBleConnectionEventReasonTimeout", "group___apple___objective-_c.html#ggabc6126af1d45847bc59afa0aa3216b04afcd8119d12c1d314431c71a91b394cb7", null ],
      [ "SGBleConnectionEventReasonLinkLoss", "group___apple___objective-_c.html#ggabc6126af1d45847bc59afa0aa3216b04a2ef464fec9871252d620bc01d2acc154", null ],
      [ "SGBleConnectionEventReasonAdpaterOff", "group___apple___objective-_c.html#ggabc6126af1d45847bc59afa0aa3216b04a37d44c9e93543a5c8874865e8dcd90b5", null ]
    ] ]
];