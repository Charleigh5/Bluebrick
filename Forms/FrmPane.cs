using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using Microsoft.Win32;
using SolidWorks.Interop.sldworks;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using Environment = System.Environment;
using System.Runtime.InteropServices;
using SolidWorks.Interop.swconst;
using DocGenerator;

namespace BlueBrick
{
    public partial class FrmPane : Form
    {
        private const string KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\ViraInsight\BlueBrick\Settings";
        private readonly ListViewColumnSorter _lvSort;
        private readonly ISldWorks _swApp;
        public string SFolder;
        private readonly ClsConv _conv;
        internal readonly ClsProperties Prop;
        private bool _pause;
        private readonly string _baseFolder;
        private ClsSettings _settings;

        //public FrmPane(ISldWorks thisSw, int dpi)
        public FrmPane(ISldWorks thisSw)
        {
            //get directory name
            var codeBase = Assembly.GetExecutingAssembly().CodeBase;
            var uri = new UriBuilder(codeBase);
            var path = Uri.UnescapeDataString(uri.Path);
            _baseFolder = Path.GetDirectoryName(path);

            _swApp = thisSw;
            _conv = new ClsConv(this, _baseFolder);
            _settings = new ClsSettings();
            InitializeComponent();
            lblConvBfMemo.Font = new Font(lblConvBfMemo.Font, lblConvBfMemo.Font.Style | FontStyle.Italic);
            lblConvEleMemo.Font = new Font(lblConvEleMemo.Font, lblConvEleMemo.Font.Style | FontStyle.Italic);
            SetStat(0, "Addon loaded.");
            Prop = new ClsProperties(this);

            //get user settings
            var key = Registry.GetValue(KeyPath, @"GenUserPath", RegistryValueKind.String);
            if (key != null)
            {
                txtGenLocPath.Text = key.ToString();
            }
            else
            {
                txtGenLocPath.Text = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                txtGenLocPath.Text += @"\Working\";
            }

            _lvSort = new ListViewColumnSorter();
            lstEPSResults.ListViewItemSorter = _lvSort;

            //bougie new tooltip test
            var custTool = new CustomizedToolTip();
            custTool.SetToolTip(picConvRlRoll, "Example");
            custTool.AutoSize = false;
            custTool.ForeColor = Color.Black;
            custTool.BackColor = Color.LightGray;
            custTool.BorderColor = Color.Black;
            custTool.ShowAlways = true;
            picConvRlRoll.Tag = Resource1.roll;

            //get conversion items
            cmbConvUnitTypes.Items.AddRange(_conv.Types);
            cmbConvRlUnitFrom.Items.AddRange(_conv.Rolls);
            cmbConvRlUnitTo.Items.AddRange(_conv.Rolls);

            //get product cat properties
            cmbPropGenPCat.DataSource = ClsEpicor.ProductCat();
            cmbPropGenPCat.DisplayMember = "catDisp";
            cmbPropGenPCat.ValueMember = "catVal";

            //start panels off collapsed
            cplMainGen.Collapse();
            cplMainLky.Collapse();
            cplMainMisc.Collapse();
            cplMainOpp.Collapse();
            cplMainTls.Collapse();
            cplMainEPS.Collapse();
            cplMainProp.Collapse();
            cplMainSfOpp.Collapse();

            SetPaneVisible();
            SfSetState();
        }

        #region Document Generator Event Methods

        private void btnGenLocExe_Click(object sender, EventArgs e)
        {
            //generate options
            var iOpt = 0;
            if (chkGenTypPDF.Checked) iOpt += (int)ClsEnums.EnumGenOptions.Pdf;
            if (chkGenTypDXF.Checked) iOpt += (int)ClsEnums.EnumGenOptions.Dxf;
            if (chkGenTypSTP.Checked) iOpt += (int)ClsEnums.EnumGenOptions.Step;
            if (chkGenTypPNG.Checked) iOpt += (int)ClsEnums.EnumGenOptions.Png;
            if (chkGenTypIGES.Checked) iOpt += (int)ClsEnums.EnumGenOptions.Iges;
            if (chkGenTypPkt.Checked) iOpt += (int)ClsEnums.EnumGenOptions.Packet;
            if (iOpt == 0) return;

            //set scope
            if (radGenScopeAll.Checked) iOpt += (int)ClsEnums.EnumGenOptions.All;
            if (radGenScopeSingle.Checked) iOpt += (int)ClsEnums.EnumGenOptions.One;

            //set location
            if (radGenLocDesk.Checked) iOpt += (int)ClsEnums.EnumGenOptions.SaveToDesktop;
            if (radGenLocUser.Checked) iOpt += (int)ClsEnums.EnumGenOptions.SaveToUser;
            if (radGenLocPDM.Checked) iOpt += (int)ClsEnums.EnumGenOptions.SaveToPdm;
            SFolder = txtGenLocPath.Text;

            //fire at will!
            PropActive(false);
            var gen = new ClsGenerators();
            gen.SetStatus += c_SetStatus;
            gen.GenerateDoc(_swApp, iOpt, SFolder);
            SetStat(100);
            PropActive(true);
        }

        private void c_SetStatus(object sender, SetStatusEventArgs e)
        {
            SetStat(e.Progress, e.Message);
        }

        private void btnGenLocPath_Click(object sender, EventArgs e)
        {
            if (fldrDiagOpen.ShowDialog() != DialogResult.OK) return;
            txtGenLocPath.Text = fldrDiagOpen.SelectedPath;
            Registry.SetValue(KeyPath, @"GenUserPath", txtGenLocPath.Text, RegistryValueKind.String);
        }

        private void btnGenPreCol_Click(object sender, EventArgs e)
        {
            radGenScopeAll.Select();
            chkGenTypPDF.Checked = true;
            chkGenTypDXF.Checked = false;
            chkGenTypSTP.Checked = false;
            chkGenTypPNG.Checked = false;
            chkGenTypIGES.Checked = false;
            chkGenTypPkt.Checked = true;
        }

        private void btnGenPreDXF_Click(object sender, EventArgs e)
        {
            radGenScopeSingle.Select();
            chkGenTypPDF.Checked = false;
            chkGenTypDXF.Checked = true;
            chkGenTypSTP.Checked = false;
            chkGenTypPNG.Checked = false;
            chkGenTypIGES.Checked = false;
            chkGenTypPkt.Checked = false;
        }

        private void btnGenPreDXFAll_Click(object sender, EventArgs e)
        {
            radGenScopeAll.Select();
            chkGenTypPDF.Checked = false;
            chkGenTypDXF.Checked = true;
            chkGenTypSTP.Checked = false;
            chkGenTypPNG.Checked = false;
            chkGenTypIGES.Checked = false;
            chkGenTypPkt.Checked = false;
        }

        private void btnGenPreFull_Click(object sender, EventArgs e)
        {
            radGenScopeAll.Select();
            chkGenTypPDF.Checked = true;
            chkGenTypDXF.Checked = true;
            chkGenTypSTP.Checked = true;
            chkGenTypIGES.Checked = false;
            chkGenTypPkt.Checked = true;
            radGenLocPDM.Select();
        }

        private void btnGenPrePDF_Click(object sender, EventArgs e)
        {
            radGenScopeSingle.Select();
            chkGenTypPDF.Checked = true;
            chkGenTypDXF.Checked = false;
            chkGenTypSTP.Checked = false;
            chkGenTypPNG.Checked = false;
            chkGenTypIGES.Checked = false;
            chkGenTypPkt.Checked = false;
        }

