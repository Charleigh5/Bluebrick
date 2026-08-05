using System;
using System.Drawing;
using System.Windows.Forms;

namespace DocGenerator
{
    public partial class FrmPdfThumbs : Form
    {
        public string[] DrwAdd;

        public FrmPdfThumbs()
        {
            InitializeComponent();
        }

        private void lstThumbs_SelectedIndexChanged(object sender, EventArgs e)
        {
            var oSel = lstThumbs.SelectedIndices;
            if (oSel.Count == 0) return;
            var iIndex = oSel[0];
            picThumb.Image = Image.FromFile(lstThumbs.Items[iIndex].SubItems[1].Text);
        }

        private void btnConfirm_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
        }

        private void FrmPdfThumbs_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (DialogResult != DialogResult.OK) return;
            if (lstThumbs.SelectedItems.Count == 0)
            {
                DialogResult = DialogResult.Abort;
                return;
            }

            DrwAdd = new string [lstThumbs.SelectedItems.Count];
            for (var iCnt = 0; iCnt < lstThumbs.SelectedItems.Count; iCnt++)
                DrwAdd[iCnt] = lstThumbs.SelectedItems[iCnt].SubItems[2].Text;
        }

        private void btnAll_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem t in lstThumbs.Items)
            {
                t.Selected = true;
            }
            DialogResult = DialogResult.OK;
        }
    }
}