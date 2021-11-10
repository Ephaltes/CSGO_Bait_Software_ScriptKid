using ScriptKidAntiCheat.Classes;
using System;
using System.Windows.Forms;
using ScriptKidAntiCheat.Data;
using ScriptKidAntiCheat.Utils;
using ScriptKidAntiCheat.Forms;
using System.Diagnostics;
using System.Collections.Generic;

namespace ScriptKidAntiCheat
{
    static class Program
    {
        public static string version = "v1.4.8";
        public static FakeCheatForm the_form;
        public static GameProcess GameProcess;
        public static GameConsole GameConsole;
        public static FakeCheat FakeCheat;
        public static GameData GameData;
        public static bool FakeCheatFormClosed = false;
        public static Utils.Debug Debug;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            DialogResult result =  System.Windows.Forms.MessageBox.Show("Use at your own risk!\r\rNo one has ever gotten vac banned for using the fake cheat as far as I know but there is always the chance that it can happen.\r\rDO NOT USE this on accounts that you are scared to lose. Also know that if you get banned, any account that is using the same mobile authenticator also gets banned.\r\rPress OK to continue or press CANCEL to exit", "WARNING!!!", System.Windows.Forms.MessageBoxButtons.OKCancel, System.Windows.Forms.MessageBoxIcon.Warning);
            if (result == DialogResult.Cancel)
            {
                System.Environment.Exit(0);
            }

            if ( Helper.DLLsLoaded() == false )
            {
                System.Windows.Forms.MessageBox.Show("Lib directory or required dlls could not be found!\r\rTroubleshooting\r1. Make sure you extracted all files from the zip.\r2. Make sure lib directory is in the same place as RageMaker.exe.\r3. Make sure RageMaker.exe.config is in the same place as RageMaker.exe.", "Problem loading required libraries", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                System.Environment.Exit(0);
            }

            Debug = new Utils.Debug();

            #if DEBUG
                Debug.AllowLocal = true;
                Debug.AllowInWarmup = true;
                Debug.DisableGoogleDriveUpload = true;
                Debug.DisableRunInBackground = true;
                Debug.DisableAcceptConditions = true;
                Debug.IgnoreActivateOnRound = true;
                Debug.ShowDebugMessages = true;
                Debug.SkipInitDelay = true;
                Debug.TripWireStage = 2;
                Debug.DisableTripWires = false;
                Debug.DisableFakeFlash = false;
            #endif

            // Make the cheater accept terms and conditions
            if (Debug.DisableAcceptConditions == false)
            {
                Application.Run(new Conditions());
            }


            // Check how many instances of the fake cheat is running
            Process[] isAlreadyInitialized = Process.GetProcessesByName("RageMaker"); // SteamInjector is our fake process name

            // Try find steam and csgo folder, if it fails open manually select form
            if(Helper.getPathToCSGO() == "" || Helper.getPathToSteam() == "")
            {
                Log.AddEntry(new LogEntry()
                {
                    LogTypes = new List<LogTypes> { LogTypes.Analytics },
                    AnalyticsCategory = "Error",
                    AnalyticsAction = "CouldNotFindCsgoOrSteamPath"
                });
                Application.Run(new SteamPath());
            }

            // Setup our memory reader and fake cheat process (only if its not already running)
            if (Helper.getPathToCSGO() != "" && isAlreadyInitialized.Length == 1)
            {
                GameConsole = new GameConsole();
                GameProcess = new GameProcess();
                GameData = new GameData(GameProcess);
                GameProcess.Start();
                GameData.Start();
                FakeCheat = new FakeCheat();
            }

            // Run fake ui form
            #if DEBUG
                Application.Run(new Tester());
            #else
                Application.Run(new FakeCheatForm());
            #endif

            FakeCheatFormClosed = true;

            // Run hidden application once they close main window (only if its not already running)
            if (isAlreadyInitialized.Length == 1 && Debug.DisableRunInBackground != true)
            {
                System.Windows.Forms.MessageBox.Show("The fake cheat will now keep running in the background! Press F7 to close the background process!", "WARNING", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                Application.Run(new Hidden());
            }

            if (Helper.getPathToCSGO() != "" && isAlreadyInitialized.Length == 1)
            {
                Dispose();
            }
        }

        private static void Dispose()
        {
            GameData.Dispose();
            GameData = default;
            GameProcess.Dispose();
            GameProcess = default;
        }

    }
}