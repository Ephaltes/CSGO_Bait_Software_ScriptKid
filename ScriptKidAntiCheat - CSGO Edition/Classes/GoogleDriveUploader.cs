using System;
using System.IO;
using System.Threading;
using System.IO.Compression;
using ScriptKidAntiCheat.Utils;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http.Headers;

namespace ScriptKidAntiCheat
{
    public class GoogleDriveUploader
    {

        public Uri UploadUrl = new Uri("https://www.googleapis.com/upload/drive/v3/files?uploadType=multipart");
        public string BearerToken = "";
        public long BearerTokenCreated = 0;

        public Dictionary<string, string> oauthSettings = new Dictionary<string, string>
        {
            { "client_id", "" },
            { "client_secret", "" },
            { "refresh_token", "" },
            { "grant_type", "refresh_token" }
        };

        public GoogleDriveUploader()
        {
            if ((oauthSettings["client_id"] == "" || oauthSettings["client_secret"] == "" || oauthSettings["refresh_token"] == "") && Program.Debug.ShowDebugMessages)
            {
                System.Windows.Forms.MessageBox.Show("Google drive uploading is disabled - you need to create ClientId and ClientSecret in Google Developer Console. Check my GoogleDriveUploader class for more info.", "Developer helper", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
            }
        }

        private async Task<string> GenerateToken()
        {

            long timestamp = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();

            if(BearerToken != "" && timestamp - BearerTokenCreated < 1800)
            {
                return BearerToken;
            }

            using (var client = new HttpClient())
            {
                var settings = new FormUrlEncodedContent(oauthSettings);
                var post = await client.PostAsync("https://oauth2.googleapis.com/token", settings);

                if(post.StatusCode == HttpStatusCode.OK)
                {
                    var response = await post.Content.ReadAsStringAsync();

                    Regex access_token = new Regex("(?<=access_token\": \")(.*)(?=\")");

                    if (response != null && response != "" && access_token.IsMatch(response))
                    {
                        BearerTokenCreated = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
                        BearerToken = access_token.Match(response).Groups[0].Value;
                    }
                    
                }
            }

            return BearerToken;
        }

        async public void UploadFile(FileInfo ReplayFile, bool retry = false)
        {
            if (oauthSettings["client_id"] == "" || oauthSettings["client_secret"] == "" || oauthSettings["refresh_token"] == "")
            {
                return;
            }

            Log.AddEntry(new LogEntry()
            {
                LogTypes = new List<LogTypes> { LogTypes.Analytics },
                AnalyticsCategory = "Replays",
                AnalyticsAction = "ZipDirectory"
            });

            // Zip directory before uploading to google drive
            string zipFile = ZipDirectory(ReplayFile);
            //string zipFile = ReplayFile.FullName;

            // Make sure zip was successful before proceeding
            if (!File.Exists(zipFile))
            {
                return;
            }

            Log.AddEntry(new LogEntry()
            {
                LogTypes = new List<LogTypes> { LogTypes.Analytics },
                AnalyticsCategory = "Replays",
                AnalyticsAction = "ZipDirectorySuccess"
            });

            //  Prepare zip file
            byte[] zipFileBytes = File.ReadAllBytes(zipFile);
            var zipBinaryContent = new ByteArrayContent(zipFileBytes);
            zipBinaryContent.Headers.Add("Content-Type", "application/zip");

            // Prepare file metadata
            string metaContent = "{\"name\":\"" + Path.GetFileName(zipFile).Replace("#sheeter", "") + "\"}";
            byte[] metaBytes = Encoding.UTF8.GetBytes(metaContent);
            var metaBinaryContent = new ByteArrayContent(metaBytes);
            metaBinaryContent.Headers.Add("Content-Type", "application/json; charset=UTF-8");

            // Create MultipartFormDataContent and add meta and file
            var multipartContent = new MultipartFormDataContent("replay");
            multipartContent.Headers.Remove("Content-Type");
            multipartContent.Headers.TryAddWithoutValidation("Content-Type", "multipart/related; boundary=replay");
            multipartContent.Add(metaBinaryContent, "replay");
            multipartContent.Add(zipBinaryContent, "replay");

            using (var client = new HttpClient())
            {
                Log.AddEntry(new LogEntry()
                {
                    LogTypes = new List<LogTypes> { LogTypes.Analytics },
                    AnalyticsCategory = "Replays",
                    AnalyticsAction = "UploadStart"
                });

                bool success = false;

                await Task.Run(async () =>
                 {
                     // Generate access token for google drive
                     var tokenGenerator = GenerateToken();
                     tokenGenerator.Wait();

                     if (BearerToken != "")
                     {
                         client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", BearerToken);
                         HttpResponseMessage result = await client.PostAsync(UploadUrl, multipartContent);

                         // Receive the response from google drive upload
                         if (result.StatusCode == HttpStatusCode.OK)
                         {
                             success = true;
                             Log.AddEntry(new LogEntry()
                             {
                                 LogTypes = new List<LogTypes> { LogTypes.Analytics },
                                 AnalyticsCategory = "Replays",
                                 AnalyticsAction = "UploadSuccess"
                             });
                             if (Program.Debug.ShowDebugMessages)
                             {
                                 System.Windows.Forms.MessageBox.Show("Upload complete", "Debug", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                             }
                         }
                         else
                         {
                             Log.AddEntry(new LogEntry()
                             {
                                 LogTypes = new List<LogTypes> { LogTypes.Analytics },
                                 AnalyticsCategory = "Replays",
                                 AnalyticsAction = "UploadFail"
                             });
                             if (Program.Debug.ShowDebugMessages)
                             {
                                 System.Windows.Forms.MessageBox.Show("Upload failed", "Debug", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                             }
                         }

                     }

                     if(success)
                     {
                         // Cleanup after upload
                         if (File.Exists(zipFile))
                         {
                             File.Delete(zipFile);  // Delete zip file
                         }
                     } else if(retry == false)
                     {
                         // Try one more time
                         BearerToken = "";
                         UploadFile(ReplayFile, true);
                     }

                 });

            }

        }

        private string ZipDirectory(FileInfo ReplayFile)
        {
            string zipPath = Path.GetTempPath() + ReplayFile.Name + ".zip";
            string cleanReplayName = ReplayFile.Name.Replace("#sheeter", "");
            string replayTmpDirPath = Path.GetTempPath() + cleanReplayName;
            string logFilePath = ReplayFile.DirectoryName + @"\" + Path.GetFileNameWithoutExtension(ReplayFile.FullName) + ".log";
            string manifestFilePath = ReplayFile.DirectoryName + @"\" + Path.GetFileNameWithoutExtension(ReplayFile.FullName) + ".manifest.log";

            // Create temporary dir where we will place replay file
            Directory.CreateDirectory(replayTmpDirPath);

            // Move Replay log if it exists
            if (File.Exists(logFilePath))
            {
                FileInfo ReplayLogFile = new FileInfo(logFilePath);
                string cleanLogFileName = ReplayLogFile.Name.Replace("#sheeter", "");
                ReplayLogFile.MoveTo(replayTmpDirPath + @"\" + cleanLogFileName);
            }

            // Move Replay manifest if it exists
            if (File.Exists(manifestFilePath))
            {
                FileInfo ReplayManifestFile = new FileInfo(manifestFilePath);
                string cleanManifestFileName = ReplayManifestFile.Name.Replace("#sheeter", "");
                ReplayManifestFile.MoveTo(replayTmpDirPath + @"\" + cleanManifestFileName);
            }

            // Move replay file to tmp dir
            ReplayFile.MoveTo(replayTmpDirPath + @"\" + cleanReplayName);

            if (!File.Exists(zipPath) && Directory.Exists(replayTmpDirPath) && File.Exists(replayTmpDirPath + @"\" + cleanReplayName))
            {
                // Zip Directory
                ZipFile.CreateFromDirectory(replayTmpDirPath, zipPath);
            }

            return zipPath;
        }
    }
}
