using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using LocationDict;
using WeaponDict;


namespace EldenEncouragement
{
    public class Changes
    {
        public double[] prevStats { get; set; }
        public string prevLocation { get; set; } = "";
        public string prevWeapon { get; set; } = "";
        public string prevWeapon2 { get; set; } = "";
        public string prevWeapon3 { get; set; } = "";
        public HashSet<string> visitedLocations { get; set; } = new();
        public HashSet<string> prevWeapons { get; set; } = new();
        public bool runes { get; set; } = false;
    }

    internal class MaidenReader
    {
        private const int PROCESS_VM_READ = 0x0010;
        private const int PROCESS_QUERY_INFORMATION = 0x0400;
        string prevStats = "";

        public async Task<string> GetEncouragement(int character)
        {
            try
            {
                var process = FindProcess("eldenring");
                using var reader = new MemoryReader(process, PROCESS_VM_READ | PROCESS_QUERY_INFORMATION);
                var addresses = Addresses.GetAddresses();

                string stats = await reader.PrintPlayerStats(addresses);
                var body = (prevStats + "\n" + "Current stats:\n" + stats);
                prevStats = "Last sent stats:\n" + stats;

                return await SendBodyAsync(body, character);

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return "Im confused, tarnished, forgive me.";
            }

        }
        public async Task UpdateEvent()
        {
            try
            {
                var process = FindProcess("eldenring");
                using var reader = new MemoryReader(process, PROCESS_VM_READ | PROCESS_QUERY_INFORMATION);
                var addresses = Addresses.GetAddresses();
                await reader.GetChangedStats(addresses);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

        }

        public async Task<string[]> GetEvent(int character)
        {
            try
            {
                var process = FindProcess("eldenring");
                using var reader = new MemoryReader(process, PROCESS_VM_READ | PROCESS_QUERY_INFORMATION);
                var addresses = Addresses.GetAddresses();
                string[] changed = await reader.GetChangedStats(addresses);

                if (changed.Length < 2 || changed[0] == "No changes detected." || changed[0] == "Initial stats assigned.")
                {
                    return new string[] { "No changes detected." };
                }
                else
                { 
                    return new string[] { await SendBodyAsync(changed[0],character), changed[1] };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return new string[] { "Im confused, tarnished, forgive me." };
            }

        }

        public static async Task<string> SendBodyAsync(string bodyValue, int characterValue)
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Body", bodyValue),
                new KeyValuePair<string, string>("Character", characterValue.ToString())
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
            //Console.WriteLine($"Found Elden Ring (PID: {proc.Id})");
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

            //Console.WriteLine($"Module base: 0x{ModuleBase.ToString("X")} (Handle: {_handle})");
        }

        public Task<string> PrintPlayerStats(Addresses.AddressesSet addrs)
        {
            return Task.Run(() =>
            {

                return
                    $"HP: {ReadChain(addrs.HPOffsets) & 0x00000000FFFFFFFF}\n" +
                    $"Max HP: {ReadChain(addrs.MaxHPOffsets) & 0x00000000FFFFFFFF}\n" +
                    $"Great Rune Active?: {ReadChain(addrs.GROffsets) & 0x00000000000FFFF}\n" +
                    $"Death Count: {ReadChain(addrs.DeathOffsets) & 0x00000000FFFFFFFF}\n" +
                    $"Player Name: {ReadString(addrs.NameOffset, 64)}\n" +
                    $"Player level: {ReadChain(addrs.LevelOffsets) & 0x00000000FFFFFFFF}\n" +
                    $"Runes: {ReadChain(addrs.RunesOffsets) & 0x00000000FFFFFFFF}\n" +
                    $"Class: {ResolveFromTable(addrs.ClassOffsets, Addresses.ClassNames)}\n" +
                    $"Gender: {ResolveFromTable(addrs.SexOffsets, Addresses.SexNames)}\n" +
                    $"Location: {ResolveLocation(addrs.LocationOffsets)}\n" +
                    $"Primary Weapon: {ResolveWeapon(addrs.Weapon1Offsets)}" +
                    $" | Secondary: {ResolveWeapon(addrs.Weapon2Offsets)}" +
                    $" | Tertiary: {ResolveWeapon(addrs.Weapon3Offsets)}";
            });
        }

        public static void SaveChanges(Changes changes, string path = "saved_stats.json")
        {
            Console.WriteLine($"Saving changes to {path}...");
            var json = System.Text.Json.JsonSerializer.Serialize(changes, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }

        public static Changes LoadChanges(string path = "saved_stats.json")
        {
            if (File.Exists(path))
            {
                Console.WriteLine($"Loading changes from {path}...");
                var json = File.ReadAllText(path);
                return System.Text.Json.JsonSerializer.Deserialize<Changes>(json);
            }
            else
            {
                Console.WriteLine($"File {path} not found. Creating a new one with default values...");
                var newChanges = new Changes();
                SaveChanges(newChanges, path); // Create the file with defaults
                return newChanges;
            }
        }

        
        public Task<string[]> GetChangedStats(Addresses.AddressesSet addrs)
        {
            return Task.Run(() =>
            {
                // Load previous save
                var changes = LoadChanges();

                // Sentiment for voice line
                string sentiment = "general";

                // Read current values
                double currentHP = ReadChain(addrs.HPOffsets) & 0x00000000FFFFFFFF;
                double currentMaxHP = ReadChain(addrs.MaxHPOffsets) & 0x00000000FFFFFFFF;
                double currentGR = ReadChain(addrs.GROffsets) & 0x00000000000FFFF;
                double currentDeath = ReadChain(addrs.DeathOffsets) & 0x00000000FFFFFFFF;
                double currentLevel = ReadChain(addrs.LevelOffsets) & 0x00000000FFFFFFFF;
                double currentRunes = ReadChain(addrs.RunesOffsets) & 0x00000000FFFFFFFF;

                string currentName = ReadString(addrs.NameOffset, 64);
                string currentLocation = ResolveLocation(addrs.LocationOffsets);
                string currentWeapon = ResolveWeapon(addrs.Weapon1Offsets);
                string currentWeapon2 = ResolveWeapon(addrs.Weapon2Offsets);
                string currentWeapon3 = ResolveWeapon(addrs.Weapon3Offsets);

                var changesList = new List<string>();
                // If this is the first time, assign the values to the previous stats
                if (changes.prevStats == null)
                {
                    changes.prevStats = new double[] { currentHP, currentMaxHP, currentGR, currentDeath, currentLevel, currentRunes };
                    changes.prevLocation = currentLocation;
                    changes.prevWeapon = currentWeapon;
                    changes.prevWeapon2 = currentWeapon2;
                    changes.prevWeapon3 = currentWeapon3;
                    changes.visitedLocations = new HashSet<string>();
                    changes.visitedLocations.Add(currentLocation);
                    changes.prevWeapons = new HashSet<string>();
                    changes.prevWeapons.Add(currentWeapon);
                    changes.prevWeapons.Add(currentWeapon2);
                    changes.prevWeapons.Add(currentWeapon3);
                    changes.runes = false;
                    SaveChanges(changes);
                    sentiment = "general";
                    return new string[] { "Event detected: First time talking to you!\n" +
                    $"HP: {ReadChain(addrs.HPOffsets) & 0x00000000FFFFFFFF}\n" +
                    $"Max HP: {ReadChain(addrs.MaxHPOffsets) & 0x00000000FFFFFFFF}\n" +
                    $"Great Rune Active?: {ReadChain(addrs.GROffsets) & 0x00000000000FFFF}\n" +
                    $"Death Count: {ReadChain(addrs.DeathOffsets) & 0x00000000FFFFFFFF}\n" +
                    $"Player Name: {ReadString(addrs.NameOffset, 64)}\n" +
                    $"Player level: {ReadChain(addrs.LevelOffsets) & 0x00000000FFFFFFFF}\n" +
                    $"Runes: {ReadChain(addrs.RunesOffsets) & 0x00000000FFFFFFFF}\n" +
                    $"Class: {ResolveFromTable(addrs.ClassOffsets, Addresses.ClassNames)}\n" +
                    $"Gender: {ResolveFromTable(addrs.SexOffsets, Addresses.SexNames)}\n" +
                    $"Location: {ResolveLocation(addrs.LocationOffsets)}\n" +
                    $"Primary Weapon: {ResolveWeapon(addrs.Weapon1Offsets)}" +
                    $" | Secondary: {ResolveWeapon(addrs.Weapon2Offsets)}" +
                    $" | Tertiary: {ResolveWeapon(addrs.Weapon3Offsets)}", sentiment};
                }
                else
                {
                    // Compare current stats with previous stats

                    if (changes.prevStats[0] != currentHP && (changes.prevStats[0] - currentHP) / currentMaxHP >= .25)
                    {
                        changesList.Add($"HP took a big hit and changed from {changes.prevStats[0]} to {currentHP}");
                        changesList.Add($"HP: {ReadChain(addrs.HPOffsets) & 0x00000000FFFFFFFF}");
                        changesList.Add($"Max HP: {ReadChain(addrs.MaxHPOffsets) & 0x00000000FFFFFFFF}");
                        sentiment = "worried";
                    }
                    if (currentHP / currentMaxHP <= .25)
                    {
                        if (currentHP == 0)
                        {
                            changesList.Add($"{currentName} died! Current HP: {currentHP} of {currentMaxHP} HP");
                            changesList.Add($"Death Count: {currentDeath}");
                            changesList.Add($"HP: {ReadChain(addrs.HPOffsets) & 0x00000000FFFFFFFF}");
                            changesList.Add($"Max HP: {ReadChain(addrs.MaxHPOffsets) & 0x00000000FFFFFFFF}");
                            sentiment = "death";
                        }
                        else { 
                            changesList.Add($"HP is low: {currentHP} of {currentMaxHP} HP");
                            changesList.Add($"HP: {ReadChain(addrs.HPOffsets) & 0x00000000FFFFFFFF}");
                            changesList.Add($"Max HP: {ReadChain(addrs.MaxHPOffsets) & 0x00000000FFFFFFFF}");
                            sentiment = "worried";
                        }
                    }
                    if (changes.prevStats[2] != currentGR && currentGR == 1)
                    {
                        changesList.Add($"Great Rune Activated!");
                        changesList.Add($"Great Rune Active?: {ReadChain(addrs.GROffsets) & 0x00000000000FFFF}");
                        changesList.Add($"HP: {ReadChain(addrs.HPOffsets) & 0x00000000FFFFFFFF}");
                        changesList.Add($"Max HP: {ReadChain(addrs.MaxHPOffsets) & 0x00000000FFFFFFFF}");
                        if (sentiment != "worried") sentiment = "impressed";
                    }
                    if (changes.prevStats[3] != currentDeath)
                    {
                        changesList.Add($"Death count changed from {changes.prevStats[3]} to {currentDeath}");
                        changesList.Add($"Death Count: {currentDeath}");
                        sentiment = "death";
                    }
                    if (changes.prevStats[4] != currentLevel)
                    {
                        changesList.Add($"Level changed from {changes.prevStats[4]} to {currentLevel}");
                        changesList.Add($"Player level: {currentLevel}");
                        changesList.Add($"HP: {ReadChain(addrs.HPOffsets) & 0x00000000FFFFFFFF}");
                        changesList.Add($"Max HP: {ReadChain(addrs.MaxHPOffsets) & 0x00000000FFFFFFFF}");
                        if (sentiment != "worried" && sentiment != "death") sentiment = "impressed";
                    }
                    var runeMultiplier = Math.Max(0, ((currentLevel + 81) - 92) * .02);
                    var runeCost = (runeMultiplier + .1) * (Math.Pow(currentLevel + 81, 2) + 1);
                    if (currentRunes >= runeCost && changes.runes==false)
                    {
                        changes.runes = true;
                        changesList.Add($"Enough runes to level up! Current Runes = {currentRunes}");
                        changesList.Add($"Runes: {ReadChain(addrs.RunesOffsets) & 0x00000000FFFFFFFF}");
                        if (sentiment != "worried" && sentiment != "death") sentiment = "impressed";
                    }
                    else if (currentRunes < runeCost)
                    {
                        changes.runes = false;
                    }
                    if (!changes.visitedLocations.Contains(currentLocation))
                    {
                        if (sentiment != "worried" && sentiment != "death") sentiment = "impressed";
                        changesList.Add($"New location visited: {currentLocation}");
                        changesList.Add($"Location: {currentLocation}");
                        changes.visitedLocations.Add(currentLocation);
                        changes.prevLocation = currentLocation;
                    }

                    //if (changes.prevWeapon != currentWeapon) changesList.Add($"Primary changed from {changes.prevWeapon} to {currentWeapon}");
                    //if (changes.prevWeapon2 != currentWeapon2) changesList.Add($"Secondary changed from {changes.prevWeapon2} to {currentWeapon2}");
                    //if (changes.prevWeapon3 != currentWeapon3) changesList.Add($"Tertiary changed from {changes.prevWeapon3} to {currentWeapon3}");

                    if (!changes.prevWeapons.Contains(currentWeapon))
                    {
                        if (sentiment != "worried" && sentiment != "death") sentiment = "impressed";
                        changesList.Add($"New primary weapon equipped: {currentWeapon}");
                        changesList.Add($"Primary Weapon: {ResolveWeapon(addrs.Weapon1Offsets)}" +
                    $" | Secondary: {ResolveWeapon(addrs.Weapon2Offsets)}" +
                    $" | Tertiary: {ResolveWeapon(addrs.Weapon3Offsets)}");
                        changes.prevWeapons.Add(currentWeapon);
                        changes.prevWeapon = currentWeapon;
                    }

                    if (!changes.prevWeapons.Contains(currentWeapon2))
                    {
                        if (sentiment != "worried" && sentiment != "death") sentiment = "impressed";
                        changesList.Add($"New secondary weapon equipped: {currentWeapon2}");
                        changesList.Add($"Primary Weapon: {ResolveWeapon(addrs.Weapon1Offsets)}" +
                    $" | Secondary: {ResolveWeapon(addrs.Weapon2Offsets)}" +
                    $" | Tertiary: {ResolveWeapon(addrs.Weapon3Offsets)}");
                        changes.prevWeapons.Add(currentWeapon2);
                        changes.prevWeapon3 = currentWeapon2;
                    }

                    if (!changes.prevWeapons.Contains(currentWeapon3))
                    {
                        if (sentiment != "worried" && sentiment != "death") sentiment = "impressed";
                        changesList.Add($"New tertiary weapon equipped: {currentWeapon3}");
                        changesList.Add($"Primary Weapon: {ResolveWeapon(addrs.Weapon1Offsets)}" +
                    $" | Secondary: {ResolveWeapon(addrs.Weapon2Offsets)}" +
                    $" | Tertiary: {ResolveWeapon(addrs.Weapon3Offsets)}");
                        changes.prevWeapons.Add(currentWeapon3);
                        changes.prevWeapon3 = currentWeapon3;
                    }

                    changes.prevLocation = currentLocation;
                    changes.prevWeapon = currentWeapon;
                    changes.prevWeapon2 = currentWeapon2;
                    changes.prevWeapon3 = currentWeapon3;
                    changes.prevStats = new double[] { currentHP, currentMaxHP, currentGR, currentDeath, currentLevel, currentRunes };

                    // Save changes to file
                    SaveChanges(changes);

                    // If there are any changes, log them
                    return changesList.Count > 0
                    ? new string[2] { $"Player Name: {currentName}\nEvent detected: {string.Join("\n", changesList)}",sentiment }
                    : new string[1] {"No changes detected."};
                }
            });
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
            int idx = (int)ReadChain(offsets) & 0xFF;
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

        private string ResolveWeapon(int[] offsets)
        {
            long wepVal = (int)ReadChain(offsets);
            if (WeaponData.WeaponMap.TryGetValue((long)wepVal, out var weaponName))
            {
                return ($"{weaponName}");
            }
            else
            {
                return ("Unknown");
            }
        }

        public void Dispose() => NativeMethods.CloseHandle(_handle);
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