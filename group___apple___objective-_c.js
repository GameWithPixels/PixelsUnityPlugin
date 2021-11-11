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
    [ "SGBlePeripheralQueue", "interface_s_g_ble_peripheral_queue.html", [
      [ "cancelQueue", "interface_s_g_ble_peripheral_queue.html#a9a0baf1d416f53dc6cb7e8dd41e361e7", null ],
      [ "initWithPeripheral:centralManagerDelegate:connectionEventHandler:", "interface_s_g_ble_peripheral_queue.html#a29ea776e83a8868246bbf65d72ce68c6", null ],
      [ "queueConnectWithServices:completionHandler:", "interface_s_g_ble_peripheral_queue.html#affd377e8dea6ea8d2ab04c862c683318", null ],
      [ "queueDisconnect:", "interface_s_g_ble_peripheral_queue.html#afdb76bdb397216d833b0f3c98003aa8b", null ],
      [ "queueReadRssi:", "interface_s_g_ble_peripheral_queue.html#a478edf978c231ac2867d8fda6b17fb01", null ],
      [ "queueReadValueForCharacteristic:valueReadHandler:", "interface_s_g_ble_peripheral_queue.html#ac71480119a984ce1f0212154ab0ed6d5", null ],
      [ "queueSetNotifyValueForCharacteristic:valueChangedHandler:completionHandler:", "interface_s_g_ble_peripheral_queue.html#a9c0cceb24f18c7b25a678d8ff61c67d6", null ],
      [ "queueWriteValue:forCharacteristic:type:completionHandler:", "interface_s_g_ble_peripheral_queue.html#af1a0c7686bf7ce15551bb82c5a2cc45d", null ],
      [ "isConnected", "interface_s_g_ble_peripheral_queue.html#ac0b7399dde3713c7bfeab18646325e35", null ],
      [ "peripheral", "interface_s_g_ble_peripheral_queue.html#ade54a6860e9ccc547723be10a80067fa", null ],
      [ "rssi", "interface_s_g_ble_peripheral_queue.html#a7f8766b8a19cb02eada08c082fb5660b", null ]
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