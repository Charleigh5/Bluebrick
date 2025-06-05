using System;
using System.Windows.Forms;
using System.Reflection;

namespace BlueBrick
{
    internal partial class FrmOpts : Form
    {
        internal readonly ClsSettings Settings;

        internal FrmOpts(ref ClsSettings settings)
        {
            Settings = settings;
            InitializeComponent();

            //general
            txtViki.Text = Settings.GetSetting("vikiPath");

            //panel states
            var chk = bool.TryParse(Settings.GetSetting("cplMainGenShow"), out var show);
            chkGen.Checked = !chk || show;
            chk = bool.TryParse(Settings.GetSetting("cplMainLkyShow"), out show);
            chkLky.Checked = !chk || show;
            chk = bool.TryParse(Settings.GetSetting("cplMainMiscShow"), out show);
            chkMisc.Checked = !chk || show;
            chk = bool.TryParse(Settings.GetSetting("cplMainOppShow"), out show);
            chkEpiOpp.Checked = !chk || show;
            chk = bool.TryParse(Settings.GetSetting("cplMainTlsShow"), out show);
            chkTools.Checked = !chk || show;
            chk = bool.TryParse(Settings.GetSetting("cplMainEPSShow"), out show);
            chkEpiSrch.Checked = !chk || show;
            chk = bool.TryParse(Settings.GetSetting("cplSalesForce"), out show);
            chkSalesForce.Checked = !chk || show;
            chk = bool.TryParse(Settings.GetSetting("cplMainPropShow"), out show);
            chkProp.Checked = !chk || show;

            //salesforce user api
            chk = bool.TryParse(Settings.GetSetting("sfLoggedIn"), out show);
            chkSfLogin.Checked = !chk || show;
            txtSfAccess.Text = Settings.GetSetting("sfAccess", true);
            txtSfRefresh.Text = Settings.GetSetting("sfRefresh", true);
            txtSfName.Text = Settings.GetSetting("sfName");
            txtSfEmail.Text = Settings.GetSetting("sfEmail");
            txtSfPic.Text = Settings.GetSetting("sfPicUrl");
        }

        private void btnDiscard_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            //set settings

            //general
            Settings.SetSetting("vikiPath", txtViki.Text);

            //panel states
            Settings.SetSetting("cplMainGenShow", chkGen.Checked.ToString());
            Settings.SetSetting("cplMainLkyShow", chkLky.Checked.ToString());
            Settings.SetSetting("cplMainMiscShow", chkMisc.Checked.ToString());
            Settings.SetSetting("cplMainOppShow", chkEpiOpp.Checked.ToString());
            Settings.SetSetting("cplMainTlsShow", chkTools.Checked.ToString());
            Settings.SetSetting("cplMainEPSShow", chkEpiSrch.Checked.ToString());
            Settings.SetSetting("cplSalesForce", chkSalesForce.Checked.ToString());
            Settings.SetSetting("cplMainPropShow", chkProp.Checked.ToString());

            //cleanup
            Settings.WriteSettings();
            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnVersion_Click(object sender, EventArgs e)
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            MessageBox.Show($@"Version: {version}", @"Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}