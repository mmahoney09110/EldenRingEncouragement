using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using LocationDict;

namespace EldenEncouragement
{

    internal class MaidenReader
    {
        private const int PROCESS_VM_READ = 0x0010;
        private const int PROCESS_QUERY_INFORMATION = 0x0400;
        string prevStats = "";

        public async Task<string> GetEncouragement()
        {
            try
            {   
                var process = FindProcess("eldenring");
                using var reader = new MemoryReader(process, PROCESS_VM_READ | PROCESS_QUERY_INFORMATION);
                var idToName = WeaponLoader.LoadIds("Weapon_IDs.csv");
                var addresses = Addresses.GetAddresses();
                
                string stats = reader.PrintPlayerStats(addresses, idToName).Result;
                var body = (prevStats + "\n" + "Current stats:\n" + stats);
                prevStats = "Last sent stats:\n" + stats;
                    
                return await SendBodyAsync(body);
                
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return "Im confused, tarnished, forgive me.";
            }
            
        }

        public static async Task<string> SendBodyAsync(string bodyValue)
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Body", bodyValue)
            });

            using var HttpClient = new HttpClient();

            var response = await HttpClient.PostAsync("https://openai-proxy-server-vo9f.onrender.com/api/response", content);

            if (response.IsSuccessStatusCode)
            {
                // Since it's now just a string, not JSON: {"message":"..."}
                return await response.Content.ReadAsStringAsync();
            }

            return $"Error: {response.StatusCode}";
        }

        public Process FindProcess(string name)
        {
            var proc = Process.GetProcessesByName(name).FirstOrDefault();
            if (proc == null)
                throw new InvalidOperationException("Elden Ring is not running. Launch it in OFFLINE mode (no EAC).");
            Console.WriteLine($"Found Elden Ring (PID: {proc.Id})");
            return proc;
        }
    }

    internal static class NativeMethods
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(int desiredAccess, bool inheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] buffer, int size, out int bytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hHandle);
        public static byte[] ReadBytesFromMemory(IntPtr hProcess, IntPtr address, int length)
        {
            byte[] buffer = new byte[length];
            ReadProcessMemory(hProcess, address, buffer, buffer.Length, out int bytesRead);
            return buffer;
        }
    }

    internal class MemoryReader : IDisposable
    {
        private readonly IntPtr _handle;
        public readonly IntPtr ModuleBase;

        public MemoryReader(Process process, int access)
        {
            ModuleBase = GetModuleBaseAddress(process, "eldenring.exe");
            if (ModuleBase == IntPtr.Zero)
                throw new InvalidOperationException("Failed to get module base address.");

            _handle = NativeMethods.OpenProcess(access, false, process.Id);
            if (_handle == IntPtr.Zero)
                throw new InvalidOperationException("Failed to open process. Are you running as admin?");

            Console.WriteLine($"Module base: 0x{ModuleBase.ToString("X")} (Handle: {_handle})");
        }

        public async Task<string> PrintPlayerStats(Addresses.AddressesSet addrs, Dictionary<int, string> idToName)
        {
            int w1 = (int)ReadChain(addrs.Weapon1Offsets);
            int w2 = (int)ReadChain(addrs.Weapon2Offsets);
            int w3 = (int)ReadChain(addrs.Weapon3Offsets);

            return ($"HP: {ReadChain(addrs.HPOffsets) & 0x00000000FFFFFFFF}\nMax HP: {ReadChain(addrs.MaxHPOffsets) & 0x00000000FFFFFFFF}" +
                $"\nGreat Rune Active?: {ReadChain(addrs.GROffsets) & 0x00000000000FFFF}" +
                $"\nDeath Count: {ReadChain(addrs.DeathOffsets) & 0x00000000FFFFFFFF}" +
                $"\nPlayer Name: {ReadString(addrs.NameOffset, 64)}" +
                $"\nPlayer level: {ReadChain(addrs.LevelOffsets) & 0x00000000FFFFFFFF}" +
                $"\nRunes: {ReadChain(addrs.RunesOffsets) & 0x00000000FFFFFFFF}" +
                $"\nClass: {ResolveFromTable(addrs.ClassOffsets, Addresses.ClassNames)}" +
                $"\nGender: {ResolveFromTable(addrs.SexOffsets, Addresses.SexNames)}" +
                $"\nLocation: {ResolveLocation(addrs.LocationOffsets)}" +
                $"\nPrimary Weapon: {idToName.GetValueOrDefault(w1, "Unknown Weapon")}"
                + $" | Secondary: {idToName.GetValueOrDefault(w2, "Unknown")}"
                + $" | Tertiary: {idToName.GetValueOrDefault(w3, "Unknown")}"
            );
        }

        public ulong ReadChain(int[] offsets)
        {
            ulong ptr = ((ulong)ModuleBase);
            //Console.WriteLine($"Base ER pointer at 0x{ptr.ToString("X")}");
            foreach (var off in offsets)
            {
                //Console.WriteLine($"Reading pointer at 0x{ptr.ToString("X")} + {off.ToString("X")}");
                ptr = ReadPointer(ptr + (uint)off);
                //if (ptr == 0) break;
            }
            return ptr;
        }

        public string ReadString(int[] offsets, int maxChars)
        {
            // Read name
            ulong currentNamePtr = ReadChain(offsets);
            int maxStringLength = 64; // characters
            int byteLength = maxStringLength * 2; // UTF-16 = 2 bytes per char

            // Read raw memory starting from currentNamePtr
            byte[] rawStringBytes = NativeMethods.ReadBytesFromMemory(_handle, (IntPtr)(currentNamePtr + 0x9C), byteLength);

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
            return Encoding.Unicode.GetString(rawStringBytes, 0, end);
        }

        public ulong ReadPointer(ulong address)
        {
            byte[] buf = new byte[8];
            ReadMemory((IntPtr)address, buf);
            return BitConverter.ToUInt64(buf, 0);
        }

        private void ReadMemory(IntPtr addr, byte[] buffer)
        {
            if (!NativeMethods.ReadProcessMemory(_handle, addr, buffer, buffer.Length, out int read)
                || read != buffer.Length)
                throw new InvalidOperationException($"Error reading memory at 0x{addr.ToString("X")}");
        }

        private static IntPtr GetModuleBaseAddress(Process proc, string name)
        {
            foreach (ProcessModule m in proc.Modules)
                if (m.ModuleName.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return m.BaseAddress;
            return IntPtr.Zero;
        }

        private string ResolveFromTable(int[] offsets, string[] table)
        {
            int idx = (int)ReadChain(offsets) & 0xFFFF;
            return idx >= 0 && idx < table.Length ? table[idx] : "Unknown";
        }

        private string ResolveLocation(int[] offsets)
        {
            long locVal = (long)ReadChain(offsets);
            if (LocationData.LocationMap.TryGetValue((long)locVal, out var locationName))
            {
                return ($"{locationName}");
            }
            else
            {
                return ("Unknown");
            }
        }

        public void Dispose() => NativeMethods.CloseHandle(_handle);
    }

    internal static class WeaponLoader
    {
        public static Dictionary<int, string> LoadIds(string csvPath)
        {
            var map = new Dictionary<int, string>();
            foreach (var line in File.ReadLines(csvPath))
            {
                if (line.StartsWith("ID")) continue;
                var parts = line.Split(',');
                if (int.TryParse(parts[0], out var id) && !string.IsNullOrWhiteSpace(parts[1]))
                    map[id] = parts[1].Trim();
            }
            return map;
        }
    }

    internal static class Addresses
    {
        public static readonly string[] ClassNames = { "Vagabond", "Warrior", "Hero", "Bandit", "Astrologer", "Prophet", "Confessor", "Samurai", "Prisoner", "Wretch" };
        public static readonly string[] SexNames = { "Female", "Male" };

        public struct AddressesSet
        {
            public int[] HPOffsets;
            public int[] MaxHPOffsets;
            public int[] GROffsets;
            public int[] DeathOffsets;
            public int[] NameOffset;
            public int[] LevelOffsets;
            public int[] RunesOffsets;
            public int[] ClassOffsets;
            public int[] SexOffsets;
            public int[] LocationOffsets;
            public int[] Weapon1Offsets;
            public int[] Weapon2Offsets;
            public int[] Weapon3Offsets;
        }

        public static AddressesSet GetAddresses()
        {
            return new AddressesSet
            {
                HPOffsets = new[] { 0x3D65F88, 0x10EF8, 0x0, 0x190, 0x0, 0x138 },
                MaxHPOffsets = new[] { 0x3D65F88, 0x10EF8, 0x0, 0x190, 0x0, 0x13C },
                GROffsets = new[] { 0x3D5DF38, 0x08, 0xFF },
                DeathOffsets = new[] { 0x3D5DF38, 0x94 },
                NameOffset = new[] { 0x3D5DF38, 0x08 },
                LevelOffsets = new[] { 0x3D5DF38, 0x08, 0x68 },
                RunesOffsets = new[] { 0x03D6B880, 0x1128 },
                ClassOffsets = new[] { 0x3D5DF38, 0x08, 0xBF },
                SexOffsets = new[] { 0x3D5DF38, 0x08, 0xBE },
                LocationOffsets = new[] { 0x3D69918, 0xB60 },
                Weapon1Offsets = new[] { 0x3D5DF38, 0x08, 0x39C },
                Weapon2Offsets = new[] { 0x3D5DF38, 0x08, 0x3A4 },
                Weapon3Offsets = new[] { 0x3D5DF38, 0x08, 0x3AC }
            };
        }
    }
}