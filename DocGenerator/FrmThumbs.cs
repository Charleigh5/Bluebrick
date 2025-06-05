using System;
using System.Drawing;
using System.Windows.Forms;
using SolidWorks.Interop.swconst;
using sw = SolidWorks.Interop.sldworks; //to resolve ambiguities with System.Windows.Forms.View

namespace DocGenerator
{
    public partial class FrmThumbs : Form
    {
        private static sw.ISldWorks _swApp; //active solidworks instance

        public FrmThumbs(sw.ISldWorks swAppL)
        {
            InitializeComponent();
            _swApp = swAppL;
        }

        private void lstThumbs_SelectedIndexChanged(object sender, EventArgs e)
        {
            var oSel = lstThumbs.SelectedIndices;
            if (oSel.Count == 0) return;
            var iIndex = oSel[0];
            picThumb.Image = Image.FromFile(lstThumbs.Items[iIndex].SubItems[2].Text);
        }

        private void lstThumbs_DoubleClick(object sender, EventArgs e)
        {
            var oSel = lstThumbs.SelectedIndices;
            if (oSel.Count == 0) return;
            var iIndex = oSel[0];
            var sFile = lstThumbs.Items[iIndex].SubItems[1].Text;
            WindowState = FormWindowState.Minimized;
            var swDocSpec = (sw.DocumentSpecification)_swApp.GetOpenDocSpec(sFile);
            swDocSpec.Silent = true;
            _swApp.OpenDoc7(swDocSpec);
            var iErr = 0;
            _swApp.ActivateDoc3(sFile, false,
                (int)swRebuildOnActivation_e.swRebuildActiveDoc, ref iErr);
        }
    }
}