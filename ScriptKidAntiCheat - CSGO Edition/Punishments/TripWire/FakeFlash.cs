using ScriptKidAntiCheat.Classes.Utils;
using ScriptKidAntiCheat.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace ScriptKidAntiCheat.Punishments
{
    /*
     PUNISHMENT: FakeFlash
     DESCRIPTION: Simulate flashbang by changing crosshair settings to cover the whole screen and fade from 0 to 100% white
    */
    class FakeFlash : Punishment
    {
        private int FlashDuration { get; set; }

        public override bool DisposeOnReset { get; set; } = false;

        public FakeFlash(int FakeFlashDuration = 2000) : base(0, true) // 0 = Always active
        {
            FlashDuration = FakeFlashDuration;
            if (Helper.IsFakeFlashed == false)
            {
                ActivatePunishment();
            }
        }

        private void ActivatePunishment()
        {
            if (base.CanActivate() == false) return;

            if (Player.IsAlive() == false) return;

            if (Program.Debug.DisableFakeFlash) return;

            Weapons ActiveWeapon = (Weapons)Player.ActiveWeapon;

            Helper.IsFakeFlashed = true;

            // Make sure we change to weapon with a scope first (all pistols have it)
            if (ActiveWeapon == Weapons.Awp     || 
                ActiveWeapon == Weapons.Scout   || 
                ActiveWeapon == Weapons.Sig     ||
                ActiveWeapon == Weapons.Scar)
            {
                Program.GameConsole.SendCommand("slot2");
            }

            Program.GameConsole.SendCommand("play flashed");

            if(Helper.HasLocalSounds == false)
            {
                System.IO.Stream str = Properties.Resources.flashed;
                System.Media.SoundPlayer snd = new System.Media.SoundPlayer(str);
                snd.Play();
            }

            Program.GameConsole.SendCommand("cl_crosshairthickness 999; cl_crosshairsize 999; cl_crosshaircolor 5; cl_crosshairdot 1; cl_crosshaircolor_r 255; cl_crosshaircolor_g 255; cl_crosshaircolor_b 255; cl_crosshairalpha 0;");

            Task.Run(() =>
            {
                int alpha = 5;
                for (int i = 0; i < 5; i++)
                {
                    if (Player.IsAlive() == false || GameData.MatchInfo.isFreezeTime) break;
                    Thread.Sleep(15);
                    alpha += 50;
                    Program.GameConsole.SendCommand("cl_crosshairalpha " + alpha + ";");
                }

                Thread.Sleep(FlashDuration);

                for (int i = 0; i < 5; i++)
                {
                    if (Player.IsAlive() == false || GameData.MatchInfo.isFreezeTime) break;
                    Thread.Sleep(15);
                    alpha -= 50;
                    Program.GameConsole.SendCommand("cl_crosshairalpha " + alpha + ";");
                }
                Helper.IsFakeFlashed = false;
                PlayerConfig.ResetConfig();
                base.Dispose();
            });

        }

    }
}
