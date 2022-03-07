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
    [ "SGBleRequest", "interface_s_g_ble_request.html", [
      [ "execute", "interface_s_g_ble_request.html#a30b72514b75ec994b08fb55cffcd3fde", null ],
      [ "initWithRequestType:executeHandler:completionHandler:", "interface_s_g_ble_request.html#a03c9497b76856972a7250d35ce2602a4", null ],
      [ "notifyResult:", "interface_s_g_ble_request.html#a03f3226508f114d1e9495025cc61897e", null ],
      [ "requestTypeToString:", "interface_s_g_ble_request.html#a66f43c95ed12a4ded19f6ff055150b42", null ],
      [ "type", "interface_s_g_ble_request.html#af4ae5924d7b0ce5c2ff259780f37079e", null ]
    ] ],
    [ "SGBleConnectionEventHandler", "group___apple___objective-_c.html#gae5d9676c0c2204f460acd039b6da9ba6", null ],
    [ "SGBlePeripheralDiscoveryHandler", "group___apple___objective-_c.html#ga662b4b1ab3054584449b801ddb83d3fd", null ],
    [ "SGBleRequestCompletionHandler", "group___apple___objective-_c.html#ga9c854b44e91a64cc5d54bd3534802f4f", null ],
    [ "SGBleRequestExecuteHandler", "group___apple___objective-_c.html#ga74898fe3d2584f11e2d373a429d3fa46", [
      [ "SGBleConnectionEventConnecting", "group___apple___objective-_c.html#ggadf764cbdea00d65edcd07bb9953ad2b7a673eb38d75dfd0ed1ecebbc9ccd8517c", null ],
      [ "SGBleConnectionEventConnected", "group___apple___objective-_c.html#ggadf764cbdea00d65edcd07bb9953ad2b7aae6c83567c94e9ff792cdf428c62678e", null ],
      [ "SGBleConnectionEventFailedToConnect", "group___apple___objective-_c.html#ggadf764cbdea00d65edcd07bb9953ad2b7a916e62d0298a5715f0fa50e3730a8491", null ],
      [ "SGBleConnectionEventReady", "group___apple___objective-_c.html#ggadf764cbdea00d65edcd07bb9953ad2b7acec85e9e9c63ca56e39300ab1ed5629c", null ],
      [ "SGBleConnectionEventDisconnecting", "group___apple___objective-_c.html#ggadf764cbdea00d65edcd07bb9953ad2b7a6ff50324c1b41a252f7c54b3307580e3", null ],
      [ "SGBleConnectionEventDisconnected", "group___apple___objective-_c.html#ggadf764cbdea00d65edcd07bb9953ad2b7a58536bb1bc9a399939941ba98dccfe12", null ],
      [ "SGBleConnectionEventReasonUnknown", "group___apple___objective-_c.html#gga99fb83031ce9923c84392b4e92f956b5a96aafed235ff9f6f276e06d9d06e54f2", null ],
      [ "SGBleConnectionEventReasonSuccess", "group___apple___objective-_c.html#gga99fb83031ce9923c84392b4e92f956b5a6e3580469f3f3ad504d3d745a5026563", null ],
      [ "SGBleConnectionEventReasonCanceled", "group___apple___objective-_c.html#gga99fb83031ce9923c84392b4e92f956b5affeb4734877e11a6fdac50ee1fda3832", null ],
      [ "SGBleConnectionEventReasonNotSupported", "group___apple___objective-_c.html#gga99fb83031ce9923c84392b4e92f956b5a70a5b369e1950d5840b7b53e0a8761c0", null ],
      [ "SGBleConnectionEventReasonTimeout", "group___apple___objective-_c.html#gga99fb83031ce9923c84392b4e92f956b5afcd8119d12c1d314431c71a91b394cb7", null ],
      [ "SGBleConnectionEventReasonLinkLoss", "group___apple___objective-_c.html#gga99fb83031ce9923c84392b4e92f956b5a2ef464fec9871252d620bc01d2acc154", null ],
      [ "SGBleConnectionEventReasonAdapterOff", "group___apple___objective-_c.html#gga99fb83031ce9923c84392b4e92f956b5a13ce53c963b9ba520d3d6943193b830b", null ],
      [ "SGBlePeripheralRequestErrorDisconnected", "group___apple___objective-_c.html#ggabc6126af1d45847bc59afa0aa3216b04a66e8a8f6a1c0d71da2ab7fb6c7a07738", null ],
      [ "SGBlePeripheralRequestErrorInvalidCall", "group___apple___objective-_c.html#ggabc6126af1d45847bc59afa0aa3216b04ac6ef78dc7e6fac319efdffa555922cf1", null ],
      [ "SGBlePeripheralRequestErrorInvalidParameters", "group___apple___objective-_c.html#ggabc6126af1d45847bc59afa0aa3216b04a9c7e9ba04c6e9891ca1ffe5b924e35a6", null ],
      [ "SGBlePeripheralRequestErrorCanceled", "group___apple___objective-_c.html#ggabc6126af1d45847bc59afa0aa3216b04a8570f2f50fccde43a929ce2c33fd1ce8", null ]
    ] ],
    [ "SGBleCanceledError", "group___apple___objective-_c.html#ga3eb718af999217b6c58cff156be0f9a3", null ],
    [ "SGBleDisconnectedError", "group___apple___objective-_c.html#ga6abb2f68f175b07ef5634407929eeb26", null ],
    [ "SGBleInvalidCallError", "group___apple___objective-_c.html#ga2b17657e68a99476c0c7215a35339a35", null ],
    [ "SGBleInvalidParametersError", "group___apple___objective-_c.html#gafce456ed50953cbc236ffd60662d873d", null ]
];