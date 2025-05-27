using System;
using System.Diagnostics;
using System.IO;

public class CheatEngineHelper
{
    private Process helperProc;

    public void RunHelperAndReadData()
    {
        string helperExe = "EldenHelper.exe";
        string helperPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, helperExe);

        if (!File.Exists(helperPath))
        {
            Console.WriteLine("EldenHelper.exe not found at: " + helperPath);
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = helperPath,
            UseShellExecute = false, // Allows you to read/write output later
            WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
        };

        try
        {
            Console.WriteLine("Launching EldenHelper...");
            Process helperProc = Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error running EldenHelper: " + ex.Message);
        }
    }

    public void StopHelper()
    {
        try
        {
            if (helperProc != null && !helperProc.HasExited)
            {
                Console.WriteLine("Attempting to close EldenHelper...");
                helperProc.Kill();
                helperProc.WaitForExit();
                Console.WriteLine("EldenHelper closed.");
            }
            else
            {
                // Fallback: try to find and kill by name
                foreach (var proc in Process.GetProcessesByName("EldenHelper"))
                {
                    proc.Kill();
                    proc.WaitForExit();
                    Console.WriteLine("EldenHelper process killed (fallback).");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error closing EldenHelper: " + ex.Message);
        }
    }
}