        private void btnGenPrePNG_Click(object sender, EventArgs e)
        {
            radGenScopeSingle.Select();
            chkGenTypPDF.Checked = false;
            chkGenTypDXF.Checked = false;
            chkGenTypSTP.Checked = false;
            chkGenTypPNG.Checked = true;
            chkGenTypIGES.Checked = false;
            chkGenTypPkt.Checked = false;
        }

        private void btnGenPreSTP_Click(object sender, EventArgs e)
        {
            radGenScopeSingle.Select();
            chkGenTypPDF.Checked = false;
            chkGenTypDXF.Checked = false;
            chkGenTypSTP.Checked = true;
            chkGenTypPNG.Checked = false;
            chkGenTypIGES.Checked = false;
            chkGenTypPkt.Checked = false;
        }

        private void btnGenPreSTPAll_Click(object sender, EventArgs e)
        {
            radGenScopeAll.Select();
            chkGenTypPDF.Checked = false;
            chkGenTypDXF.Checked = false;
            chkGenTypSTP.Checked = true;
            chkGenTypPNG.Checked = false;
            chkGenTypIGES.Checked = false;
            chkGenTypPkt.Checked = false;
        }

        private void chkGenTypPkt_CheckedChanged(object sender, EventArgs e)
        {
            if (chkGenTypPkt.Checked) radGenScopeAll.Checked = true;
        }

        private void radGenLocUser_CheckedChanged(object sender, EventArgs e)
        {
            if (txtGenLocPath.Text.Length != 0) return;
            txtGenLocPath.Text = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            txtGenLocPath.Text += @"\Working\";
        }

        private void radGenScopeSingle_CheckedChanged(object sender, EventArgs e)
        {
            if (radGenScopeSingle.Checked) chkGenTypPkt.Checked = false;
        }

        #endregion Document Generator Event Methods

        #region Tool Event Methods

        private void btnTlsAppear_Click(object sender, EventArgs e)
        {
            ClsTools.ClrAppear(this, _swApp);
        }

        private void btnTlsCard_Click(object sender, EventArgs e)
        {
            ClsCardGen.GenCard(this, _swApp);
        }

        private void btnTlsChkIn_Click(object sender, EventArgs e)
        {
            ClsTools.CheckInAll(this);
        }

        private void btnTlsCopyDwg_Click(object sender, EventArgs e)
        {
            ClsTools.CopyDwg(this, _swApp);
        }

        private void btnTlsDwgNum_Click(object sender, EventArgs e)
        {
            ClsTools.DrwTabRename(this, _swApp);
        }

        private void btnTlsFinSch_Click(object sender, EventArgs e)
        {
            ClsTools.InsertFinSchedule(this, _swApp);
        }

        private void btnTlsFinSearch_Click(object sender, EventArgs e)
        {
            ClsFinish.FinishRename(this, _swApp);
        }

        private void btnTlsFix_Click(object sender, EventArgs e)
        {
            ClsTools.SetStds(this, _swApp);
        }

        private void btnTlsLights_Click(object sender, EventArgs e)
        {
            ClsTools.SetLights(this, _swApp);
        }

        private void btnTlsSerial_Click(object sender, EventArgs e)
        {
            ClsTools.GetSerial(this);
        }

        private void btnTlsShowHidden_Click(object sender, EventArgs e)
        {
            ClsTools.ShowHidden(this, _swApp);
        }

        private void btnTlsThru_Click(object sender, EventArgs e)
        {
            ClsTools.ToggleSel(this, _swApp);
        }

        private void btnTlsViki_Click(object sender, EventArgs e)
        {
            var path = _settings.GetSetting("vikiPath");
            if (string.IsNullOrEmpty(path)) return;
            Process.Start(path);
        }

        private void btnTlsTaskQ_Click(object sender, EventArgs e)
        {
            var frm = new FrmQueue(this);
            frm.ShowDialog();
        }

        #endregion Tool Event Methods

        #region Custom Property Event Methods

        private void btnPropCmtAdd_Click(object sender, EventArgs e)
        {
            Prop.AddComment(txtPropCmtNew.Text);
            txtPropCmts.Text = Prop.GetComments();
            Prop.Model.FeatureManager.UpdateFeatureTree();
        }

        private void btnPropMdlSave_Click(object sender, EventArgs e)
        {
            var swModel = Prop.Model;
            if (swModel == null) return;
            if (swModel.IsOpenedReadOnly())
            {
                SetStat(0, "Model is opened read only and cannot be saved at this time.", true, true);
                return;
            }

            if (Prop.Dirty) btnPropSave_Click(sender, e);
            var iErr = 0;
            var iWarn = 0;
            swModel.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref iErr, ref iWarn);
        }

        private void btnPropReset_Click(object sender, EventArgs e)
        {
            if (_pause) return;
            var active = Prop.SetModel(Prop.Model, chkPropAll.Checked);
            PopulateProps(active);
        }

        private void btnPropSave_Click(object sender, EventArgs e)
        {
            Prop.WriteProp(chkPropAll.Checked);
            UpdatePropIcon();
        }

        private void txtPropGenCustNo_Leave(object sender, EventArgs e)
        {
            if (string.Equals(Prop.CustomerNum, txtPropGenCustNo.Text,
                    StringComparison.CurrentCultureIgnoreCase)) return;
            txtPropGenCustNo.Text = txtPropGenCustNo.Text.ToUpper();
            Prop.CustomerNum = txtPropGenCustNo.Text;
            Prop.Dirty = true;
            UpdatePropIcon();
        }

        private void txtPropGenDesc_Leave(object sender, EventArgs e)
        {
            if (string.Equals(Prop.Description, txtPropGenDesc.Text, StringComparison.CurrentCultureIgnoreCase)) return;
            txtPropGenDesc.Text = txtPropGenDesc.Text.ToUpper();
            Prop.Description = txtPropGenDesc.Text;
            Prop.Dirty = true;
            UpdatePropIcon();
        }

        private void txtPropGenDocNo_Leave(object sender, EventArgs e)
        {
            if (string.Equals(Prop.DocumentNum, txtPropGenDocNo.Text,
                    StringComparison.CurrentCultureIgnoreCase)) return;
            txtPropGenDocNo.Text = txtPropGenDocNo.Text.ToUpper();
            Prop.DocumentNum = txtPropGenDocNo.Text;
            Prop.Dirty = true;
            UpdatePropIcon();
        }

        private void txtPropGenOpp_Leave(object sender, EventArgs e)
        {
            if (string.Equals(Prop.Opportunity, txtPropGenOpp.Text, StringComparison.CurrentCultureIgnoreCase)) return;
            txtPropGenOpp.Text = txtPropGenOpp.Text.ToUpper();
            Prop.Opportunity = txtPropGenOpp.Text;
            Prop.Dirty = true;
            UpdatePropIcon();
        }

        private void txtPropGenPartNo_Leave(object sender, EventArgs e)
        {
            if (string.Equals(Prop.PartNum, txtPropGenPartNo.Text, StringComparison.CurrentCultureIgnoreCase)) return;
            txtPropGenPartNo.Text = txtPropGenPartNo.Text.ToUpper();
            Prop.PartNum = txtPropGenPartNo.Text;
            Prop.Dirty = true;
            UpdatePropIcon();
        }

