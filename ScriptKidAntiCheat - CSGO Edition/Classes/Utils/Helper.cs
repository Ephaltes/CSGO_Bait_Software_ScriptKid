using Microsoft.Win32;
using ScriptKidAntiCheat.Internal;
using ScriptKidAntiCheat.Utils.Maths;
using ScriptKidAntiCheat.Win32;
using SharpDX;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

/*
 * Credit: https://github.com/rciworks/RCi.Tutorials.Csgo.Cheat.External
 * */
namespace ScriptKidAntiCheat.Utils
{

    public static class Helper
    {

        public static int MOUSEEVENTF_MOVE = 0x0001;

        public static int MaxStudioBones = 128; // total bones actually used
        public static double AnglePerPixel { get; set; } = 0.000305334789057573;

        public static bool MouseIsCalibrated = false;

        public static bool HasLocalSounds = false;

        public static bool IsFakeFlashed = false;

        public static bool BloodBrothersCanTrigger = true;

        public static bool AimLocked = false;

        public static int WM_COPYDATA = 0x004A;

        private static int AimLockWait = 20;

        [StructLayout(LayoutKind.Sequential)]
        public struct COPYDATASTRUCT
        {
            public IntPtr dwData;    // Any value the sender chooses.  Perhaps its main window handle?
            public int cbData;       // The count of bytes in the message.
            public IntPtr lpData;    // The address of the message.
        }

        
        public static IntPtr IntPtrAlloc<T>(T param)
        {
            IntPtr retval = Marshal.AllocHGlobal(Marshal.SizeOf(param));
            Marshal.StructureToPtr(param, retval, false);
            return retval;
        }
        public static void IntPtrFree(ref IntPtr preAllocated)
        {
            if (IntPtr.Zero == preAllocated)
                throw (new NullReferenceException("Go Home"));
            Marshal.FreeHGlobal(preAllocated);
            preAllocated = IntPtr.Zero;
        }

        public static bool IsFileLocked(FileInfo file)
        {
            FileStream stream = null;

            try
            {
                stream = file.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                //the file is unavailable because it is:
                //still being written to
                //or being processed by another thread
                //or does not exist (has already been processed)
                return true;
            }
            finally
            {
                if (stream != null)
                    stream.Close();
            }

            //file is not locked
            return false;
        }

        public static bool PlayerIsInSpawn()
        {
            if (!Program.GameProcess.IsValid || Program.FakeCheat.ActiveMapClass == null || !Program.GameData.Player.IsAlive()) return false;

            Team PlayerTeam = Program.GameData.Player.Team;
            string PlayerLocation = Program.GameData.Player.Location.ToLower();
            string Map = Program.FakeCheat.ActiveMapClass.GetType().Name;

            if (PlayerTeam == Team.Terrorists && PlayerLocation.Contains("tspawn"))
            {
                return true;
            }

            if (PlayerTeam == Team.CounterTerrorists && PlayerLocation.Contains("ctspawn"))
            {
                return true;
            }

            string SpawnLocation = Program.GameData.MatchInfo.SpawnLocationName;

            if (SpawnLocation != "" && SpawnLocation.ToLower() == PlayerLocation)
            {
                return true;
            }

            return false;
        }

        public static System.Drawing.Rectangle GetClientRectangle(IntPtr handle)
        {
            return User32.ClientToScreen(handle, out var point) && User32.GetClientRect(handle, out var rect)
                ? new System.Drawing.Rectangle(point.X, point.Y, rect.Right - rect.Left, rect.Bottom - rect.Top)
                : default;
        }

        public static System.Diagnostics.ProcessModule GetProcessModule(this System.Diagnostics.Process process, string moduleName)
        {
            return process?.Modules.OfType<System.Diagnostics.ProcessModule>()
                .FirstOrDefault(a => string.Equals(a.ModuleName.ToLower(), moduleName.ToLower()));
        }

        public static Module GetModule(this System.Diagnostics.Process process, string moduleName)
        {
            var processModule = process.GetProcessModule(moduleName);
            return processModule is null || processModule.BaseAddress == IntPtr.Zero
                ? default
                : new Module(process, processModule);
        }

