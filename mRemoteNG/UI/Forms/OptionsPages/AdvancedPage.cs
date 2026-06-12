using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using mRemoteNG.App;
using mRemoteNG.App.Info;
using mRemoteNG.Config.Putty;
using mRemoteNG.Connection.Protocol;
using mRemoteNG.Properties;
using mRemoteNG.Tools;
using mRemoteNG.Resources.Language;
using System.Runtime.Versioning;

namespace mRemoteNG.UI.Forms.OptionsPages
{
    [SupportedOSPlatform("windows")]
    public sealed partial class AdvancedPage
    {
        public AdvancedPage()
        {
            InitializeComponent();
            ApplyTheme();
            PageIcon = Resources.ImageConverter.GetImageAsIcon(Properties.Resources.Settings_16x);
            DisplayProperties display = new();
            System.Drawing.Bitmap img = display.ScaleImage(Properties.Resources.PuttyConfig);
            btnLaunchPutty.Image = img;
        }

        #region Public Methods

        public override string PageName
        {
            get => Language.Advanced;
            set { }
        }

        public override void ApplyLanguage()
        {
            base.ApplyLanguage();

            lblSeconds.Text = Language.Seconds;
            lblMaximumPuttyWaitTime.Text = Language.PuttyTimeout;
            chkAutomaticReconnect.Text = Language.DisplayReconnectionDialog;
            chkNoReconnect.Text = Language.CheckboxAutomaticReconnect;
            chkLoadBalanceInfoUseUtf8.Text = Language.LoadBalanceInfoUseUtf8;
            lblConfigurePuttySessions.Text = Language.PuttySessionsConfig;
            btnLaunchPutty.Text = Language.ButtonLaunchPutty;
            btnBrowseCustomPuttyPath.Text = Language._Browse;
            chkUseCustomPuttyPath.Text = Language.CheckboxPuttyPath;
            lblDetectedPutty.Text = Language.DetectedPutty;
            lnkPuttyDownload.Text = Language.DownloadPutty;
            lblUVNCSCPort.Text = Language.UltraVNCSCListeningPort;
        }

        public override void LoadSettings()
        {
            chkAutomaticReconnect.Checked = Properties.OptionsAdvancedPage.Default.ReconnectOnDisconnect;
            chkNoReconnect.Checked = Properties.OptionsAdvancedPage.Default.NoReconnect;
            chkNoReconnect.Enabled = Properties.OptionsAdvancedPage.Default.ReconnectOnDisconnect;

            chkLoadBalanceInfoUseUtf8.Checked = Properties.OptionsAdvancedPage.Default.RdpLoadBalanceInfoUseUtf8;
            numPuttyWaitTime.Value = Properties.OptionsAdvancedPage.Default.MaxPuttyWaitTime;

            chkUseCustomPuttyPath.Checked = Properties.OptionsAdvancedPage.Default.UseCustomPuttyPath;
            txtCustomPuttyPath.Text = Properties.OptionsAdvancedPage.Default.CustomPuttyPath;
            SetPuttyLaunchButtonEnabled();
            UpdateDetectedPuttyPath();

            numUVNCSCPort.Value = Properties.OptionsAdvancedPage.Default.UVNCSCPort;
        }

        public override void SaveSettings()
        {
            Properties.OptionsAdvancedPage.Default.ReconnectOnDisconnect = chkAutomaticReconnect.Checked;
            Properties.OptionsAdvancedPage.Default.NoReconnect = chkNoReconnect.Checked;
            Properties.OptionsAdvancedPage.Default.RdpLoadBalanceInfoUseUtf8 = chkLoadBalanceInfoUseUtf8.Checked;

            bool puttyPathChanged = false;
            if (Properties.OptionsAdvancedPage.Default.CustomPuttyPath != txtCustomPuttyPath.Text)
            {
                puttyPathChanged = true;
                Properties.OptionsAdvancedPage.Default.CustomPuttyPath = txtCustomPuttyPath.Text;
            }

            if (Properties.OptionsAdvancedPage.Default.UseCustomPuttyPath != chkUseCustomPuttyPath.Checked)
            {
                puttyPathChanged = true;
                Properties.OptionsAdvancedPage.Default.UseCustomPuttyPath = chkUseCustomPuttyPath.Checked;
            }

            if (puttyPathChanged)
            {
                // Re-run auto-detection in case the user unchecked the custom
                // path or installed PuTTY since startup.
                GeneralAppInfo.ResetDetectedPuttyPath();
                PuttyBase.PuttyPath = Properties.OptionsAdvancedPage.Default.UseCustomPuttyPath ? Properties.OptionsAdvancedPage.Default.CustomPuttyPath : GeneralAppInfo.PuttyPath;
                PuttySessionsManager.Instance.AddSessions();
                UpdateDetectedPuttyPath();
            }

            Properties.OptionsAdvancedPage.Default.MaxPuttyWaitTime = (int)numPuttyWaitTime.Value;
            Properties.OptionsAdvancedPage.Default.UVNCSCPort = (int)numUVNCSCPort.Value;
        }

