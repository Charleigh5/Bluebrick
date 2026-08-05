using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace BlueBrick
{
    public partial class FrmCard : Form
    {
        public FrmCard()
        {
            InitializeComponent();
        }

        public string Opp { get; private set; }
        public string Upc { get; private set; }
        public string Desc { get; private set; }
        public string Cust { get; private set; }
        public string ArtF { get; private set; }
        public string ArtR { get; private set; }
        public int Type { get; private set; }

        private void btnPickF_Click(object sender, EventArgs e)
        {
            if (ofdPick.ShowDialog() == DialogResult.OK) txtArtF.Text = ofdPick.FileName;
        }

        private void btnPickR_Click(object sender, EventArgs e)
        {
            if (ofdPick.ShowDialog() == DialogResult.OK) txtArtR.Text = ofdPick.FileName;
        }

        private int RetType()
        {
            if (radPCS.Checked) return 1;
            if (radPCM.Checked) return 2;
            if (radMGB.Checked) return 3;
            if (radEDS.Checked) return 4;
            return radGBP.Checked ? 5 : 0;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            Opp = txtOpp.Text;
            Upc = txtUPC.Text;
            Desc = txtDesc.Text;
            Cust = txtCust.Text;
            ArtF = txtArtF.Text;
            ArtR = txtArtR.Text;
            Type = RetType();
            DialogResult = DialogResult.OK;
            Close();
        }

        private void radPCS_CheckedChanged(object sender, EventArgs e)
        {
            UpdateType();
        }

        private void radPCM_CheckedChanged(object sender, EventArgs e)
        {
            UpdateType();
        }

        private void radMGB_CheckedChanged(object sender, EventArgs e)
        {
            UpdateType();
        }

        private void radEDS_CheckedChanged(object sender, EventArgs e)
        {
            UpdateType();
        }

        private void radGBP_CheckedChanged(object sender, EventArgs e)
        {
            UpdateType();
        }

        private void UpdateType()
        {
            bool bRear;
            if (radGBP.Checked)
                bRear = true;
            else if (radPCM.Checked)
                bRear = true;
            else
                bRear = false;
            btnPickR.Enabled = bRear;
        }

        private void txtOpp_TextChanged(object sender, EventArgs e)
        {
            if (txtOpp.Text.Length != 0)
                lstFiles.Items.AddRange(ClsEpicor.QuoteAttach(txtOpp.Text));
            else
                lstFiles.Items.Clear();
        }

        private void btnOpen_Click(object sender, EventArgs e)
        {
            if (lstFiles.SelectedItems.Count == 0) return;
            var sFile = lstFiles.SelectedItems[0].Text;
            if (sFile.Length > 0) Process.Start(sFile, "");
        }

        private void btnAttachF_Click(object sender, EventArgs e)
        {
            if (lstFiles.SelectedItems.Count == 0) return;
            var sFile = lstFiles.SelectedItems[0].Text;
            if (sFile.Length > 0) txtArtF.Text = sFile;
        }

        private void btnAttachR_Click(object sender, EventArgs e)
        {
            if (lstFiles.SelectedItems.Count == 0) return;
            var sFile = lstFiles.SelectedItems[0].Text;
            if (sFile.Length > 0) txtArtR.Text = sFile;
        }
    }
}