        public static bool CalibrateMouseSensitivity()
        {
            if (!Program.GameProcess.IsValidAndActiveWindow || !Program.GameData.Player.CanShoot || !Program.GameData.Player.IsAlive())
            {
                return false;
            }

            // Move mouse and calculate how much view angels are changed
            try
            {
                AnglePerPixel = new[] {
                    MeasureAnglePerPixel(150),
                    MeasureAnglePerPixel(-300),
                    MeasureAnglePerPixel(300),
                    MeasureAnglePerPixel(-400),
                    MeasureAnglePerPixel(200),
                    MeasureAnglePerPixel(-300),
                    MeasureAnglePerPixel(75),
                    MeasureAnglePerPixel(-150)
                }.Average();

                MouseIsCalibrated = true;

                //Vector3 test = new Vector3(518, -1491, -140);
                //Helper.AimLock(test, 5000);

                return MouseIsCalibrated;
            }
            catch (Exception ex)
            {
                Log.AddEntry(new LogEntry()
                {
                    LogTypes = new List<LogTypes> { LogTypes.Analytics },
                    IncludeTimeAndTick = false,
                    AnalyticsCategory = "Error",
                    AnalyticsAction = "MeasureAnglePerPixelException",
                    AnalyticsLabel = ex.Message
                });
                return false;
            }

        }

        private static double MeasureAnglePerPixel(int pixels)
        {
            // Start view angle
            Thread.Sleep(25);
            Vector3 eyeStart = new Vector3();
            eyeStart = Program.GameData.Player.EyeDirection;
            eyeStart.Z = 0;

            // Move mouse
            SendInput.MouseMove(pixels, 0);

            // End view angle
            Thread.Sleep(25);
            Vector3 eyeEnd = new Vector3();
            eyeEnd = Program.GameData.Player.EyeDirection;
            eyeEnd.Z = 0;

            // Return calibration
            if(eyeStart.X == eyeEnd.X)
            {
                throw new Exception("Calibration failed");
            }

            return eyeEnd.AngleTo(eyeStart) / Math.Abs(pixels);
        }

        // Get view angles to point world
        public static Vector2 GetAimAngles(Vector3 pointWorld)
        {
            var aimDirection = Program.GameData.Player.AimDirection;

            var aimDirectionDesired = (pointWorld - Program.GameData.Player.EyePosition).Normalized();

            return new Vector2
            (
                aimDirectionDesired.AngleToSigned(aimDirection, new Vector3(0, 0, 1)),
                aimDirectionDesired.AngleToSigned(aimDirection, aimDirectionDesired.Cross(new Vector3(0, 0, 1)).Normalized())
            );
        }

        // Translate view angels into how many pixels to move mouse to new target
        public static Point GetAimPixels(Vector2 aimAngles)
        {
            var fovRatio = 90.0 / Program.GameData.Player.Fov;
            return new Point
            (
                (int)Math.Round(aimAngles.X / AnglePerPixel * fovRatio),
                (int)Math.Round(aimAngles.Y / AnglePerPixel * fovRatio)
            );
        }

        public static void SetViewAngleToWorldPoint(Vector3 WorldPoint)
        {
            if (AimLocked == false)
            {
                moveMouseAndAimAt(WorldPoint);
            }
        }



        private static bool moveMouseAndAimAt(Vector3 WorldPoint)
        {
            try
            {
                Vector2 DesiredViewAngle = new Vector2();
                Vector2 aimPixels = new Vector2();
                int MoveMouseX1, MoveMouseY1, MoveMouseX2, MoveMouseY2;

                DesiredViewAngle = Helper.GetAimAngles(WorldPoint);

                if (DesiredViewAngle.X == 0 && DesiredViewAngle.Y == 0)
                {
                    return false;
                }

                aimPixels = Helper.GetAimPixels(DesiredViewAngle);

                MoveMouseX1 = (int)Math.Round(aimPixels.X);
                MoveMouseY1 = (int)Math.Round(aimPixels.Y);

                if (aimPixels.X == 0 && aimPixels.Y == 0)
                {
                    return false;
                }

                SendInput.MouseMove(MoveMouseX1, MoveMouseY1);
                return true;
            }
            catch (Exception ex)
            {
                Log.AddEntry(new LogEntry()
                {
                    LogTypes = new List<LogTypes> { LogTypes.Analytics },
                    IncludeTimeAndTick = false,
                    AnalyticsCategory = "Error",
                    AnalyticsAction = "moveMouseAndAimAtException",
                    AnalyticsLabel = ex.Message
                });
            }

            return false;
        }