        private void cmbPropGenType_Leave(object sender, EventArgs e)
        {
            //todo: help
            if (string.Equals(Prop.ProdCat, cmbPropGenPCat.Text, StringComparison.CurrentCultureIgnoreCase)) return;
            cmbPropGenPCat.Text = cmbPropGenPCat.Text.ToUpper();
            Prop.ProdCat = cmbPropGenPCat.SelectedValue.ToString();
            Prop.Dirty = true;
            UpdatePropIcon();
        }

        private void txtPropCust_Leave(object sender, EventArgs e)
        {
            var s = (TextBox)sender;
            if (s == null) return;
            var p = Prop.CustomerProps.Find(x => x.PropertyName == (string)s.Tag);
            if (string.Equals(p.PropertyValue, s.Text, StringComparison.CurrentCultureIgnoreCase)) return;
            Prop.CustomerProps.Remove(p);
            s.Text = s.Text.ToUpper();
            p.PropertyValue = s.Text;
            Prop.CustomerProps.Add(p);
            Prop.Dirty = true;
            UpdatePropIcon();
        }

        internal void PropActive(bool allowed)
        {
            var swModel = allowed ? (ModelDoc2)_swApp.ActiveDoc : null;
            var active = Prop.SetModel(swModel, chkPropAll.Checked);
            PopulateProps(active);

            _pause = !active;
        }

        public void ModelClosing()
        {
            if (_pause) return;
            if (Prop.Model == null) return;
            var temp = Prop.Model;
            Prop.SetModel(null, chkPropAll.Checked);
            if (temp.GetSaveFlag())
            {
                //todo: save meh
                //MessageBox.Show("Do you want to save changes?", )
                //var r =
                //temp.Save3(0, ref _, ref _);
            }
        }

        private void PopulateProps(bool active)
        {
            //populate general tab
            txtPropGenDocNo.Text = Prop.DocumentNum;
            txtPropGenDocNo.ReadOnly = !active;
            txtPropGenPartNo.Text = Prop.PartNum;
            txtPropGenPartNo.ReadOnly = !active;
            txtPropGenDesc.Text = Prop.Description;
            txtPropGenDesc.ReadOnly = !active;
            txtPropGenCustNo.Text = Prop.CustomerNum;
            txtPropGenCustNo.ReadOnly = !active;
            txtPropGenOpp.Text = Prop.Opportunity;
            txtPropGenOpp.ReadOnly = !active;
            txtPropGenCust.Text = Prop.CustomerName;
            txtPropGenRev.Text = Prop.Revision;
            cmbPropGenPCat.SelectedValue = Prop.ProdCat;
            cmbPropGenPCat.Enabled = active;

            //remove old customer props
            foreach (var lbl in pnlPropCustomer.Controls.OfType<Label>())
            {
                pnlPropCustomer.Controls.Remove(lbl);
            }

            foreach (var txt in pnlPropCustomer.Controls.OfType<TextBox>())
            {
                pnlPropCustomer.Controls.Remove(txt);
            }

            //populate customer specific
            var tSize = txtPropGenPartNo.Size;
            var lSize = lblPropGenPartNo.Size;
            foreach (var c in Prop.CustomerProps)
            {
                var count = pnlPropCustomer.Controls.OfType<TextBox>().ToList().Count;

                //create label
                var cLbl = new Label();
                cLbl.Location = new Point(3, count * (tSize.Height + 5) + 2);
                cLbl.Size = lSize;
                cLbl.Name = "lbl_" + (count + 1);
                cLbl.Text = c.PropertyLabel + @":";
                pnlPropCustomer.Controls.Add(cLbl);

                //create text box
                var cTxt = new TextBox();
                cTxt.Location = new Point(lSize.Width + 8, count * (tSize.Height + 5) + 2);
                cTxt.Size = tSize;
                cTxt.Name = "txt_" + (count + 1);
                cTxt.Text = c.PropertyValue;
                cTxt.Tag = c.PropertyName;
                cTxt.Leave += txtPropCust_Leave;
                pnlPropCustomer.Controls.Add(cTxt);
            }

            //handle comments
            RefreshComments();
            btnPropCmtAdd.Enabled = active;

            //done with properties
            UpdatePropIcon();

            //populate usage tab
            lstPropOrd.Items.Clear();
            if (!active) return;
            if (chkPropSearch.Checked)
                lstPropOrd.Items.AddRange(ClsEpicor.UsageSearch(this, Prop.PartNum, Prop.CustomerNum));
        }

        internal void RefreshComments()
        {
            txtPropCmts.Text = Prop.GetComments();
        }

        public void ReadPropFromPath(string docPath)
        {
            if (_pause) return;
            var swModel = _swApp.IGetOpenDocumentByName2(docPath);
            var active = Prop.SetModel(swModel, chkPropAll.Checked);
            PopulateProps(active);
        }

        public void ReadPropFromActive()
        {
            if (_pause) return;
            var swModel = (IModelDoc2)_swApp.ActiveDoc;
            var active = Prop.SetModel(swModel, chkPropAll.Checked);
            PopulateProps(active);
        }

        public void ReadPropFromModel(IModelDoc2 swModel)
        {
            if (_pause) return;
            var active = Prop.SetModel(swModel, chkPropAll.Checked);
            PopulateProps(active);
        }

        public void SetSerial(string serial)
        {
            cplMainProp.Expand();
            txtPropGenPartNo.Text = serial.ToUpper();
            Prop.PartNum = txtPropGenPartNo.Text;
            Prop.Dirty = true;
            UpdatePropIcon();
        }

        private void UpdatePropIcon()
        {
            btnPropReset.Image = Prop.Dirty ? Resource1.icon_PropDirty : Resource1.icon_PropReset;
        }

        #endregion Custom Property Event Methods

        #region Feelin' Lucky Event Methods

        private void btnLkySrch_Click(object sender, EventArgs e)
        {
            cmbLkySrch.Items.Add(cmbLkySrch.Text);
            lstLkyResults.Items.Clear();
            imlThumbs.Images.Clear();
            ClsLucky.FeelingLucky(this, cmbLkySrch.Text);
        }

