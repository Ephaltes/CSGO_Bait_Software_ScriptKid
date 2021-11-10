using ScriptKidAntiCheat.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScriptKidAntiCheat.Forms
{
    public partial class SteamPath : Form
    {
        public SteamPath()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            DialogResult result = folderBrowserDialog1.ShowDialog();
            bool checkIfCSGOIsSamePlaceAsSteam = false;

            if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(folderBrowserDialog1.SelectedPath))
            {
                if(Helper.PathToSteam == null || Helper.PathToSteam == "")
                {
                    if (File.Exists(folderBrowserDialog1.SelectedPath + @"\steam.exe"))
                    {
                        Helper.PathToSteam = folderBrowserDialog1.SelectedPath;
                        checkIfCSGOIsSamePlaceAsSteam = true;
                    }
                    else
                    {
                        Log.AddEntry(new LogEntry()
                        {
                            LogTypes = new List<LogTypes> { LogTypes.Analytics },
                            AnalyticsCategory = "Error",
                            AnalyticsAction = "FailedToManuallySelectSteamDir"
                        });
                        MessageBox.Show("The folder you selected is not the steam install directory!\r\rExample:\r" + @"C:\Program Files(x86)\Steam", "Error - Invalid steam directory", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }

                if(Helper.PathToSteam != null && Helper.PathToSteam != "")
                {
                    label5.Text = @"Example: C:\Program Files (x86)\Steam\steamapps\common\Counter-Strike Global Offensive";
                    label6.Text = "Please select CSGO install directory manually:";
                    button1.Text = "Select CSGO Install Folder";
                    string csgoPath = @"\csgo";

                    if(checkIfCSGOIsSamePlaceAsSteam)
                    {
                        csgoPath = @"\steamapps\common\Counter-Strike Global Offensive" + csgoPath;
                    }

                    if (Directory.Exists(folderBrowserDialog1.SelectedPath + csgoPath))
                    {
                        Helper.PathToCSGO = folderBrowserDialog1.SelectedPath + csgoPath;
                        this.Close();
                    }
                    else
                    {
                        Log.AddEntry(new LogEntry()
                        {
                            LogTypes = new List<LogTypes> { LogTypes.Analytics },
                            AnalyticsCategory = "Error",
                            AnalyticsAction = "FailedToManuallySelectCsgoDir"
                        });
                        MessageBox.Show("CSGO could not be found! Did you install CSGO game and steam in different folders? Please try again and select CSGO install folder.\r\rExample:\r" + @"C:\Program Files (x86)\Steam\steamapps\common\Counter-Strike Global Offensive", "Error - Invalid CSGO directory", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            } else
            {
                MessageBox.Show("This cheat software needs to know where steam is installed to work!", "Error - Steam not found", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
