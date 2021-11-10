using ScriptKidAntiCheat.Forms;
using ScriptKidAntiCheat.Utils;
using ScriptKidAntiCheat.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static ScriptKidAntiCheat.Utils.Helper;

namespace ScriptKidAntiCheat.Classes
{
    class GameConsole
    {
        public TcpClient client;

        public bool isReading = false;

        public bool hasCheckedStatus = false;

        private NetworkStream stream;

        private GameConsoleOld BackupMethod = new GameConsoleOld();

        public event EventHandler<ConsoleReadEventArgs> ConsoleRead;

        public GameConsole()
        {
            ConsoleRead += NewConsoleOuput;
            ConsoleReader();
        }

        public virtual void OnConsoleRead(ConsoleReadEventArgs e)
        {
            EventHandler<ConsoleReadEventArgs> handler = ConsoleRead;
            handler?.Invoke(this, e);
        }
        public void NewConsoleOuput(object sender, ConsoleReadEventArgs e)
        {
            string output = e.Response;

            Console.WriteLine(output);

            if (output.Contains("Recording to"))
            {
                Log.AddEntry(new LogEntry()
                {
                    LogTypes = new List<LogTypes> { LogTypes.Analytics },
                    AnalyticsCategory = "Replays",
                    AnalyticsAction = "RecordingStarted"
                });
            }

            if (output.Contains("Completed demo"))
            {
                Log.AddEntry(new LogEntry()
                {
                    LogTypes = new List<LogTypes> { LogTypes.Analytics },
                    AnalyticsCategory = "Replays",
                    AnalyticsAction = "CompletedDemo"
                });
            }

        }

        public void ConsoleReader()
        {
            Task.Run(() =>
            {

                while (true)
                {
                    if(!isReading)
                    {
                        ReadConsole();
                    }
                    Thread.Sleep(5000);
                }

            });
        }

        public int nr = 0;

        public void ReadConsole()
        {
            isReading = true;

            try
            {
                // Dump console to file
                SendCommand("condump; clear;");

                Thread.Sleep(100);

                string CSGOPath = Helper.getPathToCSGO();

                // Check if CSGO path is defined
                if (CSGOPath != "")
                {
                    string[] condumps = Directory.GetFiles(CSGOPath, "condump*.txt");

                    // Loop through all condumps
                    foreach (string condump in condumps)
                    {
                        nr++;
                        string line;

                        StreamReader file = new StreamReader(condump);

                        // Read console dump line by line
                        while ((line = file.ReadLine()) != null)
                        {
                            // Event on each new line in console
                            ConsoleReadEventArgs args = new ConsoleReadEventArgs();
                            args.Response = line;
                            OnConsoleRead(args);
                        }

                        file.Close();

                        Thread.Sleep(100);

                        // Delete dump
                        File.Delete(condump);

                    }
                }

            }
            catch (Exception ex)
            {
                Log.AddEntry(new LogEntry()
                {
                    LogTypes = new List<LogTypes> { LogTypes.Analytics },
                    IncludeTimeAndTick = false,
                    AnalyticsCategory = "Error",
                    AnalyticsAction = "ConsoleReadException",
                    AnalyticsLabel = ex.Message
                });
            }

            isReading = false;

        }

        public void SendCommand(string Command)
        {
            try
            {
                if (!Program.GameProcess.IsValid) return;

                if(Command.Contains("status") && hasCheckedStatus == false)
                {
                    Task.Run(() =>
                    {
                        Thread.Sleep(5000);
                        hasCheckedStatus = true;
                    });
                }

                // Copy into unmanaged memory
                IntPtr buffer = Marshal.StringToHGlobalAnsi(Command);

                // Get console handle
                var m_hEngine = User32.FindWindowA("Valve001", 0);

                // Create console input struct
                COPYDATASTRUCT copyData = new COPYDATASTRUCT();

                copyData.dwData = IntPtr.Zero;
                copyData.lpData = buffer;
                copyData.cbData = Command.Length + 1;

                // Allocate copiedData in unmanaged memory
                IntPtr copyDataBuff = IntPtrAlloc(copyData);

                // Send message to CSGO console
                User32.SendMessage(m_hEngine, WM_COPYDATA, IntPtr.Zero, copyDataBuff);

                // Free unmanaged memory allocation
                IntPtrFree(ref copyDataBuff);
                IntPtrFree(ref buffer);

            }
            catch (Exception ex)
            {
                // Something went wrong
                Console.WriteLine(Command);
                Log.AddEntry(new LogEntry()
                {
                    LogTypes = new List<LogTypes> { LogTypes.Analytics },
                    IncludeTimeAndTick = false,
                    AnalyticsCategory = "Error",
                    AnalyticsAction = "SendConsoleCmdException",
                    AnalyticsLabel = ex.Message
                });
            }
        }

    }
}
