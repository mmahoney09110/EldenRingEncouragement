using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media; // Needed for Matrix
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using EldenEncouragement;

namespace EldenRingOverlay
{
    public partial class MainWindow : Window
    {
        // Periodic “nudge” timer to reassert Topmost
        private DispatcherTimer topmostNudgeTimer;
        protected override void OnDeactivated(EventArgs e)
        {
            base.OnDeactivated(e);

            // Reassert topmost
            this.Topmost = false;
            this.Topmost = true;
        }
        private DispatcherTimer checkEldenRingTimer;
        private int missingCounter = 0;
        private const int MissingThreshold = 35;
        private double screenWidth;
        private double screenHeight;
        
        // PInvoke declarations
        [DllImport("user32.dll")]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

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
            InitializeComponent();
            InitializeOverlay();
            GetScreenSize();
            StartEldenRingLauncher();
            StartMonitoringEldenRing();
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
            AppendLog($"Overlay directory: {overlayDir}");

            // Trim trailing directory separators so GetParent returns the correct parent
            overlayDir = overlayDir.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);

            // Get the parent directory (should be "C:\Games\ELDEN RING\Game")
            string parentDir = Directory.GetParent(overlayDir)?.FullName;
            AppendLog($"Parent directory: {parentDir}");
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
                    AppendLog("Attempting to launch Elden Ring");
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
                    AppendLog("Attempting to launch Elden Ring with exe file");
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
        private void InitializeOverlay()
        {
            // Base overlay setup
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            bool isFullscreen = false;
            // Fullscreen check every second
            var fsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            fsTimer.Tick += async (s, e) =>
            {
                try
                {
                    isFullscreen = await CheckFullscreenStatus();
                }
                catch (Exception ex)
                {
                    AppendLog($"Error in fullscreen check: {ex.Message}");
                }
            };
            fsTimer.Start();

            // get encouragement text
            var gtTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(300) };
            gtTimer.Tick += async (s, e) =>
            {
                try
                {
                    if (!isFullscreen)
                    {
                        AppendLog("Elden Ring is not in fullscreen mode. Skipping encouragement text.");
                        return;
                    }
                    await GetEncouragement();
                }
                catch (Exception ex)
                {
                    AppendLog($"Error in fullscreen check: {ex.Message}");
                }
            };
            gtTimer.Start();

            // Nudge Topmost every 5 seconds
            topmostNudgeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            topmostNudgeTimer.Tick += (s, e) =>
            {
                this.Topmost = false;
                this.Topmost = true;
            };
            topmostNudgeTimer.Start();
        }

        /// <summary>
        /// Checks if the Elden Ring window is in fullscreen mode (using DPI-adjusted screen size) 
        /// and shows or hides the overlay accordingly.
        /// </summary>
        private async Task<bool> CheckFullscreenStatus()
        {
            var hwnd = FindWindow(null, "ELDEN RING™");
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

                AppendLog($"Checking fullscreen: Window size: {windowWidth} x {windowHeight}");
                AppendLog($"Physical screen size: {physicalScreenWidth} x {physicalScreenHeight}");

                // Check if the game is in fullscreen mode
                if (windowWidth == physicalScreenWidth && windowHeight == physicalScreenHeight)
                {
                    AppendLog("Elden Ring is in fullscreen mode.");
                    if (!this.IsVisible)
                    {
                        AppendLog("Showing overlay.");
                        this.Show();
                        return true;
                    }

                    // Position relative to the game window when fullscreen
                    PositionOverlayRelativeToGameWindow(rect);

                    // Force overlay to remain topmost
                    this.Topmost = true;

                    return true;
                }
                else
                {
                    AppendLog("Elden Ring is not in fullscreen mode. Hiding overlay.");
                    if (this.IsVisible)
                        this.Hide();
                    return false;


                }
            }
            else
            {
                AppendLog("Elden Ring window not found, hiding overlay.");
                if (this.IsVisible) 
                    this.Hide();
                return false;

            }
        }
        MaidenReader reader = new MaidenReader();
        private async Task GetEncouragement()
        {
            string text = await reader.GetEncouragement();

            // Split on punctuation + *any* whitespace (including newline)
            var sentences = Regex.Split(text, @"(?<=[\.!\?])\s+");

            if (string.IsNullOrWhiteSpace(text))
            {
                AIEncouragement.Text = "I'm confused, forgive me...";
                AppendLog("Failed to read encouragement text.");
            }
            else
            {
                foreach (var raw in sentences)
                {
                    var sentence = raw.Trim();
                    if (sentence.Length == 0) continue;

                    // Ensure it ends with its original punctuation
                    char lastChar = sentence[^1];
                    if (!".!?".Contains(lastChar))
                        sentence += ".";

                    AIEncouragement.Text = sentence;

                    await FadeTextBlock(AIEncouragement, fadeIn: true);
                    await Task.Delay(3000);
                    await FadeTextBlock(AIEncouragement, fadeIn: false);
                }
            }
        }

        async Task FadeTextBlock(TextBlock tb, bool fadeIn, double duration = 500)
        {
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
        private void PositionOverlayRelativeToGameWindow(RECT rect)
        {
            // 1) Position and size the window itself
            this.Width = screenWidth * 0.5;
            this.Height = screenHeight;

            this.Left = screenWidth * 0.27;
            this.Top = rect.Top;

            AppendLog("Overlay positioned relative to the game window.");

            // 2) Position the AIEncouragement TextBox inside the window: here it 50% from the left and 88% from the top of the overlay
            double centerX = screenWidth * 0.27;
            double bottomY = this.Height * 0.82;

            AIEncouragement.Width = this.Width;
            Canvas.SetLeft(AIEncouragement, centerX);
            Canvas.SetTop(AIEncouragement, bottomY);
            

            // 3) If LogTextBox, position it below the encouragement box:
            double logX = screenWidth * 0.25;
            double logY = this.Height * 0.85;
            Canvas.SetLeft(LogTextBox, logX);
            Canvas.SetTop(LogTextBox, logY);

        }


        /// <summary>
        /// Logs messages to the TextBox for debugging.
        /// </summary>
        private void AppendLog(string message)
        {
            Dispatcher.Invoke(() =>
            {
                if (LogTextBox != null)
                {
                    LogTextBox.AppendText(message + Environment.NewLine);
                    LogTextBox.ScrollToEnd(); // Auto-scroll to the latest log
                }
            });
        }

    }
}
