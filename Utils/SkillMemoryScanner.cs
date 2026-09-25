using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace _4RTools.Utils
{
    internal class SkillMemoryScanner
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORY_BASIC_INFORMATION
        {
            public IntPtr BaseAddress;
            public IntPtr AllocationBase;
            public uint   AllocationProtect;
            public IntPtr RegionSize;
            public uint   State;
            public uint   Protect;
            public uint   Type;
        }

        [DllImport("kernel32.dll")]
        private static extern int VirtualQueryEx(
            IntPtr hProcess, IntPtr lpAddress,
            out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

        [DllImport("kernel32.dll")]
        private static extern bool ReadProcessMemory(
            IntPtr hProcess, IntPtr lpBaseAddress,
            [Out] byte[] lpBuffer, int nSize, out int lpNumberOfBytesRead);

        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);

        private const int  PROCESS_VM_READ        = 0x0010;
        private const int  PROCESS_QUERY_INFORMATION = 0x0400;
        private const uint MEM_COMMIT             = 0x1000;
        private const uint MEM_PRIVATE            = 0x20000;
        private const uint PAGE_GUARD             = 0x100;
        private const uint PAGE_NOACCESS          = 0x01;
        private const long MAX_REGION_BYTES       = 32 * 1024 * 1024; // 32 MB

        private IntPtr _handle = IntPtr.Zero;

        public bool Attach(int pid)
        {
            if (_handle != IntPtr.Zero) CloseHandle(_handle);
            _handle = OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION, false, pid);
            return _handle != IntPtr.Zero;
        }

        public void Detach()
        {
            if (_handle == IntPtr.Zero) return;
            CloseHandle(_handle);
            _handle = IntPtr.Zero;
        }

        // Full scan: walks all private committed regions, returns addresses whose float is in [minVal, maxVal].
        public Dictionary<IntPtr, float> ScanFloatRange(float minVal, float maxVal)
        {
            var results = new Dictionary<IntPtr, float>();
            if (_handle == IntPtr.Zero) return results;

            IntPtr cursor = IntPtr.Zero;
            uint   mbiSz  = (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION));

            while (VirtualQueryEx(_handle, cursor, out var mbi, mbiSz) != 0)
            {
                bool scannable = mbi.State   == MEM_COMMIT
                              && mbi.Type    == MEM_PRIVATE
                              && (mbi.Protect & PAGE_GUARD)    == 0
                              && (mbi.Protect & PAGE_NOACCESS) == 0;

                if (scannable)
                {
                    long size = mbi.RegionSize.ToInt64();
                    if (size > 0 && size <= MAX_REGION_BYTES)
                    {
                        var buf = new byte[size];
                        if (ReadProcessMemory(_handle, mbi.BaseAddress, buf, (int)size, out int read) && read >= 4)
                        {
                            long baseAddr = mbi.BaseAddress.ToInt64();
                            for (int i = 0; i <= read - 4; i += 4)
                            {
                                float v = BitConverter.ToSingle(buf, i);
                                if (!float.IsNaN(v) && !float.IsInfinity(v) && v >= minVal && v <= maxVal)
                                    results[(IntPtr)(baseAddr + i)] = v;
                            }
                        }
                    }
                }

                long next = mbi.BaseAddress.ToInt64() + mbi.RegionSize.ToInt64();
                if (next <= cursor.ToInt64()) break; // overflow guard
                cursor = (IntPtr)next;
            }

            return results;
        }

        // Narrow scan: re-reads only the addresses from a previous snapshot.
        // Keeps entries whose current value is in [newMin, newMax] AND has decreased since the snapshot.
        public Dictionary<IntPtr, float> FilterDecreased(
            Dictionary<IntPtr, float> prev, float newMin, float newMax)
        {
            var results = new Dictionary<IntPtr, float>();
            if (_handle == IntPtr.Zero || prev == null) return results;

            var buf = new byte[4];
            foreach (var kvp in prev)
            {
                if (!ReadProcessMemory(_handle, kvp.Key, buf, 4, out int read) || read != 4)
                    continue;
                float cur = BitConverter.ToSingle(buf, 0);
                if (!float.IsNaN(cur) && cur >= newMin && cur <= newMax && cur < kvp.Value)
                    results[kvp.Key] = cur;
            }
            return results;
        }

        // Reads the float stored at a known address. Returns 0 on failure.
        public float ReadFloat(IntPtr address)
        {
            if (_handle == IntPtr.Zero || address == IntPtr.Zero) return 0f;
            var buf = new byte[4];
            if (!ReadProcessMemory(_handle, address, buf, 4, out int read) || read != 4) return 0f;
            float v = BitConverter.ToSingle(buf, 0);
            return float.IsNaN(v) || float.IsInfinity(v) ? 0f : v;
        }
    }
}
