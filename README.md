# Malware Droppers

The goal of this project is to show a variety of custom malware droppers.

Useful websites:

* [github.com/ivan-sincek/invoker](https://github.com/ivan-sincek/invoker/blob/master/src/Invoker/Invoker/lib/invoker/invoker.cpp)
* [github.com/gentilkiwi/mimikatz](https://github.com/gentilkiwi/mimikatz)
* [elastic.co](https://www.elastic.co/blog/ten-process-injection-techniques-technical-survey-common-and-trending-process)
* [learn.microsoft.com](https://learn.microsoft.com/en-us/windows/win32/debug/pe-format)
* [processhacker.sourceforge.io](https://processhacker.sourceforge.io/doc/index.html)
* [undocumented.ntinternals.net](http://undocumented.ntinternals.net/index.html)
* [pinvoke.net](https://www.pinvoke.net)
* [C++ to C# Converter](https://www.tangiblesoftwaresolutions.com/product_details/cplusplus_to_csharp_converter_details.html) (free edition)

Made for educational purposes. I hope it will help!

## Table of Contents

* [1. C# Process Hollowing](#1-c-process-hollowing)
	* [1.1  Encoder](#11-encoder)

## 1. C# Process Hollowing

Using gzip, XOR, and Base64 to encode [Mimikatz v2.2.0](https://github.com/gentilkiwi/mimikatz/releases/tag/2.2.0-20220919) (64-bit); using process hollowing into C:\\Windows\\System32\\cmd.exe (64-bit) to run it.

Built with Visual Studio Community 2019 v16.11.10 (64-bit), written in C# (.NET Framework v3.5), and tested on Windows 10 Enterprise OS (64-bit).

Check the code in these files:

* [/src/Dropper/Dropper/Payload.cs](https://github.com/ivan-sincek/malicious-dropper/blob/master/src/Dropper/Dropper/Payload.cs) (payload | set your encoded PE string here)
* [/src/Dropper/Dropper/XZip64.cs](https://github.com/ivan-sincek/malicious-dropper/blob/master/src/Dropper/Dropper/XZip64.cs) (decoder)
* [/src/Dropper/Dropper/Program.cs](https://github.com/ivan-sincek/malicious-dropper/blob/master/src/Dropper/Dropper/Program.cs) (main | set your decoding key here)
* [/src/Dropper/Dropper/Process.cs](https://github.com/ivan-sincek/malicious-dropper/blob/master/src/Dropper/Dropper/Process.cs) (process hollowing)

### 1.1 Encoder

```fundamental
Usage: Encoder.exe <file> <key>
```
