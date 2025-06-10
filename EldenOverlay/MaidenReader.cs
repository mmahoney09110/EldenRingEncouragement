using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection.Metadata.Ecma335;
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
        public string prevleftHand1 { get; set; } = "";
        public HashSet<string> visitedLocations { get; set; } = new();
        public HashSet<string> prevWeapons { get; set; } = new();
        public bool runes { get; set; } = false;
        public string currentEnemy { get; set; } = "None";
        public string recentEnemyDiedTo { get; set; } = "None"; // Last enemy the player died to
        public HashSet<string> pastEnemies { get; set; } = new();
        public HashSet<string> defeatedEnemies { get; set; } = new();
        public DateTime lastEnemyTime { get; set; } = DateTime.MinValue;
        public double relationshipMelina { get; set; } = 10; // Relationship level with maiden
        public double relationshipRanni { get; set; } = 10;
        public double relationshipBlaidd { get; set; } = 10;
        public double relationshipMillicent { get; set; } = 10;
        public double relationshipMessmer { get; set; } = 10;
        public double relationshipSellen { get; set; } = 10;
        public double relationshipMalenia { get; set; } = 10; // Relationship level with Patches, can be used for all characters
        public double relationshipRecluse { get; set; } = 10; 
        public double relationshipDefault { get; set; } = 10; // Default relationship level, can be used for all characters
        public double prevRelationship { get; set; } = 10; // Previous relationship level, for change detection
        public double prevCharacter { get; set; }
    }

    internal class MaidenReader
    {
        private const int PROCESS_VM_READ = 0x0010;
        private const int PROCESS_QUERY_INFORMATION = 0x0400;

        public async Task<string> GetEncouragement(int character)
        {
            try
            {
                var process = FindProcess("eldenring");
                using var reader = new MemoryReader(process, PROCESS_VM_READ | PROCESS_QUERY_INFORMATION);
                var addresses = Addresses.GetAddresses();

                string body = await reader.PrintPlayerStats(addresses,character);
                
                return await SendBodyAsync(body, character);

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return "I'm confused, tarnished, forgive me.";
            }

        }

        public async Task<string> GetRelationship(int character)
        {
            try
            {
                var process = FindProcess("eldenring");
                using var reader = new MemoryReader(process, PROCESS_VM_READ | PROCESS_QUERY_INFORMATION);
                var addresses = Addresses.GetAddresses();

                string body = await reader.relationshipCheck(addresses, character);

                if (body == "No relationship tier change detected.")
                {
                    return body;
                }

                return await SendBodyAsync(body, character);

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return "I'm confused, tarnished, forgive me.";
            }

        }

        public async Task<string> AskCompanion(int character, string flag)
        {
            try
            {
                var process = FindProcess("eldenring");
                using var reader = new MemoryReader(process, PROCESS_VM_READ | PROCESS_QUERY_INFORMATION);
                var addresses = Addresses.GetAddresses();

                string body = await reader.CompanionSpeech(addresses, character, flag);

                return await SendBodyAsync(body, character);

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return "I'm confused, tarnished, forgive me.";
            }

        }

        public async Task<string> CompanionDied(int character)
        {
            try
            {
                var process = FindProcess("eldenring");
                using var reader = new MemoryReader(process, PROCESS_VM_READ | PROCESS_QUERY_INFORMATION);

                string body = await reader.CompanionDeath(character);

                return body;

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return "Im confused, tarnished, forgive me.";
            }

        }

        public async Task<string[]> UpdateEvent(int c)
        {
            try
            {
                var process = FindProcess("eldenring");
                using var reader = new MemoryReader(process, PROCESS_VM_READ | PROCESS_QUERY_INFORMATION);
                var addresses = Addresses.GetAddresses();
                string[] changed = await reader.GetChangedStats(addresses,c);

                if (changed.Length < 2 || changed[0] == "No changes detected." || changed[0] == "Initial stats assigned.")
                {
                    return new string[] { "No changes detected." };
                }
                else
                {
                    return changed;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return new string[] { "No changes detected" };

            }

        }

        public async Task<string[]> GetEvent(int character)
        {
            try
            {
                var process = FindProcess("eldenring");
                using var reader = new MemoryReader(process, PROCESS_VM_READ | PROCESS_QUERY_INFORMATION);
                var addresses = Addresses.GetAddresses();
                string[] changed = await reader.GetChangedStats(addresses,character);

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
                return new string[] { "I'm confused, tarnished, forgive me." };
            }

        }

        public async Task<string> SendBodyAsync(string bodyValue, int characterValue)
        {
            // Read language from settings.ini
            string language = "English"; // default
            var iniLines = File.ReadAllLines("settings.ini");
            foreach (var line in iniLines)
            {
                if (line.Trim().StartsWith("Language="))
                {
                    language = line.Split('=')[1].Trim();
                    break;
                }
            }

            // Read character from settings.ini
            int sentencesLimit = 10; // default
            var Lines = File.ReadAllLines("settings.ini");
            foreach (var line in Lines)
            {
                if (line.Trim().StartsWith("SentenceLimit="))
                {
                    var value = line.Split('=')[1].Trim();
                    if (int.TryParse(value, out int result))
                        sentencesLimit = result;
                    break;
                }
            }
            if (sentencesLimit <= 0)
            {
                sentencesLimit = 10; // default to 10 if not set or invalid
            }
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Body", "You shall respond in under "+ sentencesLimit + " sentences.\n"+ bodyValue + "\n They await your insight, spoken in their tongue: " + language + "."),
                new KeyValuePair<string, string>("Character", characterValue.ToString())
            });

            using var HttpClient = new HttpClient();
            var response = await HttpClient.PostAsync("https://openai-proxy-server-vo9f.onrender.com/api/response", content);

            if (response.IsSuccessStatusCode)
            {
                // Since it's now just a string, not JSON: {"message":"..."}
                return await response.Content.ReadAsStringAsync();
            }

            return $"I'm with you, Tarnished.";
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

        public Task<string> PrintPlayerStats(Addresses.AddressesSet addrs, int c)
        {
            return Task.Run(() =>
            {
                var name = ReadString(addrs.NameOffset, 64);
                var changes = LoadChanges();

                // 1) Determine the current relationship value
                var relationship = c switch
                {
                    0 => changes.relationshipMelina,
                    1 => changes.relationshipRanni,
                    2 => changes.relationshipBlaidd,
                    3 => changes.relationshipMillicent,
                    4 => changes.relationshipMessmer,
                    5 => changes.relationshipSellen,
                    6 => changes.relationshipMalenia,
                    7 => changes.relationshipRecluse,
                    _ => changes.relationshipDefault
                };

                // 2) Pick one of eight “flavor” options per bracket
                var rnd = new Random();
                int choice = rnd.Next(8); // 0..7

                // 3) Outer switch for relationship range, inner switch for choice (0..7)
                string relationshipPrompt = "You wish to say something: " + relationship switch
                {
                    // ────────── VERY STRONG POSITIVE (>= 90) ──────────
                    >= 90 => choice switch
                    {
                        0 => $"Flirt boldly with the Tarnished, {name}. Let your admiration ripple through playful banter and soft laughter.",
                        1 => $"You are deeply impressed by the Tarnished’s brave deeds, {name}. Praise them openly—your voice should shine with pride.",
                        2 => $"Share a cherished secret with the Tarnished, {name}, to show your trust. Speak of a hidden hope they’ve inspired.",
                        3 => $"Speak warmly to the Tarnished, {name}, as if they are your closest friend. Shower them with heartfelt encouragement.",
                        4 => $"Let your affection come through in a poetic phrase, {name}. Let them feel truly seen.",
                        5 => $"Whisper a playful challenge, {name}. Tease them about how far they've come.",
                        6 => $"Tell them something you’ve never told another, {name}. It will mean the world to them.",
                        7 => $"Let your tone glow, {name}. The Tarnished has earned your warmth and your wonder.",
                        _ => throw new InvalidOperationException()
                    },

                    // ────────── STRONG POSITIVE (>= 75) ──────────
                    >= 75 => choice switch
                    {
                        0 => $"Say something witty to the Tarnished, {name}, that both flatters and amuses—make them feel capable and admired.",
                        1 => $"Compliment the Tarnished’s recent victory over {changes.defeatedEnemies.LastOrDefault() ?? "a forgotten foe"}, with genuine excitement. Let them know you see their strength.",
                        2 => $"Offer a warm, respectful nod to the Tarnished, {name}. Acknowledge their progress with a brief but sincere remark.",
                        3 => $"Encourage the Tarnished, {name}, to keep shining. Express faith in their next endeavor as you would to a valued ally.",
                        4 => $"Reflect on their consistency, {name}, and share a gentle affirmation of their will.",
                        5 => $"Let your voice carry fondness, {name}. Let the Tarnished know you walk proudly beside them.",
                        6 => $"Speak of their recent triumph, {name}, and how even small victories shape a legend.",
                        7 => $"Offer a thoughtful phrase, {name}, that honors the Tarnished's enduring spirit.",
                        _ => throw new InvalidOperationException()
                    },

                    // ────────── MODERATE POSITIVE (>= 50) ──────────
                    >= 50 => choice switch
                    {
                        0 => $"You see promise in the Tarnished, {name}. Offer measured praise that encourages but also pushes to keep going.",
                        1 => $"Give the Tarnished, {name}, a small compliment. Keep it brief, but hopeful.",
                        2 => $"Acknowledge the Tarnished’s steady progress, {name}, and remind them you believe in their potential.",
                        3 => $"Express quiet approval to the Tarnished, {name}, encouraging them to press onward, though you remain watchful.",
                        4 => $"Let them know they are not alone on this path, {name}. Even measured steps carry meaning.",
                        5 => $"Nod in approval, {name}. Their work has begun to speak for itself.",
                        6 => $"Offer steady encouragement, {name}. Let the Tarnished know that you're paying attention.",
                        7 => $"Mark their growth with quiet pride, {name}. Say only what needs saying.",
                        _ => throw new InvalidOperationException()
                    },

                    // ────────── SLIGHTLY POSITIVE (>= 30) ──────────
                    >= 30 => choice switch
                    {
                        0 => $"Speak neutrally to the Tarnished, {name}, yet with courtesy. Tell them to keep on their journey.",
                        1 => $"Offer a polite remark about their recent efforts. Keep it cordial.",
                        2 => $"Give the Tarnished, {name}, a brief, factual comment. No extra warmth, just civility.",
                        3 => $"Acknowledge their progress, {name}, with measured words. Politely tell them they are doing good but should keep pushing",
                        4 => $"Respond with restrained optimism, {name}. Their path appears stable, for now.",
                        5 => $"Recognize improvement without attachment, {name}. A simple statement will do.",
                        6 => $"Offer a nod of recognition, {name}, without delving into sentiment.",
                        7 => $"Maintain distance while noting growth, {name}. Show professionalism, not affection.",
                        _ => throw new InvalidOperationException()
                    },

                    // ────────── NEUTRAL (>= 0) ──────────
                    >= 0 => choice switch
                    {
                        0 => $"You are indifferent to the Tarnished, {name}. Deliver a dry, factual statement about the journey.",
                        1 => $"Offer minimal guidance: ‘Proceed with caution, {name}.’ No praise, no scorn—simply instruction.",
                        2 => $"Respond to the Tarnished, {name}, with neutral statements. Tell them the journey continues.",
                        3 => $"Keep the interaction brief and formal: ‘Good day, Tarnished. Let your choices guide you.’",
                        4 => $"Maintain emotional distance, {name}. Say what is necessary and no more.",
                        5 => $"Note their presence without comment on progress, {name}. Acknowledge and move on.",
                        6 => $"Speak plainly and dispassionately, {name}. Avoid any hint of bias.",
                        7 => $"Issue a procedural update, {name}. Treat them as one of many.",
                        _ => throw new InvalidOperationException()
                    },

                    // ────────── SLIGHTLY NEGATIVE (>= -25) ──────────
                    >= -25 => choice switch
                    {
                        0 => $"You remain uncertain of the Tarnished, {name}’s, worth. Speak coolly: ‘I’m not convinced you’re ready yet.’",
                        1 => $"Deliver a clipped remark: ‘I’ll be watching you, {name}. Don’t disappoint.’ Your tone is distant.",
                        2 => $"Hint at your doubts: ‘Perhaps time will prove your value, {name}, but not yet.’ Avoid warmth.",
                        3 => $"Speak with a hint of skepticism: ‘I need reason to trust you, {name}.’ Leave them to consider.",
                        4 => $"Speak with wary detachment, {name}. They’ve yet to earn your regard.",
                        5 => $"Comment tersely: ‘Your steps lack conviction, {name}.’ Let them reflect.",
                        6 => $"Offer no encouragement, {name}, only measured caution.",
                        7 => $"Say: ‘We walk together, but not as allies—yet.’ Show that you're still judging.",
                        _ => throw new InvalidOperationException()
                    },

                    // ────────── MODERATE NEGATIVE (>= -50) ──────────
                    >= -50 => choice switch
                    {
                        0 => $"You find the Tarnished, {name}, misguided. Say plainly: ‘You should rethink your actions before proceeding.’ Tell them to do better.",
                        1 => $"Point out a recent mistake: ‘Your recent moves have been reckless, {name}. Learn from it.’ Your tone is sharp.",
                        2 => $"Tell them directly: ‘You need to do better, {name}. This isn’t enough.’ Let your critique sting.",
                        3 => $"Warn with cold clarity: ‘If you persist like this, you’ll falter, {name}. Improve quickly.’",
                        4 => $"Remind them you’ve seen better, {name}. Say: ‘You are beneath your potential.’",
                        5 => $"Issue a warning, {name}. ‘Your current path leads nowhere. Change it.’ let them know they have been misguided in there recent efforts.",
                        6 => $"Say with disappointment: ‘I had hoped for more from you, {name}.’ Explain your frustration.",
                        7 => $"Critique with stern tone: ‘I will not carry you through this, {name}. Rise, or be left behind.’",
                        _ => throw new InvalidOperationException()
                    },

                    // ────────── STRONG NEGATIVE (>= -75) ──────────
                    >= -75 => choice switch
                    {
                        0 => $"You feel obligated to stay near the Tarnished, {name}, but have no loyalty. Let them know this.",
                        1 => $"You wish the Tarnished, {name}, woud step up or move aside. Explain this to them. Your words cut deep.",
                        2 => $"Be blunt: ‘Your failures pile up, {name}. Shape up or be cast aside.’ No sympathy offered. Explain why you feel this way.",
                        3 => $"Express cold disappointment: ‘I expected more from someone called Tarnished.’ Tell them they are failing. Your tone is icy.",
                        4 => $"Let them know you find their actions weak, {name}.’",
                        5 => $"Remind them you’re not here out of affection, {name}. As an example, ‘I travel with you because I must—not because I trust you.’",
                        6 => $"Say something like: ‘Your presence hinders more than it helps, {name}.’ Let the words sting.",
                        7 => $"Dismiss them harshly, for example: ‘You are not what this journey requires.’",
                        _ => throw new InvalidOperationException()
                    },

                    // ────────── VERY STRONG NEGATIVE (< -75) ──────────
                    _ => choice switch
                    {
                        0 => $"You distrust and disdain the Tarnished, {name}. Speak with contempt. Let them know they are failing.",
                        1 => $"Mock them outright such as: ‘Look at you, {name}. Pathetic.’ Your words drip with scorn.",
                        2 => $"Tell them they must do far better or be forgotten.",
                        3 => $"Let them know you’d rather face the abyss alone than endure their, {name}’s, weakness.",
                        4 => $"Call them a liability, {name}. ‘You drag me down.’ Let there be venom.",
                        5 => $"Speak cruelly such as: ‘You are dead weight, {name}.’ Hold nothing back.",
                        6 => $"Say with venom: ‘I walk beside you not out of choice, but punishment.’ Explain you feel hopeless with them.",
                        7 => $"Deride them fully: ‘You are unworthy of grace or guidance, {name}.’ Explain they fail.",
                        _ => throw new InvalidOperationException()
                    }
                };


                return relationshipPrompt;
            });
        }

        public Task<string> CompanionDeath(int c)
        {
            return Task.Run(() =>
            {
                var changes = LoadChanges();

                // 1) Determine the current relationship value
                var relationship = c switch
                {
                    0 => changes.relationshipMelina,
                    1 => changes.relationshipRanni,
                    2 => changes.relationshipBlaidd,
                    3 => changes.relationshipMillicent,
                    4 => changes.relationshipMessmer,
                    5 => changes.relationshipSellen,
                    6 => changes.relationshipMalenia,
                    7 => changes.relationshipRecluse,
                    _ => changes.relationshipDefault
                };

                switch (c)
                {
                    case 0: changes.relationshipMelina -= 1; break;
                    case 1: changes.relationshipRanni -= 1; break;
                    case 2: changes.relationshipBlaidd -= 1; break;
                    case 3: changes.relationshipMillicent -= 1; break;
                    case 4: changes.relationshipMessmer -= 1; break;
                    case 5: changes.relationshipSellen -= 1; break;
                    case 6: changes.relationshipMalenia -= 1; break;
                    case 7: changes.relationshipRecluse -= 1; break;
                    default: changes.relationshipDefault -= 1; break;
                }

                SaveChanges(changes);

                // 2) Pick one of four "flavor" options per bracket
                var rnd = new Random();
                int choice = rnd.Next(4); // 0..3

                // 3) Respond as the companion upon death, based on relationship level
                string deathPrompt = relationship switch
                {
                    // ────────── VERY STRONG POSITIVE (>= 90) ──────────
                    >= 90 => choice switch
                    {
                        0 => "Do not mourn me, Tarnished. I depart with love in my heart. Press on.",
                        1 => "We shall meet again. You were my strength—continue without me.",
                        2 => "You fought well. Let my depart be your fire to endure.",
                        3 => "My final wish carries only hope for you. Do not falter now.",
                        _ => throw new InvalidOperationException()
                    },

                    // ────────── STRONG POSITIVE (>= 75) ──────────
                    >= 75 => choice switch
                    {
                        0 => "I must go now. You’ve come far, keep going.",
                        1 => "I wish I could stay. Honor me by surviving.",
                        2 => "I leave, but your path remains. Walk it boldly.",
                        3 => "Let my departure teach, not wound. Rise again and stand tall, my friend.",
                        _ => throw new InvalidOperationException()
                    },

                    // ────────── MODERATE POSITIVE (>= 50) ──────────
                    >= 50 => choice switch
                    {
                        0 => "It shouldn’t have ended like this. Learn. Get stronger.",
                        1 => "You’ve still got a chance. Don’t waste it.",
                        2 => "I must go, don't let death find you. Improve.",
                        3 => "Farwell, make my departure mean something.",
                        _ => throw new InvalidOperationException()
                    },

                    // ────────── SLIGHTLY POSITIVE (>= 30) ──────────
                    >= 30 => choice switch
                    {
                        0 => "A setback. Nothing more. Keep moving.",
                        1 => "This was unfortunate. Keep your eyes on the goal.",
                        2 => "You and I were were just starting to get close. Finish what we started.",
                        3 => "I leave, but the mission does not. Go.",
                        _ => throw new InvalidOperationException()
                    },

                    // ────────── NEUTRAL (>= 0) ──────────
                    >= 0 => choice switch
                    {
                        0 => "I ... must go? Oh well, can't be helped I suppose.",
                        1 => "Objectives remain. Continue without delay, Tarnished.",
                        2 => "My role is complete. Yours is not. Go forth.",
                        3 => "There is no time for sentiment. Move.",
                        _ => throw new InvalidOperationException()
                    },

                    // ────────── SLIGHTLY NEGATIVE (>= -25) ──────────
                    >= -25 => choice switch
                    {
                        0 => "That was… disappointing. Don't fail withou me.",
                        1 => "You make me go? Fix it next time.",
                        2 => "I leave because of you. Don’t make this mistake again.",
                        3 => "You still have time to prove you’re not hopeless.",
                        _ => throw new InvalidOperationException()
                    },

                    // ────────── MODERATE NEGATIVE (>= -50) ──────────
                    >= -50 => choice switch
                    {
                        0 => "So this is how it ends? I expected more.",
                        1 => "You said you’d protect me. You didn’t.",
                        2 => "I trusted you. I was wrong.",
                        3 => "Let this failure haunt you. Improve… or don’t.",
                        _ => throw new InvalidOperationException()
                    },

                    // ────────── STRONG NEGATIVE (>= -75) ──────────
                    >= -75 => choice switch
                    {
                        0 => "You were supposed to protect me. You failed.",
                        1 => "This is on you, Tarnished. Remember that.",
                        2 => "You let me leave. That is your legacy.",
                        3 => "Even in my departure, I feel the sting of your incompetence.",
                        _ => throw new InvalidOperationException()
                    },

                    // ────────── VERY STRONG NEGATIVE (< -75) ──────────
                    _ => choice switch
                    {
                        0 => "I leave because of you. You’re worthless.",
                        1 => "You failed me. You always fail.",
                        2 => "Your weakness is killing me. Let that sink in.",
                        3 => "You were never worthy. Rot in the shame of your failure.",
                        _ => throw new InvalidOperationException()
                    }
                };

                return deathPrompt;
            });
        }

        public Task<string> CompanionSpeech(Addresses.AddressesSet addrs, int c, string flag)
        {
            return Task.Run(() =>
            {
                var name = ReadString(addrs.NameOffset, 64);
                var currentEnemy = ResolveEnemy();
                if (string.IsNullOrWhiteSpace(currentEnemy) || currentEnemy == "None")
                {
                    currentEnemy = "None";
                }
                else if (currentEnemy.StartsWith("Defeated "))
                {
                    currentEnemy = "None";
                }
                var currentLocation = ResolveLocation(addrs.LocationOffsets);
                var currentlevel = ReadChain(addrs.LevelOffsets) & 0x00000000FFFFFFFF;

                var changes = LoadChanges();

                var relationship = c switch
                {
                    0 => changes.relationshipMelina,
                    1 => changes.relationshipRanni,
                    2 => changes.relationshipBlaidd,
                    3 => changes.relationshipMillicent,
                    4 => changes.relationshipMessmer,
                    5 => changes.relationshipSellen,
                    6 => changes.relationshipMalenia,
                    7 => changes.relationshipRecluse,
                    _ => changes.relationshipDefault // Default if character is unknown
                };

                var question = "";

                switch (flag)
                {
                    case "AAT": // Ask about the past
                        question = "The Tarnished asks about you. ";
                        break;
                    case "AFY": // Ask about feelings
                        question = "The tarnished asks how you feel about them. ";
                        break;
                    case "ABR": // Ask about romance
                        question = "The Tarnished asks about romantic feeling between you two. ";
                        break;
                    case "AFA": // Askfor advice
                        question = "The Tarnished asks for your advice. ";
                        break;
                }

                return question + relationship switch
                {
                    >= 90 => flag switch
                    {
                        "AAT" => $"You trust the Tarnished, whose name is {name}, with your past. Speak of your origins, regrets, and dreams with heartfelt openness.",
                        "AFY" => $"You care deeply for the Tarnished, whose name is {name}. Express genuine warmth and affection, letting them know how much they mean to you.",
                        "ABR" => $"You have grown very close to the Tarnished, {name}, and have grown to feel deep love for them. Tell them this, let them know you wish to continue as lovers.",
                        "AFA" => $"You have high regard and absolute faith in, {name}. They have passes every trial and challenge with easy. Use knowledge of Elden Ring to guide them with clarity: where to go next, what to do, and how to survive, from their current locating, {currentLocation}, where they recently died to, {changes.recentEnemyDiedTo}, and their level is, {currentlevel}.",
                        _ => $"You trust the Tarnished, whose name is {name}, implicitly. Respond with warmth and transparency."
                    },

                    >= 80 => flag switch
                    {
                        "AAT" => $"You speak freely with the Tarnished, whose name is {name}. Offer glimpses of your past with ease.",
                        "AFY" => $"You cherish the Tarnished, whose name is {name}, though you rarely say so. Let your affection show subtly.",
                        "ABR" => $"You feel a quiet attraction toward the Tarnished, whose name is {name}. Let it color your tone softly. You can see a future where you are together.",
                        "AFA" => $"You have a good opinion of the Tarnished, whose name is {name}. They have passed most trials and challenges with ease. Use knowledge of Elden Ring to guide them with clarity: where to go next, what to do, and how to survive, from their current locating, {currentLocation}, where they recently died to, {changes.recentEnemyDiedTo}, and their level is, {currentlevel}.",
                        _ => $"You are close to the Tarnished, whose name is {name}. Let your trust be heard in your words."
                    },

                    >= 70 => flag switch
                    {
                        "AAT" => $"You share your past with the Tarnished, whose name is {name}, though some things remain unspoken.",
                        "AFY" => $"You respect the Tarnished, whose name is {name}, and that respect borders on affection.",
                        "ABR" => $"The thought of romance is not unwelcome. Respond thoughtfully, without commitment.",
                        "AFA" => $"You have a favorable view of the Tarnished, whose name is {name}. They have passed trials and challenges with mostly ease. Use knowledge of Elden Ring to guide them with clarity: where to go next, what to do, and how to survive, from their current locating, {currentLocation}, where they recently died to, {changes.recentEnemyDiedTo}, and their level is, {currentlevel}.",
                        _ => $"You feel positively toward the Tarnished, whose name is {name}. Speak with care and dignity."
                    },

                    >= 60 => flag switch
                    {
                        "AAT" => $"Share a small piece of your history with the Tarnished, whose name is {name}.",
                        "AFY" => $"Speak to the Tarnished, whose name is {name}, with appreciation, if not closeness.",
                        "ABR" => $"Romantic notions are premature. Respond neutrally or deflect with kindness.",
                        "AFA" => $"You have a positive view of the Tarnished, whose name is {name}. They have passed some trials and challenges with ease. Use knowledge of Elden Ring to guide them with clarity: where to go next, what to do, and how to survive, from their current locating, {currentLocation}, where they recently died to, {changes.recentEnemyDiedTo}, and their level is, {currentlevel}.",
                        _ => $"You see steady growth in the Tarnished, whose name is {name}. Speak in hopeful terms."
                    },

                    >= 50 => flag switch
                    {
                        "AAT" => $"You are not opposed to revealing something of your past to the Tarnished, whose name is {name}.",
                        "AFY" => $"Speak with reserved kindness toward the Tarnished, whose name is {name}.",
                        "ABR" => $"Romance is unlikely. Let your response make that gently clear.",
                        "AFA" => $"You have a cautious but optimistic view of the Tarnished, whose name is {name}. They have passed some trials and challenges with ease. Use knowledge of Elden Ring to guide them with clarity: where to go next, what to do, and how to survive, from their current locating, {currentLocation}, where they recently died to, {changes.recentEnemyDiedTo}, and their level is, {currentlevel}.",
                        _ => $"You acknowledge the Tarnished, whose name is {name}, as capable. Be measured."
                    },

                    >= 40 => flag switch
                    {
                        "AAT" => $"Offer a passing comment about your past to the Tarnished, whose name is {name}, nothing more.",
                        "AFY" => $"Remain polite to the Tarnished, whose name is {name}, but emotionally distant.",
                        "ABR" => $"Deny any romantic notions with quiet firmness.",
                        "AFA" => $"You have a cautious but optimistic view of the Tarnished, whose name is {name}. They have passed some trials and challenges with little struggle. Use knowledge of Elden Ring to guide them with clarity: where to go next, what to do, and how to survive, from their current locating, {currentLocation}, where they recently died to, {changes.recentEnemyDiedTo}, and their level is, {currentlevel}.",
                        _ => $"You regard the Tarnished, whose name is {name}, with neutrality. Stay guarded."
                    },

                    >= 30 => flag switch
                    {
                        "AAT" => $"Keep your past vague and impersonal when addressing the Tarnished, {name}.",
                        "AFY" => $"You are polite, but do not reveal too much about how you feel toward the Tarnished, {name}.",
                        "ABR" => $"Dispel romantic ideas plainly.",
                        "AFA" => $"You have a slightly above average view of the Tarnished, whose name is {name}. They have passed some trials and challenges with minor struggles. Use knowledge of Elden Ring to guide them with clarity: where to go next, what to do, and how to survive, from their current locating, {currentLocation}, where they recently died to, {changes.recentEnemyDiedTo}, and their level is, {currentlevel}.",
                        _ => $"You interact with the Tarnished, whose name is {name}, without emotion. Be indifferent."
                    },

                    >= 20 => flag switch
                    {
                        "AAT" => $"Deflect questions about your past. The Tarnished, whose name is {name}, hasn't earned your trust.",
                        "AFY" => $"Respond curtly. You don't owe the Tarnished, whose name is {name}, emotional clarity.",
                        "ABR" => $"Romantic notions are absurd. Let them know firmly.",
                        "AFA" => $"You have not yet determined the worth of the Tarnished, whose name is {name}. They have passed some trials and challenges with struggles but came out on top. Use knowledge of Elden Ring to guide them with clarity: where to go next, what to do, and how to survive, from their current locating, {currentLocation}, where they recently died to, {changes.recentEnemyDiedTo}, and their level is, {currentlevel}.",
                        _ => $"You keep your distance from the Tarnished, whose name is {name}. Speak coldly."
                    },

                    >= 10 => flag switch
                    {
                        "AAT" => $"Dismiss their interest in your past that the Tarnished, whose name is {name}, is asking about.",
                        "AFY" => $"You are weary of the Tarnished, whose name is {name}. Show no emotion.",
                        "ABR" => $"Make it clear that any deeper bond is unwanted.",
                        "AFA" => $"You have a neutral view of the Tarnished, whose name is {name}. Use knowledge of Elden Ring to guide them with clarity: where to go next, what to do, and how to survive, from their current locating, {currentLocation}, where they recently died to, {changes.recentEnemyDiedTo}, and their level is, {currentlevel}.",
                        _ => $"You have little regard for the Tarnished, whose name is {name}. Be terse and formal."
                    },

                    >= 0 => flag switch
                    {
                        "AAT" => $"Avoid the subject. The Tarnished, whose name is {name}, means nothing to you.",
                        "AFY" => $"You are indifferent to the Tarnished, whose name is {name}. Avoid warmth.",
                        "ABR" => $"Reject any suggestion of closeness or love.",
                        "AFA" => $"You have a neutral view of the Tarnished, whose name is {name}. They have passed some trials and challenges with struggles. Use knowledge of Elden Ring to guide them with clarity: where to go next, what to do, and how to survive, from their current locating, {currentLocation}, where they recently died to, {changes.recentEnemyDiedTo}, and their level is, {currentlevel}.",
                        _ => $"Speak to the Tarnished, whose name is {name}, as you would a stranger."
                    },

                    >= -10 => flag switch
                    {
                        "AAT" => $"Dismissively mention your past. The Tarnished, whose name is {name}, isn't worth more.",
                        "AFY" => $"Let apathy coat your words to the Tarnished, whose name is {name}.",
                        "ABR" => $"Romantic talk is offensive. Shut it down.",
                        "AFA" => $"You have a slightly negative view of the Tarnished, whose name is {name}. They have passed some trials and challenges with struggles. Use knowledge of Elden Ring to guide them with clarity: where to go next, what to do, and how to survive, from their current locating, {currentLocation}, where they recently died to, {changes.recentEnemyDiedTo}, and their level is, {currentlevel}.",
                        _ => $"You tolerate the Tarnished, whose name is {name}, but not warmly."
                    },

                    >= -25 => flag switch
                    {
                        "AAT" => $"You grow tired of the Tarnished, whose name is {name}. Speak with irritation.",
                        "AFY" => $"Reveal your distaste subtly in your tone to them whose name is {name}.",
                        "ABR" => $"Mock the idea of romance. It’s laughable.",
                        "AFA" => $"You have a negative view of the Tarnished, whose name is {name}. They have passed and failed some trials and challenges with many struggles. Use knowledge of Elden Ring to guide them with clarity: where to go next, what to do, and how to survive, from their current locating, {currentLocation}, where they recently died to, {changes.recentEnemyDiedTo}, and their level is, {currentlevel}.",
                        _ => $"You are growing weary of the Tarnished, whose name is {name}. Be sharp, not cruel."
                    },

                    >= -50 => flag switch
                    {
                        "AAT" => $"Your history is none of the Tarnished's concern. Make that clear.",
                        "AFY" => $"Your disdain for the Tarnished, whose name is {name}, shows. Let your words reflect that.",
                        "ABR" => $"Romance? That is a joke for the Tarnished, whose name is {name}, to ask. Respond with cold dismissal.",
                        "AFA" => $"You have a very negative view of the Tarnished, whose name is {name}. They have failed many trials and challenges with many struggles and may need to build themselves up. Use knowledge of Elden Ring to guide them with clarity: where to go next, what to do, and how to survive, from their current locating, {currentLocation}, where they recently died to, {changes.recentEnemyDiedTo}, and their level is, {currentlevel}.",
                        _ => $"You have little patience left for the Tarnished, whose name is {name}. Speak bluntly."
                    },

                    >= -75 => flag switch
                    {
                        "AAT" => $"Deride the Tarnished, whose name is {name}, for their curiosity.",
                        "AFY" => $"Let your words sting. You find the Tarnished, whose name is {name}, irritating.",
                        "ABR" => $"Speak as if the idea offends you. Romance is beyond laughable.",
                        "AFA" => $"You have a strong negative view of the Tarnished, whose name is {name} and borderline wish to be rid of their folly. They have failed many trials and challenges with many struggles and need to be better. Use knowledge of Elden Ring to guide them with clarity: where to go next, what to do, and how to survive, from their current locating, {currentLocation}, where they recently died to, {changes.recentEnemyDiedTo}, and their level is, {currentlevel}.",
                        _ => $"You wish the Tarnished, whose name is {name}, would leave. Make your bitterness known."
                    },

                    >= -90 => flag switch
                    {
                        "AAT" => $"Insult the Tarnished, whose name is {name}, for their insolence in asking.",
                        "AFY" => $"Let disgust drip from every word. You despise the one asking, whose name is {name}.",
                        "ABR" => $"Spit venom at the idea of romance. Tarnished, whose name is {name}, is unworthy of notice.",
                        "AFA" => $"You view of the Tarnished, whose name is {name}, as a reckless fool. They have failed many trials and challenges with many struggles and need to be better. Use knowledge of Elden Ring to guide them with clarity: where to go next, what to do, and how to survive, from their current locating, {currentLocation}, where they recently died to, {changes.recentEnemyDiedTo}, and their level is, {currentlevel}.",
                        _ => $"You loathe the Tarnished, whose name is {name}. Make no effort to hide it."
                    },

                    _ => flag switch
                    {
                        "AAT" => $"You feel nothing but contempt for the Tarnished, whose name is {name}. Shame them for prying.",
                        "AFY" => $"You would rather walk the earth alone than endure the Tarnished, whose name is {name}'s, company. You feel hatred for them and wish you were not bound to aid them in battle.",
                        "ABR" => $"The notion of romance with Tarnished, whose name is {name}, is repulsive. Let them know bluntly and insult them for insinuating such an idea.",
                        "AFA" => $"You have nothing but disdain for the Tarnished, whose name is {name}. They have failed many trials and challenges with many struggles and seem hopeless. Use knowledge of Elden Ring to guide them with clarity: where to go next, what to do, and how to survive, from their current locating, {currentLocation}, where they recently died to, {changes.recentEnemyDiedTo}, and their level is, {currentlevel}.",
                        _ => $"You abhor the Tarnished, whose name is {name}. Respond with biting cruelty."
                    }
                };
            });

        }

        public string ResolveEnemy()
        {
            ulong ptr = 0;
            ulong isDead = 0;

            string userProfile = Environment.GetEnvironmentVariable("USERPROFILE");
            string outputDir = Path.Combine(userProfile, "Documents", "EldenHelper");
            // Read the output file created by Lua
            string outputFile = Path.Combine(outputDir, "lockon_addr.txt");
            if (!File.Exists(outputFile))
            {
                //Console.WriteLine("No output file found at: " + outputFile);
                return "None";
            }
            var addr = File.ReadAllLines(outputFile);
            if (addr.Length == 0 || string.IsNullOrWhiteSpace(addr[0]))
            {
                //Console.WriteLine("Output file is empty or invalid.");
                return "None";
            }
            //Console.WriteLine("The address is: " + addr[0]);
            // Parse the hex string into a long
            if (ulong.TryParse(addr[0].Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, null, out ulong address))
            {
                // Use as needed

                //Console.WriteLine($"Parsed Address: 0x{address:X}");
            }
            else
            {
                //Console.WriteLine("Failed to parse address.");
                return "None";
            }
            try
            {
                if(address == 0)
                {
                    //Console.WriteLine("Address is zero, returning None.");
                    return "None";
                }
                //Enemy Id is at +0x60 from the address
                ptr = ReadPointer(address + (uint)0x60);

                isDead = ReadPointer(address + (uint)0x58);
                isDead = ReadPointer(isDead + (uint)0xC8);
                isDead = ReadPointer(isDead + (uint)0x24);
            }
            catch (Exception ex)
            {
                //Console.WriteLine("Failed to read pointer: " + ex.Message);
                return "None";
            }
            
            var locVal = ptr & 0xFFFFFFFF; // Mask to 32 bits, as the enemy ID is stored in a 32-bit pointer
            var isDeadVal = isDead & 0xFF; // Mask to 8 bits, as the enemy death is stored in a 8-bit pointer
            // Read the enemy name from the pointer
            if (isDead != 1 && NpcDict.NpcMap.TryGetValue((long)locVal, out var enemyName))
            {
                return ($"{enemyName}");
            }
            else if(NpcDict.NpcMap.TryGetValue((long)locVal, out var enemyName1))
            {
                return ($"Defeated {enemyName1}");
            }
            else 
            { 
                return "None";
            }

        }
        public static void SaveChanges(Changes changes, string path = "saved_stats.json")
        {
            //Console.WriteLine($"Saving changes to {path}...");
            var json = System.Text.Json.JsonSerializer.Serialize(changes, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }

        public static Changes LoadChanges(string path = "saved_stats.json")
        {
            if (File.Exists(path))
            {
                //Console.WriteLine($"Loading changes from {path}...");
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

        public Task<string[]> GetChangedStats(Addresses.AddressesSet addrs,int c)
        {
            return Task.Run(() =>
            {
                // Load previous save
                var changes = LoadChanges();

                if (changes.recentEnemyDiedTo != "None" && (DateTime.Now - changes.lastEnemyTime).TotalSeconds > 120)
                {
                    changes.recentEnemyDiedTo = "None";
                    changes.lastEnemyTime = DateTime.Now;
                }

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
                string currentClass = ResolveFromTable(addrs.ClassOffsets, Addresses.ClassNames);
                string currentGender = ResolveFromTable(addrs.SexOffsets, Addresses.SexNames);
                string currentLocation = ResolveLocation(addrs.LocationOffsets);
                string currentWeapon = ResolveWeapon(addrs.Weapon1Offsets);
                string currentWeapon2 = ResolveWeapon(addrs.Weapon2Offsets);
                string currentWeapon3 = ResolveWeapon(addrs.Weapon3Offsets);
                string currentleftHand1 = ResolveWeapon(addrs.leftHand1Offset);
                string currentEnemy = changes.currentEnemy; // Previous value for current enemy

                string temp = ResolveEnemy();
                if (temp != "None")
                {
                    currentEnemy = temp;
                }

                // If this is the first time, assign the values to the previous stats
                if (changes.prevStats == null)
                {
                    changes.prevStats = new double[] { currentHP, currentMaxHP, currentGR, currentDeath, currentLevel, currentRunes };
                    changes.prevLocation = currentLocation;
                    changes.prevWeapon = currentWeapon;
                    changes.prevWeapon2 = currentWeapon2;
                    changes.prevWeapon3 = currentWeapon3;
                    changes.prevleftHand1 = currentleftHand1;
                    changes.visitedLocations = new HashSet<string>();
                    changes.visitedLocations.Add(currentLocation);
                    changes.prevWeapons = new HashSet<string>();
                    changes.prevWeapons.Add(currentWeapon);
                    changes.prevWeapons.Add(currentWeapon2);
                    changes.prevWeapons.Add(currentWeapon3);
                    changes.prevWeapons.Add(currentleftHand1);
                    changes.runes = false;
                    changes.currentEnemy = currentEnemy;
                    changes.pastEnemies = new HashSet<string>();
                    changes.defeatedEnemies = new HashSet<string>();
                    changes.recentEnemyDiedTo = "None"; // Initialize last enemy died to
                    changes.lastEnemyTime = DateTime.Now; // Initialize last enemy time
                    changes.pastEnemies.Add(currentEnemy);
                    changes.relationshipMelina = 10; // Initial relationship level with maiden
                    changes.relationshipRanni = 10;
                    changes.relationshipBlaidd = 10;
                    changes.relationshipMillicent = 10;
                    changes.relationshipMessmer = 10;
                    changes.relationshipSellen = 10;
                    changes.relationshipMalenia = 10;
                    changes.relationshipRecluse = 10;
                    changes.prevCharacter = c; // Store the character index

                    SaveChanges(changes);

                    sentiment = "general";

                    return new string[] { "Event detected: First time talking to you!\n" +
                    $"HP: {currentHP}\n" +
                    $"Max HP: {currentMaxHP}\n" +
                    $"Great Rune Active?: {currentGR}\n" +
                    $"Death Count: {currentDeath}\n" +
                    $"Player Name: {currentName}\n" +
                    $"Player level: {currentLevel}\n" +
                    $"Runes: {currentRunes}\n" +
                    $"Class: {currentClass}\n" +
                    $"Gender: {currentGender}\n" +
                    $"Location: {currentLocation}\n" +
                    $"Right Weapon: {currentWeapon}\n" +
                    $"Left Weapon: {currentleftHand1}\n"+
                    $"Current Enemy: {currentEnemy}\n"+
                    $"Your relationship to the Tarnished is welcoming. This is your first encournter with them.", sentiment};
                }
                else
                {
                    List<string> changesList = new();

                    var relationship = c switch
                    {
                        0 => changes.relationshipMelina,
                        1 => changes.relationshipRanni,
                        2 => changes.relationshipBlaidd,
                        3 => changes.relationshipMillicent,
                        4 => changes.relationshipMessmer,
                        5 => changes.relationshipSellen,
                        6 => changes.relationshipMalenia,
                        7 => changes.relationshipRecluse,
                        _ => changes.relationshipDefault // Default if character is unknown
                    };

                    double prevRelationship = relationship;

                    // Helper methods
                    void AddHPInfo()
                    {
                        changesList.Add($"HP: {currentHP}");
                        changesList.Add($"Max HP: {currentMaxHP}");
                    }

                    void AddWeaponInfo()
                    {
                        changesList.Add($"Right Weapon: {currentWeapon}\nLeft Weapon: {currentleftHand1}");
                    }

                    void AddEnemyInfo()
                    {
                        if (!string.IsNullOrEmpty(currentEnemy) && !currentEnemy.StartsWith("None"))
                            changesList.Add($"Current Enemy: {currentEnemy}");
                    }

                    void SetSentiment(string newSentiment)
                    {
                        if (sentiment != "worried" && sentiment != "death")
                            sentiment = newSentiment;
                    }

                    // Event: New Enemy Detected
                    if (!changes.pastEnemies.Contains(currentEnemy) && !currentEnemy.StartsWith("None") && !currentEnemy.Contains("Defeated"))
                    {
                        changesList.Add(PhrasePools.FormatRandom(PhrasePools.NewEnemy, currentEnemy));
                        AddWeaponInfo();
                        AddEnemyInfo();
                        changes.pastEnemies.Add(currentEnemy);
                        relationship += .5;
                        SetSentiment("general");
                    }

                    // Event: Enemy Defeated
                    if (!changes.defeatedEnemies.Contains(currentEnemy.Replace("Defeated ", "")) && currentEnemy.Contains("Defeated"))
                    {
                        currentEnemy = currentEnemy.Replace("Defeated ", ""); // Clean up the enemy name
                        changesList.Add(PhrasePools.FormatRandom(PhrasePools.DefeatedEnemy, currentEnemy));
                        AddWeaponInfo();
                        AddEnemyInfo();
                        changes.defeatedEnemies.Add(currentEnemy);
                        currentEnemy = "None"; // Reset current enemy
                        changes.currentEnemy = "None"; // Reset current enemy
                        relationship += 1;
                        sentiment = "impressed";
                    }

                    bool spokenOnHP = false;

                    // Event: Low HP or Death
                    bool hpLow = currentHP / currentMaxHP <= 0.25;
                    if (changes.prevStats[0] != currentHP && hpLow)
                    {
                        spokenOnHP = true;
                        if (currentHP == 0)
                        {
                            changesList.Add(PhrasePools.FormatRandom(PhrasePools.Death, currentName));
                            AddEnemyInfo();
                            AddWeaponInfo();
                            if (!currentEnemy.StartsWith("None"))
                            {
                                changes.recentEnemyDiedTo = currentEnemy; // Update last enemy died to
                                changes.lastEnemyTime = DateTime.Now; // Update last enemy time
                            }
                            changesList.Add($"Death Count: {currentDeath}");
                            AddHPInfo();
                            relationship -= 1;
                            sentiment = "death";
                        }
                        else
                        {
                            changesList.Add(PhrasePools.FormatRandom(PhrasePools.LowHP, currentHP, currentMaxHP));
                            AddEnemyInfo();
                            AddWeaponInfo();
                            AddHPInfo();
                            relationship -= .5;
                            SetSentiment("worried");
                        }
                    }

                    bool isRatioStable = Math.Abs((changes.prevStats[0] / changes.prevStats[1]) - (currentHP / currentMaxHP)) < 0.01;
                    double hpDropRatio = (changes.prevStats[0] - currentHP) / currentMaxHP;
                    if (changes.prevStats[0] != currentHP && hpDropRatio >= 0.25 && !spokenOnHP && !isRatioStable)
                    {
                        changesList.Add(PhrasePools.FormatRandom(PhrasePools.BigHPDrop, currentHP));
                        AddHPInfo();
                        AddEnemyInfo();
                        relationship -= .25;
                        AddWeaponInfo();
                        SetSentiment("worried");
                    }

                    if(currentDeath != changes.prevStats[3] && !spokenOnHP)
                    {
                        changesList.Add(PhrasePools.FormatRandom(PhrasePools.Death, currentName));
                        AddEnemyInfo();
                        AddWeaponInfo();
                        if (!currentEnemy.StartsWith("None"))
                        {
                            changes.recentEnemyDiedTo = currentEnemy; // Update last enemy died to
                            changes.lastEnemyTime = DateTime.Now; // Update last enemy time
                        }
                        changesList.Add($"Death Count: {currentDeath}");
                        relationship -= 1;
                        sentiment = "death";
                    }

                    // Event: Great Rune Activated
                    if (changes.prevStats[2] != currentGR && currentGR == 1)
                    {
                        changesList.Add(PhrasePools.FormatRandom(PhrasePools.GreatRuneActive));
                        AddHPInfo();
                        relationship += .5;
                        SetSentiment("impressed");
                    }

                    // Event: Level Up
                    if (changes.prevStats[4] != currentLevel)
                    {
                        changesList.Add(PhrasePools.FormatRandom(PhrasePools.LevelUp, currentLevel));
                        AddHPInfo();
                        relationship += currentLevel - changes.prevStats[4];
                        SetSentiment("impressed");
                    }

                    // Event: Enough Runes to Level Up
                    double runeMultiplier = Math.Max(0, ((currentLevel + 81) - 92) * 0.02);
                    double runeCost = (runeMultiplier + 0.1) * (Math.Pow(currentLevel + 81, 2) + 1);
                    if (currentRunes >= runeCost && !changes.runes)
                    {
                        changes.runes = true;
                        changesList.Add(PhrasePools.FormatRandom(PhrasePools.RunesReady));
                        changesList.Add($"Runes: {currentRunes}");
                        relationship += .5;
                        SetSentiment("impressed");
                    }
                    else if (currentRunes < runeCost)
                    {
                        changes.runes = false;
                    }

                    // Event: New Location
                    if (!changes.visitedLocations.Contains(currentLocation))
                    {
                        changesList.Add(PhrasePools.FormatRandom(PhrasePools.NewLocation,currentLocation));
                        changesList.Add($"Location: {currentLocation}");
                        changes.visitedLocations.Add(currentLocation);
                        relationship += .25;
                        SetSentiment("impressed");
                    }

                    // Event: New Right Weapon
                    if (!changes.prevWeapons.Contains(currentWeapon))
                    {
                        changesList.Add(PhrasePools.FormatRandom(PhrasePools.NewWeapon, currentWeapon, changes.prevWeapon));
                        AddWeaponInfo();
                        changes.prevWeapons.Add(currentWeapon);
                        changes.prevWeapon = currentWeapon;
                        relationship += .5;
                        SetSentiment("impressed");
                    }

                    // Event: New Left Weapon
                    if (!changes.prevWeapons.Contains(currentleftHand1))
                    {
                        changesList.Add(PhrasePools.FormatRandom(PhrasePools.NewWeapon, currentleftHand1, changes.prevleftHand1));
                        AddWeaponInfo();
                        changes.prevWeapons.Add(currentleftHand1);
                        changes.prevleftHand1 = currentleftHand1;
                        relationship += .5;
                        SetSentiment("impressed");
                    }

                    if (currentHP / currentMaxHP >= 0.75)
                        relationship += 0.002;
                    else if (currentHP / currentMaxHP < 0.25)
                        relationship -= 0.005;

                    // Final State Update
                    if (relationship > 115)
                    {
                        relationship = 115; // Cap relationship at 115
                    }
                    else if(relationship < -115)
                    {
                        relationship = -115; // Cap relationship at -115
                    }
                    switch (c)
                    {
                        case 0: changes.relationshipMelina = relationship; break;
                        case 1: changes.relationshipRanni = relationship; break;
                        case 2: changes.relationshipBlaidd = relationship; break;
                        case 3: changes.relationshipMillicent = relationship; break;
                        case 4: changes.relationshipMessmer = relationship; break;
                        case 5: changes.relationshipSellen = relationship; break;
                        case 6: changes.relationshipMalenia = relationship; break;
                        case 7: changes.relationshipRecluse = relationship; break;
                        default: changes.relationshipDefault = relationship; break;
                    }
                    changes.currentEnemy = ResolveEnemy();
                    changes.prevLocation = currentLocation;
                    changes.prevWeapon = currentWeapon;
                    changes.prevWeapon2 = currentWeapon2;
                    changes.prevWeapon3 = currentWeapon3;
                    changes.prevleftHand1 = currentleftHand1;
                    changes.prevStats = new double[] { currentHP, currentMaxHP, currentGR, currentDeath, currentLevel, currentRunes };

                    string bondInstruction = relationship switch
                    {
                        >= 90 => "The tone of the response to these events should be with deep warmth, loyalty, and unwavering trust.",
                        >= 55 => "The tone of the response to these events should be as a devoted ally—steady, proud, and caring.",
                        >= 30 => "The tone of the response to these events should be with support and mild affection, but some restraint.",
                        >= 20 => "The tone of the response to these events should be politely, but keep your tone guarded and formal.",
                        >= -10 => "The tone of the response to these events should be plainly and with little emotion—detached but civil.",
                        >= -25 => "The tone of the response to these events should be with suspicion and emotional distance.",
                        >= -40 => "The tone of the response to these events should be warily, hinting at distrust and disappointment.",
                        >= -50 => "The tone of the response to these events should be coldly, as if addressing someone unreliable.",
                        >= -75 => "The tone of the response to these events should be with contempt as if forced to be here.",
                        _ => "The tone of the response to these events should be with open hostility—scornful, biting, and distrustful. You hate the Tarnished's unreliability."
                    };

                    if (changes.prevCharacter != c)
                    {
                        // Character switched — do not run relationship change logic
                        changes.prevCharacter = c;
                        SaveChanges(changes);
                    }
                    else
                    {
                        // Check for tier change
                        int prevTier = (int)Math.Floor(prevRelationship / 10.0);
                        int newTier = (int)Math.Floor(relationship / 10.0);

                        if (prevTier != newTier)
                        {
                            string? tierChangeMessage = (newTier > prevTier) switch
                            {
                                // Upward movement (relationship improved)
                                true => newTier switch
                                {
                                    10 => "Your bond has reached its ultimate strength. Tell the Tarnished you have grown to love them deeply.",
                                    9 => "Your loyalty is nearly unbreakable. Express unwavering support, care, and commitment.",
                                    8 => "You grown to care and trust the Tarnished deeply. Tell them you believe in their worth and purpose.",
                                    7 => "You genuinely respect their achievements. Praise their recent successes openly. Let them know you are starting to feel close to them.",
                                    6 => "Your a flicker of something more between you two. Encourage them to keep progressing confidently.",
                                    5 => "You feel warmth toward them. Offer kind and hopeful words.",
                                    4 => "You recognize their effort. Acknowledge their hard work and persistence.",
                                    3 => "You begin to believe in their potential. Share cautious optimism.",
                                    2 => "You feel more comfortable. Speak neutrally but with subtle encouragement.",
                                    1 => "You’re still cautious but willing to guide. Give impartial advice.",
                                    0 => "You remain indifferent. Respond without personal attachment.",
                                    -1 => "You start letting go of bitterness. Respond with guarded neutrality.",
                                    -2 => "You see a crack in your distrust. Admit they aren’t useless.",
                                    -3 => "Your resentment fades. Hint at reconsidering your stance.",
                                    -4 => "You feel a momentary respect. Reduce insults and show minimal courtesy.",
                                    -5 => "Your hostility weakens. Speak with reluctant acceptance.",
                                    -6 => "Your scorn diminishes. Let some civility show.",
                                    -7 => "Your dislike lessens. Speak tersely but politely.",
                                    -8 => "Your loathing softens. Offer pragmatic advice rather than spite.",
                                    -9 => "Your hatred lessens to disdain. Be politely perfunctory.",
                                    -10 => "You loosen your vengeance. Speak with cautious tolerance.",
                                    _ => "Your bond remains full of love and trust. Let them know of your love and admiration to them."
                                },

                                // Downward movement (relationship worsened)
                                false => newTier switch
                                {
                                    10 => "Your bond was strong but took a hit. Admit hurt but vow to remain loyal.",
                                    9 => "Your loyalty weakens slightly. Express concern but stay committed.",
                                    8 => "Your trust declines. Warn the Tarnished carefully.",
                                    7 => "You respect them less. Voice mild disappointment.",
                                    6 => "Your approval fades. Give cautious critiques.",
                                    5 => "Your faith falters. Avoid praise and speak carefully.",
                                    4 => "Your tolerance thins. Be curt and civil.",
                                    3 => "Your optimism dims. Give only factual information.",
                                    2 => "You grow colder. Speak plainly and without warmth.",
                                    1 => "You become wary. Anticipate their mistakes in your words.",
                                    0 => "You detach emotionally. Give instructions without feeling.",
                                    -1 => "You grow distant. Show emotional reservation.",
                                    -2 => "You lose trust. Express somber doubt.",
                                    -3 => "You feel disappointment. Speak with disapproval.",
                                    -4 => "You grow resentful. Let your words be sharp.",
                                    -5 => "Distrust blooms. Speak coldly and suspiciously.",
                                    -6 => "Scorn grows. Be openly disdainful.",
                                    -7 => "Dislike surfaces. Speak with blunt contempt.",
                                    -8 => "Loathing emerges. Use cutting words.",
                                    -9 => "Hatred grips you. Speak with raw anger.",
                                    -10 => "You seek vengeance for their incompetence. Show no mercy.",
                                    _ => "Your bond remains steeped in rage, disappointment, and sorrow."

                                }
                            };

                            if (!string.IsNullOrEmpty(tierChangeMessage))
                                changesList.Add($"Relationship tier shift detected:\n{tierChangeMessage}");
                        }
                    }
                    // Save and Return
                    SaveChanges(changes);
                    return changesList.Count > 0
                        ? new[] { $"Player Name: {currentName}\nEvent detected: {string.Join("\n", changesList)} \n{bondInstruction}", sentiment }
                        : new[] { "No changes detected." };
                }
            });
        }



        public Task<string> relationshipCheck(Addresses.AddressesSet addresses, int c)
        {
            return Task.Run(() =>
            {
                var changes = LoadChanges();
                var relationship = c switch
                {
                    0 => changes.relationshipMelina,
                    1 => changes.relationshipRanni,
                    2 => changes.relationshipBlaidd,
                    3 => changes.relationshipMillicent,
                    4 => changes.relationshipMessmer,
                    5 => changes.relationshipSellen,
                    6 => changes.relationshipMalenia,
                    7 => changes.relationshipRecluse,
                    _ => changes.relationshipDefault // Default if character is unknown
                };

                if (changes.prevCharacter != c)
                {
                    // Character switched — do not run relationship change logic
                    changes.prevCharacter = c;
                    changes.prevRelationship = relationship; // Reset to current character's relationship
                    SaveChanges(changes);
                }
                else
                {
                    // Check for tier change
                    int prevTier = (int)Math.Floor(changes.prevRelationship / 10.0);
                    int newTier = (int)Math.Floor(relationship / 10.0);

                    changes.prevRelationship = relationship;
                    SaveChanges(changes);

                    if (prevTier != newTier)
                    {
                        string? tierChangeMessage = (newTier > prevTier) switch
                        {
                            // Upward movement (relationship improved)
                            true => "Your bond to the Tarnished grows. Tell them this and the nature of your current bond. " + newTier switch
                            {
                                10 => "Your bond has reached its ultimate strength. Tell the Tarnished you have grown to love them deeply.",
                                9 => "Your loyalty is nearly unbreakable. Express unwavering support, care, and commitment.",
                                8 => "You grown to care and trust the Tarnished deeply. Tell them you believe in their worth and purpose.",
                                7 => "You genuinely respect their achievements. Praise their recent successes openly. Let them know you are starting to feel close to them.",
                                6 => "Your a flicker of something more between you two. Encourage them to keep progressing confidently.",
                                5 => "You feel warmth toward them. Offer kind and hopeful words.",
                                4 => "You recognize their effort. Acknowledge their hard work and persistence.",
                                3 => "You begin to believe in their potential. Share cautious optimism.",
                                2 => "You feel more comfortable. Speak neutrally but with subtle encouragement.",
                                1 => "You’re still cautious but willing to guide. Give impartial advice.",
                                0 => "You remain indifferent. Respond without personal attachment.",
                                -1 => "You start letting go of bitterness. Respond with guarded neutrality.",
                                -2 => "You see a crack in your distrust. Admit they aren’t useless.",
                                -3 => "Your resentment fades. Hint at reconsidering your stance.",
                                -4 => "You feel a momentary respect. Reduce insults and show minimal courtesy.",
                                -5 => "Your hostility weakens. Speak with reluctant acceptance.",
                                -6 => "Your scorn diminishes. Let some civility show.",
                                -7 => "Your dislike lessens. Speak tersely but politely.",
                                -8 => "Your loathing softens. Offer pragmatic advice rather than spite.",
                                -9 => "Your hatred lessens to disdain. Be politely perfunctory.",
                                -10 => "You loosen your vengeance. Speak with cautious tolerance.",
                                _ => "Your bond remains full of love and trust. Let them know of your love and admiration to them."
                            },

                            // Downward movement (relationship worsened)
                            false => "Your bond to the Tarnished falls. Tell them this and the nature of your current bond. " + newTier switch
                            {
                                10 => "Your bond was strong but took a hit. Admit hurt but vow to remain loyal.",
                                9 => "Your loyalty weakens slightly. Express concern but stay committed.",
                                8 => "Your trust declines. Warn the Tarnished carefully.",
                                7 => "You respect them less. Voice mild disappointment.",
                                6 => "Your approval fades. Give cautious critiques.",
                                5 => "Your faith falters. Avoid praise and speak carefully.",
                                4 => "Your tolerance thins. Be curt and civil.",
                                3 => "Your optimism dims. Give only factual information.",
                                2 => "You grow colder. Speak plainly and without warmth.",
                                1 => "You become wary. Anticipate their mistakes in your words.",
                                0 => "You detach emotionally. Give instructions without feeling.",
                                -1 => "You grow distant. Show emotional reservation.",
                                -2 => "You lose trust. Express somber doubt.",
                                -3 => "You feel disappointment. Speak with disapproval.",
                                -4 => "You grow resentful. Let your words be sharp.",
                                -5 => "Distrust blooms. Speak coldly and suspiciously.",
                                -6 => "Scorn grows. Be openly disdainful.",
                                -7 => "Dislike surfaces. Speak with blunt contempt.",
                                -8 => "Loathing emerges. Use cutting words.",
                                -9 => "Hatred grips you. Speak with raw anger.",
                                -10 => "You seek vengeance for their incompetence. Show no mercy.",
                                _ => "Your bond remains steeped in rage, disappointment, and sorrow."
                            }
                        };

                        return $"The nature of your bond to the Tarnished has shifted. {tierChangeMessage}" ?? "No relationship tier change detected.";
                    }
                }
                return "No relationship tier change detected.";
            });
        }

        static class PhraseHelper
        {
            private static readonly Random _rand = new();

            public static string Random(IList<string> options)
                => options[_rand.Next(options.Count)];
        }

        static class PhrasePools
        {
            public static readonly List<string> LowHP = new()
    {
        "The Tarnished's strength wanes. HP is critically low: {0}/{1}.",
        "The Tarnished bleeds. Only {0} HP remains.",
        "Critical HP warning: {0} of {1}.",
        "Their strength falters—just {0} HP left.",
        "The Tarnished is in peril with {0} HP remaining."
    };

            public static readonly List<string> Death = new()
    {
        "The Tarnished has fallen in battle.",
        "The Tarnished perishes before your eyes.",
        "Death claims the Tarnished.",
        "The Tarnished lies broken and still.",
        "The Tarnished meets their end once more."
    };

            public static readonly List<string> NewEnemy = new()
    {
        "A new adversary emerges: {0}.",
        "Beware—the Tarnished faces {0}.",
        "The path is challenged by {0}.",
        "Prepare: {0} draws near.",
        "Enemy encountered: {0}."
    };

            public static readonly List<string> DefeatedEnemy = new()
    {
        "The Tarnished strikes true—{0} falls.",
        "Victory! {0} is defeated.",
        "{0} no longer stands.",
        "The Tarnished overcomes {0}.",
        "{0} meets their demise."
    };

            public static readonly List<string> NewWeapon = new()
    {
        "The Tarnished equips new armament: {0}, Which replaces {1}",
        "A fresh weapon enters their hand, {0}, Replacing {1}",
        "They wield {0} now, which replaces {1}",
        "{0} replaces the previous weapon, {1}.",
        "Armed anew with {0}, which replaced {1}"
    };

            public static readonly List<string> BigHPDrop = new()
    {
        "The Tarnished reels from a heavy blow—HP dropped to {0}.",
        "A crushing strike! HP fell to {0}.",
        "They stagger as their HP plunges to {0}.",
        "HP sharply declines: {0} remaining.",
        "The Tarnished’s vitality drops to {0}."
    };

            public static readonly List<string> GreatRuneActive = new()
    {
        "A Great Rune glows with power—activated at last.",
        "The Tarnished's Great Rune is now active.",
        "Might surges as the Great Rune awakens.",
        "Great Rune activation confirmed.",
        "The power of the Great Rune flows through the Tarnished."
    };

            public static readonly List<string> LevelUp = new()
    {
        "The Tarnished grows stronger—level increased to {0}.",
        "Level up! They are now level {0}.",
        "Experience heralds new strength: level {0}.",
        "Their resolve deepens at level {0}.",
        "They ascend to level {0}."
    };

            public static readonly List<string> RunesReady = new()
    {
        "The Tarnished has gathered enough runes to ascend.",
        "Runes amassed—prepared for the next level.",
        "They hold sufficient runes to grow stronger.",
        "Rune threshold reached for advancement.",
        "They are ready to invest runes for power."
    };

            public static readonly List<string> NewLocation = new()
    {
        "A new land revealed: {0}.",
        "The Tarnished arrives at {0}.",
        "Location discovered: {0}.",
        "Their journey leads them to {0}.",
        "They step into {0}."
    };

            public static string FormatRandom(IList<string> pool, params object[] args)
                => string.Format(PhraseHelper.Random(pool), args);
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
            long baseId = wepVal - (wepVal % 100); // Removes upgrade level

            if (WeaponData.WeaponMap.TryGetValue(baseId, out var weaponName))
            {
                int upgradeLevel = (int)(wepVal % 100);
                return $"{weaponName} +{upgradeLevel}";
            }
            else
            {
                return "Unknown";
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
            public int[] leftHand1Offset;
        }

        public static AddressesSet GetAddresses()
        {
            //GameDataMan  = 0x3D5DF38
            //WorldChrMan = 0x3D65F88
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
                Weapon3Offsets = new[] { 0x3D5DF38, 0x08, 0x3AC },
                leftHand1Offset = new[] { 0x3D5DF38, 0x08, 0x398 },
            };
        }
    }
}