using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;

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

        public static byte[] ReadBytesFromMemory(IntPtr hProcess, IntPtr address, int length)
        {
            byte[] buffer = new byte[length];
            ReadProcessMemory(hProcess, address, buffer, buffer.Length, out int bytesRead);
            return buffer;
        }


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
            int[] HPoffsets = new int[] { 0x10EF8, 0x0, 0x190, 0x0, 0x138 };
            int[] GRoffsets = new int[] { 0x08, 0xFF };
            int[] Deathoffsets = new int[] { 0x94 };
            int[] nameOffsets = new int[] { 0x08 };
            int[] levelOffsets = new int[] { 0x08, 0x68 };
            Console.WriteLine($"Module base: 0x{moduleBase.ToString("X")}");
            IntPtr worldChrManAddress = moduleBase + 0x3D65F88;
            IntPtr gameDataMan = moduleBase + 0x3D5DF38;

            Console.WriteLine($"Base HP pointer address: 0x{worldChrManAddress.ToString("X")}");
            Console.WriteLine($"Base GR pointer address: 0x{gameDataMan.ToString("X")}");

            ulong basePtrWCM = ReadPointer64FromMemory(hProcess, worldChrManAddress);
            ulong basePtrGDM = ReadPointer64FromMemory(hProcess, gameDataMan);
            Console.WriteLine($"Base HP pointer address contains: 0x{basePtrWCM.ToString("X")}");
            Console.WriteLine($"Base GR pointer address contains: 0x{basePtrGDM.ToString("X")}");

            ulong currentHPPtr = (ulong)basePtrWCM;
            foreach (int offset in HPoffsets)
            {
                IntPtr readAddr = (IntPtr)(currentHPPtr + (uint)offset);
                //Console.WriteLine($"Reading pointer at 0x{readAddr.ToString("X")}");

                currentHPPtr = ReadPointer64FromMemory(hProcess, readAddr);

                if (currentHPPtr == 0)
                {
                    //Console.WriteLine("Pointer chain broken.");
                    break;
                }

                //Console.WriteLine($"Next pointer: 0x{currentHPPtr:X}");
            }

            // Mask to remove sign-extension junk if needed (keep lower 48 bits)
            ulong trimmed = currentHPPtr & 0x0000000000FFFFFF;
            
            Console.WriteLine($"HP: {trimmed}");

            ulong currentGRPtr = (ulong)basePtrGDM;
            foreach (int offset in GRoffsets)
            {
                IntPtr readAddr = (IntPtr)(currentGRPtr + (uint)offset);
                //Console.WriteLine($"Reading {currentGRPtr.ToString("X")} + {offset.ToString("X")} at 0x{readAddr.ToString("X")}");

                currentGRPtr = ReadPointer64FromMemory(hProcess, readAddr);

                if (currentGRPtr == 0)
                {
                    //Console.WriteLine("Pointer chain broken.");
                    break;
                }

                //Console.WriteLine($"Next pointer: 0x{currentGRPtr:X}");
            }

            // Mask to remove sign-extension junk if needed (keep lower 48 bits)
            ulong trimmedGR = currentGRPtr & 0x000000000000FFFF;

            Console.WriteLine($"Great Rune Active?: {trimmedGR}");

            ulong currentDeathPtr = (ulong)basePtrGDM;
            foreach (int offset in Deathoffsets)
            {
                IntPtr readAddr = (IntPtr)(currentDeathPtr + (uint)offset);
                //Console.WriteLine($"Reading {currentDeathPtr.ToString("X")} + {offset.ToString("X")} at 0x{readAddr.ToString("X")}");

                currentDeathPtr = ReadPointer64FromMemory(hProcess, readAddr);

                if (currentDeathPtr == 0)
                {
                    //Console.WriteLine("Pointer chain broken.");
                    break;
                }

                //Console.WriteLine($"Next pointer: 0x{currentDeathPtr:X}");
            }

            // Mask to remove sign-extension junk if needed (keep lower 48 bits)
            ulong trimmedDeath = currentDeathPtr & 0x0000000000FFFFFF;

            Console.WriteLine($"Death Count: {trimmedDeath}");

            ulong currentNamePtr = (ulong)basePtrGDM;
            foreach (int offset in nameOffsets)
            {
                IntPtr readAddr = (IntPtr)(currentNamePtr + (uint)offset);
                //Console.WriteLine($"Reading {currentNamePtr.ToString("X")} + {offset.ToString("X")} at 0x{readAddr.ToString("X")}");

                currentNamePtr = ReadPointer64FromMemory(hProcess, readAddr);

                if (currentNamePtr == 0)
                {
                    //Console.WriteLine("Pointer chain broken.");
                    break;
                }

                //Console.WriteLine($"Next pointer: 0x{currentDeathPtr:X}");
            }

            // Read name
            int maxStringLength = 64; // characters
            int byteLength = maxStringLength * 2; // UTF-16 = 2 bytes per char

            // Read raw memory starting from currentNamePtr
            byte[] rawStringBytes = ReadBytesFromMemory(hProcess, (IntPtr)(currentNamePtr + 0x9C), byteLength);

            // Look for null terminator (0x00 0x00)
            int end = 0;
            for (int i = 0; i < rawStringBytes.Length - 1; i += 2)
            {
                if (rawStringBytes[i] == 0x00 && rawStringBytes[i + 1] == 0x00)
                {
                    end = i;
                    break;
                }
            }
            if (end == 0) end = byteLength; // fallback

            // Decode as UTF-16
            string finalName = Encoding.Unicode.GetString(rawStringBytes, 0, end);
            Console.WriteLine("Player Name: " + finalName);

            // Read level
            ulong currentLevelPtr = (ulong)basePtrGDM;
            foreach (int offset in levelOffsets)
            {
                IntPtr readAddr = (IntPtr)(currentLevelPtr + (uint)offset);
                //Console.WriteLine($"Reading {currentLevelPtr.ToString("X")} + {offset.ToString("X")} at 0x{readAddr.ToString("X")}");

                currentLevelPtr = ReadPointer64FromMemory(hProcess, readAddr);

                if (currentLevelPtr == 0)
                {
                    //Console.WriteLine("Pointer chain broken.");
                    break;
                }

                //Console.WriteLine($"Next pointer: 0x{currentGRPtr:X}");
            }

            // Mask to remove sign-extension junk if needed (keep lower 48 bits)
            ulong trimmedLevel = currentLevelPtr & 0x00000000FFFFFFFF;

            Console.WriteLine($"Player level: {trimmedLevel}");

            // Read level
            ulong currentRunesPtr = (ulong)moduleBase + 0x03D6B880;
            currentRunesPtr = ReadPointer64FromMemory(hProcess, (IntPtr)currentRunesPtr);
            currentRunesPtr = ReadPointer64FromMemory(hProcess, (IntPtr)currentRunesPtr + 0x1128);

            // Mask to remove sign-extension junk if needed (keep lower 48 bits)
            ulong trimmedRunes = currentRunesPtr & 0x00000000FFFFFFFF;

            Console.WriteLine($"Runes: {trimmedRunes}");

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