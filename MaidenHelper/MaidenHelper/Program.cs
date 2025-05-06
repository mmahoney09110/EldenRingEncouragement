using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace EldenRingMemoryReader
{
    internal class Program
    {
        // Import required WinAPI functions
        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

        [DllImport("kernel32.dll")]
        public static extern bool CloseHandle(IntPtr hObject);

        const int PROCESS_VM_READ = 0x0010;
        const int PROCESS_QUERY_INFORMATION = 0x0400;

        static void Main(string[] args)
        {
            // 1. Check if running as Administrator
            if (!IsAdministrator())
            {
                Console.WriteLine("Please run this program as Administrator.");
                return;
            }

            // 2. Check if Elden Ring process is running
            var process = Process.GetProcessesByName("eldenring").FirstOrDefault();
            if (process == null)
            {
                Console.WriteLine("Elden Ring is not running. Launch it in OFFLINE mode (no EAC).");
                return;
            }

            Console.WriteLine($"Found Elden Ring (PID: {process.Id})");

            // 3. Open process
            IntPtr hProcess = OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION, false, process.Id);
            if (hProcess == IntPtr.Zero)
            {
                Console.WriteLine("Failed to open Elden Ring process. Are you running as admin?");
                return;
            }

            // 4. Try to read known address (e.g., HP pointer)
            IntPtr moduleBase = GetModuleBaseAddress(process, "eldenring.exe");
            if (moduleBase == IntPtr.Zero)
            {
                Console.WriteLine("Failed to get module base address.");
                return;
            }

            // actual offsets fo HP
            int[] offsets = new int[] { 0x10EF8, 0x0, 0x190, 0x0, 0x138 };
            Console.WriteLine($"Module base: 0x{moduleBase.ToString("X")}");
            IntPtr worldChrManAddress = moduleBase + 0x3D65F88;
            Console.WriteLine($"Base pointer address: 0x{worldChrManAddress.ToString("X")}");
            ulong basePtr = ReadPointer64FromMemory(hProcess, worldChrManAddress);
            Console.WriteLine($"Base pointer address contains: 0x{basePtr.ToString("X")}");
            
            ulong currentPtr = (ulong)basePtr;
            foreach (int offset in offsets)
            {
                IntPtr readAddr = (IntPtr)(currentPtr + (uint)offset);
                Console.WriteLine($"Reading pointer at 0x{readAddr.ToString("X")}");

                currentPtr = ReadPointer64FromMemory(hProcess, readAddr);

                if (currentPtr == 0)
                {
                    Console.WriteLine("Pointer chain broken.");
                    break;
                }

                Console.WriteLine($"Next pointer: 0x{currentPtr:X}");
            }

            // Mask to remove sign-extension junk if needed (keep lower 48 bits)
            ulong trimmed = currentPtr & 0x0000000000FFFFFF;
            
            Console.WriteLine($"HP: {trimmed}");

            // 5. Cleanup
            CloseHandle(hProcess);
        }

        static bool IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        static ulong ReadPointer64FromMemory(IntPtr hProcess, IntPtr address)
        {
            byte[] buffer = new byte[8];
            int bytesRead;

            bool success = ReadProcessMemory(hProcess, address, buffer, buffer.Length, out bytesRead);
            if (!success || bytesRead != buffer.Length)
            {
                Console.WriteLine($"Error reading memory at 0x{address.ToString("X")}");
                return 0;
            }

            return BitConverter.ToUInt64(buffer, 0);
        }

        public static IntPtr GetModuleBaseAddress(Process process, string moduleName)
        {
            foreach (ProcessModule module in process.Modules)
            {
                if (module.ModuleName.Equals(moduleName, StringComparison.OrdinalIgnoreCase))
                {
                    return module.BaseAddress;
                }
            }
            return IntPtr.Zero;
        }

    }
}