        #endregion

        #region Private Methods

        #region Event Handlers

        private void chkUseCustomPuttyPath_CheckedChanged(object sender, EventArgs e)
        {
            txtCustomPuttyPath.Enabled = chkUseCustomPuttyPath.Checked;
            btnBrowseCustomPuttyPath.Enabled = chkUseCustomPuttyPath.Checked;
            SetPuttyLaunchButtonEnabled();
        }

        private void txtCustomPuttyPath_TextChanged(object sender, EventArgs e)
        {
            SetPuttyLaunchButtonEnabled();
        }

        private void btnBrowseCustomPuttyPath_Click(object sender, EventArgs e)
        {
            using FolderBrowserDialog folderDialog = new()
            {
                Description = Language.SelectPuttyFolder,
                UseDescriptionForTitle = true,
                ShowNewFolderButton = false
            };

            // Seed with the folder of the current/detected putty if we have one.
            string current = string.IsNullOrEmpty(txtCustomPuttyPath.Text)
                ? GeneralAppInfo.PuttyPath
                : txtCustomPuttyPath.Text;
            if (!string.IsNullOrEmpty(current))
            {
                try { folderDialog.SelectedPath = Path.GetDirectoryName(current); }
                catch { /* ignored - bad path, dialog opens at default */ }
            }

            if (folderDialog.ShowDialog() != DialogResult.OK) return;

            string resolved = PuttyLocator.ResolveFromDirectory(folderDialog.SelectedPath);
            if (string.IsNullOrEmpty(resolved))
            {
                MessageBox.Show(
                    string.Format(Language.PuttyExeNotFoundInFolder, PuttyLocator.PuttyExeName),
                    Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            txtCustomPuttyPath.Text = resolved;
            SetPuttyLaunchButtonEnabled();
        }

        private void lnkPuttyDownload_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(GeneralAppInfo.UrlPuttyDownload) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("Could not open the PuTTY download page.", ex);
            }
        }

        private void btnLaunchPutty_Click(object sender, EventArgs e)
        {
            try
            {
                PuttyProcessController puttyProcess = new();
                string fileName = chkUseCustomPuttyPath.Checked ? txtCustomPuttyPath.Text : GeneralAppInfo.PuttyPath;
                puttyProcess.Start(fileName);
                puttyProcess.SetControlText("Button", "&Cancel", "&Close");
                puttyProcess.SetControlVisible("Button", "&Open", false);
                puttyProcess.WaitForExit();
            }
            catch (Exception ex)
            {
                MessageBox.Show(Language.ErrorCouldNotLaunchPutty, Application.ProductName,
                                MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
                Runtime.MessageCollector.AddExceptionMessage(Language.ErrorCouldNotLaunchPutty, ex);
            }
        }

        #endregion

        private void SetPuttyLaunchButtonEnabled()
        {
            string puttyPath = chkUseCustomPuttyPath.Checked ? txtCustomPuttyPath.Text : GeneralAppInfo.PuttyPath;

            bool exists = false;
            try
            {
                exists = File.Exists(puttyPath);
            }
            catch
            {
                // ignored
            }

            lblConfigurePuttySessions.Enabled = exists;
            btnLaunchPutty.Enabled = exists;
        }

        /// <summary>
        /// Shows the auto-detected official PuTTY path (independent of the
        /// custom path setting) so the user can confirm what mRemoteNG found
        /// on their system.
        /// </summary>
        private void UpdateDetectedPuttyPath()
        {
            string detected = GeneralAppInfo.PuttyPath;
            txtDetectedPuttyPath.Text = string.IsNullOrEmpty(detected)
                ? Language.PuttyNotDetected
                : detected;
        }

        private void chkNoReconnect_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void chkAutomaticReconnect_CheckedChanged(object sender, EventArgs e)
        {
            chkNoReconnect.Enabled = chkAutomaticReconnect.Checked;
        }

        #endregion

    }
}