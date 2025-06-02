using System.Diagnostics;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media; // Needed for Matrix
using System.Windows.Media.Animation;
using System.Windows.Threading;
using EldenEncouragement;
using EldenTTS;

namespace EldenRingOverlay
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer checkEldenRingTimer;
        private int missingCounter = 0;
        private bool speaking = false;
        private const int MissingThreshold = 35;
        private double screenWidth;
        private double screenHeight;
        private int duration = 5;
        // Global or static variable to remember the last known ID
        private static int? lastCharacterId = null;

        // Win32 constants
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TOPMOST = 0x00000008;

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        // ShowWindow flags
        const int SW_HIDE = 0;
        const int SW_SHOWNA = 8;  // Show window, do not activate

        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        public static extern bool AllocConsole();
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var hwnd = new WindowInteropHelper(this).Handle;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

            // Combine flags:
            // WS_EX_LAYERED   → Allows transparency 
            // WS_EX_TRANSPARENT → Click-through
            // WS_EX_NOACTIVATE  → Never take focus
            // WS_EX_TOPMOST     → Always on top
            exStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE;
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);

            // Ensure WPF doesn’t try to activate it
            ShowActivated = false;
            Focusable = false;
        }

        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

        [DllImport("user32.dll")]
        static extern bool IsWindowVisible(IntPtr hwnd);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public MainWindow()
        {
            EnsureAdministrator();
            //Cap all WPF animations (and render passes) to 30 FPS
            Timeline.DesiredFrameRateProperty.OverrideMetadata(
                typeof(Timeline),
                new FrameworkPropertyMetadata { DefaultValue = 30 }
            );
            AllocConsole(); // Shows console window
            InitializeComponent();
            InitializeOverlay();
            GetScreenSize();
            StartEldenRingLauncher();
            StartMonitoringEldenRing();
            // Start the helper process to read data from Elden Ring
            CheatEngineHelper helper = new CheatEngineHelper();
            helper.RunHelperAndReadData(); // Start the helper process to read Elden Ring data
            Task.Delay(15000).Wait(); // Wait for the helper to initialize
        }

        [DllImport("libc")]
        private static extern uint geteuid();
        private static void EnsureAdministrator()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = Process.GetCurrentProcess().MainModule.FileName,
                        UseShellExecute = true,
                        Verb = "runas" // triggers UAC
                    };

                    try
                    {
                        Process.Start(psi);
                        Console.WriteLine("Restarting with administrator privileges...");
                    }
                    catch
                    {
                        Console.Error.WriteLine("Administrator privileges are required.");
                    }

                    Environment.Exit(0);
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                if (geteuid() != 0)
                {
                    Console.Error.WriteLine("Please run this program as root (e.g., using sudo).");
                    Environment.Exit(1);
                }
            }
        }
        private void StartEldenRingLauncher()
        {
            // Get the overlay's directory (e.g. "C:\Games\ELDEN RING\Game\SoulWeapon\")
            string overlayDir = AppDomain.CurrentDomain.BaseDirectory;
            Console.WriteLine($"Overlay directory: {overlayDir}");

            // Trim trailing directory separators so GetParent returns the correct parent
            overlayDir = overlayDir.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);

            // Get the parent directory (should be "C:\Games\ELDEN RING\Game")
            string parentDir = Directory.GetParent(overlayDir)?.FullName;
            Console.WriteLine($"Parent directory: {parentDir}");
            if (parentDir == null)
            {
                MessageBox.Show("Failed to determine Elden Ring directory.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string batFile = System.IO.Path.Combine(parentDir ?? "", "launchmod_eldenring.bat");
            string exeFile = System.IO.Path.Combine(parentDir ?? "", "modengine2_launcher.exe");

            _ = TryLaunchGame(batFile, exeFile);
        }

        private async Task TryLaunchGame(string batFile, string exeFile)
        {
            int tryAndRun = 0;
            int hasLaunchFiles = 0;

            while (tryAndRun < 3)
            {
                if (File.Exists(batFile))
                {
                    hasLaunchFiles++;
                    Console.WriteLine("Attempting to launch Elden Ring");
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = batFile,
                        UseShellExecute = true,
                        WorkingDirectory = System.IO.Path.GetDirectoryName(batFile)
                    });
                    await Task.Delay(10000); // Increased delay for safety
                    if (IsEldenRingRunning()) return;
                }

                if (File.Exists(exeFile))
                {
                    //hasLaunchFiles++;
                    Console.WriteLine("Attempting to launch Elden Ring with exe file");
                    Process.Start(new ProcessStartInfo { FileName = exeFile, UseShellExecute = true });
                    await Task.Delay(10000); // Increased delay for safety
                    if (IsEldenRingRunning()) return;
                }

                tryAndRun++;
            }

            if (hasLaunchFiles > 0)
            {
                MessageBox.Show("Failed to start Elden Ring after multiple attempts. Try launching it manually, then launching EldenOverlay.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                MessageBox.Show("Ignore if game is already open:\nlaunchmod_eldenring.bat not found. Ensure launchmod_eldenring.bat or modengine2_launcher.exe are in the correct directory.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }


        private void StartMonitoringEldenRing()
        {
            var helper = new CheatEngineHelper();
            checkEldenRingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            checkEldenRingTimer.Tick += async (s, e) =>
            {
                if (IsEldenRingRunning())
                {
                    missingCounter = 0;
                }
                else
                {
                    missingCounter++;
                    if (missingCounter >= MissingThreshold)
                    {
                        helper.StopHelper(); // Stop the helper process if Elden Ring is not running
                        Application.Current.Shutdown();
                    }
                }
            };
            checkEldenRingTimer.Start();
        }

        private bool IsEldenRingRunning()
        {
            return Process.GetProcessesByName("eldenring").Length > 0;
        }


        /// <summary>
        /// Sets up the overlay window and starts a timer that checks fullscreen status.
        /// </summary>
        MaidenReader reader = new MaidenReader();
        bool positioned = false;
        private void InitializeOverlay()
        {
            // Base overlay setup
            WindowStyle = WindowStyle.None;
            Background = Brushes.Transparent;
            //Topmost = false;

            DateTime lastEventTime = DateTime.MinValue;
            bool isFullscreen = false;

            // Set the font size and duration from settings.ini
            setFont();

            // Read interval from settings.ini
            int intervalESeconds = 300; // default
            var iniLines = File.ReadAllLines("settings.ini");
            foreach (var line in iniLines)
            {
                if (line.Trim().StartsWith("AIEventResponse="))
                {
                    var value = line.Split('=')[1].Trim();
                    if (int.TryParse(value, out int result))
                        intervalESeconds = Math.Max(0, result); // Ensure 0 second or more
                    break;
                }
            }

            // Read character from settings.ini
            int character = 0; // default
            var Lines = File.ReadAllLines("settings.ini");
            foreach (var line in Lines)
            {
                if (line.Trim().StartsWith("Character="))
                {
                    var value = line.Split('=')[1].Trim();
                    if (int.TryParse(value, out int result))
                        character = Math.Max(0, Math.Min(result,5)); // Ensure 0 - 5
                    break;
                }
            }
            var tts = new EldenTTS.EldenTTS("settings.ini");

            // Fullscreen check every second
            var fsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            fsTimer.Tick += async (s, e) =>
            {
                try
                {
                    isFullscreen = CheckFullscreenStatus();
                    if (isFullscreen && !positioned)
                    {
                        Console.WriteLine("Elden Ring is in fullscreen mode. Positioning overlay.");
                        PositionOverlayRelativeToGameWindow(new RECT(),character);
                        positioned = true;
                    }
                    else if (!isFullscreen)
                    {
                        //Console.WriteLine("Elden Ring is not in fullscreen mode. Hiding overlay.");
                        positioned = false;
                    }
                    if (isFullscreen && (DateTime.Now - lastEventTime).TotalSeconds >= intervalESeconds && speaking == false && character != -1)
                    {
                        speaking = true;
                        bool spoke = await GetEvent(character, tts);
                        if (spoke)
                        {
                            lastEventTime = DateTime.Now;
                        }
                    }
                    else if (isFullscreen && intervalESeconds>0)
                    {
                        await reader.UpdateEvent();
                    }
                    if (isFullscreen)
                    {
                        character = await CheckCharacterSwitch(character);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in fullscreen check: {ex.Message}");
                }
            };
            fsTimer.Start();

            // Read interval from settings.ini
            int intervalSeconds = 300; // default
            foreach (var line in iniLines)
            {
                if (line.Trim().StartsWith("AIGeneralResponse="))
                {
                    var value = line.Split('=')[1].Trim();
                    if (int.TryParse(value, out int result))
                        intervalSeconds = Math.Max(0, result); // Ensure 0 second or more
                    break;
                }
            }
            // get encouragement text
            var gtTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(intervalSeconds) };
            gtTimer.Tick += async (s, e) =>
            {
                try
                {
                    if (!isFullscreen)
                    {
                        Console.WriteLine("Elden Ring is not in fullscreen mode. Skipping encouragement text.");
                        return;
                    }
                    if (!speaking && character != -1)
                    {
                        speaking = true;
                        await GetEncouragement(character, tts);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in fullscreen check: {ex.Message}");
                }
            };
            gtTimer.Start();
        }

        /// <summary>
        /// Checks if the Elden Ring window is in fullscreen mode (using DPI-adjusted screen size) 
        /// and shows or hides the overlay accordingly.
        /// </summary>
        private bool CheckFullscreenStatus()
        {
            Process[] processes = Process.GetProcessesByName("eldenring");
            IntPtr hwnd = IntPtr.Zero;
            if (processes.Length > 0)
            {
                Process gameProc = processes[0];

                // Wait for MainWindowHandle to be valid
                int attempts = 0;
                while (hwnd == IntPtr.Zero && attempts++ < 10)
                {
                    gameProc.Refresh();
                    hwnd = gameProc.MainWindowHandle;
                    Thread.Sleep(500);
                }
            }

            if (hwnd != IntPtr.Zero && IsWindowVisible(hwnd))
            {
                GetWindowRect(hwnd, out RECT rect);
                int windowWidth = rect.Right - rect.Left;
                int windowHeight = rect.Bottom - rect.Top;

                PresentationSource source = PresentationSource.FromVisual(this);
                double dpiFactorX = 1.0, dpiFactorY = 1.0;
                if (source != null)
                {
                    Matrix m = source.CompositionTarget.TransformToDevice;
                    dpiFactorX = m.M11;
                    dpiFactorY = m.M22;
                }
                double physicalScreenWidth = SystemParameters.PrimaryScreenWidth * dpiFactorX;
                double physicalScreenHeight = SystemParameters.PrimaryScreenHeight * dpiFactorY;

                Console.WriteLine($"Checking fullscreen: Window size: {windowWidth} x {windowHeight}");
                Console.WriteLine($"Physical screen size: {physicalScreenWidth} x {physicalScreenHeight}");

                // Check if the game is in fullscreen mode
                if (windowWidth == physicalScreenWidth && windowHeight == physicalScreenHeight)
                {
                    //Console.WriteLine("Elden Ring is in fullscreen mode.");
                    if (!this.IsVisible)
                    {
                        Console.WriteLine("Showing overlay.");
                        this.Show();
                        return true;
                    }

                    // Force overlay to remain topmost

                    //this.Topmost = true;

                    return true;
                }
                else
                {
                    //Console.WriteLine("Elden Ring is not in fullscreen mode. Hiding overlay.");
                    //this.Hide();
                    return false;


                }
            }
            else
            {
                Console.WriteLine("Elden Ring window not found, hiding overlay.");
                //this.Hide();
                return false;

            }
        }

        private async Task<int> CheckCharacterSwitch(int character)
        {
            // Read character from settings.ini
            string userProfile = Environment.GetEnvironmentVariable("USERPROFILE");
            string outputDir = Path.Combine(userProfile, "Documents", "EldenHelper");
            // Read the output file created by Lua
            string outputFile = Path.Combine(outputDir, "saveComp.txt");
            if (File.Exists(outputFile))
            {
                character = File.ReadAllText(outputFile).Trim().Length > 0 ? int.Parse(File.ReadAllText(outputFile).Trim()) : character;
            }
            // If the character has changed, update the last known ID
            if (lastCharacterId != character && character != -1)
            {
                lastCharacterId = character;
                this.Topmost = false;
                this.Topmost = true;

                speaking = true;

                // Read voice from settings.ini
                int voice = 1; // default
                var iniLines = File.ReadAllLines("settings.ini");
                foreach (var line in iniLines)
                {
                    if (line.Trim().StartsWith("Voice="))
                    {
                        var value = line.Split('=')[1].Trim();
                        if (int.TryParse(value, out int result))
                            voice = Math.Min(1, Math.Max(0, result)); // Clamp between 0–1
                        break;
                    }
                }

                var sentence = "Lets have a good journey, Tarnished!";

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    UpdateSubtitle(sentence);
                }));

                Random r = new Random();
                int fileNumber = r.Next(1, 6);
                string wavFilePath = $@"Audio\{character}_general_{fileNumber}.wav";
                // play random of 5 files when speaking
                                        
                if (File.Exists(wavFilePath) && voice == 1)
                {
                    var player = new SoundPlayer(wavFilePath);
                    player.Play();
                }

                await FadeTextBlock(AIEncouragement, fadeIn: true);
                await Task.Delay(duration * 1000);
                await FadeTextBlock(AIEncouragement, fadeIn: false);
                
                speaking = false;
                
            }
            return character;
        }

        private void setFont()
        {
            // Set font size
            string[] iniLines = File.ReadAllLines("settings.ini");
            foreach (var line in iniLines)
            {
                if (line.Trim().StartsWith("FontSize="))
                {
                    var value = line.Split('=')[1].Trim();
                    if (int.TryParse(value, out int result))
                        AIEncouragement.FontSize = Math.Max(1, result); // Clamp 1
                    break;
                }
            }
            foreach (var line in iniLines)
            {
                if (line.Trim().StartsWith("Duration="))
                {
                    var value = line.Split('=')[1].Trim();
                    if (int.TryParse(value, out int result))
                        duration = Math.Max(0, result); // Clamp 0
                    break;
                }
            }
        }

        static string[] SplitIntoSentences(string text)
        {
            // define every character you want to treat as “end of sentence”:
            var terminators = new HashSet<char> { '.', '!', '?', '\u3002'};

            var list = new List<string>();
            var sb = new StringBuilder();

            foreach (var c in text)
            {
                sb.Append(c);
                if (terminators.Contains(c))
                {
                    var s = sb.ToString().Trim();
                    if (s.Length > 0)
                        list.Add(s);
                    sb.Clear();
                }
            }

            // catch any trailing text without a final terminator
            var tail = sb.ToString().Trim();
            if (tail.Length > 0)
                list.Add(tail);

            return list.ToArray();
        }

        private async Task GetEncouragement(int c, EldenTTS.EldenTTS tts)
        {
            string text = await reader.GetEncouragement(c);

            // Split on punctuation + *any* whitespace (including newline)
            var sentences = SplitIntoSentences(text);

            //this.Topmost = false;
            //this.Topmost = true;
            if (string.IsNullOrWhiteSpace(text))
            {
                AIEncouragement.Text = "I'm confused, forgive me...";
                Console.WriteLine("Failed to read encouragement text.");
            }
            else
            {
                this.Topmost = false;
                this.Topmost = true;

                string userProfile = Environment.GetEnvironmentVariable("USERPROFILE");
                string outputDir = Path.Combine(userProfile, "Documents", "EldenHelper");
                // Read the output file created by Lua
                string outputFile = Path.Combine(outputDir, "saveComp.txt");
                if (File.Exists(outputFile))
                {
                    c = File.ReadAllText(outputFile).Trim().Length > 0 ? int.Parse(File.ReadAllText(outputFile).Trim()) : c;
                }

                // Read voice from settings.ini
                int voice = 1; // default
                var iniLines = File.ReadAllLines("settings.ini");
                foreach (var line in iniLines)
                {
                    if (line.Trim().StartsWith("Voice="))
                    {
                        var value = line.Split('=')[1].Trim();
                        if (int.TryParse(value, out int result))
                            voice = Math.Min(1, Math.Max(0, result)); // Clamp between 0–1
                        break;
                    }
                }

                int temp = -1;
                bool playSpecial = true;
                foreach (var raw in sentences)
                {
                    var sentence = raw.Trim();
                    if (sentence == "") continue;

                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        UpdateSubtitle(sentence);
                    }));


                    Random r = new Random();
                    int fileNumber = r.Next(1, 6);
                    string wavFilePath = $@"Audio\{c}_omp_{fileNumber}.wav";
                    // play random of 5 files when speaking
                    if (playSpecial)
                    {
                        playSpecial = false;
                        wavFilePath = $@"Audio\{c}_general_{fileNumber}.wav";
                    }
                    else
                    {
                        while (temp == fileNumber)
                        {
                            fileNumber = r.Next(1, 6);
                        }
                        wavFilePath = $@"Audio\{c}_omp_{fileNumber}.wav";
                        temp = fileNumber;  
                    }

                    // Read azure from settings.ini
                    var iniLine = File.ReadAllLines("settings.ini");

                    foreach (var line in iniLines)
                    {
                        if (line.Trim().StartsWith("azure_key="))
                        {
                            var value = line.Split('=')[1].Trim();
                            if (!string.IsNullOrWhiteSpace(value))
                                await tts.SynthesizeToFileAsync(sentence, "output.wav");
                                var player = new SoundPlayer("output.wav");
                                player.Play();
                            break;
                        }
                    }
                    

                    if (File.Exists(wavFilePath) && voice == 1)
                    {
                        var player = new SoundPlayer(wavFilePath);
                        player.Play();
                    }

                    await FadeTextBlock(AIEncouragement, fadeIn: true);
                    await Task.Delay(duration*1000);
                    await FadeTextBlock(AIEncouragement, fadeIn: false);
                }
                speaking = false;
            }
        }

        private async Task<bool> GetEvent(int c, EldenTTS.EldenTTS tts)
        {
            string[] text = await reader.GetEvent(c);

            // If we only got one element back, there was no "real" event
            if (text.Length < 2 || text[0] == "No changes detected.")
            {
                speaking = false;
                return false;
            }

            // Split on punctuation + *any* whitespace (including newline)
            var sentences = SplitIntoSentences(text[0]);
            var sentiment = text[1];

            if (string.IsNullOrWhiteSpace(text[0]))
            {
                AIEncouragement.Text = "I'm confused, forgive me...";
                Console.WriteLine("Failed to read event text.");
            }
            else
            {
                this.Topmost = false;
                this.Topmost = true;

                string userProfile = Environment.GetEnvironmentVariable("USERPROFILE");
                string outputDir = Path.Combine(userProfile, "Documents", "EldenHelper");
                // Read the output file created by Lua
                string outputFile = Path.Combine(outputDir, "saveComp.txt");
                if (File.Exists(outputFile))
                {
                    c = File.ReadAllText(outputFile).Trim().Length > 0 ? int.Parse(File.ReadAllText(outputFile).Trim()) : c;
                }

                // Read voice from settings.ini
                int voice = 1; // default
                var iniLines = File.ReadAllLines("settings.ini");
                foreach (var line in iniLines)
                {
                    if (line.Trim().StartsWith("Voice="))
                    {
                        var value = line.Split('=')[1].Trim();
                        if (int.TryParse(value, out int result))
                            voice = Math.Min(1, Math.Max(0, result)); // Clamp between 0–1
                        break;
                    }
                }

                int temp = -1;
                var playSpecial = true;
                foreach (var raw in sentences)
                {
                    var sentence = raw.Trim();
                    if (sentence == "") continue;

                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        UpdateSubtitle(sentence);
                    }));


                    Random r = new Random();
                    int fileNumber = r.Next(1, 6);
                    string wavFilePath = $@"Audio\{c}_omp_{fileNumber}.wav";
                    // play random of 5 files when speaking
                    if (playSpecial)
                    {
                        playSpecial = false;
                        wavFilePath = $@"Audio\{c}_{sentiment}_{fileNumber}.wav";
                    }
                    else
                    {
                        while (temp == fileNumber)
                        {
                            fileNumber = r.Next(1, 6);
                        }
                        wavFilePath = $@"Audio\{c}_omp_{fileNumber}.wav";
                        temp = fileNumber;
                    }

                    // Read azure from settings.ini
                    var iniLine = File.ReadAllLines("settings.ini");

                    foreach (var line in iniLines)
                    {
                        if (line.Trim().StartsWith("azure_key="))
                        {
                            var value = line.Split('=')[1].Trim();
                            if (!string.IsNullOrWhiteSpace(value))
                                await tts.SynthesizeToFileAsync(sentence, "output.wav");
                                var player = new SoundPlayer("output.wav");
                                player.Play();
                                break;
                        }
                    }

                    if (File.Exists(wavFilePath) && voice == 1)
                    {
                        var player = new SoundPlayer(wavFilePath);
                        player.Play();
                    }

                    await FadeTextBlock(AIEncouragement, fadeIn: true);
                    await Task.Delay(duration * 1000);
                    await FadeTextBlock(AIEncouragement, fadeIn: false);
                }
                speaking = false;
            }
            return true;
        }

        private async Task Welcome(int c)
        {
            string text = "Welcome back, tarnished";

            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateSubtitle(text);
            }));

            string userProfile = Environment.GetEnvironmentVariable("USERPROFILE");
            string outputDir = Path.Combine(userProfile, "Documents", "EldenHelper");
            // Read the output file created by Lua
            string outputFile = Path.Combine(outputDir, "saveComp.txt");
            if (File.Exists(outputFile))
            {
                c = File.ReadAllText(outputFile).Trim().Length > 0 ? int.Parse(File.ReadAllText(outputFile).Trim()) : c;
            }
            lastCharacterId = c; // Update last known ID
            // Read voice from settings.ini
            int voice = 1; // default
            var iniLines = File.ReadAllLines("settings.ini");
            foreach (var line in iniLines)
            {
                if (line.Trim().StartsWith("Voice="))
                {
                    var value = line.Split('=')[1].Trim();
                    if (int.TryParse(value, out int result))
                        voice = Math.Min(1, Math.Max(0, result)); // Clamp between 0–1
                    break;
                }
            }

            if (File.Exists($@"Audio\{c}_welcome.wav") && voice == 1)
            {
                Task.Delay(2000).Wait();
                var player = new SoundPlayer($@"Audio\{c}_welcome.wav");
                player.Play();
            }
            else
            {
                Console.WriteLine($@"Audio\{c}_welcome.mp3 not found.");
            }

            await FadeTextBlock(AIEncouragement, fadeIn: true);
            await Task.Delay(duration * 1000);
            await FadeTextBlock(AIEncouragement, fadeIn: false);

        }

        async Task FadeTextBlock(TextBlock tb, bool fadeIn, double duration = 500)
        {
            var hwnd = new WindowInteropHelper(this).Handle;

            // If fading in, show via Win32 (no activation)
            if (fadeIn)
            {
                ShowWindow(hwnd, SW_SHOWNA);
            }

            var animation = new DoubleAnimation
            {
                From = fadeIn ? 0 : 1,
                To = fadeIn ? 1 : 0,
                Duration = TimeSpan.FromMilliseconds(duration),
                EasingFunction = new QuadraticEase()
            };

            var tcs = new TaskCompletionSource<bool>();
            animation.Completed += (s, e) => tcs.SetResult(true);
            tb.BeginAnimation(UIElement.OpacityProperty, animation);

            await tcs.Task;

            // If fading out, hide via Win32
            if (!fadeIn)
            {
                ShowWindow(hwnd, SW_HIDE);
            }
        }


        /// <summary>
        /// Sets the subtitle text _and_ invalidates the layout only if it’s different.
        /// </summary>
        private void UpdateSubtitle(string newText)
        {
            // avoid redundant visual invalidation
            if (AIEncouragement.Text == newText)
                return;

            AIEncouragement.Text = newText;
            AIEncouragement.InvalidateVisual();
        }

        private void GetScreenSize()
        {
            PresentationSource source = PresentationSource.FromVisual(this);
            double dpiFactorX = 1.0, dpiFactorY = 1.0;
            if (source != null)
            {
                Matrix m = source.CompositionTarget.TransformToDevice;
                dpiFactorX = m.M11;
                dpiFactorY = m.M22;
            }

            screenWidth = SystemParameters.PrimaryScreenWidth * dpiFactorX;
            screenHeight = SystemParameters.PrimaryScreenHeight * dpiFactorY;

        }
        private async void PositionOverlayRelativeToGameWindow(RECT rect,int c)
        {
            this.Topmost = false;
            this.Topmost = true;
            // 1) Position and size the window itself
            this.Width = screenWidth * 0.5;
            this.Height = screenHeight;

            this.Left = screenWidth * 0.27;
            this.Top = rect.Top;

            Console.WriteLine("Overlay positioned relative to the game window.");

            // 2) Position the AIEncouragement TextBox inside the window: here it 50% from the left and 88% from the top of the overlay
            double centerX = screenWidth * 0.27;
            double bottomY = this.Height * 0.82;

            AIEncouragement.Width = this.Width;
            Canvas.SetLeft(AIEncouragement, centerX);
            Canvas.SetTop(AIEncouragement, bottomY);

            if (!speaking) 
            { 
            await Welcome(c);
            }

        }
    }
}