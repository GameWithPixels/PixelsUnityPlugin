using System;
using UnityEngine;

namespace Systemic.Pixels.Unity.BluetoothLE.Internal.Android
{
    internal static class JavaUtils
    {
        public static sbyte[] ToSignedArray(byte[] data)
        {
            var signedArray = new sbyte[data.Length];
            Buffer.BlockCopy(data, 0, signedArray, 0, data.Length);
            return signedArray;
        }

        public static byte[] ToDotNetArray(AndroidJavaObject javaArray)
        {
            var signedArray = AndroidJNI.FromSByteArray(javaArray.GetRawObject());
            var data = new byte[signedArray.Length];
            Buffer.BlockCopy(signedArray, 0, data, 0, signedArray.Length);
            return data;
        }
    }
}
