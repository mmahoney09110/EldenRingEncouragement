using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

class FlagChecker
{
    // P/Invoke definitions
    [DllImport("kernel32.dll")]
    static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool ReadProcessMemory(
        IntPtr hProcess,
        IntPtr lpBaseAddress,
        [Out] byte[] lpBuffer,
        int dwSize,
        out int lpNumberOfBytesRead
    );

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool WriteProcessMemory(
        IntPtr hProcess,
        IntPtr lpBaseAddress,
        byte[] lpBuffer,
        int dwSize,
        out int lpNumberOfBytesWritten
    );

    const int PROCESS_VM_READ = 0x0010;
    const int PROCESS_VM_WRITE = 0x0020;
    const int PROCESS_VM_OPERATION = 0x0008;
    const int PROCESS_QUERY_INFORMATION = 0x0400;

    public static string GetFlag()
    {
        // 1) Attach to Elden Ring
        Process[] procs = Process.GetProcessesByName("eldenring");
        if (procs.Length == 0)
        {
            Console.WriteLine("eldenring.exe not found.");
            return "";
        }
        Process er = procs[0];
        IntPtr hProc = OpenProcess(
            PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION | PROCESS_QUERY_INFORMATION,
            false, er.Id
        );
        if (hProc == IntPtr.Zero)
        {
            Console.WriteLine("OpenProcess failed.");
            return "";
        }

        // 2) Compute eventFlagManPtr = moduleBase + 0x3D68448
        IntPtr moduleBase = er.MainModule.BaseAddress;
        IntPtr eventFlagManPtr = moduleBase + 0x3D68448;

        // 3) Read [eventFlagManPtr] → p1
        byte[] bufPtr = new byte[8];
        ReadProcessMemory(hProc, eventFlagManPtr, bufPtr, 8, out _);
        IntPtr p1 = (IntPtr)BitConverter.ToInt64(bufPtr, 0);

        // 4) Read [p1 + 0x28] → flagsBase
        IntPtr p1Plus28 = p1 + 0x28;
        byte[] bufPtr2 = new byte[8];
        ReadProcessMemory(hProc, p1Plus28, bufPtr2, 8, out _);
        IntPtr flagsBase = (IntPtr)BitConverter.ToInt64(bufPtr2, 0);

        // 5) Target byte address = flagsBase + 0x333
        IntPtr flagByteAddr = flagsBase + 0x333;
        byte[] flagByteBuf = new byte[1];
        ReadProcessMemory(hProc, flagByteAddr, flagByteBuf, 1, out _);
        byte flagByte = flagByteBuf[0];

        var Flag = "";
        // 6) Check bits 4, 3, 2, 1 (from highest to lowest)
        for (int bit = 4; bit >= 1; bit--)
        {
            bool isSet = (flagByte & (1 << bit)) != 0;

            if (isSet)
            {
                switch (bit)
                {
                    case 4:
                        Console.WriteLine($"Flag AAT is {(isSet ? "SET" : "NOT SET")}");
                        Flag = "AAT";
                        break;
                    case 3:
                        Console.WriteLine($"Flag AFY is {(isSet ? "SET" : "NOT SET")}");
                        Flag = "AFY";
                        break;
                    case 2:
                        Console.WriteLine($"Flag ABR is {(isSet ? "SET" : "NOT SET")}");
                        Flag = "ABR";
                        break;
                    case 1:
                        Console.WriteLine($"Flag AFA is {(isSet ? "SET" : "NOT SET")}");
                        Flag = "AFA";
                        break;
                }
                flagByte &= (byte)~(1 << bit); // Clear the bit
                Console.WriteLine($"→ Bit {bit} cleared.");
            }
            
        }
        
        // 7) Write back only if any bit was cleared
        WriteProcessMemory(hProc, flagByteAddr, new byte[] { flagByte }, 1, out _);
        return Flag;
    }
}
