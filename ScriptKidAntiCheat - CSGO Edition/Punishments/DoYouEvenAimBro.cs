using ScriptKidAntiCheat.Internal;
using ScriptKidAntiCheat.Utils;
using ScriptKidAntiCheat.Utils.Maths;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using static ScriptKidAntiCheat.Utils.MouseHook;

/*
 * Credit: https://github.com/rciworks/RCi.Tutorials.Csgo.Cheat.External/tree/master/Tutorial%20008%20-%20Trigger%20Bot
 */
namespace ScriptKidAntiCheat.Punishments
{
    /*
     PUNISHMENT: DoYouEvenAimBro
     DESCRIPTION: Cause the cheater to look straight up into the sky when crosshair enters enemy hitbox
    */
    class DoYouEvenAimBro : Punishment
    {

        public static bool logDelay = false;

        public override int ActivateOnRound { get; set; } = 8;
        public bool IsShooting { get; private set; }

        public DoYouEvenAimBro() : base(0, false, 100) // 0 = Always active
        {
            GameData = Program.GameData;
            Player = Program.GameData.Player;
            MouseHook.MouseAction += new EventHandler(Event);
        }

        private void Event(object MouseEvent, EventArgs e)
        {
            try
            {
                if (!Program.GameProcess.IsValidAndActiveWindow || !Program.GameData.Player.IsAlive() || Program.GameData.MatchInfo.isFreezeTime) return;

                if (Player.CanShoot == false) return;

                // If Player press left mouse button (fire)
                if ((MouseEvents)MouseEvent == MouseEvents.WM_LBUTTONDOWN)
                {
                    IsShooting = true;
                }

                // If Player release left mouse button (fire)
                if ((MouseEvents)MouseEvent == MouseEvents.WM_LBUTTONUP)
                {
                    IsShooting = false;
                }
            }
            catch (Exception ex)
            {
                Log.AddEntry(new LogEntry()
                {
                    LogTypes = new List<LogTypes> { LogTypes.Analytics },
                    AnalyticsCategory = "Error",
                    AnalyticsAction = "DoYouEvenAimBroException1",
                    AnalyticsLabel = ex.Message
                });
            }
        }

        override public void Tick(Object source, ElapsedEventArgs e)
        {
            try
            {
                if (Program.GameProcess.IsValidAndActiveWindow == false || Player.IsAlive() == false || base.CanActivate() == false || Program.GameData.MatchInfo.isFreezeTime)
                {
                    return;
                }

                // Don't trigger on specific weapons
                Weapons activeWeapon = (Weapons)Player.ActiveWeapon;
                if (
                    activeWeapon == Weapons.Knife_CT ||
                    activeWeapon == Weapons.Knife_T ||
                    activeWeapon == Weapons.Smoke ||
                    activeWeapon == Weapons.Flashbang ||
                    activeWeapon == Weapons.Grenade ||
                    activeWeapon == Weapons.Incendiary ||
                    activeWeapon == Weapons.Molotov)
                {
                    return;
                }

                Vector3 AimDirection = Player.AimDirection;
                Vector3 GetPlayerLocation = Player.EyePosition;

                // get aim ray in world
                if (AimDirection.Length() < 0.001)
                {
                    return;
                }

                var aimRayWorld = new Line3D(GetPlayerLocation, GetPlayerLocation + AimDirection * 8192);

                // go through entities
                foreach (var entity in GameData.Entities)
                {

                    if (!entity.IsAlive() || entity.AddressBase == Player.AddressBase)
                    {
                        continue;
                    }

                    if (entity.Team == Player.Team)
                    {
                        continue;
                    }

                    if (entity.Spotted == false)
                    {
                        continue;
                    }

                    // Check if hitbox intersect with aimRay
                    var hitBoxId = Helper.IntersectsHitBox(aimRayWorld, entity);
                    if (hitBoxId >= 0)
                    {
                        ActivatePunishment();
                    }

                    Thread.Sleep(10);
                }
            }
            catch (Exception ex)
            {
                Log.AddEntry(new LogEntry()
                {
                    LogTypes = new List<LogTypes> { LogTypes.Analytics },
                    AnalyticsCategory = "Error",
                    AnalyticsAction = "DoYouEvenAimBroException2",
                    AnalyticsLabel = ex.Message
                });
            }
        }

        public void ActivatePunishment()
        {
            if (base.CanActivate() == false) return;

            if (!IsShooting) return;

            Task.Run(() => {
                SendInput.MouseMove(0, -10000);
            });

            // Prevent flooding the replay log with multiple instances of this punishment in a row
            if(logDelay == false)
            {
                logDelay = true;
                base.AfterActivate();
                Task.Run(() => {
                    Thread.Sleep(12000);
                    logDelay = false;
                });
            }
            
        }

    }
}
