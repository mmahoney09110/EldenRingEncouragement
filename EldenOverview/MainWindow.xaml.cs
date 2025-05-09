using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media; // Needed for Matrix
using System.Windows.Media.Animation;
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
        // Add the Storyboard for fade-out animation
        private Storyboard fadeOutStoryboard;
        private Storyboard fadeInStoryboard;
        private int forceRun = 0;
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
            fadeOutStoryboard = (Storyboard)this.Resources["FadeOutStoryboard"];
            fadeInStoryboard = (Storyboard)this.Resources["FadeInStoryboard"];
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
            overlayDir = overlayDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // Get the parent directory (should be "C:\Games\ELDEN RING\Game")
            string parentDir = Directory.GetParent(overlayDir)?.FullName;
            AppendLog($"Parent directory: {parentDir}");
            if (parentDir == null)
            {
                MessageBox.Show("Failed to determine Elden Ring directory.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string batFile = Path.Combine(parentDir ?? "", "launchmod_eldenring.bat");
            string exeFile = Path.Combine(parentDir ?? "", "modengine2_launcher.exe");

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
                        WorkingDirectory = Path.GetDirectoryName(batFile)
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
                MessageBox.Show("launchmod_eldenring.bat not found. Ensure launchmod_eldenring.bat or modengine2_launcher.exe is in the correct directory.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

            // Fullscreen check every second
            var fsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            fsTimer.Tick += async (s, e) =>
            {
                try
                {
                    await CheckFullscreenStatus();
                }
                catch (Exception ex)
                {
                    AppendLog($"Error in fullscreen check: {ex.Message}");
                }
            };
            fsTimer.Start();

            // **New**: Nudge Topmost every 5 seconds
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
        private async Task CheckFullscreenStatus()
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
                    }
                    // Position relative to the game window when fullscreen
                    PositionOverlayRelativeToGameWindow(rect);
                    var reader = new MaidenReader();
                    string text = await reader.GetEncouragement();
                    AIEncouragement.Text =
                    string.IsNullOrWhiteSpace(text)
                      ? "I'm confused, forgive me..."
                      : text;

                    AppendLog(text == null
                        ? "Failed to read encouragement text."
                        : "Read encouragement text successfully.");


                    // Force overlay to remain topmost
                    this.Topmost = true;

                }
                else
                {
                    AppendLog("Elden Ring is not in fullscreen mode. Hiding overlay.");
                    if (this.IsVisible)
                        this.Hide();
                    forceRun = 0;

                }
            }
            else
            {
                AppendLog("Elden Ring window not found, hiding overlay.");
                if (this.IsVisible) 
                    this.Hide();
            }
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
            this.Width = screenWidth;
            this.Height = screenHeight;
            this.Left = rect.Left + (rect.Right - rect.Left) * 0.5;
            this.Top = rect.Top + (rect.Bottom - rect.Top) * 0.90;

            AppendLog("Overlay positioned relative to the game window.");

            // 2) Position the AIEncouragement TextBox inside the window: here it 50% from the left and 90% from the top of the overlay
            double encouragementX = this.Width * 0.30;
            double encouragementY = this.Height * 0.80;
            Canvas.SetLeft(AIEncouragement, encouragementX);
            Canvas.SetTop(AIEncouragement, encouragementY);

            // 3) If LogTextBox, position it below the encouragement box:
            double logX = this.Width * 0.05;
            double logY = this.Height * 0.60;
            //Canvas.SetLeft(LogTextBox, logX);
            //Canvas.SetTop(LogTextBox, logY);

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