        public static void AimLock(Vector3 WorldPoint, int LockTime = 1000, Vector3 WorldPointAfter = default)
        {
            if (AimLocked) return;

            Task.Run(() => {
                AimLocked = true;
                DateTime startTime, endTime;
                startTime = DateTime.Now;
                int x = 0;

                while (true)
                {
                    if(moveMouseAndAimAt(WorldPoint))
                    {
                        Thread.Sleep(AimLockWait);
                    }

                    // Cancel after lock time elpased
                    endTime = DateTime.Now;
                    Double elapsedMillisecs = ((TimeSpan)(endTime - startTime)).TotalMilliseconds;
                    if (elapsedMillisecs > LockTime)
                    {
                        AimLocked = false;
                        // Optional set ViewAngle to something else after lock released (etc used can be used for resetting)
                        if (WorldPointAfter != default)
                        {
                            moveMouseAndAimAt(WorldPointAfter);
                        }
                        break;
                    }
                }
            });
        }

        public static void AimLock(Entity Entity, int LockTime = 1000)
        {
            if (AimLocked) return;

            Task.Run(() => {
                AimLocked = true;
                DateTime startTime, endTime;
                startTime = DateTime.Now;
                int x = 0;

                while (true)
                {

                    if (Entity != null)
                    {
                        if (moveMouseAndAimAt(Entity.Origin))
                        {
                            Thread.Sleep(AimLockWait);
                        }
                    }

                    // Cancel after lock time elpased
                    endTime = DateTime.Now;
                    Double elapsedMillisecs = ((TimeSpan)(endTime - startTime)).TotalMilliseconds;
                    if (elapsedMillisecs > LockTime)
                    {
                        AimLocked = false;
                        break;
                    }
                }
            });
        }

        public static void AimLock(Entity Entity, int BonePos = 0, int LockTime = 1000)
        {
            if (AimLocked) return;

            Task.Run(() => {
                AimLocked = true;
                DateTime startTime, endTime;
                startTime = DateTime.Now;
                int x = 0;

                while (true)
                {

                    if (Entity != null && Entity.BonesPos != null && Entity.BonesPos.Length >= BonePos)
                    {
                        if (Entity.BonesPos[BonePos] != null)
                        {
                            if (moveMouseAndAimAt(Entity.BonesPos[BonePos]))
                            {
                                Thread.Sleep(AimLockWait);
                            }
                        }
                    }

                    // Cancel after lock time elpased
                    endTime = DateTime.Now;
                    Double elapsedMillisecs = ((TimeSpan)(endTime - startTime)).TotalMilliseconds;
                    if (elapsedMillisecs > LockTime)
                    {
                        AimLocked = false;
                        break;
                    }
                }
            });
        }

