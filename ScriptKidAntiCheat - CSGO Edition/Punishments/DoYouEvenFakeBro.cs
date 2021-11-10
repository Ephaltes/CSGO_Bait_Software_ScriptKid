using ScriptKidAntiCheat.Classes.Utils;
using ScriptKidAntiCheat.Utils;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using static ScriptKidAntiCheat.Utils.MouseHook;

namespace ScriptKidAntiCheat.Punishments
{
    /*
     PUNISHMENT: DoYouEvenFakeBro
     DESCRIPTION: Force cheater to drop flash or smoke when trying to throw them
    */
    class DoYouEvenFakeBro : Punishment
    {
        public bool IsThrowing = false;

        public Weapons CurrentWeapon { get; set; }

        public override int ActivateOnRound { get; set; } = 1;

        public DoYouEvenFakeBro() : base(0, false, 50) // 0 = Always active
        {
            MouseHook.MouseAction += new EventHandler(Event);
        }

        override public void Tick(Object source, ElapsedEventArgs e)
        {
            try
            {
                Weapons ActiveWeapon = (Weapons)Player.ActiveWeapon;

                if (CurrentWeapon == ActiveWeapon) return;

                CurrentWeapon = ActiveWeapon;

                // If player is holding flashbang or smoke
                if ((CurrentWeapon == Weapons.Flashbang || CurrentWeapon == Weapons.Smoke) && base.CanActivate() == true)
                {
                    Program.GameConsole.SendCommand("bind mouse1 drop; bind mouse2 drop;");
                } else
                {
                    Program.GameConsole.SendCommand("bind mouse1 +attack; bind mouse2 +attack2;");
                }
            }
            catch (Exception ex)
            {
                Log.AddEntry(new LogEntry()
                {
                    LogTypes = new List<LogTypes> { LogTypes.Analytics },
                    AnalyticsCategory = "Error",
                    AnalyticsAction = "DoYouEvenFakeBroException1",
                    AnalyticsLabel = ex.Message
                });
            }

        }

        private void Event(object MouseEvent, EventArgs e)
        {
            try
            {
                if (!Program.GameProcess.IsValidAndActiveWindow || !Program.GameData.Player.IsAlive() || Program.GameData.MatchInfo.isFreezeTime) return;

                if (Player.CanShoot == false || IsThrowing) return;

                // If Player release left mouse button (fire)
                if ((MouseEvents)MouseEvent == MouseEvents.WM_LBUTTONUP || (MouseEvents)MouseEvent == MouseEvents.WM_RBUTTONUP)
                {
                    Weapons ActiveWeapon = (Weapons)Player.ActiveWeapon;

                    // If player is throwing flashbang
                    if (ActiveWeapon == Weapons.Flashbang || ActiveWeapon == Weapons.Smoke)
                    {
                        ActivatePunishment();
                    }
                }
            }
            catch (Exception ex)
            {

                Log.AddEntry(new LogEntry()
                {
                    LogTypes = new List<LogTypes> { LogTypes.Analytics },
                    AnalyticsCategory = "Error",
                    AnalyticsAction = "DoYouEvenFakeBroException2",
                    AnalyticsLabel = ex.Message
                });
            }
        }

        public void ActivatePunishment()
        {
            if (base.CanActivate() == false) return;

            PlayerConfig.ResetConfig();

            base.AfterActivate();
        }

    }
}
