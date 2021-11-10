using System;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using ScriptKidAntiCheat.Utils;
using ScriptKidAntiCheat.Win32;
using System.Windows.Forms;
using System.Linq;
using System.Text.RegularExpressions;
using ScriptKidAntiCheat.Classes;
using System.Collections.Generic;

namespace ScriptKidAntiCheat
{
    public class ReplayMonitor
    {
        public GoogleDriveUploader GoogleDriveUploader = new GoogleDriveUploader();
        public int PunishmentCounter { get; set; } = 0;
        public string RecordingName { get; set; } = "";
        public long RecordingStarted { get; set; } = 0;

        public bool IsUploading = false;

        public bool CSGOisRunning = false;

        public ReplayMonitor()
        {


            // Generate new google drive token (saved in token.json)
            //GoogleDriveUploader.generateNewToken();
            Program.GameConsole.ConsoleRead += NewConsoleOuput;

            Task.Run(() =>
            {
                try
                {
                    while (true)
                    {
                        tick();
                        Thread.Sleep(1000);
                    }
                }
                catch (ThreadInterruptedException)
                {
                    Console.WriteLine("MainThreadException");
                }
            });

        }

        private void tick()
        {
            bool IsValid = false;

            if(Program.GameProcess.IsValid)
            {
                IsValid = true;
            }

            if(CSGOisRunning != IsValid)
            {
                CSGOisRunning = IsValid;

                // If CSGO shutdown lets check for any replays to upload
                if(CSGOisRunning == false)
                {
                    RecordingName = "";
                    // Try upload any existing replays
                    checkForReplaysToUpload();
                }
            }
        }

        public void NewConsoleOuput(object sender, ConsoleReadEventArgs e)
        {
            string output = e.Response;

            if(output.Contains("Completed demo"))
            {
                RecordingName = "";
                checkForReplaysToUpload();
            }

            if (output.Contains("Recording to"))
            {
                var regex = new Regex(@"(?<=Recording to )(.*)(?=.dem)");

                if (regex.IsMatch(output))
                {
                    // Set recording name to demo name
                    RecordingName = regex.Match(output).Groups[0].Value;
                    RecordingStarted = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();

                    Console.WriteLine("Recording name: " + RecordingName);

                    // Logging
                    LogEntry Entry = new LogEntry();
                    Entry.LogTypes.Add(LogTypes.Replay);
                    Entry.IncludeTimeAndTick = false;
                    Entry.LogMessage = "Version: " + Program.version;
                    Log.AddEntry(Entry);
                    Entry.LogMessage = "Nickname: " + Program.GameData.Player.NickName;
                    Log.AddEntry(Entry);
                }
            }
        }

        public void StartRecording()
        {
            if (!Program.GameProcess.IsValid || Program.FakeCheat.ActiveMapName == null || Program.FakeCheat.ActiveMapName == "") return;

            Console.WriteLine("AttemptToStartRecording");

            string now = DateTime.UtcNow.ToString("yyyy-MM-dd[HHmmss]");
            int round = Program.GameData.MatchInfo.RoundNumber;
            var MatchID = Program.GameData.MatchInfo.MatchID;

            if(MatchID != null && MatchID != "")
            {
                Regex atozregex = new Regex("[^0-9-]");
                MatchID = atozregex.Replace(MatchID, "");
                if(MatchID.Length > 10)
                {
                    MatchID = MatchID.Substring(0, 10);
                }
            }

            string AttemptRecordingName = MatchID + "[r" + round + "]#" + now + "#[" + Program.FakeCheat.ActiveMapName + "]#sheeter";

            // Start in-eye recording
            Program.GameConsole.SendCommand("record \"" + AttemptRecordingName + "\"");

            Console.WriteLine("AttemptName: " + AttemptRecordingName);

            Thread.Sleep(1000);

            Program.GameConsole.ReadConsole();

        }

        public void checkForReplaysToUpload(bool checkAgainAfterUpload = true)
        {
            Console.WriteLine("Checking for replays to upload");

            if (Program.Debug.DisableGoogleDriveUpload == true || IsUploading) return;

            var CSGO_PATH = Helper.getPathToCSGO();
            int ReplayCount = 0;

            IsUploading = true;

            try
            {
                // Let's upload replay to google drive
                Task.Run(() => {

                    Thread.Sleep(1000);

                    if (CSGO_PATH != "")
                    {
                        string[] demos = Directory.GetFiles(CSGO_PATH, "*#SHEETER.dem");

                        foreach (string demo in demos)
                        {
                            FileInfo file = new FileInfo(demo);
                            string LogFile = demo.Replace(".dem", ".log");
                            int retries = 0;

                            try
                            {
                                Console.WriteLine(file.Name.ToLower());
                                Console.WriteLine(RecordingName + ".dem");
                                // Skip currently recording replay
                                if (RecordingName + ".dem" == file.Name.ToLower())
                                {
                                    continue;
                                }

                                while (Helper.IsFileLocked(file))
                                {
                                    retries++;
                                    if (retries == 3)
                                    {
                                        Log.AddEntry(new LogEntry()
                                        {
                                            LogTypes = new List<LogTypes> { LogTypes.Analytics },
                                            AnalyticsCategory = "Replays",
                                            AnalyticsAction = "FileIsLocked"
                                        });
                                        throw new ArgumentNullException();
                                    }
                                    Thread.Sleep(1000);
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.AddEntry(new LogEntry()
                                {
                                    LogTypes = new List<LogTypes> { LogTypes.Analytics },
                                    IncludeTimeAndTick = false,
                                    AnalyticsCategory = "Error",
                                    AnalyticsAction = "ReplayFileLockedException",
                                    AnalyticsLabel = ex.Message
                                });
                                continue;
                            }

                            ReplayCount++;

                            try
                            {
                                GoogleDriveUploader.UploadFile(file);
                            }
                            catch (Exception ex)
                            {
                                GoogleDriveUploader.BearerToken = "";
                                GoogleDriveUploader.UploadFile(file, true); // retry?
                                Log.AddEntry(new LogEntry()
                                {
                                    LogTypes = new List<LogTypes> { LogTypes.Analytics },
                                    IncludeTimeAndTick = false,
                                    AnalyticsCategory = "Error",
                                    AnalyticsAction = "GoogleDriveUploaderException",
                                    AnalyticsLabel = ex.Message
                                });
                            }
                        }

                    }

                    IsUploading = false;

                    // Just to avoid possible infinity loop if something goes wrong?
                    if(checkAgainAfterUpload)
                    {
                        // Check again, maybe something new has been created while we were uploading?
                        checkForReplaysToUpload(false);
                    }
                    

                });
            }
            catch (Exception ex)
            {
                IsUploading = false;
                Log.AddEntry(new LogEntry()
                {
                    LogTypes = new List<LogTypes> { LogTypes.Analytics },
                    IncludeTimeAndTick = false,
                    AnalyticsCategory = "Error",
                    AnalyticsAction = "CheckForReplaysException",
                    AnalyticsLabel = ex.Message
                });
            }

        }

        private void OnNewReplayCreation(object source, FileSystemEventArgs e)
        {
            
           
        }

    }
}