        public static bool IsRunning(this System.Diagnostics.Process process)
        {
            try
            {
                System.Diagnostics.Process.GetProcessById(process.Id);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            return true;
        }

        public static string PathToCSGO;

        public static string getPathToCSGO()
        {
            if(PathToCSGO != null)
            {
                return PathToCSGO;
            }

            try
            {
                // Try find in registry
                var RegistryInstallPath = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Valve\\Steam\\").GetValue("SteamPath");

                if (RegistryInstallPath != null)
                {
                    string RegistryInstallPathStr = RegistryInstallPath.ToString().Replace("/", "\\").Replace("/", @"\");
                    if (Directory.Exists(RegistryInstallPathStr + @"\steamapps\common\Counter-Strike Global Offensive\csgo"))
                    {
                        PathToCSGO = RegistryInstallPathStr + @"\steamapps\common\Counter-Strike Global Offensive\csgo";
                        return PathToCSGO;
                    }
                    else if (File.Exists(RegistryInstallPathStr + @"\steamapps\libraryfolders.vdf"))
                    {
                        // Try find seperate steam library
                        StreamReader reader = new StreamReader(RegistryInstallPathStr + @"\steamapps\libraryfolders.vdf");
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            var match = System.Text.RegularExpressions.Regex.Matches(line, @"([""'])(?:(?=(\\?))\2.)*?\1");
                            if (match.Count < 2)
                            {
                                continue;
                            }
                            int n;
                            bool isNumeric = int.TryParse(match[0].ToString().Replace(@"""", ""), out n);
                            if (isNumeric && match[1].Length > 0)
                            {
                                var libraryPath = match[1].ToString().Replace(@"""", "");
                                if (Directory.Exists(libraryPath + @"\steamapps\common\Counter-Strike Global Offensive\csgo"))
                                {

                                    PathToCSGO = libraryPath + @"\steamapps\common\Counter-Strike Global Offensive\csgo";
                                    Console.WriteLine(PathToCSGO);
                                    return PathToCSGO;
                                }
                            }
                        }
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
                    AnalyticsAction = "getPathToCSGOException",
                    AnalyticsLabel = ex.Message
                });
            }

            // Try find manually
            var csgo_32 = Environment.GetEnvironmentVariable("ProgramFiles(x86)") + @"\Steam\steamapps\common\Counter-Strike Global Offensive\csgo";
            var csgo_64 = Environment.GetEnvironmentVariable("ProgramFiles") + @"\Steam\steamapps\common\Counter-Strike Global Offensive\csgo";

            if (Directory.Exists(csgo_64)) {
                PathToCSGO = csgo_64;
                return PathToCSGO;
            } else if(Directory.Exists(csgo_32))
            {
                PathToCSGO = csgo_32;
                return PathToCSGO;
            }

            // Try find manually 2
            csgo_32 = @"C:\Program Files (x86)\Steam\steamapps\common\Counter-Strike Global Offensive\csgo";
            csgo_64 = @"C:\Program Files\Steam\steamapps\common\Counter-Strike Global Offensive\csgo";

            if (Directory.Exists(csgo_64))
            {
                PathToCSGO = csgo_64;
                return PathToCSGO;
            }
            else if (Directory.Exists(csgo_32))
            {
                PathToCSGO = csgo_32;
                return PathToCSGO;
            }

            return "";
        }

        public static string PathToSteam;

        public static string getPathToSteam()
        {
            if (PathToSteam != null)
            {
                return PathToSteam;
            }

            // Try find in registry
            var RegistryInstallPath = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Valve\\Steam\\").GetValue("SteamPath");

            if (RegistryInstallPath != null)
            {
                string RegistryInstallPathStr = RegistryInstallPath.ToString().Replace("/", "\\").Replace("/", @"\");
                if (Directory.Exists(RegistryInstallPathStr))
                {
                    PathToSteam = RegistryInstallPathStr;
                    return PathToSteam;
                }
            }

            var steam_32 = Environment.GetEnvironmentVariable("ProgramFiles(x86)") + @"\Steam";
            var steam_64 = Environment.GetEnvironmentVariable("ProgramFiles") + @"\Steam";

            if (Directory.Exists(steam_64))
            {
                PathToSteam = steam_64;
                return PathToSteam;
            }
            else if (Directory.Exists(steam_32))
            {
                PathToSteam = steam_32;
                return PathToSteam;
            }

            steam_32 = @"C:\Program Files (x86)\Steam";
            steam_64 = @"C:\Program Files\Steam";

            if (Directory.Exists(steam_64))
            {
                PathToSteam = steam_64;
                return PathToSteam;
            }
            else if (Directory.Exists(steam_32))
            {
                PathToSteam = steam_32;
                return PathToSteam;
            }

            return "";
        }

        public static int IntersectsHitBox(Line3D aimRayWorld, Entity entity, float offset = 1)
        {
            for (var hitBoxId = 0; hitBoxId < entity.StudioHitBoxSet.numhitboxes; hitBoxId++)
            {
                var hitBox = entity.StudioHitBoxes[hitBoxId];
                var boneId = hitBox.bone;
                if (boneId < 0 || boneId > Helper.MaxStudioBones || hitBox.radius <= 0)
                {
                    continue;
                }

                // intersect capsule
                var matrixBoneModelToWorld = entity.BonesMatrices[boneId];
                var boneStartWorld = matrixBoneModelToWorld.Transform(hitBox.bbmin);
                var boneEndWorld = matrixBoneModelToWorld.Transform(hitBox.bbmax);
                var boneWorld = new Line3D(boneStartWorld, boneEndWorld);
                var (p0, p1) = aimRayWorld.ClosestPointsBetween(boneWorld, true);
                var distance = (p1 - p0).Length();
                if (distance < hitBox.radius * offset)
                {
                    // intersects
                    return hitBoxId;
                }
            }

            return -1;
        }

        public static int pointerI = 0;
        public static string pointers = "";

        public static void TripWireMaker(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 'v')
            {
                Vector3 WorldPoint = new Vector3(-1245, -621, -65);
                Helper.SetViewAngleToWorldPoint(WorldPoint);
            }


            // Reset current pointer when pressing C
            if (e.KeyChar == 'c')
            {
                pointers = "";
                pointerI = 0;
            }

            // Generate pointer when pressing p (after 4 pointers generated it will be printed in console)
            if (e.KeyChar == 'p')
            {

                // Find dwLocalPlayer base address in csgo memory
                var baseAddressdwLocalPlayer = Program.GameProcess.ModuleClient.Read<IntPtr>(Offsets.dwLocalPlayer);
                if (baseAddressdwLocalPlayer != IntPtr.Zero)
                {
                    // Read player location on map
                    var origin = Program.GameProcess.Process.Read<Vector3>(baseAddressdwLocalPlayer + Offsets.m_vecOrigin);
                    var viewOffset = Program.GameProcess.Process.Read<Vector3>(baseAddressdwLocalPlayer + Offsets.m_vecViewOffset);
                    var eyePosition = origin + viewOffset;

                    // Add pointer to string in correct format to be copied for tripwire setup
                    pointerI = pointerI + 1;
                    pointers += "x" + pointerI.ToString() + " = " + (int)eyePosition.X + ", y" + pointerI.ToString() + " = " + (int)eyePosition.Y + ",";

                    // Print the pointers generated by pressing p 4 times
                    if (pointerI == 4)
                    {
                        Console.WriteLine("######################################");
                        Console.WriteLine("TRIPWIRE LOCATION:");
                        Console.WriteLine(pointers);
                        Console.WriteLine("z = " + (int)eyePosition.Z);
                        Console.WriteLine("######################################");
                        pointers = "";
                        pointerI = 0;
                    }
                    else
                    {
                        // New line
                        pointers += "\r";
                    }

                }
            }

        }
        public static float GetDistance(Vector3 to, Vector3 from)
        {
            float deltaX = to.X - from.X;
            float deltaY = to.Y - from.Y;
            float deltaZ = to.Z - from.Z;

            return (float)Math.Sqrt(deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ);
        }

        public static string MD5Hash(string input)
        {
            StringBuilder hash = new StringBuilder();
            MD5CryptoServiceProvider md5provider = new MD5CryptoServiceProvider();
            byte[] bytes = md5provider.ComputeHash(new UTF8Encoding().GetBytes(input));

            for (int i = 0; i < bytes.Length; i++)
            {
                hash.Append(bytes[i].ToString("x2"));
            }
            return hash.ToString();
        }

        public static float GetCurrentTick()
        {
            if(Program.FakeCheat.ReplayMonitor.RecordingStarted == 0) return 0;
            float TickRate = 1.0f / Program.GameData.MatchInfo.GlobalVars.interval_per_tick;
            long CurrentTime = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
            float CurrentTick = (CurrentTime - Program.FakeCheat.ReplayMonitor.RecordingStarted) * TickRate;
            return CurrentTick - Math.Max(TickRate * 5, 0); // Make sure our tick count in the log is 5 seconds before the actual event
        }

        public static string GetActiveWindowTitle()
        {
            const int nChars = 256;
            StringBuilder Buff = new StringBuilder(nChars);
            IntPtr handle = User32.GetForegroundWindow();

            if (User32.GetWindowText(handle, Buff, nChars) > 0)
            {
                return Buff.ToString();
            }
            return null;
        }

        public static WeaponType GetWeaponType(Weapons Weapon)
        {

            // Check if Heavy
            if( Weapon == Weapons.NOVA  ||
                Weapon == Weapons.XM    ||
                Weapon == Weapons.MAG7  ||
                Weapon == Weapons.M249  ||
                Weapon == Weapons.NEGEV ||
                Weapon == Weapons.SAWOFF )
            {
                return WeaponType.HEAVY;
            }

            // Check if Rifle
            if ( Weapon == Weapons.M4A1  ||
                 Weapon == Weapons.M4A4  ||
                 Weapon == Weapons.AK47  ||
                 Weapon == Weapons.AUG   ||
                 Weapon == Weapons.FAMAS ||
                 Weapon == Weapons.Sig   || 
                 Weapon == Weapons.Scar  ||
                 Weapon == Weapons.SG553 ||
                 Weapon == Weapons.GALIL ||
                 Weapon == Weapons.Scout ||
                 Weapon == Weapons.Awp )
            {
                return WeaponType.RIFLE;
            }

            // Check if SMG
            if ( Weapon == Weapons.MAC10 ||
                 Weapon == Weapons.MP5   ||
                 Weapon == Weapons.MP7   ||
                 Weapon == Weapons.MP9   ||
                 Weapon == Weapons.UMP   ||
                 Weapon == Weapons.P90 )
            {
                return WeaponType.SMG;
            }

            // Check if Pistol
            if ( Weapon == Weapons.USP          ||
                 Weapon == Weapons.GLOCK        ||
                 Weapon == Weapons.DESERT_EAGLE ||
                 Weapon == Weapons.P2000        ||
                 Weapon == Weapons.DUELLIES     ||
                 Weapon == Weapons.P250         ||
                 Weapon == Weapons.FIVE_SEVEN   ||
                 Weapon == Weapons.TEC9         ||
                 Weapon == Weapons.CZ75         ||
                 Weapon == Weapons.REVOLVER)
            {
                return WeaponType.PISTOL;
            }

            // Check if Grenade
            if ( Weapon == Weapons.Grenade      ||
                 Weapon == Weapons.Flashbang    ||
                 Weapon == Weapons.Molotov      ||
                 Weapon == Weapons.Incendiary   ||
                 Weapon == Weapons.Decoy        ||
                 Weapon == Weapons.Smoke )
            {
                return WeaponType.GRENADE;
            }

            // Check if Equipment
            if ( Weapon == Weapons.ZEUS )
            {
                return WeaponType.EQUIPMENT;
            }

            return WeaponType.OTHER;
        }

        public static bool IsPrimaryWeapon(short Weapon)
        {
            WeaponType weaponType = GetWeaponType((Weapons)Weapon);
            if(weaponType == WeaponType.SMG || weaponType == WeaponType.HEAVY || weaponType == WeaponType.RIFLE)
            {
                return true;
            }

            return false;
        }

        public static bool HasPrimaryWeapon(Entity Entity)
        {
            for (int i = 0; i < 48; i++)
            {
                // Get active weapon from entity
                int currentWeapon = Program.GameProcess.Process.Read<int>(Entity.AddressBase + Offsets.m_hMyWeapons + ((i - 1) * 0x04));
                int currentWeaponEntityID = (currentWeapon & 0xfff);
                IntPtr weaponEntity = Program.GameProcess.ModuleClient.Read<IntPtr>(Offsets.dwEntityList + (currentWeaponEntityID - 1) * 0x10);
                short EntityWeaponID = Program.GameProcess.Process.Read<short>(weaponEntity + Offsets.m_iItemDefinitionIndex);
                WeaponType weaponType = Helper.GetWeaponType((Weapons)EntityWeaponID);

                // If primary weapon
                if (weaponType == WeaponType.SMG || weaponType == WeaponType.HEAVY || weaponType == WeaponType.RIFLE)
                {
                    return true;
                }

            }

            return false;
        }

        // Check if required references are available
        public static bool DLLsLoaded()
        {
            try
            {
                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] DLLs = { "AxInterop.WMPLib.dll", "Gma.System.MouseKeyHook.dll", "SharpDX.dll", "SharpDX.Mathematics.dll" };

                if (Directory.Exists(exeDir + "/lib") && !File.Exists(exeDir + "/RageMaker.exe.config"))
                {
                    return false;
                }

                foreach (string DLL in DLLs)
                {
                    if (!File.Exists(exeDir + "lib/" + DLL))
                    {
                        if (!File.Exists(exeDir + DLL))
                        {
                            return false;
                        }
                    }
                }

                return true; // YAY
            }
            catch (IOException)
            {
                return false;
            }

        }

    }
}