        private void cmbLkySrch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) btnLkySrch_Click(sender, e);
        }

        private void lstLkyResults_DoubleClick(object sender, EventArgs e)
        {
            var sFile = lstLkyResults.SelectedItems[0].SubItems[2].Text;
            sFile += @"\" + lstLkyResults.SelectedItems[0].SubItems[1].Text;
            var iFileId = int.Parse(lstLkyResults.SelectedItems[0].Text);
            ClsLucky.PdmFile(this, _swApp, iFileId, sFile);
        }

        private void lstLkyResults_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.C || !e.Control) return;
            if (lstLkyResults.SelectedItems.Count == 0) return;
            var txt = lstLkyResults.SelectedItems[0].SubItems[1].Text;
            txt += @"\" + lstLkyResults.SelectedItems[0].SubItems[2].Text;
            Clipboard.SetText(txt);
        }

        private void lstLkyResults_ItemDrag(object sender, ItemDragEventArgs e)
        {
            var sFile = lstLkyResults.SelectedItems[0].SubItems[2].Text;
            sFile += @"\" + lstLkyResults.SelectedItems[0].SubItems[1].Text;
            var iFileId = int.Parse(lstLkyResults.SelectedItems[0].Text);
            ClsLucky.PdmFile(this, _swApp, iFileId, sFile, true);
        }

        private void tsiInsertPart_Click(object sender, EventArgs e)
        {
            if (lstLkyResults.SelectedItems.Count == 0) return;
            var sFile = lstLkyResults.SelectedItems[0].SubItems[2].Text;
            sFile += @"\" + lstLkyResults.SelectedItems[0].SubItems[1].Text;
            var iFileId = int.Parse(lstLkyResults.SelectedItems[0].Text);
            ClsLucky.PdmFile(this, _swApp, iFileId, sFile, true);
        }

        private void tsiOpenFile_Click(object sender, EventArgs e)
        {
            if (lstLkyResults.SelectedItems.Count == 0) return;
            lstLkyResults_DoubleClick(sender, e);
        }

        private void tsiOpenExplore_Click(object sender, EventArgs e)
        {
            if (lstLkyResults.SelectedItems.Count == 0) return;
            var sFold = lstLkyResults.SelectedItems[0].SubItems[2].Text + @"\";
            Process.Start(sFold);
        }

        #endregion Feelin' Lucky Event Methods

        #region Epicor Part Search Event Methods

        private void btnEPSClear_Click(object sender, EventArgs e)
        {
            txtEPSPart.Clear();
            txtEPSDesc.Clear();
            lstEPSResults.Items.Clear();
        }

        private void btnEPSSearch_Click(object sender, EventArgs e)
        {
            lstEPSResults.Items.Clear();
            ClsEpicor.EpiSearch(this, txtEPSPart.Text, txtEPSDesc.Text);
        }

        private void lstEPSResults_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (e.Column == _lvSort.SortColumn) //determine if clicked column is already the column that is being sorted
            {
                //reverse the current sort direction for this column
                _lvSort.SoOrder = _lvSort.SoOrder == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
            }
            else //set the column number that is to be sorted; default to ascending
            {
                _lvSort.SortColumn = e.Column;
                _lvSort.SoOrder = SortOrder.Ascending;
            }

            lstEPSResults.Sort(); //perform the sort with these new sort options
        }

        private void lstEPSResults_DoubleClick(object sender, EventArgs e)
        {
            cplMainLky.Expand();
            cmbLkySrch.Text = lstEPSResults.SelectedItems[0].Text;
            btnLkySrch_Click(sender, e);
        }

        private void lstEPSResults_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.C || !e.Control) return;
            if (lstEPSResults.SelectedItems.Count > 0) Clipboard.SetText(lstEPSResults.SelectedItems[0].Text);
        }

        private void tsiSearch_Click(object sender, EventArgs e)
        {
            if (lstEPSResults.SelectedItems.Count == 0) return;
            cplMainLky.Expand();
            cmbLkySrch.Text = lstEPSResults.SelectedItems[0].Text;
            btnLkySrch_Click(sender, e);
        }

        private void tsiCopyDesc_Click(object sender, EventArgs e)
        {
            if (lstEPSResults.SelectedItems.Count > 0)
                Clipboard.SetText(lstEPSResults.SelectedItems[0].SubItems[1].Text);
        }

        private void tsiCopyPart_Click(object sender, EventArgs e)
        {
            if (lstEPSResults.SelectedItems.Count > 0) Clipboard.SetText(lstEPSResults.SelectedItems[0].Text);
        }

        private void txtEPSDesc_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter) btnEPSSearch_Click(sender, e);
        }

        private void txtEPSPart_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter) btnEPSSearch_Click(sender, e);
        }

        #endregion Epicor Part Search Event Methods

        #region Epicor Opp Search Event Methods

        private void btnEOSUser_Click(object sender, EventArgs e)
        {
            var form = new FrmTasks(this, _baseFolder);
            var result = form.ShowDialog();
            if (result != DialogResult.OK) return;
            txtEOSOpp.Text = form.SOpp;
            GetTaskEpi();
        }

        private void btnEOSOpp_Click(object sender, EventArgs e)
        {
            var swModel = (IModelDoc2)_swApp.ActiveDoc;
            if (swModel != null)
            {
                txtEOSOpp.Text = swModel.CustomInfo[@"Opp"];
            }
            else
            {
                SetStat(100, @"No open documents.", true, true);
                txtEOSOpp.Text = string.Empty;
            }

            GetTaskEpi();
        }

        private void btnEOSEmail_Click(object sender, EventArgs e)
        {
            var sOpp = txtEOSOpp.Text.Trim();
            if (sOpp.Length == 0)
            {
                MessageBox.Show(@"No opportunity selected!", @"Validation Error", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            ClsEpicor.OppEmail(this, sOpp, txtEOSDesc.Text.Trim());
        }

        private void lstEOSAttach_DoubleClick(object sender, EventArgs e)
        {
            if (lstEOSAttach.SelectedItems.Count == 0) return;
            var sFile = lstEOSAttach.SelectedItems[0].Text;
            if (sFile.Length > 0) Process.Start(sFile, "");
        }

        private void lstEOSAttach_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.C || !e.Control) return;
            if (lstEOSAttach.SelectedItems.Count > 0) Clipboard.SetText(lstEOSAttach.SelectedItems[0].Text);
        }

        private void txtEOSOpp_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter) GetTaskEpi();
        }

        private void txtEOSOpp_Leave(object sender, EventArgs e)
        {
            GetTaskEpi();
        }

        private void GetTaskEpi()
        {
            var bClear = false;
            string[,] asRecord = default;
            if (txtEOSOpp.Text.Length > 0)
            {
                asRecord = ClsEpicor.TaskSearch(this, txtEOSOpp.Text, false);
                if (asRecord == null)
                {
                    bClear = true;
                    SetStat(100, @"Task not found.", true);
                }
            }
            else
            {
                bClear = true;
            }

            if (asRecord != null)
            {
                // ReSharper disable once LocalizableElement
                txtEOSDue.Text = DateTime.TryParse(asRecord[0, 2], out var dt) ? $"{dt:M/d/yyyy}" : "";
                txtEOSCust.Text = asRecord[0, 4];
                txtEOSUser.Text = asRecord[0, 1];
                txtEOSCmt.Text = asRecord[0, 3];
                txtEOSDesc.Text = asRecord[0, 5];

                //setup memo list
                cmbEOSMemoSel.DataSource = null;
                var memos = new List<OppMemo>();
                for (var i = 0; i < asRecord.GetUpperBound(0) + 1; i++)
                {
                    var title = asRecord[i, 6].Trim();
                    if (title.Length == 0) title += @"(blank)";
                    memos.Add(new OppMemo { Title = title, Description = asRecord[i, 7] });
                }

                cmbEOSMemoSel.DataSource = memos;
                cmbEOSMemoSel.DisplayMember = "Title";
                cmbEOSMemoSel.ValueMember = "Description";
                cmbEOSMemoSel.DropDownStyle = ComboBoxStyle.DropDownList;
                cmbEOSMemoSel.SelectedIndex = 0;
                txtEOSMemoBody.Text = cmbEOSMemoSel.SelectedValue.ToString().Replace("\r\n", "\n").Replace("\r", "\n")
                    .Replace("\n", "\r\n");

                //setup attachments
                lstEOSAttach.Items.Clear();
                var lstItems = ClsEpicor.QuoteAttach(txtEOSOpp.Text);
                foreach (var li in lstItems)
                {
                    lstEOSAttach.Items.Add(li.SubItems[0].Text);
                }

                colEOSFile.Width = -1;
            }

            if (!bClear) return;
            txtEOSCmt.Clear();
            txtEOSCust.Clear();
            txtEOSDue.Clear();
            txtEOSUser.Clear();
            txtEOSDesc.Clear();
            txtEOSMemoBody.Clear();
            lstEOSAttach.Items.Clear();
            cmbEOSMemoSel.DataSource = null;
        }

        private void cmbEOSMemoSel_SelectedIndexChanged(object sender, EventArgs e)
        {
            txtEOSMemoBody.Text = cmbEOSMemoSel.SelectedValue != null ? cmbEOSMemoSel.SelectedValue.ToString() : "";
        }

        public struct OppMemo
        {
            public string Title { get; set; }
            public string Description { get; set; }
        }

        private void tsiOpBrowse_Click(object sender, EventArgs e)
        {
            if (lstEOSAttach.SelectedItems.Count == 0) return;
            var sFold = Path.GetDirectoryName(lstEOSAttach.SelectedItems[0].Text);
            if (sFold != null) Process.Start(sFold);
        }

        #endregion Epicor Opp Search Event Methods

        #region SalesForce Opp Search Event Methods

        private void SfSetState()
        {
            //get logged in state
            var chk = bool.TryParse(_settings.GetSetting("sfLoggedIn"), out var val);
            var loggedIn = !chk || val;

            //set proper control states
            tlpMainSfOpp.Visible = loggedIn;
            lblSfUserName.Text = loggedIn ? _settings.GetSetting("sfName") : @"Not logged in";
            lblSfEmail.Text = loggedIn ? _settings.GetSetting("sfEmail") : string.Empty;
            picSfUser.ImageLocation = loggedIn ? _settings.GetSetting("sfPicUrl") : string.Empty;
            btnSfLogin.Text = loggedIn ? @"Logout" : @"Login";
        }

        private void btnSfLogin_Click(object sender, EventArgs e)
        {
            //get logged in state
            var chk = bool.TryParse(_settings.GetSetting("sfLoggedIn"), out var val);
            var loggedIn = !chk || val;

            var msg = string.Empty;

            //log in
            if (!loggedIn)
            {
                msg = "Logging in...";
                SetStat(0, msg, false, true);
                var sf = new ClsSalesForce(ref _settings);
                msg = !sf.Login() ? "Login failed!" : "Logged in successfully!";
            }
            else //logout
            {
                _settings.SetSetting("sfAccess", string.Empty);
                _settings.SetSetting("sfRefresh", string.Empty);
                _settings.SetSetting("sfName", string.Empty);
                _settings.SetSetting("sfEmail", string.Empty);
                _settings.SetSetting("sfPicUrl", string.Empty);
                _settings.SetSetting("sfLoggedIn", "false");
                _settings.WriteSettings();

                msg = "Logged out successfully!" + Environment.NewLine;
            }

            //set control state and finish
            SfSetState();
            SetStat(100, msg, true);
        }

        private void btnSFOSUser_Click(object sender, EventArgs e)
        {
            //todo: implement for salesforce
            MessageBox.Show(@"Not yet implemented for SalesForce opps.");
        }

        private void btnSFOSOpp_Click(object sender, EventArgs e)
        {
            var swModel = (IModelDoc2)_swApp.ActiveDoc;
            if (swModel != null)
            {
                txtSFOSOpp.Text = swModel.CustomInfo[@"Opp"];
            }
            else
            {
                SetStat(100, @"No open documents.", true, true);
                txtSFOSOpp.Text = string.Empty;
            }

            GetTaskSf();
        }

        private void btnSFOSEmail_Click(object sender, EventArgs e)
        {
            MessageBox.Show(@"Not yet implemented for SalesForce opps.");
            
            //todo: add email to sf opps
            //var sOpp = txtSFOSOpp.Text.Trim();
            //if (sOpp.Length == 0)
            //{
            //    MessageBox.Show(@"No opportunity selected!", @"Validation Error", MessageBoxButtons.OK,
            //        MessageBoxIcon.Error);
            //    return;
            //}

            //ClsEpicor.OppEmail(this, sOpp, txtSFOSOpp.Text.Trim());
        }

        private void lstSFOSAttach_DoubleClick(object sender, EventArgs e)
        {
            if (lstSFOSAttach.SelectedItems.Count == 0) return;
            var sFile = lstSFOSAttach.SelectedItems[0].Text;
            if (sFile.Length > 0) Process.Start(sFile, "");
        }

        private void lstSFOSAttach_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.C || !e.Control) return;
            if (lstSFOSAttach.SelectedItems.Count > 0) Clipboard.SetText(lstSFOSAttach.SelectedItems[0].Text);
        }

        private void txtSFOSOpp_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter) GetTaskSf();
        }

        private void txtSFOSOpp_Leave(object sender, EventArgs e)
        {
            GetTaskSf();
        }

        private void GetTaskSf()
        {
            var bClear = false;
            ClsSalesForce.Opportunity asRecord;
            if (txtSFOSOpp.Text.Length > 0)
            {
                var sf = new ClsSalesForce(ref _settings);
                asRecord = sf.GetOpp(txtSFOSOpp.Text);
                if (asRecord == null)
                {
                    bClear = true;
                    SetStat(100, @"Task not found.", true);
                }
            }
            else
            {
                bClear = true;
                asRecord = null;
            }

            if (asRecord != null)
            {
                //todo: missing fields
                //todo: task list
                //todo: attachments
                
                // ReSharper disable once LocalizableElement
                //txtSFOSDue.Text = DateTime.TryParse(asRecord[0, 2], out var dt) ? $"{dt:M/d/yyyy}" : "";
                txtSFOSCust.Text = asRecord.records[0].Account.Name;
                //txtSFOSUser.Text = asRecord[0, 1];
                txtSFOSDesc.Text = asRecord.records[0].Opportunity_Description__c;

                //setup memo list
                //cmbSFOSMemoSel.DataSource = null;
                //var memos = new List<OppMemo>();
                //for (var i = 0; i < asRecord.GetUpperBound(0) + 1; i++)
                //{
                //    var title = asRecord[i, 6].Trim();
                //    if (title.Length == 0) title += @"(blank)";
                //    memos.Add(new OppMemo { Title = title, Description = asRecord[i, 7] });
                //}

                //cmbSFOSMemoSel.DataSource = memos;
                //cmbSFOSMemoSel.DisplayMember = "Title";
                //cmbSFOSMemoSel.ValueMember = "Description";
                //cmbSFOSMemoSel.DropDownStyle = ComboBoxStyle.DropDownList;
                //cmbSFOSMemoSel.SelectedIndex = 0;

                //setup attachments
                //lstSFOSAttach.Items.Clear();
                //var lstItems = ClsEpicor.QuoteAttach(txtSFOSOpp.Text);
                //foreach (var li in lstItems)
                //{
                //    lstSFOSAttach.Items.Add(li.SubItems[0].Text);
                //}

                //colSFOSFile.Width = -1;
            }

            if (!bClear) return;
            txtSFOSCust.Clear();
            txtSFOSDue.Clear();
            txtSFOSUser.Clear();
            txtSFOSDesc.Clear();
            lstSFOSAttach.Items.Clear();
            cmbSFOSMemoSel.DataSource = null;
        }

        private void cmbSFOSMemoSel_SelectedIndexChanged(object sender, EventArgs e)
        {
            //txtSFOSMemoBody.Text = cmbSFOSMemoSel.SelectedValue != null ? cmbSFOSMemoSel.SelectedValue.ToString() : "";
        }

        private void tsiSfOpBrowse_Click(object sender, EventArgs e)
        {
            if (lstSFOSAttach.SelectedItems.Count == 0) return;
            var sFold = Path.GetDirectoryName(lstSFOSAttach.SelectedItems[0].Text);
            if (sFold != null) Process.Start(sFold);
        }

        #endregion SalesForce Opp Search Event Methods

        #region Misc. Utility Event Methods

        private void btnConvBfAdd_Click(object sender, EventArgs e)
        {
            //validate
            var err = 0;
            var values = new float[4];
            var chk = float.TryParse(txtConvBfThk.Text, out values[0]);
            if (!chk && values[0] > 0)
            {
                SetStat(0, @"Please enter a valid thickness!");
                err++;
            }

            chk = float.TryParse(txtConvBfWid.Text, out values[1]);
            if (!chk && values[1] > 0)
            {
                SetStat(0, @"Please enter a valid width!");
                err++;
            }

            chk = float.TryParse(txtConvBfLen.Text, out values[2]);
            if (!chk && values[2] > 0)
            {
                SetStat(0, @"Please enter a valid length!");
                err++;
            }

            chk = float.TryParse(txtConvBfQty.Text, out values[3]);
            if (!chk && values[3] > 0)
            {
                SetStat(0, @"Please enter a valid quantity!");
                err++;
            }

            if (err > 0) return;

            //calculate values
            var s = values[0] + "x" + values[1] + "x" + values[2] + " * " + values[3];
            var f = values[0] * values[1] * values[2] / 144 * values[3];

            //add to list
            var lvi = new ListViewItem(s);
            lvi.SubItems.Add(Math.Round(f, 1).ToString(CultureInfo.CurrentCulture));
            lvwConvBf.Items.Add(lvi);

            //update total
            float.TryParse(txtConvBfTot.Text, out var t);
            t += f;
            txtConvBfTot.Text = Math.Round(t, 1).ToString(CultureInfo.CurrentCulture);

            //reset form fields
            txtConvBfThk.Text = string.Empty;
            txtConvBfWid.Text = string.Empty;
            txtConvBfLen.Text = string.Empty;
            txtConvBfQty.Text = string.Empty;
        }

        private void btnConvBfClr_Click(object sender, EventArgs e)
        {
            txtConvBfThk.Text = string.Empty;
            txtConvBfWid.Text = string.Empty;
            txtConvBfLen.Text = string.Empty;
            txtConvBfQty.Text = string.Empty;
            txtConvBfTot.Text = @"0";
            lvwConvBf.Items.Clear();
        }

        private void btnConvBfDel_Click(object sender, EventArgs e)
        {
            //get value and remove list item
            var lvi = lvwConvBf.Items[lvwConvBf.SelectedIndices[0]];
            float.TryParse(lvi.SubItems[1].Text, out var f);
            lvwConvBf.Items.Remove(lvi);

            //update total
            float.TryParse(txtConvBfTot.Text, out var t);
            t -= f;
            txtConvBfTot.Text = Math.Round(t, 1).ToString(CultureInfo.CurrentCulture);
        }

        private void btnConvEleClr_Click(object sender, EventArgs e)
        {
            _conv.ElectReset();
        }

        private void btnConvRlCalc_Click(object sender, EventArgs e)
        {
            RollCalc();
        }

        private void btnConvRlReset_Click(object sender, EventArgs e)
        {
            txtConvRlID.Text = string.Empty;
            txtConvRlOD.Text = string.Empty;
            txtConvRlCore.Text = string.Empty;
            txtConvRlMat.Text = string.Empty;
            txtConvRlTotal.Text = string.Empty;
        }

        private void btnConvUnitCalc_Click(object sender, EventArgs e)
        {
            UnitConv();
        }

        private void btnConvUnitSwap_Click(object sender, EventArgs e)
        {
            (cmbConvUnitFrom.SelectedItem, cmbConvUnitTo.SelectedItem) =
                (cmbConvUnitTo.SelectedItem, cmbConvUnitFrom.SelectedItem);
            UnitConv();
        }

        private void cmbConvUnitFrom_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if (e.KeyCode == Keys.Tab) e.IsInputKey = true;
        }

        private void cmbConvUnitFrom_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbConvUnitFrom.SelectedItem == null)
            {
                lblConvUnitFSym.Text = string.Empty;
                lblConvUnitFDesc.Text = string.Empty;
                return;
            }

            var unit = (ClsConv.Unit)cmbConvUnitFrom.SelectedItem;
            lblConvUnitFSym.Text = unit.Symbol;
            lblConvUnitFDesc.Text = unit.Description;
            UnitConv();
        }

        private void cmbConvUnitTo_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbConvUnitTo.SelectedItem == null)
            {
                lblConvUnitTSym.Text = string.Empty;
                lblConvUnitTDesc.Text = string.Empty;
                return;
            }

            var unit = (ClsConv.Unit)cmbConvUnitTo.SelectedItem;
            lblConvUnitTSym.Text = unit.Symbol;
            lblConvUnitTDesc.Text = unit.Description;
            UnitConv();
        }

        private void cmbConvUnitTypes_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                cmbConvUnitFrom.Items.Clear();
                cmbConvUnitTo.Items.Clear();
                var items = _conv.GetItems(cmbConvUnitTypes.Text);
                cmbConvUnitFrom.Items.AddRange(items);
                if (cmbConvUnitFrom.Items.Count > 0) cmbConvUnitFrom.SelectedItem = cmbConvUnitFrom.Items[0];
                cmbConvUnitTo.Items.AddRange(items);
                if (cmbConvUnitTo.Items.Count > 0)
                    cmbConvUnitTo.SelectedItem =
                        cmbConvUnitTo.Items.Count > 1 ? cmbConvUnitTo.Items[1] : cmbConvUnitTo.Items[0];
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void txtConvEleAmp_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter || e.KeyChar == (char)Keys.Tab)
                txtConvEleAmp_Leave(sender, e);
        }

        private void txtConvEleAmp_Leave(object sender, EventArgs e)
        {
            _conv.ElectUpdate("amp", txtConvEleAmp.Text);
        }

        private void txtConvEleOhm_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter || e.KeyChar == (char)Keys.Tab)
                txtConvEleOhm_Leave(sender, e);
        }

        private void txtConvEleOhm_Leave(object sender, EventArgs e)
        {
            _conv.ElectUpdate("ohm", txtConvEleOhm.Text);
        }

        private void txtConvEleVolt_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter || e.KeyChar == (char)Keys.Tab)
                txtConvEleVolt_Leave(sender, e);
        }

        private void txtConvEleVolt_Leave(object sender, EventArgs e)
        {
            _conv.ElectUpdate("volt", txtConvEleVolt.Text);
        }

        private void txtConvEleWatt_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter || e.KeyChar == (char)Keys.Tab)
                txtConvEleWatt_Leave(sender, e);
        }

        private void txtConvEleWatt_Leave(object sender, EventArgs e)
        {
            _conv.ElectUpdate("watt", txtConvEleWatt.Text);
        }

        private void txtConvUnitFVal_TextChanged(object sender, EventArgs e)
        {
            UnitConv();
        }

        private void RollCalc()
        {
            if (cmbConvRlUnitFrom.SelectedItem == null) return;
            if (cmbConvRlUnitTo.SelectedItem == null) return;
            txtConvRlTotal.Text =
                ClsConv.RollCalc(txtConvRlID.Text, txtConvRlOD.Text, txtConvRlCore.Text, txtConvRlMat.Text,
                    cmbConvRlUnitFrom.SelectedItem, cmbConvRlUnitTo.SelectedItem).ToString(@"G");
        }

        private void UnitConv()
        {
            //validation
            var valid = cmbConvUnitFrom.SelectedItem != null;
            if (cmbConvUnitTo.SelectedItem == null) valid = false;
            if (!double.TryParse(txtConvUnitFVal.Text, out var from)) valid = false;
            if (from == 0) valid = false;
            if (!valid) return;

            //call conversion
            txtConvUnitTVal.Text =
                _conv.UnitConvert(cmbConvUnitFrom.SelectedItem,
                    cmbConvUnitTo.SelectedItem, from).ToString(@"G");
        }

        #endregion Misc. Utility Event Methods

        #region General Event Methods

        private void FrmPane_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Tab) return;
            var con = ActiveControl;
            if (con.GetType().ToString() != "BlueBrick.CollapsePanel") return;
            var cont = (CollapsePanel)con;
            if (cont.Collapsed)
                ProcessTabKey(e.Modifiers != Keys.Shift);
            else
                cont.ProcTab(e.Modifiers != Keys.Shift);
            e.Handled = true;
        }

        internal int GetPrg()
        {
            return prgStatus.Value;
        }

        public void SetStat(int iStat = 0, string sStat = "", bool bDone = false, bool bStart = false)
        {
            if (sStat.Length > 0)
            {
                sStat = DateTime.Now.ToString("g") + ": " + sStat;
                txtStatus.AppendText(sStat);
                txtStatus.AppendText(Environment.NewLine);
            }

            if (bStart) prgStatus.Value = 0;
            prgStatus.Value = bDone ? 100 : iStat;
            Application.DoEvents();
        }

        private void SetPaneVisible()
        {
            var chk = bool.TryParse(_settings.GetSetting("cplMainGenShow"), out var show);
            cplMainGen.Visible = !chk || show;
            chk = bool.TryParse(_settings.GetSetting("cplMainLkyShow"), out show);
            cplMainLky.Visible = !chk || show;
            chk = bool.TryParse(_settings.GetSetting("cplMainMiscShow"), out show);
            cplMainMisc.Visible = !chk || show;
            chk = bool.TryParse(_settings.GetSetting("cplMainOppShow"), out show);
            cplMainOpp.Visible = !chk || show;
            chk = bool.TryParse(_settings.GetSetting("cplMainTlsShow"), out show);
            cplMainTls.Visible = !chk || show;
            chk = bool.TryParse(_settings.GetSetting("cplMainEPSShow"), out show);
            cplMainEPS.Visible = !chk || show;
            chk = bool.TryParse(_settings.GetSetting("cplMainPropShow"), out show);
            cplMainProp.Visible = !chk || show;
            chk = bool.TryParse(_settings.GetSetting("cplSalesForce"), out show);
            cplMainSfOpp.Visible = !chk || show;
        }

        private void btnOpt_Click(object sender, EventArgs e)
        {
            using (var formO = new FrmOpts(ref _settings))
            {
                var result = formO.ShowDialog();
                if (result != DialogResult.OK) return;
                _settings = formO.Settings;
                _settings.WriteSettings();
            }

            SetPaneVisible();
        }

        #endregion General Event Methods
    }

    public class ListViewColumnSorter : IComparer
    {
        private readonly CaseInsensitiveComparer _cmpOCompare; //case insensitive comparer object

        public ListViewColumnSorter() //initializes various elements
        {
            SortColumn = 0;
            SoOrder = SortOrder.None;
            _cmpOCompare = new CaseInsensitiveComparer();
        }

        public int SortColumn { set; get; } //number of the column to apply the sorting operation (defaults to '0').

        public SortOrder SoOrder { set; get; } //order of sorting (for example, 'Ascending' or 'Descending').

        public int Compare(object x, object y) //inherited from IComparer, compares the two objects passed
        {
            //cast the objects to be compared to ListViewItem objects
            var compareResult = 0;
            var listviewX = (ListViewItem)x;
            var listviewY = (ListViewItem)y;
            if ((SortColumn == 3) | (SortColumn == 4)) //check for in columns
            {
                if (listviewX != null)
                    if (listviewY != null)
                        compareResult = _cmpOCompare.Compare(float.Parse(listviewX.SubItems[SortColumn].Text),
                            float.Parse(listviewY.SubItems[SortColumn].Text));
            }
            else
            {
                if (listviewX != null)
                    if (listviewY != null)
                        compareResult = _cmpOCompare.Compare(listviewX.SubItems[SortColumn].Text,
                            listviewY.SubItems[SortColumn].Text);
            }

            switch (SoOrder) //calculate correct return value based on object comparison
            {
                case SortOrder.Ascending: //ascending, return normal result of compare operation
                    return compareResult;
                case SortOrder.Descending: //descending, return negative result of compare operation
                    return -compareResult;
                case SortOrder.None: //return '0' to indicate they are equal
                default:
                    return 0;
            }
        }
    }

    /*[ Compilation unit ----------------------------------------------------------
    Component       : ToolTip With Image
    Name            : CustomizedToolTip.cs
    Last Author     : Ravikant, Siemens Medical Solutions, Inc.
    Language        : C#
    Description     : Implementation of CustomizedToolTip
    Copyright (C) ravikant.cse@gmail.com 2006-2010 All Rights Reserved
    -----------------------------------------------------------------------------*/
    /*] END */

    /// <summary>
    /// CustomizedToolTip to create tooltips with Image.
    /// </summary>
    internal class CustomizedToolTip : ToolTip
    {
        #region Constants

        private const int TooltipWidth = 250;
        private const int TooltipHeight = 250;
        private const int BorderThickness = 1;
        private const int Padding = 10;
        private const int DefaultImageWidth = 250;

        #endregion

        #region Fields

        private static Color _myBorderColor = Color.Black;
        private static readonly Font MyFont = new Font("Arial", 8);
        private readonly StringFormat _myTextFormat = new StringFormat();
        private Rectangle _myImageRectangle;
        private Rectangle _myTextRectangle;
        private Rectangle _myToolTipRectangle;
        private Brush _myBackColorBrush = new SolidBrush(Color.LightGray);
        private Brush _myTextBrush = new SolidBrush(Color.Black);
        private Brush _myBorderBrush = new SolidBrush(_myBorderColor);
        private Size _mySize = new Size(TooltipWidth, TooltipHeight);
        private int _myInternalImageWidth = DefaultImageWidth;
        private bool _myAutoSize = true;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets a value indicating whether the ToolTip is drawn by the operating
        /// system or by code that you provide.
        /// If true, the properties 'ToolTipIcon' and 'ToolTipTitle' will set to their default values
        /// and the image will display in ToolTip otherwise only text will display.
        /// </summary>
        [Category("Custom Settings"),
         Description(
             @"Gets or sets a value indicating whether the ToolTip is drawn by the operating system or by code that you provide. If true, the properties 'ToolTipIcon' and 'ToolTipTitle' will set to their default values and the image will display in ToolTip otherwise only text will display.")]
        private new bool OwnerDraw
        {
            get => base.OwnerDraw;
            set
            {
                if (value)
                {
                    ToolTipIcon = ToolTipIcon.None;
                    ToolTipTitle = string.Empty;
                }

                base.OwnerDraw = value;
            }
        }

        /// <summary>
        /// Gets or sets a value that defines the type of icon to be displayed alongside
        /// the ToolTip text.
        /// Cannot set if the property 'OwnerDraw' is true.
        /// </summary>
        [Category("Custom Settings"),
         Description(
             @"Gets or sets a value that defines the type of icon to be displayed alongside the ToolTip text. Cannot set if the property 'OwnerDraw' is true.")]
        private new ToolTipIcon ToolTipIcon
        {
            //get => base.ToolTipIcon;
            set
            {
                if (!OwnerDraw)
                {
                    base.ToolTipIcon = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets a title for the ToolTip window.
        /// Cannot set if the property 'OwnerDraw' is true.
        /// </summary>
        [Category("Custom Settings"),
         Description(@"Gets or sets a title for the ToolTip window. Cannot set if the property 'OwnerDraw' is true.")]
        private new string ToolTipTitle
        {
            set
            {
                if (!OwnerDraw)
                {
                    base.ToolTipTitle = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the background color for the ToolTip.
        /// </summary>
        [Category("Custom Settings"), Description(@"Gets or sets the background color for the ToolTip.")]
        public new Color BackColor
        {
            set
            {
                base.BackColor = value;
                var temp = _myBackColorBrush;
                _myBackColorBrush = new SolidBrush(value);
                temp.Dispose();
            }
        }

        /// <summary>
        /// Gets or sets the foreground color for the ToolTip.
        /// </summary>
        [Category("Custom Settings"), Description(@"Gets or sets the foreground color for the ToolTip.")]
        public new Color ForeColor
        {
            set
            {
                base.ForeColor = value;
                var temp = _myTextBrush;
                _myTextBrush = new SolidBrush(value);
                temp.Dispose();
            }
        }

        /// <summary>
        /// Gets or sets a value that indicates whether the ToolTip resizes based on its text.
        /// True if the ToolTip automatically resizes based on its text; otherwise, false. The default is true.
        /// </summary>
        [Category("Custom Settings"),
         Description(
             @"Gets or sets a value that indicates whether the ToolTip resizes based on its text. true if the ToolTip automatically resizes based on its text; otherwise, false. The default is true.")]
        public bool AutoSize
        {
            set
            {
                _myAutoSize = value;
                _myTextFormat.Trimming = _myAutoSize ? StringTrimming.None : StringTrimming.EllipsisCharacter;
            }
        }

        /// <summary>
        /// Gets or sets the border color for the ToolTip.
        /// </summary>
        [Category("Custom Settings"), Description(@"Gets or sets the border color for the ToolTip.")]
        public Color BorderColor
        {
            set
            {
                _myBorderColor = value;
                var temp = _myBorderBrush;
                _myBorderBrush = new SolidBrush(value);
                temp.Dispose();
            }
        }

        #endregion

        #region Constructor

        /// <summary>
        /// Constructor to create instance of CustomizedToolTip class that can display Thumbnails/Images in it.
        /// </summary>
        public CustomizedToolTip()
        {
            try
            {
                OwnerDraw = true;
                _myTextFormat.FormatFlags = StringFormatFlags.LineLimit;
                _myTextFormat.Alignment = StringAlignment.Near;
                _myTextFormat.LineAlignment = StringAlignment.Center;
                _myTextFormat.Trimming = StringTrimming.None;
                Popup += CustomizedToolTip_Popup;
                Draw += CustomizedToolTip_Draw;
            }
            catch (Exception ex)
            {
                var logMessage = "Exception in CustomizedToolTip.CustomizedToolTip () " + ex;
                Trace.TraceError(logMessage);
                throw;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            try
            {
                //Dispose of the disposable objects.
                try
                {
                    if (!disposing) return;
                    MyFont?.Dispose();
                    _myTextBrush?.Dispose();
                    _myBackColorBrush?.Dispose();
                    _myBorderBrush?.Dispose();
                    _myTextFormat?.Dispose();
                }
                finally
                {
                    base.Dispose(disposing);
                }
            }
            catch (Exception ex)
            {
                var logMessage = "Exception in CustomizedToolTip.Dispose (bool) " + ex;
                Trace.TraceError(logMessage);
                throw;
            }
        }

        /// <summary>
        /// CustomizedToolTip_Draw raised when tooltip is drawn.
        /// </summary>
        /// <param name="sender">sender</param>
        /// <param name="e">e</param>
        private void CustomizedToolTip_Draw(object sender, DrawToolTipEventArgs e)
        {
            try
            {
                var coords = e.AssociatedControl.PointToScreen(Point.Empty);
                var t = sender as ToolTip;
                // ReSharper disable once PossibleNullReferenceException
                var h = (IntPtr)t.GetType().GetProperty("Handle",
                        BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.GetValue(t);
                MoveWindow(h, coords.X - 150, coords.Y + 100, e.Bounds.Width, e.Bounds.Height, false);
                e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
                _myToolTipRectangle.Size = e.Bounds.Size;
                e.Graphics.FillRectangle(_myBorderBrush, _myToolTipRectangle);
                _myImageRectangle = Rectangle.Inflate(_myToolTipRectangle, -BorderThickness, -BorderThickness);
                e.Graphics.FillRectangle(_myBackColorBrush, _myImageRectangle);
                var parent = e.AssociatedControl;
                if (parent.Tag is Image toolTipImage)
                {
                    _myImageRectangle.Width = _myInternalImageWidth;
                    _myTextRectangle = new Rectangle(_myImageRectangle.Right, _myImageRectangle.Top,
                        _myToolTipRectangle.Width - _myImageRectangle.Right - BorderThickness, _myImageRectangle.Height)
                    {
                        Location = new Point(_myImageRectangle.Right, _myImageRectangle.Top)
                    };
                    e.Graphics.FillRectangle(_myBackColorBrush, _myTextRectangle);
                    e.Graphics.DrawImage(toolTipImage, _myImageRectangle);
                    e.Graphics.DrawString(e.ToolTipText, MyFont, _myTextBrush, _myTextRectangle, _myTextFormat);
                }
                else
                {
                    e.Graphics.DrawString(e.ToolTipText, MyFont, _myTextBrush, _myImageRectangle, _myTextFormat);
                }
            }
            catch (Exception ex)
            {
                var logMessage =
                    "Exception in CustomizedToolTip.BlindHeaderToolTip_Draw (object, DrawToolTipEventArgs) " + ex;
                Trace.TraceError(logMessage);
                throw;
            }
        }

        /// <summary>
        /// CustomizedToolTip_Popup raised when tooltip pops up.
        /// </summary>
        /// <param name="sender">sender</param>
        /// <param name="e">e</param>
        private void CustomizedToolTip_Popup(object sender, PopupEventArgs e)
        {
            try
            {
                if (!OwnerDraw) return;
                if (!_myAutoSize)
                {
                    e.ToolTipSize = _mySize;
                    _myInternalImageWidth = _mySize.Height;
                }
                else
                {
                    var oldSize = e.ToolTipSize;
                    var parent = e.AssociatedControl;
                    if (parent.Tag is Image)
                    {
                        _myInternalImageWidth = oldSize.Height;
                        oldSize.Width += _myInternalImageWidth + Padding;
                    }
                    else
                    {
                        oldSize.Width += Padding;
                    }

                    e.ToolTipSize = oldSize;
                }
            }
            catch (Exception ex)
            {
                var logMessage = "Exception in CustomizedToolTip.CustomizedToolTip_Popup (object, PopupEventArgs) " +
                                 ex;
                Trace.TraceError(logMessage);
                throw;
            }
        }

        #endregion

        [DllImport("User32.dll")]
        private static extern bool MoveWindow(IntPtr h, int x, int y, int width, int height, bool redraw);
    }
}