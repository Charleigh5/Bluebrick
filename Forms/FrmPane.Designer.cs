using System.ComponentModel;
using System.Windows.Forms;

namespace BlueBrick
{
    partial class FrmPane
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }

            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
	        this.components = new System.ComponentModel.Container();
	        this.btnConvBfAdd = new System.Windows.Forms.Button();
	        this.btnConvBfClr = new System.Windows.Forms.Button();
	        this.btnConvBfDel = new System.Windows.Forms.Button();
	        this.btnConvEleClr = new System.Windows.Forms.Button();
	        this.btnConvRlCalc = new System.Windows.Forms.Button();
	        this.btnConvRlReset = new System.Windows.Forms.Button();
	        this.btnConvUnitCalc = new System.Windows.Forms.Button();
	        this.btnConvUnitSwap = new System.Windows.Forms.Button();
	        this.btnEOSUser = new System.Windows.Forms.Button();
	        this.btnEOSEmail = new System.Windows.Forms.Button();
	        this.btnEPSClear = new System.Windows.Forms.Button();
	        this.btnEPSSearch = new System.Windows.Forms.Button();
	        this.btnGenLocExe = new System.Windows.Forms.Button();
	        this.btnGenLocPath = new System.Windows.Forms.Button();
	        this.btnGenPreCol = new System.Windows.Forms.Button();
	        this.btnGenPreDXF = new System.Windows.Forms.Button();
	        this.btnGenPreDXFAll = new System.Windows.Forms.Button();
	        this.btnGenPreFull = new System.Windows.Forms.Button();
	        this.btnGenPrePDF = new System.Windows.Forms.Button();
	        this.btnGenPrePNG = new System.Windows.Forms.Button();
	        this.btnGenPreSTP = new System.Windows.Forms.Button();
	        this.btnGenPreSTPAll = new System.Windows.Forms.Button();
	        this.btnLkySrch = new System.Windows.Forms.Button();
	        this.btnPropMdlSave = new System.Windows.Forms.Button();
	        this.btnPropReset = new System.Windows.Forms.Button();
	        this.btnPropSave = new System.Windows.Forms.Button();
	        this.btnTlsAppear = new System.Windows.Forms.Button();
	        this.btnTlsCard = new System.Windows.Forms.Button();
	        this.btnTlsChkIn = new System.Windows.Forms.Button();
	        this.btnTlsCopyDwg = new System.Windows.Forms.Button();
	        this.btnTlsFinSch = new System.Windows.Forms.Button();
	        this.btnTlsFinSearch = new System.Windows.Forms.Button();
	        this.btnTlsFix = new System.Windows.Forms.Button();
	        this.btnTlsLights = new System.Windows.Forms.Button();
	        this.btnTlsSerial = new System.Windows.Forms.Button();
	        this.btnTlsShowHidden = new System.Windows.Forms.Button();
	        this.btnTlsThru = new System.Windows.Forms.Button();
	        this.btnTlsViki = new System.Windows.Forms.Button();
	        this.chkGenTypDXF = new System.Windows.Forms.CheckBox();
	        this.chkGenTypIGES = new System.Windows.Forms.CheckBox();
	        this.chkGenTypPDF = new System.Windows.Forms.CheckBox();
	        this.chkGenTypPNG = new System.Windows.Forms.CheckBox();
	        this.chkGenTypPkt = new System.Windows.Forms.CheckBox();
	        this.chkGenTypSTP = new System.Windows.Forms.CheckBox();
	        this.chkPropAll = new System.Windows.Forms.CheckBox();
	        this.chkPropSearch = new System.Windows.Forms.CheckBox();
	        this.cmbConvRlUnitFrom = new System.Windows.Forms.ComboBox();
	        this.cmbConvRlUnitTo = new System.Windows.Forms.ComboBox();
	        this.cmbConvUnitFrom = new System.Windows.Forms.ComboBox();
	        this.cmbConvUnitTo = new System.Windows.Forms.ComboBox();
	        this.cmbConvUnitTypes = new System.Windows.Forms.ComboBox();
	        this.cmbLkySrch = new System.Windows.Forms.ComboBox();
	        this.cmbPropGenPCat = new System.Windows.Forms.ComboBox();
	        this.cmsLucky = new System.Windows.Forms.ContextMenuStrip(this.components);
	        this.tsiInsertPart = new System.Windows.Forms.ToolStripMenuItem();
	        this.tsiOpenFile = new System.Windows.Forms.ToolStripMenuItem();
	        this.tsiOpenExplore = new System.Windows.Forms.ToolStripMenuItem();
	        this.cmsRClick = new System.Windows.Forms.ContextMenuStrip(this.components);
	        this.tsiCopyPart = new System.Windows.Forms.ToolStripMenuItem();
	        this.tsiCopyDesc = new System.Windows.Forms.ToolStripMenuItem();
	        this.tsiSearch = new System.Windows.Forms.ToolStripMenuItem();
	        this.colConvBfDims = new System.Windows.Forms.ColumnHeader();
	        this.colConvBfQty = new System.Windows.Forms.ColumnHeader();
	        this.colEPSResultsAlloc = new System.Windows.Forms.ColumnHeader();
	        this.colEPSResultsDesc = new System.Windows.Forms.ColumnHeader();
	        this.colEPSResultsOH = new System.Windows.Forms.ColumnHeader();
	        this.colEPSResultsPart = new System.Windows.Forms.ColumnHeader();
	        this.colEPSResultsUM = new System.Windows.Forms.ColumnHeader();
	        this.colLkyResults1 = new System.Windows.Forms.ColumnHeader();
	        this.colLkyResults2 = new System.Windows.Forms.ColumnHeader();
	        this.colLkyResults3 = new System.Windows.Forms.ColumnHeader();
	        this.colPropOrd1 = new System.Windows.Forms.ColumnHeader();
	        this.colPropOrd2 = new System.Windows.Forms.ColumnHeader();
	        this.colPropOrd3 = new System.Windows.Forms.ColumnHeader();
	        this.colPropOrd4 = new System.Windows.Forms.ColumnHeader();
	        this.colPropOrd5 = new System.Windows.Forms.ColumnHeader();
	        this.colPropOrd6 = new System.Windows.Forms.ColumnHeader();
	        this.colPropOrd7 = new System.Windows.Forms.ColumnHeader();
	        this.colPropOrd8 = new System.Windows.Forms.ColumnHeader();
	        this.cplMainEPS = new BlueBrick.CollapsePanel();
	        this.tlpMainEPS = new System.Windows.Forms.TableLayoutPanel();
	        this.txtEPSPart = new System.Windows.Forms.TextBox();
	        this.txtEPSDesc = new System.Windows.Forms.TextBox();
	        this.lstEPSResults = new System.Windows.Forms.ListView();
	        this.cplMainGen = new BlueBrick.CollapsePanel();
	        this.tlpMainGen = new System.Windows.Forms.TableLayoutPanel();
	        this.grpGenPre = new System.Windows.Forms.GroupBox();
	        this.tlpGenPre = new System.Windows.Forms.TableLayoutPanel();
	        this.grpGenTyp = new System.Windows.Forms.GroupBox();
	        this.tlpGenTyp = new System.Windows.Forms.TableLayoutPanel();
	        this.grpGenScope = new System.Windows.Forms.GroupBox();
	        this.tlpGenScope = new System.Windows.Forms.TableLayoutPanel();
	        this.radGenScopeAll = new System.Windows.Forms.RadioButton();
	        this.radGenScopeSingle = new System.Windows.Forms.RadioButton();
	        this.grpGenLoc = new System.Windows.Forms.GroupBox();
	        this.tlpGenLoc = new System.Windows.Forms.TableLayoutPanel();
	        this.radGenLocUser = new System.Windows.Forms.RadioButton();
	        this.radGenLocDesk = new System.Windows.Forms.RadioButton();
	        this.radGenLocPDM = new System.Windows.Forms.RadioButton();
	        this.txtGenLocPath = new System.Windows.Forms.TextBox();
	        this.cplMainLky = new BlueBrick.CollapsePanel();
	        this.tlpMainLky = new System.Windows.Forms.TableLayoutPanel();
	        this.lstLkyResults = new System.Windows.Forms.ListView();
	        this.imlThumbs = new System.Windows.Forms.ImageList(this.components);
	        this.cplMainMisc = new BlueBrick.CollapsePanel();
	        this.tlpMainMisc = new System.Windows.Forms.TableLayoutPanel();
	        this.tabMainConv = new System.Windows.Forms.TabControl();
	        this.tpgConvUnit = new System.Windows.Forms.TabPage();
	        this.tlpConvUnit = new System.Windows.Forms.TableLayoutPanel();
	        this.lblConvUnits = new System.Windows.Forms.Label();
	        this.lblConvUnitFDesc = new System.Windows.Forms.Label();
	        this.lblConvUnitFSym = new System.Windows.Forms.Label();
	        this.txtConvUnitFVal = new System.Windows.Forms.TextBox();
	        this.txtConvUnitTVal = new System.Windows.Forms.TextBox();
	        this.lblConvUnitTSym = new System.Windows.Forms.Label();
	        this.lblConvUnitTDesc = new System.Windows.Forms.Label();
	        this.tpgConvElec = new System.Windows.Forms.TabPage();
	        this.tlpConvEle = new System.Windows.Forms.TableLayoutPanel();
	        this.lblConvEleWatt = new System.Windows.Forms.Label();
	        this.lblConvEleVolt = new System.Windows.Forms.Label();
	        this.lblConvEleAmp = new System.Windows.Forms.Label();
	        this.lblConvEleOhm = new System.Windows.Forms.Label();
	        this.lblConvEleW80 = new System.Windows.Forms.Label();
	        this.lblConvEleW50 = new System.Windows.Forms.Label();
	        this.lblConvEleMemo = new System.Windows.Forms.Label();
	        this.txtConvEleWatt = new System.Windows.Forms.TextBox();
	        this.txtConvEleVolt = new System.Windows.Forms.TextBox();
	        this.txtConvEleAmp = new System.Windows.Forms.TextBox();
	        this.txtConvEleOhm = new System.Windows.Forms.TextBox();
	        this.txtConvEleW80 = new System.Windows.Forms.TextBox();
	        this.txtConvEleW50 = new System.Windows.Forms.TextBox();
	        this.tpgConvRoll = new System.Windows.Forms.TabPage();
	        this.tlpConvRl = new System.Windows.Forms.TableLayoutPanel();
	        this.lblConvRlUnitFrom = new System.Windows.Forms.Label();
	        this.lblConvRlMat = new System.Windows.Forms.Label();
	        this.lblConvRlOD = new System.Windows.Forms.Label();
	        this.lblConvRlID = new System.Windows.Forms.Label();
	        this.lblConvRlCore = new System.Windows.Forms.Label();
	        this.lblConvRlTotal = new System.Windows.Forms.Label();
	        this.txtConvRlMat = new System.Windows.Forms.TextBox();
	        this.txtConvRlOD = new System.Windows.Forms.TextBox();
	        this.txtConvRlID = new System.Windows.Forms.TextBox();
	        this.txtConvRlCore = new System.Windows.Forms.TextBox();
	        this.txtConvRlTotal = new System.Windows.Forms.TextBox();
	        this.picConvRlRoll = new System.Windows.Forms.PictureBox();
	        this.tpgConvBFoot = new System.Windows.Forms.TabPage();
	        this.tlpConvBf = new System.Windows.Forms.TableLayoutPanel();
	        this.lblConvBfThk = new System.Windows.Forms.Label();
	        this.lblConvBfWid = new System.Windows.Forms.Label();
	        this.lblConvBfLen = new System.Windows.Forms.Label();
	        this.lblConvBfQty = new System.Windows.Forms.Label();
	        this.lblConvBfTot = new System.Windows.Forms.Label();
	        this.txtConvBfThk = new System.Windows.Forms.TextBox();
	        this.txtConvBfWid = new System.Windows.Forms.TextBox();
	        this.txtConvBfLen = new System.Windows.Forms.TextBox();
	        this.txtConvBfQty = new System.Windows.Forms.TextBox();
	        this.txtConvBfTot = new System.Windows.Forms.TextBox();
	        this.lvwConvBf = new System.Windows.Forms.ListView();
	        this.lblConvBfMemo = new System.Windows.Forms.Label();
	        this.cplMainOpp = new BlueBrick.CollapsePanel();
	        this.tlpMainOpp = new System.Windows.Forms.TableLayoutPanel();
	        this.btnEOSOpp = new System.Windows.Forms.Button();
	        this.lblEOSDue = new System.Windows.Forms.Label();
	        this.lblEOSDesc = new System.Windows.Forms.Label();
	        this.lblEOSCmt = new System.Windows.Forms.Label();
	        this.lblEOSMemo = new System.Windows.Forms.Label();
	        this.lblEOSCust = new System.Windows.Forms.Label();
	        this.txtEOSOpp = new System.Windows.Forms.TextBox();
	        this.txtEOSDue = new System.Windows.Forms.TextBox();
	        this.txtEOSDesc = new System.Windows.Forms.TextBox();
	        this.txtEOSCmt = new System.Windows.Forms.TextBox();
	        this.txtEOSCust = new System.Windows.Forms.TextBox();
	        this.txtEOSUser = new System.Windows.Forms.TextBox();
	        this.txtEOSMemoBody = new System.Windows.Forms.TextBox();
	        this.lblEOSAttach = new System.Windows.Forms.Label();
	        this.lstEOSAttach = new System.Windows.Forms.ListView();
	        this.colEOSFile = new System.Windows.Forms.ColumnHeader();
	        this.cmsOpAttach = new System.Windows.Forms.ContextMenuStrip(this.components);
	        this.tsiOpBrowse = new System.Windows.Forms.ToolStripMenuItem();
	        this.cmbEOSMemoSel = new System.Windows.Forms.ComboBox();
	        this.cplMainSfOpp = new BlueBrick.CollapsePanel();
	        this.tlpSfMain = new System.Windows.Forms.TableLayoutPanel();
	        this.tlpSfUserInfo = new System.Windows.Forms.TableLayoutPanel();
	        this.lblSfUserName = new System.Windows.Forms.Label();
	        this.picSfUser = new System.Windows.Forms.PictureBox();
	        this.lblSfEmail = new System.Windows.Forms.Label();
	        this.btnSfLogin = new System.Windows.Forms.Button();
	        this.tlpMainSfOpp = new System.Windows.Forms.TableLayoutPanel();
	        this.btnSFOSOpp = new System.Windows.Forms.Button();
	        this.lblSFOSDue = new System.Windows.Forms.Label();
	        this.lblSFOSDesc = new System.Windows.Forms.Label();
	        this.lblSFOSTask = new System.Windows.Forms.Label();
	        this.lblSFOSCust = new System.Windows.Forms.Label();
	        this.txtSFOSOpp = new System.Windows.Forms.TextBox();
	        this.txtSFOSDue = new System.Windows.Forms.TextBox();
	        this.txtSFOSDesc = new System.Windows.Forms.TextBox();
	        this.txtSFOSCust = new System.Windows.Forms.TextBox();
	        this.txtSFOSUser = new System.Windows.Forms.TextBox();
	        this.btnSFOSUser = new System.Windows.Forms.Button();
	        this.btnSFOSEmail = new System.Windows.Forms.Button();
	        this.lblSFOSAttach = new System.Windows.Forms.Label();
	        this.lstSFOSAttach = new System.Windows.Forms.ListView();
	        this.colSFOSFile = new System.Windows.Forms.ColumnHeader();
	        this.cmbSFOSMemoSel = new System.Windows.Forms.ComboBox();
	        this.cmsSfOpAttach = new System.Windows.Forms.ContextMenuStrip(this.components);
	        this.tsiSfOpBrowse = new System.Windows.Forms.ToolStripMenuItem();
	        this.cplMainProp = new BlueBrick.CollapsePanel();
	        this.tlpMainProp = new System.Windows.Forms.TableLayoutPanel();
	        this.tabMainProp = new System.Windows.Forms.TabControl();
	        this.tpgPropGen = new System.Windows.Forms.TabPage();
	        this.tlpPropGen = new System.Windows.Forms.TableLayoutPanel();
	        this.lblPropGenPCat = new System.Windows.Forms.Label();
	        this.lblPropGenDocNo = new System.Windows.Forms.Label();
	        this.lblPropGenPartNo = new System.Windows.Forms.Label();
	        this.lblPropGenDesc = new System.Windows.Forms.Label();
	        this.lblPropGenCustNo = new System.Windows.Forms.Label();
	        this.lblPropGenOpp = new System.Windows.Forms.Label();
	        this.lblPropGenRev = new System.Windows.Forms.Label();
	        this.lblPropGenCust = new System.Windows.Forms.Label();
	        this.txtPropGenDocNo = new System.Windows.Forms.TextBox();
	        this.txtPropGenPartNo = new System.Windows.Forms.TextBox();
	        this.txtPropGenDesc = new System.Windows.Forms.TextBox();
	        this.txtPropGenCustNo = new System.Windows.Forms.TextBox();
	        this.txtPropGenOpp = new System.Windows.Forms.TextBox();
	        this.txtPropGenRev = new System.Windows.Forms.TextBox();
	        this.txtPropGenCust = new System.Windows.Forms.TextBox();
	        this.tpgPropCut = new System.Windows.Forms.TabPage();
	        this.tpgPropCust = new System.Windows.Forms.TabPage();
	        this.pnlPropCustomer = new System.Windows.Forms.Panel();
	        this.tpgPropCmt = new System.Windows.Forms.TabPage();
	        this.tlpPropCmts = new System.Windows.Forms.TableLayoutPanel();
	        this.txtPropCmts = new System.Windows.Forms.TextBox();
	        this.txtPropCmtNew = new System.Windows.Forms.TextBox();
	        this.btnPropCmtAdd = new System.Windows.Forms.Button();
	        this.tpgPropOrd = new System.Windows.Forms.TabPage();
	        this.lstPropOrd = new System.Windows.Forms.ListView();
	        this.tlpPropCmds = new System.Windows.Forms.TableLayoutPanel();
	        this.cplMainTls = new BlueBrick.CollapsePanel();
	        this.tlpMainTls = new System.Windows.Forms.TableLayoutPanel();
	        this.btnTlsTaskQ = new System.Windows.Forms.Button();
	        this.btnTlsDwgNum = new System.Windows.Forms.Button();
	        this.fldrDiagOpen = new System.Windows.Forms.FolderBrowserDialog();
	        this.lblMainStat = new System.Windows.Forms.Label();
	        this.pnlMain = new System.Windows.Forms.Panel();
	        this.prgStatus = new System.Windows.Forms.ProgressBar();
	        this.tipTool = new System.Windows.Forms.ToolTip(this.components);
	        this.tlpMain = new System.Windows.Forms.TableLayoutPanel();
	        this.tlpMainStat = new System.Windows.Forms.TableLayoutPanel();
	        this.txtStatus = new System.Windows.Forms.TextBox();
	        this.btnOpt = new System.Windows.Forms.Button();
	        this.cmsLucky.SuspendLayout();
	        this.cmsRClick.SuspendLayout();
	        this.cplMainEPS.Content.SuspendLayout();
	        this.tlpMainEPS.SuspendLayout();
	        this.cplMainGen.Content.SuspendLayout();
	        this.tlpMainGen.SuspendLayout();
	        this.grpGenPre.SuspendLayout();
	        this.tlpGenPre.SuspendLayout();
	        this.grpGenTyp.SuspendLayout();
	        this.tlpGenTyp.SuspendLayout();
	        this.grpGenScope.SuspendLayout();
	        this.tlpGenScope.SuspendLayout();
	        this.grpGenLoc.SuspendLayout();
	        this.tlpGenLoc.SuspendLayout();
	        this.cplMainLky.Content.SuspendLayout();
	        this.tlpMainLky.SuspendLayout();
	        this.cplMainMisc.Content.SuspendLayout();
	        this.tlpMainMisc.SuspendLayout();
	        this.tabMainConv.SuspendLayout();
	        this.tpgConvUnit.SuspendLayout();
	        this.tlpConvUnit.SuspendLayout();
	        this.tpgConvElec.SuspendLayout();
	        this.tlpConvEle.SuspendLayout();
	        this.tpgConvRoll.SuspendLayout();
	        this.tlpConvRl.SuspendLayout();
	        ((System.ComponentModel.ISupportInitialize)(this.picConvRlRoll)).BeginInit();
	        this.tpgConvBFoot.SuspendLayout();
	        this.tlpConvBf.SuspendLayout();
	        this.cplMainOpp.Content.SuspendLayout();
	        this.tlpMainOpp.SuspendLayout();
	        this.cmsOpAttach.SuspendLayout();
	        this.cplMainSfOpp.Content.SuspendLayout();
	        this.tlpSfMain.SuspendLayout();
	        this.tlpSfUserInfo.SuspendLayout();
	        ((System.ComponentModel.ISupportInitialize)(this.picSfUser)).BeginInit();
	        this.tlpMainSfOpp.SuspendLayout();
	        this.cmsSfOpAttach.SuspendLayout();
	        this.cplMainProp.Content.SuspendLayout();
	        this.tlpMainProp.SuspendLayout();
	        this.tabMainProp.SuspendLayout();
	        this.tpgPropGen.SuspendLayout();
	        this.tlpPropGen.SuspendLayout();
	        this.tpgPropCust.SuspendLayout();
	        this.tpgPropCmt.SuspendLayout();
	        this.tlpPropCmts.SuspendLayout();
	        this.tpgPropOrd.SuspendLayout();
	        this.tlpPropCmds.SuspendLayout();
	        this.cplMainTls.Content.SuspendLayout();
	        this.tlpMainTls.SuspendLayout();
	        this.pnlMain.SuspendLayout();
	        this.tlpMain.SuspendLayout();
	        this.tlpMainStat.SuspendLayout();
	        this.SuspendLayout();
	        // 
	        // btnConvBfAdd
	        // 
	        this.btnConvBfAdd.AutoSize = true;
	        this.btnConvBfAdd.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.btnConvBfAdd.Location = new System.Drawing.Point(41, 94);
	        this.btnConvBfAdd.Margin = new System.Windows.Forms.Padding(2);
	        this.btnConvBfAdd.Name = "btnConvBfAdd";
	        this.btnConvBfAdd.Size = new System.Drawing.Size(35, 30);
	        this.btnConvBfAdd.TabIndex = 8;
	        this.btnConvBfAdd.Text = "Add";
	        this.btnConvBfAdd.UseVisualStyleBackColor = true;
	        this.btnConvBfAdd.Click += new System.EventHandler(this.btnConvBfAdd_Click);
	        // 
	        // btnConvBfClr
	        // 
	        this.btnConvBfClr.AutoSize = true;
	        this.btnConvBfClr.Dock = System.Windows.Forms.DockStyle.Top;
	        this.btnConvBfClr.Location = new System.Drawing.Point(158, 128);
	        this.btnConvBfClr.Margin = new System.Windows.Forms.Padding(2);
	        this.btnConvBfClr.Name = "btnConvBfClr";
	        this.btnConvBfClr.Size = new System.Drawing.Size(35, 26);
	        this.btnConvBfClr.TabIndex = 12;
	        this.btnConvBfClr.Text = "Clear";
	        this.btnConvBfClr.UseVisualStyleBackColor = true;
	        this.btnConvBfClr.Click += new System.EventHandler(this.btnConvBfClr_Click);
	        // 
	        // btnConvBfDel
	        // 
	        this.btnConvBfDel.AutoSize = true;
	        this.btnConvBfDel.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.btnConvBfDel.Location = new System.Drawing.Point(80, 94);
	        this.btnConvBfDel.Margin = new System.Windows.Forms.Padding(2);
	        this.btnConvBfDel.Name = "btnConvBfDel";
	        this.btnConvBfDel.Size = new System.Drawing.Size(35, 30);
	        this.btnConvBfDel.TabIndex = 9;
	        this.btnConvBfDel.Text = "Del";
	        this.btnConvBfDel.UseVisualStyleBackColor = true;
	        this.btnConvBfDel.Click += new System.EventHandler(this.btnConvBfDel_Click);
	        // 
	        // btnConvEleClr
	        // 
	        this.btnConvEleClr.AutoSize = true;
	        this.btnConvEleClr.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.btnConvEleClr.Location = new System.Drawing.Point(50, 164);
	        this.btnConvEleClr.Margin = new System.Windows.Forms.Padding(2);
	        this.btnConvEleClr.Name = "btnConvEleClr";
	        this.btnConvEleClr.Size = new System.Drawing.Size(64, 22);
	        this.btnConvEleClr.TabIndex = 12;
	        this.btnConvEleClr.Text = "Clear";
	        this.btnConvEleClr.UseVisualStyleBackColor = true;
	        this.btnConvEleClr.Click += new System.EventHandler(this.btnConvEleClr_Click);
	        // 
	        // btnConvRlCalc
	        // 
	        this.btnConvRlCalc.AutoSize = true;
	        this.btnConvRlCalc.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.btnConvRlCalc.Location = new System.Drawing.Point(54, 117);
	        this.btnConvRlCalc.Margin = new System.Windows.Forms.Padding(2);
	        this.btnConvRlCalc.Name = "btnConvRlCalc";
	        this.btnConvRlCalc.Size = new System.Drawing.Size(50, 19);
	        this.btnConvRlCalc.TabIndex = 11;
	        this.btnConvRlCalc.Text = "Calculate";
	        this.btnConvRlCalc.UseVisualStyleBackColor = true;
	        this.btnConvRlCalc.Click += new System.EventHandler(this.btnConvRlCalc_Click);
	        // 
	        // btnConvRlReset
	        // 
	        this.btnConvRlReset.AutoSize = true;
	        this.btnConvRlReset.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.btnConvRlReset.Location = new System.Drawing.Point(155, 2);
	        this.btnConvRlReset.Margin = new System.Windows.Forms.Padding(2);
	        this.btnConvRlReset.Name = "btnConvRlReset";
	        this.btnConvRlReset.Size = new System.Drawing.Size(38, 19);
	        this.btnConvRlReset.TabIndex = 2;
	        this.btnConvRlReset.Text = "Reset";
	        this.btnConvRlReset.UseVisualStyleBackColor = true;
	        this.btnConvRlReset.Click += new System.EventHandler(this.btnConvRlReset_Click);
	        // 
	        // btnConvUnitCalc
	        // 
	        this.btnConvUnitCalc.AutoSize = true;
	        this.btnConvUnitCalc.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.btnConvUnitCalc.Location = new System.Drawing.Point(155, 26);
	        this.btnConvUnitCalc.Margin = new System.Windows.Forms.Padding(0);
	        this.btnConvUnitCalc.Name = "btnConvUnitCalc";
	        this.btnConvUnitCalc.Size = new System.Drawing.Size(40, 26);
	        this.btnConvUnitCalc.TabIndex = 3;
	        this.btnConvUnitCalc.Text = "Calc";
	        this.btnConvUnitCalc.UseVisualStyleBackColor = true;
	        this.btnConvUnitCalc.Click += new System.EventHandler(this.btnConvUnitCalc_Click);
	        // 
	        // btnConvUnitSwap
	        // 
	        this.btnConvUnitSwap.AutoSize = true;
	        this.btnConvUnitSwap.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.btnConvUnitSwap.Location = new System.Drawing.Point(155, 121);
	        this.btnConvUnitSwap.Margin = new System.Windows.Forms.Padding(0);
	        this.btnConvUnitSwap.Name = "btnConvUnitSwap";
	        this.btnConvUnitSwap.Size = new System.Drawing.Size(40, 26);
	        this.btnConvUnitSwap.TabIndex = 8;
	        this.btnConvUnitSwap.Text = "Swap";
	        this.btnConvUnitSwap.UseVisualStyleBackColor = true;
	        this.btnConvUnitSwap.Click += new System.EventHandler(this.btnConvUnitSwap_Click);
	        // 
	        // btnEOSUser
	        // 
	        this.btnEOSUser.AutoSize = true;
	        this.btnEOSUser.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.btnEOSUser.Location = new System.Drawing.Point(100, 23);
	        this.btnEOSUser.Margin = new System.Windows.Forms.Padding(0);
	        this.btnEOSUser.Name = "btnEOSUser";
	        this.btnEOSUser.Size = new System.Drawing.Size(33, 23);
	        this.btnEOSUser.TabIndex = 6;
	        this.btnEOSUser.Text = "User";
	        this.btnEOSUser.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
	        this.tipTool.SetToolTip(this.btnEOSUser, "Launch tasks by user window");
	        this.btnEOSUser.UseVisualStyleBackColor = true;
	        this.btnEOSUser.Click += new System.EventHandler(this.btnEOSUser_Click);
	        // 
	        // btnEOSEmail
	        // 
	        this.btnEOSEmail.AutoSize = true;
	        this.tlpMainOpp.SetColumnSpan(this.btnEOSEmail, 2);
	        this.btnEOSEmail.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.btnEOSEmail.Location = new System.Drawing.Point(100, 92);
	        this.btnEOSEmail.Margin = new System.Windows.Forms.Padding(0);
	        this.btnEOSEmail.Name = "btnEOSEmail";
	        this.btnEOSEmail.Size = new System.Drawing.Size(111, 23);
	        this.btnEOSEmail.TabIndex = 13;
	        this.btnEOSEmail.Text = "Email Requestor";
	        this.tipTool.SetToolTip(this.btnEOSEmail, "Create email about this opp");
	        this.btnEOSEmail.UseVisualStyleBackColor = true;
	        this.btnEOSEmail.Click += new System.EventHandler(this.btnEOSEmail_Click);
	        // 
	        // btnEPSClear
	        // 
	        this.btnEPSClear.AutoSize = true;
	        this.btnEPSClear.BackColor = System.Drawing.Color.Transparent;
	        this.btnEPSClear.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.btnEPSClear.FlatAppearance.BorderSize = 0;
	        this.btnEPSClear.FlatAppearance.MouseDownBackColor = System.Drawing.SystemColors.ButtonFace;
	        this.btnEPSClear.FlatAppearance.MouseOverBackColor = System.Drawing.SystemColors.ButtonHighlight;
	        this.btnEPSClear.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
	        this.btnEPSClear.Image = global::BlueBrick.Resource1.icon_EpiClear;
	        this.btnEPSClear.Location = new System.Drawing.Point(189, 22);
	        this.btnEPSClear.Margin = new System.Windows.Forms.Padding(0);
	        this.btnEPSClear.Name = "btnEPSClear";
	        this.btnEPSClear.Size = new System.Drawing.Size(22, 22);
	        this.btnEPSClear.TabIndex = 3;
	        this.tipTool.SetToolTip(this.btnEPSClear, "Clear search input");
	        this.btnEPSClear.UseVisualStyleBackColor = false;
	        this.btnEPSClear.Click += new System.EventHandler(this.btnEPSClear_Click);
	        // 
	        // btnEPSSearch
	        // 
	        this.btnEPSSearch.AutoSize = true;
	        this.btnEPSSearch.BackColor = System.Drawing.Color.Transparent;
	        this.btnEPSSearch.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.btnEPSSearch.FlatAppearance.BorderSize = 0;
	        this.btnEPSSearch.FlatAppearance.MouseDownBackColor = System.Drawing.SystemColors.ButtonFace;
	        this.btnEPSSearch.FlatAppearance.MouseOverBackColor = System.Drawing.SystemColors.ButtonHighlight;
	        this.btnEPSSearch.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
	        this.btnEPSSearch.Image = global::BlueBrick.Resource1.icon_EpiEpicor;
	        this.btnEPSSearch.Location = new System.Drawing.Point(189, 0);
	        this.btnEPSSearch.Margin = new System.Windows.Forms.Padding(0);
	        this.btnEPSSearch.Name = "btnEPSSearch";
	        this.btnEPSSearch.Size = new System.Drawing.Size(22, 22);
	        this.btnEPSSearch.TabIndex = 2;
	        this.tipTool.SetToolTip(this.btnEPSSearch, "Searches Epicor for parts matching the input");
	        this.btnEPSSearch.UseVisualStyleBackColor = false;
	        this.btnEPSSearch.Click += new System.EventHandler(this.btnEPSSearch_Click);
	        // 
	        // btnGenLocExe
	        // 
	        this.btnGenLocExe.AutoSize = true;
	        this.tlpGenLoc.SetColumnSpan(this.btnGenLocExe, 2);
	        this.btnGenLocExe.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.btnGenLocExe.Location = new System.Drawing.Point(72, 50);
	        this.btnGenLocExe.Margin = new System.Windows.Forms.Padding(2);
	        this.btnGenLocExe.Name = "btnGenLocExe";
	        this.btnGenLocExe.Size = new System.Drawing.Size(66, 20);
	        this.btnGenLocExe.TabIndex = 5;
	        this.btnGenLocExe.Text = "Generate";
	        this.tipTool.SetToolTip(this.btnGenLocExe, "Go baby!");
	        this.btnGenLocExe.UseVisualStyleBackColor = true;
	        this.btnGenLocExe.Click += new System.EventHandler(this.btnGenLocExe_Click);
	        // 
	        // btnGenLocPath
	        // 
	        this.btnGenLocPath.AutoSize = true;
	        this.btnGenLocPath.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.btnGenLocPath.Location = new System.Drawing.Point(121, 26);
	        this.btnGenLocPath.Margin = new System.Windows.Forms.Padding(2);
	        this.btnGenLocPath.Name = "btnGenLocPath";
	        this.btnGenLocPath.Size = new System.Drawing.Size(17, 20);
	        this.btnGenLocPath.TabIndex = 3;
	        this.btnGenLocPath.Text = "…";
	        this.tipTool.SetToolTip(this.btnGenLocPath, "Browse for folder");
	        this.btnGenLocPath.UseVisualStyleBackColor = true;
	        this.btnGenLocPath.Click += new System.EventHandler(this.btnGenLocPath_Click);
	        // 
	        // btnGenPreCol
	        // 
	        this.btnGenPreCol.AutoSize = true;
	        this.btnGenPreCol.BackColor = System.Drawing.Color.Transparent;
	        this.btnGenPreCol.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.btnGenPreCol.FlatAppearance.BorderSize = 0;
	        this.btnGenPreCol.FlatAppearance.MouseDownBackColor = System.Drawing.SystemColors.ButtonFace;
	        this.btnGenPreCol.FlatAppearance.MouseOverBackColor = System.Drawing.SystemColors.ButtonHighlight;
	        this.btnGenPreCol.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
	        this.btnGenPreCol.Image = global::BlueBrick.Resource1.icon_GenPDFAll;
	        this.btnGenPreCol.Location = new System.Drawing.Point(100, 0);
	        this.btnGenPreCol.Margin = new System.Windows.Forms.Padding(0);
	        this.btnGenPreCol.Name = "btnGenPreCol";
	        this.btnGenPreCol.Size = new System.Drawing.Size(25, 25);
	        this.btnGenPreCol.TabIndex = 4;
	        this.tipTool.SetToolTip(this.btnGenPreCol, "Drawing Collector");
	        this.btnGenPreCol.UseVisualStyleBackColor = false;
	        this.btnGenPreCol.Click += new System.EventHandler(this.btnGenPreCol_Click);
	        // 
	        // btnGenPreDXF
	        // 
	        this.btnGenPreDXF.AutoSize = true;
	        this.btnGenPreDXF.BackColor = System.Drawing.Color.Transparent;
	        this.btnGenPreDXF.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.btnGenPreDXF.FlatAppearance.BorderSize = 0;
	        this.btnGenPreDXF.FlatAppearance.MouseDownBackColor = System.Drawing.SystemColors.ButtonFace;
	        this.btnGenPreDXF.FlatAppearance.MouseOverBackColor = System.Drawing.SystemColors.ButtonHighlight;
	        this.btnGenPreDXF.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
	        this.btnGenPreDXF.Image = global::BlueBrick.Resource1.icon_GenDXFOne;
	        this.btnGenPreDXF.Location = new System.Drawing.Point(25, 0);
	        this.btnGenPreDXF.Margin = new System.Windows.Forms.Padding(0);
	        this.btnGenPreDXF.Name = "btnGenPreDXF";
	        this.btnGenPreDXF.Size = new System.Drawing.Size(25, 25);
	        this.btnGenPreDXF.TabIndex = 1;
	        this.tipTool.SetToolTip(this.btnGenPreDXF, "Single DXF");
	        this.btnGenPreDXF.UseVisualStyleBackColor = false;
	        this.btnGenPreDXF.Click += new System.EventHandler(this.btnGenPreDXF_Click);
	        // 
	        // btnGenPreDXFAll
	        // 
	        this.btnGenPreDXFAll.AutoSize = true;
	        this.btnGenPreDXFAll.BackColor = System.Drawing.Color.Transparent;
	        this.btnGenPreDXFAll.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.btnGenPreDXFAll.FlatAppearance.BorderSize = 0;
	        this.btnGenPreDXFAll.FlatAppearance.MouseDownBackColor = System.Drawing.SystemColors.ButtonFace;
	        this.btnGenPreDXFAll.FlatAppearance.MouseOverBackColor = System.Drawing.SystemColors.ButtonHighlight;
	        this.btnGenPreDXFAll.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
	        this.btnGenPreDXFAll.Image = global::BlueBrick.Resource1.icon_GenDXFAll;
	        this.btnGenPreDXFAll.Location = new System.Drawing.Point(125, 0);
	        this.btnGenPreDXFAll.Margin = new System.Windows.Forms.Padding(0);
	        this.btnGenPreDXFAll.Name = "btnGenPreDXFAll";
	        this.btnGenPreDXFAll.Size = new System.Drawing.Size(25, 25);
	        this.btnGenPreDXFAll.TabIndex = 5;
	        this.tipTool.SetToolTip(this.btnGenPreDXFAll, "DXF of all parts");
	        this.btnGenPreDXFAll.UseVisualStyleBackColor = false;
	        this.btnGenPreDXFAll.Click += new System.EventHandler(this.btnGenPreDXFAll_Click);
	        // 
	        // btnGenPreFull
	        // 
	        this.btnGenPreFull.AutoSize = true;
	        this.btnGenPreFull.BackColor = System.Drawing.Color.Transparent;
	        this.btnGenPreFull.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.btnGenPreFull.FlatAppearance.BorderSize = 0;
	        this.btnGenPreFull.FlatAppearance.MouseDownBackColor = System.Drawing.SystemColors.ButtonFace;
	        this.btnGenPreFull.FlatAppearance.MouseOverBackColor = System.Drawing.SystemColors.ButtonHighlight;
	        this.btnGenPreFull.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
	        this.btnGenPreFull.Image = global::BlueBrick.Resource1.icon_GenFullAll;
	        this.btnGenPreFull.Location = new System.Drawing.Point(175, 0);
	        this.btnGenPreFull.Margin = new System.Windows.Forms.Padding(0);
	        this.btnGenPreFull.Name = "btnGenPreFull";
	        this.btnGenPreFull.Size = new System.Drawing.Size(26, 25);
	        this.btnGenPreFull.TabIndex = 7;
	        this.tipTool.SetToolTip(this.btnGenPreFull, "Full Product Data");
	        this.btnGenPreFull.UseVisualStyleBackColor = false;
	        this.btnGenPreFull.Click += new System.EventHandler(this.btnGenPreFull_Click);
	        // 
	        // btnGenPrePDF
	        // 
	        this.btnGenPrePDF.AutoSize = true;
	        this.btnGenPrePDF.BackColor = System.Drawing.Color.Transparent;
	        this.btnGenPrePDF.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.btnGenPrePDF.FlatAppearance.BorderSize = 0;
	        this.btnGenPrePDF.FlatAppearance.MouseDownBackColor = System.Drawing.SystemColors.ButtonFace;
	        this.btnGenPrePDF.FlatAppearance.MouseOverBackColor = System.Drawing.SystemColors.ButtonHighlight;
	        this.btnGenPrePDF.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
	        this.btnGenPrePDF.Image = global::BlueBrick.Resource1.icon_GenPDFOne;
	        this.btnGenPrePDF.Location = new System.Drawing.Point(0, 0);
	        this.btnGenPrePDF.Margin = new System.Windows.Forms.Padding(0);
	        this.btnGenPrePDF.Name = "btnGenPrePDF";
	        this.btnGenPrePDF.Size = new System.Drawing.Size(25, 25);
	        this.btnGenPrePDF.TabIndex = 0;
	        this.tipTool.SetToolTip(this.btnGenPrePDF, "Single PDF");
	        this.btnGenPrePDF.UseVisualStyleBackColor = false;
	        this.btnGenPrePDF.Click += new System.EventHandler(this.btnGenPrePDF_Click);
	        // 
	        // btnGenPrePNG
	        // 
	        this.btnGenPrePNG.AutoSize = true;
	        this.btnGenPrePNG.BackColor = System.Drawing.Color.Transparent;
	        this.btnGenPrePNG.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.btnGenPrePNG.FlatAppearance.BorderSize = 0;
	        this.btnGenPrePNG.FlatAppearance.MouseDownBackColor = System.Drawing.SystemColors.ButtonFace;
	        this.btnGenPrePNG.FlatAppearance.MouseOverBackColor = System.Drawing.SystemColors.ButtonHighlight;
	        this.btnGenPrePNG.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
	        this.btnGenPrePNG.Image = global::BlueBrick.Resource1.icon_GenPNGOne;
	        this.btnGenPrePNG.Location = new System.Drawing.Point(75, 0);
	        this.btnGenPrePNG.Margin = new System.Windows.Forms.Padding(0);
	        this.btnGenPrePNG.Name = "btnGenPrePNG";
	        this.btnGenPrePNG.Size = new System.Drawing.Size(25, 25);
	        this.btnGenPrePNG.TabIndex = 3;
	        this.tipTool.SetToolTip(this.btnGenPrePNG, "Single PNG");
	        this.btnGenPrePNG.UseVisualStyleBackColor = false;
	        this.btnGenPrePNG.Click += new System.EventHandler(this.btnGenPrePNG_Click);
	        // 
	        // btnGenPreSTP
	        // 
	        this.btnGenPreSTP.AutoSize = true;
	        this.btnGenPreSTP.BackColor = System.Drawing.Color.Transparent;
	        this.btnGenPreSTP.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.btnGenPreSTP.FlatAppearance.BorderSize = 0;
	        this.btnGenPreSTP.FlatAppearance.MouseDownBackColor = System.Drawing.SystemColors.ButtonFace;
	        this.btnGenPreSTP.FlatAppearance.MouseOverBackColor = System.Drawing.SystemColors.ButtonHighlight;
	        this.btnGenPreSTP.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
	        this.btnGenPreSTP.Image = global::BlueBrick.Resource1.icon_GenSTPOne;
	        this.btnGenPreSTP.Location = new System.Drawing.Point(50, 0);
	        this.btnGenPreSTP.Margin = new System.Windows.Forms.Padding(0);
	        this.btnGenPreSTP.Name = "btnGenPreSTP";
	        this.btnGenPreSTP.Size = new System.Drawing.Size(25, 25);
	        this.btnGenPreSTP.TabIndex = 2;
	        this.tipTool.SetToolTip(this.btnGenPreSTP, "Single STEP");
	        this.btnGenPreSTP.UseVisualStyleBackColor = false;
	        this.btnGenPreSTP.Click += new System.EventHandler(this.btnGenPreSTP_Click);
	        // 
	        // btnGenPreSTPAll
	        // 
	        this.btnGenPreSTPAll.AutoSize = true;
	        this.btnGenPreSTPAll.BackColor = System.Drawing.Color.Transparent;
	        this.btnGenPreSTPAll.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.btnGenPreSTPAll.FlatAppearance.BorderSize = 0;
	        this.btnGenPreSTPAll.FlatAppearance.MouseDownBackColor = System.Drawing.SystemColors.ButtonFace;
	        this.btnGenPreSTPAll.FlatAppearance.MouseOverBackColor = System.Drawing.SystemColors.ButtonHighlight;
	        this.btnGenPreSTPAll.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
	        this.btnGenPreSTPAll.Image = global::BlueBrick.Resource1.icon_GenSTPAll;
	        this.btnGenPreSTPAll.Location = new System.Drawing.Point(150, 0);
	        this.btnGenPreSTPAll.Margin = new System.Windows.Forms.Padding(0);
	        this.btnGenPreSTPAll.Name = "btnGenPreSTPAll";
	        this.btnGenPreSTPAll.Size = new System.Drawing.Size(25, 25);
	        this.btnGenPreSTPAll.TabIndex = 6;
	        this.tipTool.SetToolTip(this.btnGenPreSTPAll, "STEP of all parts");
	        this.btnGenPreSTPAll.UseVisualStyleBackColor = false;
	        this.btnGenPreSTPAll.Click += new System.EventHandler(this.btnGenPreSTPAll_Click);
	        // 
	        // btnLkySrch
	        // 
	        this.btnLkySrch.AutoSize = true;
	        this.btnLkySrch.BackColor = System.Drawing.Color.Transparent;
	        this.btnLkySrch.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.btnLkySrch.FlatAppearance.BorderSize = 0;
	        this.btnLkySrch.FlatAppearance.MouseDownBackColor = System.Drawing.SystemColors.ButtonFace;
	        this.btnLkySrch.FlatAppearance.MouseOverBackColor = System.Drawing.SystemColors.ButtonHighlight;
	        this.btnLkySrch.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
	        this.btnLkySrch.Image = global::BlueBrick.Resource1.icon_FeelinLucky;
	        this.btnLkySrch.Location = new System.Drawing.Point(189, 0);
	        this.btnLkySrch.Margin = new System.Windows.Forms.Padding(0);
	        this.btnLkySrch.Name = "btnLkySrch";
	        this.btnLkySrch.Size = new System.Drawing.Size(22, 22);
	        this.btnLkySrch.TabIndex = 1;
	        this.tipTool.SetToolTip(this.btnLkySrch, "Searches PDM for files matching the input");
	        this.btnLkySrch.UseVisualStyleBackColor = false;
	        this.btnLkySrch.Click += new System.EventHandler(this.btnLkySrch_Click);
	        // 
	        // btnPropMdlSave
	        // 
	        this.btnPropMdlSave.AutoSize = true;
	        this.btnPropMdlSave.BackColor = System.Drawing.Color.Transparent;
	        this.btnPropMdlSave.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.btnPropMdlSave.FlatAppearance.BorderSize = 0;
	        this.btnPropMdlSave.FlatAppearance.MouseDownBackColor = System.Drawing.SystemColors.ButtonFace;
	        this.btnPropMdlSave.FlatAppearance.MouseOverBackColor = System.Drawing.SystemColors.ButtonHighlight;
	        this.btnPropMdlSave.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
	        this.btnPropMdlSave.Image = global::BlueBrick.Resource1.icon_PropMdlSave;
	        this.btnPropMdlSave.Location = new System.Drawing.Point(100, 0);
	        this.btnPropMdlSave.Margin = new System.Windows.Forms.Padding(0);
	        this.btnPropMdlSave.Name = "btnPropMdlSave";
	        this.btnPropMdlSave.Size = new System.Drawing.Size(25, 27);
	        this.btnPropMdlSave.TabIndex = 4;
	        this.tipTool.SetToolTip(this.btnPropMdlSave, "Save linked model");
	        this.btnPropMdlSave.UseVisualStyleBackColor = false;
	        this.btnPropMdlSave.Click += new System.EventHandler(this.btnPropMdlSave_Click);
	        // 
	        // btnPropReset
	        // 
	        this.btnPropReset.AutoSize = true;
	        this.btnPropReset.BackColor = System.Drawing.Color.Transparent;
	        this.btnPropReset.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.btnPropReset.FlatAppearance.BorderSize = 0;
	        this.btnPropReset.FlatAppearance.MouseDownBackColor = System.Drawing.SystemColors.ButtonFace;
	        this.btnPropReset.FlatAppearance.MouseOverBackColor = System.Drawing.SystemColors.ButtonHighlight;
	        this.btnPropReset.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
	        this.btnPropReset.Image = global::BlueBrick.Resource1.icon_PropReset;
	        this.btnPropReset.Location = new System.Drawing.Point(25, 0);
	        this.btnPropReset.Margin = new System.Windows.Forms.Padding(0);
	        this.btnPropReset.Name = "btnPropReset";
	        this.btnPropReset.Size = new System.Drawing.Size(25, 27);
	        this.btnPropReset.TabIndex = 1;
	        this.tipTool.SetToolTip(this.btnPropReset, "Reset properties");
	        this.btnPropReset.UseVisualStyleBackColor = false;
	        this.btnPropReset.Click += new System.EventHandler(this.btnPropReset_Click);
	        // 
	        // btnPropSave
	        // 
	        this.btnPropSave.AutoSize = true;
	        this.btnPropSave.BackColor = System.Drawing.Color.Transparent;
	        this.btnPropSave.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.btnPropSave.FlatAppearance.BorderSize = 0;
	        this.btnPropSave.FlatAppearance.MouseDownBackColor = System.Drawing.SystemColors.ButtonFace;
	        this.btnPropSave.FlatAppearance.MouseOverBackColor = System.Drawing.SystemColors.ButtonHighlight;
	        this.btnPropSave.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
	        this.btnPropSave.Image = global::BlueBrick.Resource1.icon_PropCheck;
	        this.btnPropSave.Location = new System.Drawing.Point(0, 0);
	        this.btnPropSave.Margin = new System.Windows.Forms.Padding(0);
	        this.btnPropSave.Name = "btnPropSave";
	        this.btnPropSave.Size = new System.Drawing.Size(25, 27);
	        this.btnPropSave.TabIndex = 0;
	        this.tipTool.SetToolTip(this.btnPropSave, "Write properties to model");
	        this.btnPropSave.UseVisualStyleBackColor = false;
	        this.btnPropSave.Click += new System.EventHandler(this.btnPropSave_Click);
	        // 
	        // btnTlsAppear
	        // 
	        this.btnTlsAppear.AutoSize = true;
	        this.btnTlsAppear.BackColor = System.Drawing.Color.Transparent;
	        this.btnTlsAppear.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.btnTlsAppear.FlatAppearance.BorderSize = 0;
	        this.btnTlsAppear.FlatAppearance.MouseDownBackColor = System.Drawing.SystemColors.ButtonFace;
	        this.btnTlsAppear.FlatAppearance.MouseOverBackColor = System.Drawing.SystemColors.ButtonHighlight;
	        this.btnTlsAppear.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
	        this.btnTlsAppear.Image = global::BlueBrick.Resource1.icon_TlsBaseAppear;
	        this.btnTlsAppear.Location = new System.Drawing.Point(130, 0);
	        this.btnTlsAppear.Margin = new System.Windows.Forms.Padding(0);
	        this.btnTlsAppear.Name = "btnTlsAppear";
	        this.btnTlsAppear.Size = new System.Drawing.Size(26, 27);
	        this.btnTlsAppear.TabIndex = 5;
	        this.tipTool.SetToolTip(this.btnTlsAppear, "Remove all appearances from features and restore base material appearance");
	        this.btnTlsAppear.UseVisualStyleBackColor = true;
	        this.btnTlsAppear.Click += new System.EventHandler(this.btnTlsAppear_Click);
	        // 
	        // btnTlsCard
	        // 
	        this.btnTlsCard.AutoSize = true;
	        this.btnTlsCard.BackColor = System.Drawing.Color.Transparent;
	        this.btnTlsCard.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.btnTlsCard.FlatAppearance.BorderSize = 0;
	        this.btnTlsCard.FlatAppearance.MouseDownBackColor = System.Drawing.SystemColors.ButtonFace;
	        this.btnTlsCard.FlatAppearance.MouseOverBackColor = System.Drawing.SystemColors.ButtonHighlight;
	        this.btnTlsCard.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
	        this.btnTlsCard.Image = global::BlueBrick.Resource1.icon_TlsGarmin;
	        this.btnTlsCard.Location = new System.Drawing.Point(182, 0);
	        this.btnTlsCard.Margin = new System.Windows.Forms.Padding(0);
	        this.btnTlsCard.Name = "btnTlsCard";
	        this.btnTlsCard.Size = new System.Drawing.Size(26, 27);
	        this.btnTlsCard.TabIndex = 7;
	        this.tipTool.SetToolTip(this.btnTlsCard, "Generate Garmin product cards");
	        this.btnTlsCard.UseVisualStyleBackColor = true;
	        this.btnTlsCard.Click += new System.EventHandler(this.btnTlsCard_Click);
	        // 
	        // btnTlsChkIn
	        // 
	        this.btnTlsChkIn.AutoSize = true;
	        this.btnTlsChkIn.BackColor = System.Drawing.Color.Transparent;
	        this.btnTlsChkIn.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.btnTlsChkIn.FlatAppearance.BorderSize = 0;
	        this.btnTlsChkIn.FlatAppearance.MouseDownBackColor = System.Drawing.SystemColors.ButtonFace;
	        this.btnTlsChkIn.FlatAppearance.MouseOverBackColor = System.Drawing.SystemColors.ButtonHighlight;
	        this.btnTlsChkIn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
	        this.btnTlsChkIn.Image = global::BlueBrick.Resource1.icon_TlsCheckIn;
	        this.btnTlsChkIn.Location = new System.Drawing.Point(0, 0);
	        this.btnTlsChkIn.Margin = new System.Windows.Forms.Padding(0);
	        this.btnTlsChkIn.Name = "btnTlsChkIn";
	        this.btnTlsChkIn.Size = new System.Drawing.Size(26, 27);
	        this.btnTlsChkIn.TabIndex = 0;
	        this.tipTool.SetToolTip(this.btnTlsChkIn, "Check in all files for the current user");
	        this.btnTlsChkIn.UseVisualStyleBackColor = false;
	        this.btnTlsChkIn.Click += new System.EventHandler(this.btnTlsChkIn_Click);
	        // 
	        // btnTlsCopyDwg
	        // 
	        this.btnTlsCopyDwg.AutoSize = true;
	        this.btnTlsCopyDwg.BackColor = System.Drawing.Color.Transparent;
	        this.btnTlsCopyDwg.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.btnTlsCopyDwg.FlatAppearance.BorderSize = 0;
	        this.btnTlsCopyDwg.FlatAppearance.MouseDownBackColor = System.Drawing.SystemColors.ButtonFace;
	        this.btnTlsCopyDwg.FlatAppearance.MouseOverBackColor = System.Drawing.SystemColors.ButtonHighlight;
	        this.btnTlsCopyDwg.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
	        this.btnTlsCopyDwg.Image = global::BlueBrick.Resource1.icon_GenCopyDwg;
	        this.btnTlsCopyDwg.Location = new System.Drawing.Point(26, 27);
	        this.btnTlsCopyDwg.Margin = new System.Windows.Forms.Padding(0);
	        this.btnTlsCopyDwg.Name = "btnTlsCopyDwg";
	        this.btnTlsCopyDwg.Size = new System.Drawing.Size(26, 27);
	        this.btnTlsCopyDwg.TabIndex = 9;
	        this.tipTool.SetToolTip(this.btnTlsCopyDwg, "Copy existing drawing and reference current model");
	        this.btnTlsCopyDwg.UseVisualStyleBackColor = true;
	        this.btnTlsCopyDwg.Click += new System.EventHandler(this.btnTlsCopyDwg_Click);
	        // 
	        // btnTlsFinSch
	        // 
	        this.btnTlsFinSch.AutoSize = true;
	        this.btnTlsFinSch.BackColor = System.Drawing.Color.Transparent;
	        this.btnTlsFinSch.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.btnTlsFinSch.FlatAppearance.BorderSize = 0;
	        this.btnTlsFinSch.FlatAppearance.MouseDownBackColor = System.Drawing.SystemColors.ButtonFace;
	        this.btnTlsFinSch.FlatAppearance.MouseOverBackColor = System.Drawing.SystemColors.ButtonHighlight;
	        this.btnTlsFinSch.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
	        this.btnTlsFinSch.Image = global::BlueBrick.Resource1.icon_TlsFinSched;
	        this.btnTlsFinSch.Location = new System.Drawing.Point(52, 0);
	        this.btnTlsFinSch.Margin = new System.Windows.Forms.Padding(0);
	        this.btnTlsFinSch.Name = "btnTlsFinSch";
	        this.btnTlsFinSch.Size = new System.Drawing.Size(26, 27);
	        this.btnTlsFinSch.TabIndex = 2;
	        this.tipTool.SetToolTip(this.btnTlsFinSch, "Insert blank finish schedule");
	        this.btnTlsFinSch.UseVisualStyleBackColor = true;
	        this.btnTlsFinSch.Click += new System.EventHandler(this.btnTlsFinSch_Click);
	        // 
	        // btnTlsFinSearch
	        // 
	        this.btnTlsFinSearch.AutoSize = true;
	        this.btnTlsFinSearch.BackColor = System.Drawing.Color.Transparent;
	        this.btnTlsFinSearch.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.btnTlsFinSearch.FlatAppearance.BorderSize = 0;
	        this.btnTlsFinSearch.FlatAppearance.MouseDownBackColor = System.Drawing.SystemColors.ButtonFace;
	        this.btnTlsFinSearch.FlatAppearance.MouseOverBackColor = System.Drawing.SystemColors.ButtonHighlight;
	        this.btnTlsFinSearch.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
	        this.btnTlsFinSearch.Image = global::BlueBrick.Resource1.icon_TlsFinSearch;
	        this.btnTlsFinSearch.Location = new System.Drawing.Point(0, 27);
	        this.btnTlsFinSearch.Margin = new System.Windows.Forms.Padding(0);
	        this.btnTlsFinSearch.Name = "btnTlsFinSearch";
	        this.btnTlsFinSearch.Size = new System.Drawing.Size(26, 27);
	        this.btnTlsFinSearch.TabIndex = 8;
	        this.tipTool.SetToolTip(this.btnTlsFinSearch, "Replace a finish on all finish schedules");
	        this.btnTlsFinSearch.UseVisualStyleBackColor = true;
	        this.btnTlsFinSearch.Click += new System.EventHandler(this.btnTlsFinSearch_Click);
	        // 
	        // btnTlsFix
	        // 
	        this.btnTlsFix.AutoSize = true;
	        this.btnTlsFix.BackColor = System.Drawing.Color.Transparent;
	        this.btnTlsFix.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.btnTlsFix.FlatAppearance.BorderSize = 0;
	        this.btnTlsFix.FlatAppearance.MouseDownBackColor = System.Drawing.SystemColors.ButtonFace;
	        this.btnTlsFix.FlatAppearance.MouseOverBackColor = System.Drawing.SystemColors.ButtonHighlight;
	        this.btnTlsFix.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
	        this.btnTlsFix.Image = global::BlueBrick.Resource1.icon_TlsFixCrap;
	        this.btnTlsFix.Location = new System.Drawing.Point(156, 0);
	        this.btnTlsFix.Margin = new System.Windows.Forms.Padding(0);
	        this.btnTlsFix.Name = "btnTlsFix";
	        this.btnTlsFix.Size = new System.Drawing.Size(26, 27);
	        this.btnTlsFix.TabIndex = 6;
	        this.tipTool.SetToolTip(this.btnTlsFix, "Applies ViraInsight preferred values to various settings");
	        this.btnTlsFix.UseVisualStyleBackColor = true;
	        this.btnTlsFix.Click += new System.EventHandler(this.btnTlsFix_Click);
	        // 
	        // btnTlsLights
	        // 
	        this.btnTlsLights.AutoSize = true;
	        this.btnTlsLights.BackColor = System.Drawing.Color.Transparent;
	        this.btnTlsLights.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.btnTlsLights.FlatAppearance.BorderSize = 0;
	        this.btnTlsLights.FlatAppearance.MouseDownBackColor = System.Drawing.SystemColors.ButtonFace;
	        this.btnTlsLights.FlatAppearance.MouseOverBackColor = System.Drawing.SystemColors.ButtonHighlight;
	        this.btnTlsLights.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
	        this.btnTlsLights.Image = global::BlueBrick.Resource1.icon_TlsSetLights;
	        this.btnTlsLights.Location = new System.Drawing.Point(104, 0);
	        this.btnTlsLights.Margin = new System.Windows.Forms.Padding(0);
	        this.btnTlsLights.Name = "btnTlsLights";
	        this.btnTlsLights.Size = new System.Drawing.Size(26, 27);
	        this.btnTlsLights.TabIndex = 4;
	        this.tipTool.SetToolTip(this.btnTlsLights, "Reset lighting to default values");
	        this.btnTlsLights.UseVisualStyleBackColor = true;
	        this.btnTlsLights.Click += new System.EventHandler(this.btnTlsLights_Click);
	        // 
	        // btnTlsSerial
	        // 
	        this.btnTlsSerial.AutoSize = true;
	        this.btnTlsSerial.BackColor = System.Drawing.Color.Transparent;
	        this.btnTlsSerial.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.btnTlsSerial.FlatAppearance.BorderSize = 0;
	        this.btnTlsSerial.FlatAppearance.MouseDownBackColor = System.Drawing.SystemColors.ButtonFace;
	        this.btnTlsSerial.FlatAppearance.MouseOverBackColor = System.Drawing.SystemColors.ButtonHighlight;
	        this.btnTlsSerial.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
	        this.btnTlsSerial.Image = global::BlueBrick.Resource1.icon_TlsSerial;
	        this.btnTlsSerial.Location = new System.Drawing.Point(26, 0);
	        this.btnTlsSerial.Margin = new System.Windows.Forms.Padding(0);
	        this.btnTlsSerial.Name = "btnTlsSerial";
	        this.btnTlsSerial.Size = new System.Drawing.Size(26, 27);
	        this.btnTlsSerial.TabIndex = 1;
	        this.tipTool.SetToolTip(this.btnTlsSerial, "Pull new serial number");
	        this.btnTlsSerial.UseVisualStyleBackColor = true;
	        this.btnTlsSerial.Click += new System.EventHandler(this.btnTlsSerial_Click);
	        // 
	        // btnTlsShowHidden
	        // 
	        this.btnTlsShowHidden.AutoSize = true;
	        this.btnTlsShowHidden.BackColor = System.Drawing.Color.Transparent;
	        this.btnTlsShowHidden.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.btnTlsShowHidden.FlatAppearance.BorderSize = 0;
	        this.btnTlsShowHidden.FlatAppearance.MouseDownBackColor = System.Drawing.SystemColors.ButtonFace;
	        this.btnTlsShowHidden.FlatAppearance.MouseOverBackColor = System.Drawing.SystemColors.ButtonHighlight;
	        this.btnTlsShowHidden.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
	        this.btnTlsShowHidden.Image = global::BlueBrick.Resource1.icon_TlsShowHide;
	        this.btnTlsShowHidden.Location = new System.Drawing.Point(52, 27);
	        this.btnTlsShowHidden.Margin = new System.Windows.Forms.Padding(0);
	        this.btnTlsShowHidden.Name = "btnTlsShowHidden";
	        this.btnTlsShowHidden.Size = new System.Drawing.Size(26, 27);
	        this.btnTlsShowHidden.TabIndex = 10;
	        this.tipTool.SetToolTip(this.btnTlsShowHidden, "Open assembly model and unhide all parts");
	        this.btnTlsShowHidden.UseVisualStyleBackColor = true;
	        this.btnTlsShowHidden.Click += new System.EventHandler(this.btnTlsShowHidden_Click);
	        // 
	        // btnTlsThru
	        // 
	        this.btnTlsThru.AutoSize = true;
	        this.btnTlsThru.BackColor = System.Drawing.Color.Transparent;
	        this.btnTlsThru.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.btnTlsThru.FlatAppearance.BorderSize = 0;
	        this.btnTlsThru.FlatAppearance.MouseDownBackColor = System.Drawing.SystemColors.ButtonFace;
	        this.btnTlsThru.FlatAppearance.MouseOverBackColor = System.Drawing.SystemColors.ButtonHighlight;
	        this.btnTlsThru.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
	        this.btnTlsThru.Image = global::BlueBrick.Resource1.icon_TlsSelectThru;
	        this.btnTlsThru.Location = new System.Drawing.Point(78, 0);
	        this.btnTlsThru.Margin = new System.Windows.Forms.Padding(0);
	        this.btnTlsThru.Name = "btnTlsThru";
	        this.btnTlsThru.Size = new System.Drawing.Size(26, 27);
	        this.btnTlsThru.TabIndex = 3;
	        this.tipTool.SetToolTip(this.btnTlsThru, "Toggle select through faces");
	        this.btnTlsThru.UseVisualStyleBackColor = true;
	        this.btnTlsThru.Click += new System.EventHandler(this.btnTlsThru_Click);
	        // 
	        // btnTlsViki
	        // 
	        this.btnTlsViki.AutoSize = true;
	        this.btnTlsViki.BackColor = System.Drawing.Color.Transparent;
	        this.btnTlsViki.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.btnTlsViki.FlatAppearance.BorderSize = 0;
	        this.btnTlsViki.FlatAppearance.MouseDownBackColor = System.Drawing.SystemColors.ButtonFace;
	        this.btnTlsViki.FlatAppearance.MouseOverBackColor = System.Drawing.SystemColors.ButtonHighlight;
	        this.btnTlsViki.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
	        this.btnTlsViki.Image = global::BlueBrick.Resource1.icon_TlsViki;
	        this.btnTlsViki.Location = new System.Drawing.Point(104, 27);
	        this.btnTlsViki.Margin = new System.Windows.Forms.Padding(0);
	        this.btnTlsViki.Name = "btnTlsViki";
	        this.btnTlsViki.Size = new System.Drawing.Size(26, 27);
	        this.btnTlsViki.TabIndex = 12;
	        this.tipTool.SetToolTip(this.btnTlsViki, "Launch Viki: The Vira Insight Engineering Wiki");
	        this.btnTlsViki.UseVisualStyleBackColor = true;
	        this.btnTlsViki.Click += new System.EventHandler(this.btnTlsViki_Click);
	        // 
	        // chkGenTypDXF
	        // 
	        this.chkGenTypDXF.AutoSize = true;
	        this.chkGenTypDXF.Dock = System.Windows.Forms.DockStyle.Left;
	        this.chkGenTypDXF.Location = new System.Drawing.Point(2, 22);
	        this.chkGenTypDXF.Margin = new System.Windows.Forms.Padding(2);
	        this.chkGenTypDXF.Name = "chkGenTypDXF";
	        this.chkGenTypDXF.Size = new System.Drawing.Size(47, 16);
	        this.chkGenTypDXF.TabIndex = 1;
	        this.chkGenTypDXF.Text = "DXF";
	        this.tipTool.SetToolTip(this.chkGenTypDXF, "Create DXFs of parts");
	        this.chkGenTypDXF.UseVisualStyleBackColor = true;
	        // 
	        // chkGenTypIGES
	        // 
	        this.chkGenTypIGES.AutoSize = true;
	        this.chkGenTypIGES.Dock = System.Windows.Forms.DockStyle.Left;
	        this.chkGenTypIGES.Location = new System.Drawing.Point(2, 82);
	        this.chkGenTypIGES.Margin = new System.Windows.Forms.Padding(2);
	        this.chkGenTypIGES.Name = "chkGenTypIGES";
	        this.chkGenTypIGES.Size = new System.Drawing.Size(51, 16);
	        this.chkGenTypIGES.TabIndex = 4;
	        this.chkGenTypIGES.Text = "IGES";
	        this.tipTool.SetToolTip(this.chkGenTypIGES, "Create IGESs of parts");
	        this.chkGenTypIGES.UseVisualStyleBackColor = true;
	        // 
	        // chkGenTypPDF
	        // 
	        this.chkGenTypPDF.AutoSize = true;
	        this.chkGenTypPDF.Dock = System.Windows.Forms.DockStyle.Left;
	        this.chkGenTypPDF.Location = new System.Drawing.Point(2, 2);
	        this.chkGenTypPDF.Margin = new System.Windows.Forms.Padding(2);
	        this.chkGenTypPDF.Name = "chkGenTypPDF";
	        this.chkGenTypPDF.Size = new System.Drawing.Size(47, 16);
	        this.chkGenTypPDF.TabIndex = 0;
	        this.chkGenTypPDF.Text = "PDF";
	        this.tipTool.SetToolTip(this.chkGenTypPDF, "Create PDFs of drawings");
	        this.chkGenTypPDF.UseVisualStyleBackColor = true;
	        // 
	        // chkGenTypPNG
	        // 
	        this.chkGenTypPNG.AutoSize = true;
	        this.chkGenTypPNG.Dock = System.Windows.Forms.DockStyle.Left;
	        this.chkGenTypPNG.Location = new System.Drawing.Point(2, 62);
	        this.chkGenTypPNG.Margin = new System.Windows.Forms.Padding(2);
	        this.chkGenTypPNG.Name = "chkGenTypPNG";
	        this.chkGenTypPNG.Size = new System.Drawing.Size(49, 16);
	        this.chkGenTypPNG.TabIndex = 3;
	        this.chkGenTypPNG.Text = "PNG";
	        this.tipTool.SetToolTip(this.chkGenTypPNG, "Create PNGs of parts");
	        this.chkGenTypPNG.UseVisualStyleBackColor = true;
	        // 
	        // chkGenTypPkt
	        // 
	        this.chkGenTypPkt.AutoSize = true;
	        this.chkGenTypPkt.Dock = System.Windows.Forms.DockStyle.Left;
	        this.chkGenTypPkt.Location = new System.Drawing.Point(2, 102);
	        this.chkGenTypPkt.Margin = new System.Windows.Forms.Padding(2);
	        this.chkGenTypPkt.Name = "chkGenTypPkt";
	        this.chkGenTypPkt.Size = new System.Drawing.Size(51, 21);
	        this.chkGenTypPkt.TabIndex = 5;
	        this.chkGenTypPkt.Text = "Packet";
	        this.tipTool.SetToolTip(this.chkGenTypPkt, "Create a PDF packet");
	        this.chkGenTypPkt.UseVisualStyleBackColor = true;
	        this.chkGenTypPkt.CheckedChanged += new System.EventHandler(this.chkGenTypPkt_CheckedChanged);
	        // 
	        // chkGenTypSTP
	        // 
	        this.chkGenTypSTP.AutoSize = true;
	        this.chkGenTypSTP.Dock = System.Windows.Forms.DockStyle.Left;
	        this.chkGenTypSTP.Location = new System.Drawing.Point(2, 42);
	        this.chkGenTypSTP.Margin = new System.Windows.Forms.Padding(2);
	        this.chkGenTypSTP.Name = "chkGenTypSTP";
	        this.chkGenTypSTP.Size = new System.Drawing.Size(51, 16);
	        this.chkGenTypSTP.TabIndex = 2;
	        this.chkGenTypSTP.Text = "STEP";
	        this.tipTool.SetToolTip(this.chkGenTypSTP, "Create STEPs of parts");
	        this.chkGenTypSTP.UseVisualStyleBackColor = true;
	        // 
	        // chkPropAll
	        // 
	        this.chkPropAll.Appearance = System.Windows.Forms.Appearance.Button;
	        this.chkPropAll.AutoSize = true;
	        this.chkPropAll.BackColor = System.Drawing.Color.Transparent;
	        this.chkPropAll.CheckAlign = System.Drawing.ContentAlignment.MiddleCenter;
	        this.chkPropAll.Checked = true;
	        this.chkPropAll.CheckState = System.Windows.Forms.CheckState.Checked;
	        this.chkPropAll.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.chkPropAll.FlatAppearance.BorderSize = 0;
	        this.chkPropAll.FlatAppearance.CheckedBackColor = System.Drawing.SystemColors.ButtonHighlight;
	        this.chkPropAll.FlatAppearance.MouseDownBackColor = System.Drawing.SystemColors.ButtonFace;
	        this.chkPropAll.FlatAppearance.MouseOverBackColor = System.Drawing.SystemColors.ButtonHighlight;
	        this.chkPropAll.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
	        this.chkPropAll.Image = global::BlueBrick.Resource1.icon_PropAll;
	        this.chkPropAll.Location = new System.Drawing.Point(50, 0);
	        this.chkPropAll.Margin = new System.Windows.Forms.Padding(0);
	        this.chkPropAll.Name = "chkPropAll";
	        this.chkPropAll.Size = new System.Drawing.Size(25, 27);
	        this.chkPropAll.TabIndex = 2;
	        this.tipTool.SetToolTip(this.chkPropAll, "Write to all configurations");
	        this.chkPropAll.UseVisualStyleBackColor = false;
	        // 
	        // chkPropSearch
	        // 
	        this.chkPropSearch.Appearance = System.Windows.Forms.Appearance.Button;
	        this.chkPropSearch.AutoSize = true;
	        this.chkPropSearch.BackColor = System.Drawing.Color.Transparent;
	        this.chkPropSearch.CheckAlign = System.Drawing.ContentAlignment.MiddleCenter;
	        this.chkPropSearch.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.chkPropSearch.FlatAppearance.BorderSize = 0;
	        this.chkPropSearch.FlatAppearance.CheckedBackColor = System.Drawing.SystemColors.ButtonHighlight;
	        this.chkPropSearch.FlatAppearance.MouseDownBackColor = System.Drawing.SystemColors.ButtonFace;
	        this.chkPropSearch.FlatAppearance.MouseOverBackColor = System.Drawing.SystemColors.ButtonHighlight;
	        this.chkPropSearch.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
	        this.chkPropSearch.Image = global::BlueBrick.Resource1.icon_PropSearch;
	        this.chkPropSearch.Location = new System.Drawing.Point(75, 0);
	        this.chkPropSearch.Margin = new System.Windows.Forms.Padding(0);
	        this.chkPropSearch.Name = "chkPropSearch";
	        this.chkPropSearch.Size = new System.Drawing.Size(25, 27);
	        this.chkPropSearch.TabIndex = 3;
	        this.tipTool.SetToolTip(this.chkPropSearch, "Search jobs and POs in Epicor");
	        this.chkPropSearch.UseVisualStyleBackColor = false;
	        // 
	        // cmbConvRlUnitFrom
	        // 
	        this.tlpConvRl.SetColumnSpan(this.cmbConvRlUnitFrom, 2);
	        this.cmbConvRlUnitFrom.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.cmbConvRlUnitFrom.FormattingEnabled = true;
	        this.cmbConvRlUnitFrom.Location = new System.Drawing.Point(54, 2);
	        this.cmbConvRlUnitFrom.Margin = new System.Windows.Forms.Padding(2);
	        this.cmbConvRlUnitFrom.Name = "cmbConvRlUnitFrom";
	        this.cmbConvRlUnitFrom.Size = new System.Drawing.Size(97, 21);
	        this.cmbConvRlUnitFrom.TabIndex = 1;
	        // 
	        // cmbConvRlUnitTo
	        // 
	        this.tlpConvRl.SetColumnSpan(this.cmbConvRlUnitTo, 2);
	        this.cmbConvRlUnitTo.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.cmbConvRlUnitTo.FormattingEnabled = true;
	        this.cmbConvRlUnitTo.Location = new System.Drawing.Point(108, 140);
	        this.cmbConvRlUnitTo.Margin = new System.Windows.Forms.Padding(2);
	        this.cmbConvRlUnitTo.Name = "cmbConvRlUnitTo";
	        this.cmbConvRlUnitTo.Size = new System.Drawing.Size(85, 21);
	        this.cmbConvRlUnitTo.TabIndex = 14;
	        // 
	        // cmbConvUnitFrom
	        // 
	        this.tlpConvUnit.SetColumnSpan(this.cmbConvUnitFrom, 3);
	        this.cmbConvUnitFrom.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.cmbConvUnitFrom.FormattingEnabled = true;
	        this.cmbConvUnitFrom.Location = new System.Drawing.Point(2, 28);
	        this.cmbConvUnitFrom.Margin = new System.Windows.Forms.Padding(2);
	        this.cmbConvUnitFrom.Name = "cmbConvUnitFrom";
	        this.cmbConvUnitFrom.Size = new System.Drawing.Size(151, 21);
	        this.cmbConvUnitFrom.TabIndex = 2;
	        this.cmbConvUnitFrom.SelectedIndexChanged += new System.EventHandler(this.cmbConvUnitFrom_SelectedIndexChanged);
	        this.cmbConvUnitFrom.PreviewKeyDown += new System.Windows.Forms.PreviewKeyDownEventHandler(this.cmbConvUnitFrom_PreviewKeyDown);
	        // 
	        // cmbConvUnitTo
	        // 
	        this.tlpConvUnit.SetColumnSpan(this.cmbConvUnitTo, 3);
	        this.cmbConvUnitTo.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.cmbConvUnitTo.FormattingEnabled = true;
	        this.cmbConvUnitTo.Location = new System.Drawing.Point(2, 123);
	        this.cmbConvUnitTo.Margin = new System.Windows.Forms.Padding(2);
	        this.cmbConvUnitTo.Name = "cmbConvUnitTo";
	        this.cmbConvUnitTo.Size = new System.Drawing.Size(151, 21);
	        this.cmbConvUnitTo.TabIndex = 7;
	        this.cmbConvUnitTo.SelectedIndexChanged += new System.EventHandler(this.cmbConvUnitTo_SelectedIndexChanged);
	        // 
	        // cmbConvUnitTypes
	        // 
	        this.tlpConvUnit.SetColumnSpan(this.cmbConvUnitTypes, 2);
	        this.cmbConvUnitTypes.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.cmbConvUnitTypes.FormattingEnabled = true;
	        this.cmbConvUnitTypes.Location = new System.Drawing.Point(37, 2);
	        this.cmbConvUnitTypes.Margin = new System.Windows.Forms.Padding(2);
	        this.cmbConvUnitTypes.Name = "cmbConvUnitTypes";
	        this.cmbConvUnitTypes.Size = new System.Drawing.Size(116, 21);
	        this.cmbConvUnitTypes.TabIndex = 1;
	        this.cmbConvUnitTypes.SelectedIndexChanged += new System.EventHandler(this.cmbConvUnitTypes_SelectedIndexChanged);
	        // 
	        // cmbLkySrch
	        // 
	        this.cmbLkySrch.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
	        this.cmbLkySrch.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
	        this.cmbLkySrch.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.cmbLkySrch.Location = new System.Drawing.Point(2, 2);
	        this.cmbLkySrch.Margin = new System.Windows.Forms.Padding(2);
	        this.cmbLkySrch.Name = "cmbLkySrch";
	        this.cmbLkySrch.Size = new System.Drawing.Size(185, 21);
	        this.cmbLkySrch.Sorted = true;
	        this.cmbLkySrch.TabIndex = 0;
	        this.tipTool.SetToolTip(this.cmbLkySrch, "Input search criteria");
	        this.cmbLkySrch.KeyDown += new System.Windows.Forms.KeyEventHandler(this.cmbLkySrch_KeyDown);
	        // 
	        // cmbPropGenPCat
	        // 
	        this.cmbPropGenPCat.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.cmbPropGenPCat.Enabled = false;
	        this.cmbPropGenPCat.FormattingEnabled = true;
	        this.cmbPropGenPCat.Location = new System.Drawing.Point(50, 2);
	        this.cmbPropGenPCat.Margin = new System.Windows.Forms.Padding(2);
	        this.cmbPropGenPCat.Name = "cmbPropGenPCat";
	        this.cmbPropGenPCat.Size = new System.Drawing.Size(143, 21);
	        this.cmbPropGenPCat.TabIndex = 1;
	        this.cmbPropGenPCat.Leave += new System.EventHandler(this.cmbPropGenType_Leave);
	        // 
	        // cmsLucky
	        // 
	        this.cmsLucky.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { this.tsiInsertPart, this.tsiOpenFile, this.tsiOpenExplore });
	        this.cmsLucky.Name = "cmsLucky";
	        this.cmsLucky.Size = new System.Drawing.Size(161, 70);
	        // 
	        // tsiInsertPart
	        // 
	        this.tsiInsertPart.Name = "tsiInsertPart";
	        this.tsiInsertPart.Size = new System.Drawing.Size(160, 22);
	        this.tsiInsertPart.Text = "&Insert Part";
	        this.tsiInsertPart.Click += new System.EventHandler(this.tsiInsertPart_Click);
	        // 
	        // tsiOpenFile
	        // 
	        this.tsiOpenFile.Name = "tsiOpenFile";
	        this.tsiOpenFile.Size = new System.Drawing.Size(160, 22);
	        this.tsiOpenFile.Text = "&Open Part";
	        this.tsiOpenFile.Click += new System.EventHandler(this.tsiOpenFile_Click);
	        // 
	        // tsiOpenExplore
	        // 
	        this.tsiOpenExplore.Name = "tsiOpenExplore";
	        this.tsiOpenExplore.Size = new System.Drawing.Size(160, 22);
	        this.tsiOpenExplore.Text = "&Browse to folder";
	        this.tsiOpenExplore.Click += new System.EventHandler(this.tsiOpenExplore_Click);
	        // 
	        // cmsRClick
	        // 
	        this.cmsRClick.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { this.tsiCopyPart, this.tsiCopyDesc, this.tsiSearch });
	        this.cmsRClick.Name = "cmsRClick";
	        this.cmsRClick.Size = new System.Drawing.Size(166, 70);
	        // 
	        // tsiCopyPart
	        // 
	        this.tsiCopyPart.Name = "tsiCopyPart";
	        this.tsiCopyPart.Size = new System.Drawing.Size(165, 22);
	        this.tsiCopyPart.Text = "Copy &Part";
	        this.tsiCopyPart.Click += new System.EventHandler(this.tsiCopyPart_Click);
	        // 
	        // tsiCopyDesc
	        // 
	        this.tsiCopyDesc.Name = "tsiCopyDesc";
	        this.tsiCopyDesc.Size = new System.Drawing.Size(165, 22);
	        this.tsiCopyDesc.Text = "Copy &Description";
	        this.tsiCopyDesc.Click += new System.EventHandler(this.tsiCopyDesc_Click);
	        // 
	        // tsiSearch
	        // 
	        this.tsiSearch.Name = "tsiSearch";
	        this.tsiSearch.Size = new System.Drawing.Size(165, 22);
	        this.tsiSearch.Text = "&Search PDM";
	        this.tsiSearch.Click += new System.EventHandler(this.tsiSearch_Click);
	        // 
	        // colConvBfDims
	        // 
	        this.colConvBfDims.Text = "Dims";
	        this.colConvBfDims.Width = 120;
	        // 
	        // colConvBfQty
	        // 
	        this.colConvBfQty.Text = "Bf";
	        this.colConvBfQty.Width = 45;
	        // 
	        // colEPSResultsAlloc
	        // 
	        this.colEPSResultsAlloc.Text = "Commit";
	        this.colEPSResultsAlloc.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
	        // 
	        // colEPSResultsDesc
	        // 
	        this.colEPSResultsDesc.Text = "Description";
	        this.colEPSResultsDesc.Width = 200;
	        // 
	        // colEPSResultsOH
	        // 
	        this.colEPSResultsOH.Text = "OnHand";
	        this.colEPSResultsOH.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
	        // 
	        // colEPSResultsPart
	        // 
	        this.colEPSResultsPart.Text = "Part";
	        this.colEPSResultsPart.Width = 120;
	        // 
	        // colEPSResultsUM
	        // 
	        this.colEPSResultsUM.Text = "UM";
	        this.colEPSResultsUM.Width = 30;
	        // 
	        // colLkyResults1
	        // 
	        this.colLkyResults1.Text = "Preview";
	        this.colLkyResults1.Width = 65;
	        // 
	        // colLkyResults2
	        // 
	        this.colLkyResults2.Text = "Filename";
	        this.colLkyResults2.Width = 120;
	        // 
	        // colLkyResults3
	        // 
	        this.colLkyResults3.Text = "Path";
	        this.colLkyResults3.Width = 200;
	        // 
	        // colPropOrd1
	        // 
	        this.colPropOrd1.Text = "ID";
	        this.colPropOrd1.Width = 80;
	        // 
	        // colPropOrd2
	        // 
	        this.colPropOrd2.Text = "Src";
	        // 
	        // colPropOrd3
	        // 
	        this.colPropOrd3.Text = "Part";
	        this.colPropOrd3.Width = 115;
	        // 
	        // colPropOrd4
	        // 
	        this.colPropOrd4.Text = "Rev";
	        this.colPropOrd4.Width = 40;
	        // 
	        // colPropOrd5
	        // 
	        this.colPropOrd5.Text = "Drawing";
	        this.colPropOrd5.Width = 115;
	        // 
	        // colPropOrd6
	        // 
	        this.colPropOrd6.Text = "Qty";
	        this.colPropOrd6.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
	        this.colPropOrd6.Width = 40;
	        // 
	        // colPropOrd7
	        // 
	        this.colPropOrd7.Text = "Due";
	        this.colPropOrd7.Width = 80;
	        // 
	        // colPropOrd8
	        // 
	        this.colPropOrd8.Text = "Status";
	        this.colPropOrd8.Width = 115;
	        // 
	        // cplMainEPS
	        // 
	        this.cplMainEPS.AutoScroll = true;
	        // 
	        // cplMainEPS.Content
	        // 
	        this.cplMainEPS.Content.AutoSize = true;
	        this.cplMainEPS.Content.Controls.Add(this.tlpMainEPS);
	        this.cplMainEPS.Content.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.cplMainEPS.Content.Location = new System.Drawing.Point(5, 24);
	        this.cplMainEPS.Content.Margin = new System.Windows.Forms.Padding(4);
	        this.cplMainEPS.Content.Name = "Content";
	        this.cplMainEPS.Content.Size = new System.Drawing.Size(211, 249);
	        this.cplMainEPS.Content.TabIndex = 1;
	        this.cplMainEPS.Dock = System.Windows.Forms.DockStyle.Top;
	        this.cplMainEPS.Location = new System.Drawing.Point(0, 850);
	        this.cplMainEPS.Margin = new System.Windows.Forms.Padding(4);
	        this.cplMainEPS.MaximumSize = new System.Drawing.Size(750, 812);
	        this.cplMainEPS.MinimumSize = new System.Drawing.Size(90, 18);
	        this.cplMainEPS.Name = "cplMainEPS";
	        this.cplMainEPS.Size = new System.Drawing.Size(223, 280);
	        this.cplMainEPS.TabIndex = 4;
	        this.cplMainEPS.Title = "Epicor Part Search";
	        // 
	        // tlpMainEPS
	        // 
	        this.tlpMainEPS.AutoSize = true;
	        this.tlpMainEPS.ColumnCount = 2;
	        this.tlpMainEPS.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 90F));
	        this.tlpMainEPS.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 10F));
	        this.tlpMainEPS.Controls.Add(this.txtEPSPart, 0, 0);
	        this.tlpMainEPS.Controls.Add(this.txtEPSDesc, 0, 1);
	        this.tlpMainEPS.Controls.Add(this.btnEPSSearch, 1, 0);
	        this.tlpMainEPS.Controls.Add(this.btnEPSClear, 1, 1);
	        this.tlpMainEPS.Controls.Add(this.lstEPSResults, 0, 2);
	        this.tlpMainEPS.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.tlpMainEPS.Location = new System.Drawing.Point(0, 0);
	        this.tlpMainEPS.Name = "tlpMainEPS";
	        this.tlpMainEPS.RowCount = 3;
	        this.tlpMainEPS.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 9F));
	        this.tlpMainEPS.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 9F));
	        this.tlpMainEPS.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 82F));
	        this.tlpMainEPS.Size = new System.Drawing.Size(211, 249);
	        this.tlpMainEPS.TabIndex = 0;
	        // 
	        // txtEPSPart
	        // 
	        this.txtEPSPart.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.txtEPSPart.Location = new System.Drawing.Point(2, 2);
	        this.txtEPSPart.Margin = new System.Windows.Forms.Padding(2);
	        this.txtEPSPart.Name = "txtEPSPart";
	        this.txtEPSPart.Size = new System.Drawing.Size(185, 20);
	        this.txtEPSPart.TabIndex = 0;
	        this.tipTool.SetToolTip(this.txtEPSPart, "Part number field search criteria");
	        this.txtEPSPart.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.txtEPSPart_KeyPress);
	        // 
	        // txtEPSDesc
	        // 
	        this.txtEPSDesc.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.txtEPSDesc.Location = new System.Drawing.Point(2, 24);
	        this.txtEPSDesc.Margin = new System.Windows.Forms.Padding(2);
	        this.txtEPSDesc.Name = "txtEPSDesc";
	        this.txtEPSDesc.Size = new System.Drawing.Size(185, 20);
	        this.txtEPSDesc.TabIndex = 1;
	        this.tipTool.SetToolTip(this.txtEPSDesc, "Description field search criteria");
	        this.txtEPSDesc.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.txtEPSDesc_KeyPress);
	        // 
	        // lstEPSResults
	        // 
	        this.lstEPSResults.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] { this.colEPSResultsPart, this.colEPSResultsDesc, this.colEPSResultsUM, this.colEPSResultsOH, this.colEPSResultsAlloc });
	        this.tlpMainEPS.SetColumnSpan(this.lstEPSResults, 2);
	        this.lstEPSResults.ContextMenuStrip = this.cmsRClick;
	        this.lstEPSResults.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lstEPSResults.FullRowSelect = true;
	        this.lstEPSResults.HideSelection = false;
	        this.lstEPSResults.Location = new System.Drawing.Point(2, 46);
	        this.lstEPSResults.Margin = new System.Windows.Forms.Padding(2);
	        this.lstEPSResults.MultiSelect = false;
	        this.lstEPSResults.Name = "lstEPSResults";
	        this.lstEPSResults.ShowItemToolTips = true;
	        this.lstEPSResults.Size = new System.Drawing.Size(207, 201);
	        this.lstEPSResults.TabIndex = 4;
	        this.lstEPSResults.UseCompatibleStateImageBehavior = false;
	        this.lstEPSResults.View = System.Windows.Forms.View.Details;
	        this.lstEPSResults.ColumnClick += new System.Windows.Forms.ColumnClickEventHandler(this.lstEPSResults_ColumnClick);
	        this.lstEPSResults.DoubleClick += new System.EventHandler(this.lstEPSResults_DoubleClick);
	        this.lstEPSResults.KeyUp += new System.Windows.Forms.KeyEventHandler(this.lstEPSResults_KeyUp);
	        // 
	        // cplMainGen
	        // 
	        this.cplMainGen.AutoScroll = true;
	        // 
	        // cplMainGen.Content
	        // 
	        this.cplMainGen.Content.AutoSize = true;
	        this.cplMainGen.Content.Controls.Add(this.tlpMainGen);
	        this.cplMainGen.Content.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.cplMainGen.Content.Location = new System.Drawing.Point(5, 24);
	        this.cplMainGen.Content.Margin = new System.Windows.Forms.Padding(4);
	        this.cplMainGen.Content.Name = "Content";
	        this.cplMainGen.Content.Size = new System.Drawing.Size(211, 194);
	        this.cplMainGen.Content.TabIndex = 1;
	        this.cplMainGen.Dock = System.Windows.Forms.DockStyle.Top;
	        this.cplMainGen.Location = new System.Drawing.Point(0, 0);
	        this.cplMainGen.Margin = new System.Windows.Forms.Padding(4);
	        this.cplMainGen.MaximumSize = new System.Drawing.Size(750, 812);
	        this.cplMainGen.MinimumSize = new System.Drawing.Size(90, 18);
	        this.cplMainGen.Name = "cplMainGen";
	        this.cplMainGen.Size = new System.Drawing.Size(223, 225);
	        this.cplMainGen.TabIndex = 0;
	        this.cplMainGen.Title = "Document Generators";
	        // 
	        // tlpMainGen
	        // 
	        this.tlpMainGen.AutoSize = true;
	        this.tlpMainGen.ColumnCount = 2;
	        this.tlpMainGen.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 30F));
	        this.tlpMainGen.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 70F));
	        this.tlpMainGen.Controls.Add(this.grpGenPre, 0, 0);
	        this.tlpMainGen.Controls.Add(this.grpGenTyp, 0, 1);
	        this.tlpMainGen.Controls.Add(this.grpGenScope, 1, 1);
	        this.tlpMainGen.Controls.Add(this.grpGenLoc, 1, 2);
	        this.tlpMainGen.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.tlpMainGen.Location = new System.Drawing.Point(0, 0);
	        this.tlpMainGen.Name = "tlpMainGen";
	        this.tlpMainGen.RowCount = 3;
	        this.tlpMainGen.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
	        this.tlpMainGen.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 23F));
	        this.tlpMainGen.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 52F));
	        this.tlpMainGen.Size = new System.Drawing.Size(211, 194);
	        this.tlpMainGen.TabIndex = 0;
	        // 
	        // grpGenPre
	        // 
	        this.grpGenPre.AutoSize = true;
	        this.tlpMainGen.SetColumnSpan(this.grpGenPre, 2);
	        this.grpGenPre.Controls.Add(this.tlpGenPre);
	        this.grpGenPre.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.grpGenPre.Location = new System.Drawing.Point(3, 3);
	        this.grpGenPre.Name = "grpGenPre";
	        this.grpGenPre.Padding = new System.Windows.Forms.Padding(2);
	        this.grpGenPre.Size = new System.Drawing.Size(205, 42);
	        this.grpGenPre.TabIndex = 0;
	        this.grpGenPre.TabStop = false;
	        this.grpGenPre.Text = "Presets";
	        // 
	        // tlpGenPre
	        // 
	        this.tlpGenPre.AutoSize = true;
	        this.tlpGenPre.ColumnCount = 8;
	        this.tlpGenPre.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 12.5F));
	        this.tlpGenPre.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 12.5F));
	        this.tlpGenPre.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 12.5F));
	        this.tlpGenPre.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 12.5F));
	        this.tlpGenPre.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 12.5F));
	        this.tlpGenPre.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 12.5F));
	        this.tlpGenPre.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 12.5F));
	        this.tlpGenPre.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 12.5F));
	        this.tlpGenPre.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 15F));
	        this.tlpGenPre.Controls.Add(this.btnGenPrePDF, 0, 0);
	        this.tlpGenPre.Controls.Add(this.btnGenPreDXF, 1, 0);
	        this.tlpGenPre.Controls.Add(this.btnGenPreSTP, 2, 0);
	        this.tlpGenPre.Controls.Add(this.btnGenPrePNG, 3, 0);
	        this.tlpGenPre.Controls.Add(this.btnGenPreCol, 4, 0);
	        this.tlpGenPre.Controls.Add(this.btnGenPreDXFAll, 5, 0);
	        this.tlpGenPre.Controls.Add(this.btnGenPreSTPAll, 6, 0);
	        this.tlpGenPre.Controls.Add(this.btnGenPreFull, 7, 0);
	        this.tlpGenPre.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.tlpGenPre.Location = new System.Drawing.Point(2, 15);
	        this.tlpGenPre.Margin = new System.Windows.Forms.Padding(2);
	        this.tlpGenPre.Name = "tlpGenPre";
	        this.tlpGenPre.RowCount = 1;
	        this.tlpGenPre.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
	        this.tlpGenPre.Size = new System.Drawing.Size(201, 25);
	        this.tlpGenPre.TabIndex = 0;
	        // 
	        // grpGenTyp
	        // 
	        this.grpGenTyp.AutoSize = true;
	        this.grpGenTyp.Controls.Add(this.tlpGenTyp);
	        this.grpGenTyp.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.grpGenTyp.Location = new System.Drawing.Point(2, 50);
	        this.grpGenTyp.Margin = new System.Windows.Forms.Padding(2);
	        this.grpGenTyp.Name = "grpGenTyp";
	        this.grpGenTyp.Padding = new System.Windows.Forms.Padding(2);
	        this.tlpMainGen.SetRowSpan(this.grpGenTyp, 2);
	        this.grpGenTyp.Size = new System.Drawing.Size(59, 142);
	        this.grpGenTyp.TabIndex = 1;
	        this.grpGenTyp.TabStop = false;
	        this.grpGenTyp.Text = "Formats";
	        // 
	        // tlpGenTyp
	        // 
	        this.tlpGenTyp.AutoSize = true;
	        this.tlpGenTyp.ColumnCount = 1;
	        this.tlpGenTyp.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
	        this.tlpGenTyp.Controls.Add(this.chkGenTypPDF, 0, 0);
	        this.tlpGenTyp.Controls.Add(this.chkGenTypDXF, 0, 1);
	        this.tlpGenTyp.Controls.Add(this.chkGenTypSTP, 0, 2);
	        this.tlpGenTyp.Controls.Add(this.chkGenTypPNG, 0, 3);
	        this.tlpGenTyp.Controls.Add(this.chkGenTypIGES, 0, 4);
	        this.tlpGenTyp.Controls.Add(this.chkGenTypPkt, 0, 5);
	        this.tlpGenTyp.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.tlpGenTyp.Location = new System.Drawing.Point(2, 15);
	        this.tlpGenTyp.Margin = new System.Windows.Forms.Padding(2);
	        this.tlpGenTyp.Name = "tlpGenTyp";
	        this.tlpGenTyp.RowCount = 6;
	        this.tlpGenTyp.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 16.66F));
	        this.tlpGenTyp.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 16.66F));
	        this.tlpGenTyp.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 16.66F));
	        this.tlpGenTyp.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 16.66F));
	        this.tlpGenTyp.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 16.66F));
	        this.tlpGenTyp.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 16.7F));
	        this.tlpGenTyp.Size = new System.Drawing.Size(55, 125);
	        this.tlpGenTyp.TabIndex = 0;
	        // 
	        // grpGenScope
	        // 
	        this.grpGenScope.AutoSize = true;
	        this.grpGenScope.Controls.Add(this.tlpGenScope);
	        this.grpGenScope.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.grpGenScope.Location = new System.Drawing.Point(65, 50);
	        this.grpGenScope.Margin = new System.Windows.Forms.Padding(2);
	        this.grpGenScope.Name = "grpGenScope";
	        this.grpGenScope.Padding = new System.Windows.Forms.Padding(2);
	        this.grpGenScope.Size = new System.Drawing.Size(144, 40);
	        this.grpGenScope.TabIndex = 2;
	        this.grpGenScope.TabStop = false;
	        this.grpGenScope.Text = "Scope";
	        // 
	        // tlpGenScope
	        // 
	        this.tlpGenScope.AutoSize = true;
	        this.tlpGenScope.ColumnCount = 2;
	        this.tlpGenScope.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
	        this.tlpGenScope.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
	        this.tlpGenScope.Controls.Add(this.radGenScopeAll, 0, 0);
	        this.tlpGenScope.Controls.Add(this.radGenScopeSingle, 1, 0);
	        this.tlpGenScope.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.tlpGenScope.Location = new System.Drawing.Point(2, 15);
	        this.tlpGenScope.Margin = new System.Windows.Forms.Padding(2);
	        this.tlpGenScope.Name = "tlpGenScope";
	        this.tlpGenScope.RowCount = 1;
	        this.tlpGenScope.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
	        this.tlpGenScope.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 23F));
	        this.tlpGenScope.Size = new System.Drawing.Size(140, 23);
	        this.tlpGenScope.TabIndex = 0;
	        // 
	        // radGenScopeAll
	        // 
	        this.radGenScopeAll.AutoSize = true;
	        this.radGenScopeAll.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.radGenScopeAll.Location = new System.Drawing.Point(2, 2);
	        this.radGenScopeAll.Margin = new System.Windows.Forms.Padding(2);
	        this.radGenScopeAll.Name = "radGenScopeAll";
	        this.radGenScopeAll.Size = new System.Drawing.Size(66, 19);
	        this.radGenScopeAll.TabIndex = 0;
	        this.radGenScopeAll.TabStop = true;
	        this.radGenScopeAll.Text = "All";
	        this.tipTool.SetToolTip(this.radGenScopeAll, "All models in tree");
	        this.radGenScopeAll.UseVisualStyleBackColor = true;
	        // 
	        // radGenScopeSingle
	        // 
	        this.radGenScopeSingle.AutoSize = true;
	        this.radGenScopeSingle.Checked = true;
	        this.radGenScopeSingle.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.radGenScopeSingle.Location = new System.Drawing.Point(72, 2);
	        this.radGenScopeSingle.Margin = new System.Windows.Forms.Padding(2);
	        this.radGenScopeSingle.Name = "radGenScopeSingle";
	        this.radGenScopeSingle.Size = new System.Drawing.Size(66, 19);
	        this.radGenScopeSingle.TabIndex = 1;
	        this.radGenScopeSingle.TabStop = true;
	        this.radGenScopeSingle.Text = "Single";
	        this.tipTool.SetToolTip(this.radGenScopeSingle, "Top level only");
	        this.radGenScopeSingle.UseVisualStyleBackColor = true;
	        this.radGenScopeSingle.CheckedChanged += new System.EventHandler(this.radGenScopeSingle_CheckedChanged);
	        // 
	        // grpGenLoc
	        // 
	        this.grpGenLoc.AutoSize = true;
	        this.grpGenLoc.Controls.Add(this.tlpGenLoc);
	        this.grpGenLoc.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.grpGenLoc.Location = new System.Drawing.Point(65, 94);
	        this.grpGenLoc.Margin = new System.Windows.Forms.Padding(2);
	        this.grpGenLoc.Name = "grpGenLoc";
	        this.grpGenLoc.Padding = new System.Windows.Forms.Padding(2);
	        this.grpGenLoc.Size = new System.Drawing.Size(144, 98);
	        this.grpGenLoc.TabIndex = 3;
	        this.grpGenLoc.TabStop = false;
	        this.grpGenLoc.Text = "Location";
	        // 
	        // tlpGenLoc
	        // 
	        this.tlpGenLoc.AutoSize = true;
	        this.tlpGenLoc.ColumnCount = 3;
	        this.tlpGenLoc.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
	        this.tlpGenLoc.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 35F));
	        this.tlpGenLoc.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 15F));
	        this.tlpGenLoc.Controls.Add(this.radGenLocUser, 0, 0);
	        this.tlpGenLoc.Controls.Add(this.radGenLocDesk, 1, 0);
	        this.tlpGenLoc.Controls.Add(this.radGenLocPDM, 0, 2);
	        this.tlpGenLoc.Controls.Add(this.txtGenLocPath, 0, 1);
	        this.tlpGenLoc.Controls.Add(this.btnGenLocPath, 2, 1);
	        this.tlpGenLoc.Controls.Add(this.btnGenLocExe, 1, 2);
	        this.tlpGenLoc.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.tlpGenLoc.Location = new System.Drawing.Point(2, 15);
	        this.tlpGenLoc.Margin = new System.Windows.Forms.Padding(2);
	        this.tlpGenLoc.Name = "tlpGenLoc";
	        this.tlpGenLoc.RowCount = 4;
	        this.tlpGenLoc.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 30F));
	        this.tlpGenLoc.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 30F));
	        this.tlpGenLoc.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 30F));
	        this.tlpGenLoc.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 10F));
	        this.tlpGenLoc.Size = new System.Drawing.Size(140, 81);
	        this.tlpGenLoc.TabIndex = 0;
	        // 
	        // radGenLocUser
	        // 
	        this.radGenLocUser.AutoSize = true;
	        this.radGenLocUser.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.radGenLocUser.Location = new System.Drawing.Point(2, 2);
	        this.radGenLocUser.Margin = new System.Windows.Forms.Padding(2);
	        this.radGenLocUser.Name = "radGenLocUser";
	        this.radGenLocUser.Size = new System.Drawing.Size(66, 20);
	        this.radGenLocUser.TabIndex = 0;
	        this.radGenLocUser.TabStop = true;
	        this.radGenLocUser.Text = "User";
	        this.tipTool.SetToolTip(this.radGenLocUser, "Save to user specified directory");
	        this.radGenLocUser.UseVisualStyleBackColor = true;
	        this.radGenLocUser.CheckedChanged += new System.EventHandler(this.radGenLocUser_CheckedChanged);
	        // 
	        // radGenLocDesk
	        // 
	        this.radGenLocDesk.AutoSize = true;
	        this.radGenLocDesk.Checked = true;
	        this.tlpGenLoc.SetColumnSpan(this.radGenLocDesk, 2);
	        this.radGenLocDesk.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.radGenLocDesk.Location = new System.Drawing.Point(72, 2);
	        this.radGenLocDesk.Margin = new System.Windows.Forms.Padding(2);
	        this.radGenLocDesk.Name = "radGenLocDesk";
	        this.radGenLocDesk.Size = new System.Drawing.Size(66, 20);
	        this.radGenLocDesk.TabIndex = 1;
	        this.radGenLocDesk.TabStop = true;
	        this.radGenLocDesk.Text = "Desktop";
	        this.tipTool.SetToolTip(this.radGenLocDesk, "Save to Desktop");
	        this.radGenLocDesk.UseVisualStyleBackColor = true;
	        // 
	        // radGenLocPDM
	        // 
	        this.radGenLocPDM.AutoSize = true;
	        this.radGenLocPDM.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.radGenLocPDM.Location = new System.Drawing.Point(2, 50);
	        this.radGenLocPDM.Margin = new System.Windows.Forms.Padding(2);
	        this.radGenLocPDM.Name = "radGenLocPDM";
	        this.radGenLocPDM.Size = new System.Drawing.Size(66, 20);
	        this.radGenLocPDM.TabIndex = 4;
	        this.radGenLocPDM.TabStop = true;
	        this.radGenLocPDM.Text = "PDM";
	        this.tipTool.SetToolTip(this.radGenLocPDM, "Save to appropriate folders in PDM");
	        this.radGenLocPDM.UseVisualStyleBackColor = true;
	        // 
	        // txtGenLocPath
	        // 
	        this.txtGenLocPath.BackColor = System.Drawing.SystemColors.ControlLight;
	        this.tlpGenLoc.SetColumnSpan(this.txtGenLocPath, 2);
	        this.txtGenLocPath.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.txtGenLocPath.Location = new System.Drawing.Point(2, 26);
	        this.txtGenLocPath.Margin = new System.Windows.Forms.Padding(2);
	        this.txtGenLocPath.Name = "txtGenLocPath";
	        this.txtGenLocPath.Size = new System.Drawing.Size(115, 20);
	        this.txtGenLocPath.TabIndex = 2;
	        this.tipTool.SetToolTip(this.txtGenLocPath, "Custom directory");
	        // 
	        // cplMainLky
	        // 
	        this.cplMainLky.AutoScroll = true;
	        // 
	        // cplMainLky.Content
	        // 
	        this.cplMainLky.Content.AutoSize = true;
	        this.cplMainLky.Content.Controls.Add(this.tlpMainLky);
	        this.cplMainLky.Content.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.cplMainLky.Content.Location = new System.Drawing.Point(5, 24);
	        this.cplMainLky.Content.Margin = new System.Windows.Forms.Padding(4);
	        this.cplMainLky.Content.Name = "Content";
	        this.cplMainLky.Content.Size = new System.Drawing.Size(211, 229);
	        this.cplMainLky.Content.TabIndex = 1;
	        this.cplMainLky.Dock = System.Windows.Forms.DockStyle.Top;
	        this.cplMainLky.Location = new System.Drawing.Point(0, 590);
	        this.cplMainLky.Margin = new System.Windows.Forms.Padding(4);
	        this.cplMainLky.MaximumSize = new System.Drawing.Size(750, 812);
	        this.cplMainLky.MinimumSize = new System.Drawing.Size(90, 18);
	        this.cplMainLky.Name = "cplMainLky";
	        this.cplMainLky.Size = new System.Drawing.Size(223, 260);
	        this.cplMainLky.TabIndex = 3;
	        this.cplMainLky.Title = "Feelin\' Lucky";
	        // 
	        // tlpMainLky
	        // 
	        this.tlpMainLky.AutoSize = true;
	        this.tlpMainLky.ColumnCount = 2;
	        this.tlpMainLky.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 90F));
	        this.tlpMainLky.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 10F));
	        this.tlpMainLky.Controls.Add(this.cmbLkySrch, 0, 0);
	        this.tlpMainLky.Controls.Add(this.btnLkySrch, 1, 0);
	        this.tlpMainLky.Controls.Add(this.lstLkyResults, 0, 1);
	        this.tlpMainLky.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.tlpMainLky.Location = new System.Drawing.Point(0, 0);
	        this.tlpMainLky.Name = "tlpMainLky";
	        this.tlpMainLky.RowCount = 2;
	        this.tlpMainLky.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 10F));
	        this.tlpMainLky.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 90F));
	        this.tlpMainLky.Size = new System.Drawing.Size(211, 229);
	        this.tlpMainLky.TabIndex = 0;
	        // 
	        // lstLkyResults
	        // 
	        this.lstLkyResults.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] { this.colLkyResults1, this.colLkyResults2, this.colLkyResults3 });
	        this.tlpMainLky.SetColumnSpan(this.lstLkyResults, 2);
	        this.lstLkyResults.ContextMenuStrip = this.cmsLucky;
	        this.lstLkyResults.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lstLkyResults.FullRowSelect = true;
	        this.lstLkyResults.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
	        this.lstLkyResults.HideSelection = false;
	        this.lstLkyResults.LargeImageList = this.imlThumbs;
	        this.lstLkyResults.Location = new System.Drawing.Point(2, 24);
	        this.lstLkyResults.Margin = new System.Windows.Forms.Padding(2);
	        this.lstLkyResults.MultiSelect = false;
	        this.lstLkyResults.Name = "lstLkyResults";
	        this.lstLkyResults.Size = new System.Drawing.Size(207, 203);
	        this.lstLkyResults.SmallImageList = this.imlThumbs;
	        this.lstLkyResults.TabIndex = 2;
	        this.lstLkyResults.UseCompatibleStateImageBehavior = false;
	        this.lstLkyResults.View = System.Windows.Forms.View.Details;
	        this.lstLkyResults.ItemDrag += new System.Windows.Forms.ItemDragEventHandler(this.lstLkyResults_ItemDrag);
	        this.lstLkyResults.DoubleClick += new System.EventHandler(this.lstLkyResults_DoubleClick);
	        this.lstLkyResults.KeyUp += new System.Windows.Forms.KeyEventHandler(this.lstLkyResults_KeyUp);
	        // 
	        // imlThumbs
	        // 
	        this.imlThumbs.ColorDepth = System.Windows.Forms.ColorDepth.Depth32Bit;
	        this.imlThumbs.ImageSize = new System.Drawing.Size(64, 64);
	        this.imlThumbs.TransparentColor = System.Drawing.Color.Transparent;
	        // 
	        // cplMainMisc
	        // 
	        this.cplMainMisc.AutoScroll = true;
	        // 
	        // cplMainMisc.Content
	        // 
	        this.cplMainMisc.Content.AutoSize = true;
	        this.cplMainMisc.Content.Controls.Add(this.tlpMainMisc);
	        this.cplMainMisc.Content.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.cplMainMisc.Content.Location = new System.Drawing.Point(5, 24);
	        this.cplMainMisc.Content.Margin = new System.Windows.Forms.Padding(4);
	        this.cplMainMisc.Content.Name = "Content";
	        this.cplMainMisc.Content.Size = new System.Drawing.Size(211, 252);
	        this.cplMainMisc.Content.TabIndex = 1;
	        this.cplMainMisc.Dock = System.Windows.Forms.DockStyle.Top;
	        this.cplMainMisc.Location = new System.Drawing.Point(0, 2084);
	        this.cplMainMisc.Margin = new System.Windows.Forms.Padding(4);
	        this.cplMainMisc.MaximumSize = new System.Drawing.Size(750, 812);
	        this.cplMainMisc.MinimumSize = new System.Drawing.Size(90, 18);
	        this.cplMainMisc.Name = "cplMainMisc";
	        this.cplMainMisc.Size = new System.Drawing.Size(223, 283);
	        this.cplMainMisc.TabIndex = 6;
	        this.cplMainMisc.Title = "Misc. Utilities";
	        // 
	        // tlpMainMisc
	        // 
	        this.tlpMainMisc.AutoSize = true;
	        this.tlpMainMisc.ColumnCount = 1;
	        this.tlpMainMisc.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
	        this.tlpMainMisc.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
	        this.tlpMainMisc.Controls.Add(this.tabMainConv, 0, 0);
	        this.tlpMainMisc.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.tlpMainMisc.Location = new System.Drawing.Point(0, 0);
	        this.tlpMainMisc.Name = "tlpMainMisc";
	        this.tlpMainMisc.RowCount = 1;
	        this.tlpMainMisc.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
	        this.tlpMainMisc.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 306F));
	        this.tlpMainMisc.Size = new System.Drawing.Size(211, 252);
	        this.tlpMainMisc.TabIndex = 1;
	        // 
	        // tabMainConv
	        // 
	        this.tabMainConv.Controls.Add(this.tpgConvUnit);
	        this.tabMainConv.Controls.Add(this.tpgConvElec);
	        this.tabMainConv.Controls.Add(this.tpgConvRoll);
	        this.tabMainConv.Controls.Add(this.tpgConvBFoot);
	        this.tabMainConv.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.tabMainConv.Location = new System.Drawing.Point(2, 2);
	        this.tabMainConv.Margin = new System.Windows.Forms.Padding(2);
	        this.tabMainConv.Name = "tabMainConv";
	        this.tabMainConv.SelectedIndex = 0;
	        this.tabMainConv.Size = new System.Drawing.Size(207, 248);
	        this.tabMainConv.TabIndex = 0;
	        // 
	        // tpgConvUnit
	        // 
	        this.tpgConvUnit.Controls.Add(this.tlpConvUnit);
	        this.tpgConvUnit.Location = new System.Drawing.Point(4, 22);
	        this.tpgConvUnit.Margin = new System.Windows.Forms.Padding(2);
	        this.tpgConvUnit.Name = "tpgConvUnit";
	        this.tpgConvUnit.Padding = new System.Windows.Forms.Padding(2);
	        this.tpgConvUnit.Size = new System.Drawing.Size(199, 222);
	        this.tpgConvUnit.TabIndex = 0;
	        this.tpgConvUnit.Text = "Units";
	        this.tpgConvUnit.UseVisualStyleBackColor = true;
	        // 
	        // tlpConvUnit
	        // 
	        this.tlpConvUnit.AutoSize = true;
	        this.tlpConvUnit.ColumnCount = 4;
	        this.tlpConvUnit.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 18F));
	        this.tlpConvUnit.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 46F));
	        this.tlpConvUnit.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 16F));
	        this.tlpConvUnit.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20F));
	        this.tlpConvUnit.Controls.Add(this.lblConvUnits, 0, 0);
	        this.tlpConvUnit.Controls.Add(this.lblConvUnitFDesc, 0, 3);
	        this.tlpConvUnit.Controls.Add(this.lblConvUnitFSym, 2, 2);
	        this.tlpConvUnit.Controls.Add(this.txtConvUnitFVal, 0, 2);
	        this.tlpConvUnit.Controls.Add(this.cmbConvUnitTypes, 1, 0);
	        this.tlpConvUnit.Controls.Add(this.cmbConvUnitFrom, 0, 1);
	        this.tlpConvUnit.Controls.Add(this.btnConvUnitCalc, 3, 1);
	        this.tlpConvUnit.Controls.Add(this.btnConvUnitSwap, 3, 4);
	        this.tlpConvUnit.Controls.Add(this.cmbConvUnitTo, 0, 4);
	        this.tlpConvUnit.Controls.Add(this.txtConvUnitTVal, 0, 5);
	        this.tlpConvUnit.Controls.Add(this.lblConvUnitTSym, 2, 5);
	        this.tlpConvUnit.Controls.Add(this.lblConvUnitTDesc, 0, 6);
	        this.tlpConvUnit.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.tlpConvUnit.Location = new System.Drawing.Point(2, 2);
	        this.tlpConvUnit.Margin = new System.Windows.Forms.Padding(2);
	        this.tlpConvUnit.Name = "tlpConvUnit";
	        this.tlpConvUnit.RowCount = 7;
	        this.tlpConvUnit.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 12F));
	        this.tlpConvUnit.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 12F));
	        this.tlpConvUnit.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 12F));
	        this.tlpConvUnit.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 20F));
	        this.tlpConvUnit.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 12F));
	        this.tlpConvUnit.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 12F));
	        this.tlpConvUnit.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 20F));
	        this.tlpConvUnit.Size = new System.Drawing.Size(195, 218);
	        this.tlpConvUnit.TabIndex = 0;
	        // 
	        // lblConvUnits
	        // 
	        this.lblConvUnits.AutoSize = true;
	        this.lblConvUnits.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lblConvUnits.Location = new System.Drawing.Point(2, 0);
	        this.lblConvUnits.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
	        this.lblConvUnits.Name = "lblConvUnits";
	        this.lblConvUnits.Size = new System.Drawing.Size(31, 26);
	        this.lblConvUnits.TabIndex = 0;
	        this.lblConvUnits.Text = "Units";
	        // 
	        // lblConvUnitFDesc
	        // 
	        this.tlpConvUnit.SetColumnSpan(this.lblConvUnitFDesc, 4);
	        this.lblConvUnitFDesc.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lblConvUnitFDesc.Location = new System.Drawing.Point(2, 78);
	        this.lblConvUnitFDesc.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
	        this.lblConvUnitFDesc.Name = "lblConvUnitFDesc";
	        this.lblConvUnitFDesc.Size = new System.Drawing.Size(191, 43);
	        this.lblConvUnitFDesc.TabIndex = 6;
	        this.lblConvUnitFDesc.Text = "Unit A";
	        // 
	        // lblConvUnitFSym
	        // 
	        this.lblConvUnitFSym.AutoSize = true;
	        this.lblConvUnitFSym.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lblConvUnitFSym.Location = new System.Drawing.Point(126, 52);
	        this.lblConvUnitFSym.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
	        this.lblConvUnitFSym.Name = "lblConvUnitFSym";
	        this.lblConvUnitFSym.Size = new System.Drawing.Size(27, 26);
	        this.lblConvUnitFSym.TabIndex = 5;
	        this.lblConvUnitFSym.Text = "A";
	        // 
	        // txtConvUnitFVal
	        // 
	        this.tlpConvUnit.SetColumnSpan(this.txtConvUnitFVal, 2);
	        this.txtConvUnitFVal.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.txtConvUnitFVal.Location = new System.Drawing.Point(2, 54);
	        this.txtConvUnitFVal.Margin = new System.Windows.Forms.Padding(2);
	        this.txtConvUnitFVal.Name = "txtConvUnitFVal";
	        this.txtConvUnitFVal.Size = new System.Drawing.Size(120, 20);
	        this.txtConvUnitFVal.TabIndex = 4;
	        this.txtConvUnitFVal.TextChanged += new System.EventHandler(this.txtConvUnitFVal_TextChanged);
	        // 
	        // txtConvUnitTVal
	        // 
	        this.tlpConvUnit.SetColumnSpan(this.txtConvUnitTVal, 2);
	        this.txtConvUnitTVal.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.txtConvUnitTVal.Location = new System.Drawing.Point(2, 149);
	        this.txtConvUnitTVal.Margin = new System.Windows.Forms.Padding(2);
	        this.txtConvUnitTVal.Name = "txtConvUnitTVal";
	        this.txtConvUnitTVal.ReadOnly = true;
	        this.txtConvUnitTVal.Size = new System.Drawing.Size(120, 20);
	        this.txtConvUnitTVal.TabIndex = 9;
	        // 
	        // lblConvUnitTSym
	        // 
	        this.lblConvUnitTSym.AutoSize = true;
	        this.lblConvUnitTSym.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lblConvUnitTSym.Location = new System.Drawing.Point(126, 147);
	        this.lblConvUnitTSym.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
	        this.lblConvUnitTSym.Name = "lblConvUnitTSym";
	        this.lblConvUnitTSym.Size = new System.Drawing.Size(27, 26);
	        this.lblConvUnitTSym.TabIndex = 10;
	        this.lblConvUnitTSym.Text = "B";
	        // 
	        // lblConvUnitTDesc
	        // 
	        this.lblConvUnitTDesc.AutoSize = true;
	        this.tlpConvUnit.SetColumnSpan(this.lblConvUnitTDesc, 4);
	        this.lblConvUnitTDesc.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lblConvUnitTDesc.Location = new System.Drawing.Point(2, 173);
	        this.lblConvUnitTDesc.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
	        this.lblConvUnitTDesc.Name = "lblConvUnitTDesc";
	        this.lblConvUnitTDesc.Size = new System.Drawing.Size(191, 45);
	        this.lblConvUnitTDesc.TabIndex = 11;
	        this.lblConvUnitTDesc.Text = "Unit B";
	        // 
	        // tpgConvElec
	        // 
	        this.tpgConvElec.Controls.Add(this.tlpConvEle);
	        this.tpgConvElec.Location = new System.Drawing.Point(4, 22);
	        this.tpgConvElec.Margin = new System.Windows.Forms.Padding(2);
	        this.tpgConvElec.Name = "tpgConvElec";
	        this.tpgConvElec.Padding = new System.Windows.Forms.Padding(2);
	        this.tpgConvElec.Size = new System.Drawing.Size(199, 222);
	        this.tpgConvElec.TabIndex = 1;
	        this.tpgConvElec.Text = "Electricity";
	        this.tpgConvElec.UseVisualStyleBackColor = true;
	        // 
	        // tlpConvEle
	        // 
	        this.tlpConvEle.AutoSize = true;
	        this.tlpConvEle.ColumnCount = 3;
	        this.tlpConvEle.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
	        this.tlpConvEle.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 35F));
	        this.tlpConvEle.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 40F));
	        this.tlpConvEle.Controls.Add(this.lblConvEleWatt, 0, 0);
	        this.tlpConvEle.Controls.Add(this.lblConvEleVolt, 0, 1);
	        this.tlpConvEle.Controls.Add(this.lblConvEleAmp, 0, 2);
	        this.tlpConvEle.Controls.Add(this.lblConvEleOhm, 0, 3);
	        this.tlpConvEle.Controls.Add(this.lblConvEleW80, 0, 5);
	        this.tlpConvEle.Controls.Add(this.lblConvEleW50, 0, 6);
	        this.tlpConvEle.Controls.Add(this.btnConvEleClr, 1, 7);
	        this.tlpConvEle.Controls.Add(this.lblConvEleMemo, 2, 0);
	        this.tlpConvEle.Controls.Add(this.txtConvEleWatt, 1, 0);
	        this.tlpConvEle.Controls.Add(this.txtConvEleVolt, 1, 1);
	        this.tlpConvEle.Controls.Add(this.txtConvEleAmp, 1, 2);
	        this.tlpConvEle.Controls.Add(this.txtConvEleOhm, 1, 3);
	        this.tlpConvEle.Controls.Add(this.txtConvEleW80, 1, 5);
	        this.tlpConvEle.Controls.Add(this.txtConvEleW50, 1, 6);
	        this.tlpConvEle.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.tlpConvEle.Location = new System.Drawing.Point(2, 2);
	        this.tlpConvEle.Margin = new System.Windows.Forms.Padding(2);
	        this.tlpConvEle.Name = "tlpConvEle";
	        this.tlpConvEle.RowCount = 9;
	        this.tlpConvEle.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 12F));
	        this.tlpConvEle.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 12F));
	        this.tlpConvEle.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 12F));
	        this.tlpConvEle.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 12F));
	        this.tlpConvEle.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 3F));
	        this.tlpConvEle.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 12F));
	        this.tlpConvEle.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 12F));
	        this.tlpConvEle.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 12F));
	        this.tlpConvEle.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 13F));
	        this.tlpConvEle.Size = new System.Drawing.Size(195, 218);
	        this.tlpConvEle.TabIndex = 0;
	        // 
	        // lblConvEleWatt
	        // 
	        this.lblConvEleWatt.AutoSize = true;
	        this.lblConvEleWatt.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lblConvEleWatt.Location = new System.Drawing.Point(2, 0);
	        this.lblConvEleWatt.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
	        this.lblConvEleWatt.Name = "lblConvEleWatt";
	        this.lblConvEleWatt.Size = new System.Drawing.Size(44, 26);
	        this.lblConvEleWatt.TabIndex = 0;
	        this.lblConvEleWatt.Text = "Watts";
	        // 
	        // lblConvEleVolt
	        // 
	        this.lblConvEleVolt.AutoSize = true;
	        this.lblConvEleVolt.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lblConvEleVolt.Location = new System.Drawing.Point(2, 26);
	        this.lblConvEleVolt.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
	        this.lblConvEleVolt.Name = "lblConvEleVolt";
	        this.lblConvEleVolt.Size = new System.Drawing.Size(44, 26);
	        this.lblConvEleVolt.TabIndex = 2;
	        this.lblConvEleVolt.Text = "Volts";
	        // 
	        // lblConvEleAmp
	        // 
	        this.lblConvEleAmp.AutoSize = true;
	        this.lblConvEleAmp.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lblConvEleAmp.Location = new System.Drawing.Point(2, 52);
	        this.lblConvEleAmp.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
	        this.lblConvEleAmp.Name = "lblConvEleAmp";
	        this.lblConvEleAmp.Size = new System.Drawing.Size(44, 26);
	        this.lblConvEleAmp.TabIndex = 4;
	        this.lblConvEleAmp.Text = "Amps";
	        // 
	        // lblConvEleOhm
	        // 
	        this.lblConvEleOhm.AutoSize = true;
	        this.lblConvEleOhm.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lblConvEleOhm.Location = new System.Drawing.Point(2, 78);
	        this.lblConvEleOhm.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
	        this.lblConvEleOhm.Name = "lblConvEleOhm";
	        this.lblConvEleOhm.Size = new System.Drawing.Size(44, 26);
	        this.lblConvEleOhm.TabIndex = 6;
	        this.lblConvEleOhm.Text = "Ohms";
	        // 
	        // lblConvEleW80
	        // 
	        this.lblConvEleW80.AutoSize = true;
	        this.lblConvEleW80.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lblConvEleW80.Location = new System.Drawing.Point(2, 110);
	        this.lblConvEleW80.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
	        this.lblConvEleW80.Name = "lblConvEleW80";
	        this.lblConvEleW80.Size = new System.Drawing.Size(44, 26);
	        this.lblConvEleW80.TabIndex = 8;
	        this.lblConvEleW80.Text = "80% W";
	        // 
	        // lblConvEleW50
	        // 
	        this.lblConvEleW50.AutoSize = true;
	        this.lblConvEleW50.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lblConvEleW50.Location = new System.Drawing.Point(2, 136);
	        this.lblConvEleW50.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
	        this.lblConvEleW50.Name = "lblConvEleW50";
	        this.lblConvEleW50.Size = new System.Drawing.Size(44, 26);
	        this.lblConvEleW50.TabIndex = 10;
	        this.lblConvEleW50.Text = "50% W";
	        // 
	        // lblConvEleMemo
	        // 
	        this.lblConvEleMemo.AutoSize = true;
	        this.lblConvEleMemo.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lblConvEleMemo.Location = new System.Drawing.Point(118, 0);
	        this.lblConvEleMemo.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
	        this.lblConvEleMemo.Name = "lblConvEleMemo";
	        this.tlpConvEle.SetRowSpan(this.lblConvEleMemo, 2);
	        this.lblConvEleMemo.Size = new System.Drawing.Size(75, 52);
	        this.lblConvEleMemo.TabIndex = 13;
	        this.lblConvEleMemo.Text = "Enter values for any two items.";
	        // 
	        // txtConvEleWatt
	        // 
	        this.txtConvEleWatt.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.txtConvEleWatt.Location = new System.Drawing.Point(50, 2);
	        this.txtConvEleWatt.Margin = new System.Windows.Forms.Padding(2);
	        this.txtConvEleWatt.Name = "txtConvEleWatt";
	        this.txtConvEleWatt.Size = new System.Drawing.Size(64, 20);
	        this.txtConvEleWatt.TabIndex = 1;
	        this.txtConvEleWatt.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.txtConvEleWatt_KeyPress);
	        this.txtConvEleWatt.Leave += new System.EventHandler(this.txtConvEleWatt_Leave);
	        // 
	        // txtConvEleVolt
	        // 
	        this.txtConvEleVolt.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.txtConvEleVolt.Location = new System.Drawing.Point(50, 28);
	        this.txtConvEleVolt.Margin = new System.Windows.Forms.Padding(2);
	        this.txtConvEleVolt.Name = "txtConvEleVolt";
	        this.txtConvEleVolt.Size = new System.Drawing.Size(64, 20);
	        this.txtConvEleVolt.TabIndex = 3;
	        this.txtConvEleVolt.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.txtConvEleVolt_KeyPress);
	        this.txtConvEleVolt.Leave += new System.EventHandler(this.txtConvEleVolt_Leave);
	        // 
	        // txtConvEleAmp
	        // 
	        this.txtConvEleAmp.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.txtConvEleAmp.Location = new System.Drawing.Point(50, 54);
	        this.txtConvEleAmp.Margin = new System.Windows.Forms.Padding(2);
	        this.txtConvEleAmp.Name = "txtConvEleAmp";
	        this.txtConvEleAmp.Size = new System.Drawing.Size(64, 20);
	        this.txtConvEleAmp.TabIndex = 5;
	        this.txtConvEleAmp.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.txtConvEleAmp_KeyPress);
	        this.txtConvEleAmp.Leave += new System.EventHandler(this.txtConvEleAmp_Leave);
	        // 
	        // txtConvEleOhm
	        // 
	        this.txtConvEleOhm.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.txtConvEleOhm.Location = new System.Drawing.Point(50, 80);
	        this.txtConvEleOhm.Margin = new System.Windows.Forms.Padding(2);
	        this.txtConvEleOhm.Name = "txtConvEleOhm";
	        this.txtConvEleOhm.Size = new System.Drawing.Size(64, 20);
	        this.txtConvEleOhm.TabIndex = 7;
	        this.txtConvEleOhm.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.txtConvEleOhm_KeyPress);
	        this.txtConvEleOhm.Leave += new System.EventHandler(this.txtConvEleOhm_Leave);
	        // 
	        // txtConvEleW80
	        // 
	        this.txtConvEleW80.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.txtConvEleW80.Location = new System.Drawing.Point(50, 112);
	        this.txtConvEleW80.Margin = new System.Windows.Forms.Padding(2);
	        this.txtConvEleW80.Name = "txtConvEleW80";
	        this.txtConvEleW80.ReadOnly = true;
	        this.txtConvEleW80.Size = new System.Drawing.Size(64, 20);
	        this.txtConvEleW80.TabIndex = 9;
	        // 
	        // txtConvEleW50
	        // 
	        this.txtConvEleW50.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.txtConvEleW50.Location = new System.Drawing.Point(50, 138);
	        this.txtConvEleW50.Margin = new System.Windows.Forms.Padding(2);
	        this.txtConvEleW50.Name = "txtConvEleW50";
	        this.txtConvEleW50.ReadOnly = true;
	        this.txtConvEleW50.Size = new System.Drawing.Size(64, 20);
	        this.txtConvEleW50.TabIndex = 11;
	        // 
	        // tpgConvRoll
	        // 
	        this.tpgConvRoll.Controls.Add(this.tlpConvRl);
	        this.tpgConvRoll.Location = new System.Drawing.Point(4, 22);
	        this.tpgConvRoll.Margin = new System.Windows.Forms.Padding(2);
	        this.tpgConvRoll.Name = "tpgConvRoll";
	        this.tpgConvRoll.Padding = new System.Windows.Forms.Padding(2);
	        this.tpgConvRoll.Size = new System.Drawing.Size(199, 222);
	        this.tpgConvRoll.TabIndex = 2;
	        this.tpgConvRoll.Text = "Roll";
	        this.tpgConvRoll.UseVisualStyleBackColor = true;
	        // 
	        // tlpConvRl
	        // 
	        this.tlpConvRl.AutoSize = true;
	        this.tlpConvRl.ColumnCount = 4;
	        this.tlpConvRl.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 27F));
	        this.tlpConvRl.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 27.75F));
	        this.tlpConvRl.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 24.25F));
	        this.tlpConvRl.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 21F));
	        this.tlpConvRl.Controls.Add(this.lblConvRlUnitFrom, 0, 0);
	        this.tlpConvRl.Controls.Add(this.lblConvRlMat, 0, 1);
	        this.tlpConvRl.Controls.Add(this.lblConvRlOD, 0, 2);
	        this.tlpConvRl.Controls.Add(this.lblConvRlID, 0, 3);
	        this.tlpConvRl.Controls.Add(this.lblConvRlCore, 0, 4);
	        this.tlpConvRl.Controls.Add(this.lblConvRlTotal, 0, 6);
	        this.tlpConvRl.Controls.Add(this.txtConvRlMat, 1, 1);
	        this.tlpConvRl.Controls.Add(this.txtConvRlOD, 1, 2);
	        this.tlpConvRl.Controls.Add(this.txtConvRlID, 1, 3);
	        this.tlpConvRl.Controls.Add(this.txtConvRlCore, 1, 4);
	        this.tlpConvRl.Controls.Add(this.txtConvRlTotal, 1, 6);
	        this.tlpConvRl.Controls.Add(this.btnConvRlCalc, 1, 5);
	        this.tlpConvRl.Controls.Add(this.btnConvRlReset, 3, 0);
	        this.tlpConvRl.Controls.Add(this.cmbConvRlUnitFrom, 1, 0);
	        this.tlpConvRl.Controls.Add(this.cmbConvRlUnitTo, 2, 6);
	        this.tlpConvRl.Controls.Add(this.picConvRlRoll, 2, 1);
	        this.tlpConvRl.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.tlpConvRl.Location = new System.Drawing.Point(2, 2);
	        this.tlpConvRl.Margin = new System.Windows.Forms.Padding(2);
	        this.tlpConvRl.Name = "tlpConvRl";
	        this.tlpConvRl.RowCount = 8;
	        this.tlpConvRl.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 11F));
	        this.tlpConvRl.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 11F));
	        this.tlpConvRl.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 11F));
	        this.tlpConvRl.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 11F));
	        this.tlpConvRl.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 11F));
	        this.tlpConvRl.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 11F));
	        this.tlpConvRl.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 11F));
	        this.tlpConvRl.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 23F));
	        this.tlpConvRl.Size = new System.Drawing.Size(195, 218);
	        this.tlpConvRl.TabIndex = 0;
	        // 
	        // lblConvRlUnitFrom
	        // 
	        this.lblConvRlUnitFrom.AutoSize = true;
	        this.lblConvRlUnitFrom.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lblConvRlUnitFrom.Location = new System.Drawing.Point(2, 0);
	        this.lblConvRlUnitFrom.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
	        this.lblConvRlUnitFrom.Name = "lblConvRlUnitFrom";
	        this.lblConvRlUnitFrom.Size = new System.Drawing.Size(48, 23);
	        this.lblConvRlUnitFrom.TabIndex = 0;
	        this.lblConvRlUnitFrom.Text = "Units";
	        // 
	        // lblConvRlMat
	        // 
	        this.lblConvRlMat.AutoSize = true;
	        this.lblConvRlMat.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lblConvRlMat.Location = new System.Drawing.Point(2, 23);
	        this.lblConvRlMat.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
	        this.lblConvRlMat.Name = "lblConvRlMat";
	        this.lblConvRlMat.Size = new System.Drawing.Size(48, 23);
	        this.lblConvRlMat.TabIndex = 3;
	        this.lblConvRlMat.Text = "Mat T (A)";
	        // 
	        // lblConvRlOD
	        // 
	        this.lblConvRlOD.AutoSize = true;
	        this.lblConvRlOD.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lblConvRlOD.Location = new System.Drawing.Point(2, 46);
	        this.lblConvRlOD.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
	        this.lblConvRlOD.Name = "lblConvRlOD";
	        this.lblConvRlOD.Size = new System.Drawing.Size(48, 23);
	        this.lblConvRlOD.TabIndex = 5;
	        this.lblConvRlOD.Text = "OD (B)";
	        // 
	        // lblConvRlID
	        // 
	        this.lblConvRlID.AutoSize = true;
	        this.lblConvRlID.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lblConvRlID.Location = new System.Drawing.Point(2, 69);
	        this.lblConvRlID.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
	        this.lblConvRlID.Name = "lblConvRlID";
	        this.lblConvRlID.Size = new System.Drawing.Size(48, 23);
	        this.lblConvRlID.TabIndex = 7;
	        this.lblConvRlID.Text = "ID (C)";
	        // 
	        // lblConvRlCore
	        // 
	        this.lblConvRlCore.AutoSize = true;
	        this.lblConvRlCore.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lblConvRlCore.Location = new System.Drawing.Point(2, 92);
	        this.lblConvRlCore.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
	        this.lblConvRlCore.Name = "lblConvRlCore";
	        this.lblConvRlCore.Size = new System.Drawing.Size(48, 23);
	        this.lblConvRlCore.TabIndex = 9;
	        this.lblConvRlCore.Text = "Core T (D)";
	        // 
	        // lblConvRlTotal
	        // 
	        this.lblConvRlTotal.AutoSize = true;
	        this.lblConvRlTotal.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lblConvRlTotal.Location = new System.Drawing.Point(2, 138);
	        this.lblConvRlTotal.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
	        this.lblConvRlTotal.Name = "lblConvRlTotal";
	        this.lblConvRlTotal.Size = new System.Drawing.Size(48, 23);
	        this.lblConvRlTotal.TabIndex = 12;
	        this.lblConvRlTotal.Text = "Total";
	        // 
	        // txtConvRlMat
	        // 
	        this.txtConvRlMat.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.txtConvRlMat.Location = new System.Drawing.Point(54, 25);
	        this.txtConvRlMat.Margin = new System.Windows.Forms.Padding(2);
	        this.txtConvRlMat.Name = "txtConvRlMat";
	        this.txtConvRlMat.Size = new System.Drawing.Size(50, 20);
	        this.txtConvRlMat.TabIndex = 4;
	        // 
	        // txtConvRlOD
	        // 
	        this.txtConvRlOD.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.txtConvRlOD.Location = new System.Drawing.Point(54, 48);
	        this.txtConvRlOD.Margin = new System.Windows.Forms.Padding(2);
	        this.txtConvRlOD.Name = "txtConvRlOD";
	        this.txtConvRlOD.Size = new System.Drawing.Size(50, 20);
	        this.txtConvRlOD.TabIndex = 6;
	        // 
	        // txtConvRlID
	        // 
	        this.txtConvRlID.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.txtConvRlID.Location = new System.Drawing.Point(54, 71);
	        this.txtConvRlID.Margin = new System.Windows.Forms.Padding(2);
	        this.txtConvRlID.Name = "txtConvRlID";
	        this.txtConvRlID.Size = new System.Drawing.Size(50, 20);
	        this.txtConvRlID.TabIndex = 8;
	        // 
	        // txtConvRlCore
	        // 
	        this.txtConvRlCore.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.txtConvRlCore.Location = new System.Drawing.Point(54, 94);
	        this.txtConvRlCore.Margin = new System.Windows.Forms.Padding(2);
	        this.txtConvRlCore.Name = "txtConvRlCore";
	        this.txtConvRlCore.Size = new System.Drawing.Size(50, 20);
	        this.txtConvRlCore.TabIndex = 10;
	        // 
	        // txtConvRlTotal
	        // 
	        this.txtConvRlTotal.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.txtConvRlTotal.Location = new System.Drawing.Point(54, 140);
	        this.txtConvRlTotal.Margin = new System.Windows.Forms.Padding(2);
	        this.txtConvRlTotal.Name = "txtConvRlTotal";
	        this.txtConvRlTotal.ReadOnly = true;
	        this.txtConvRlTotal.Size = new System.Drawing.Size(50, 20);
	        this.txtConvRlTotal.TabIndex = 13;
	        // 
	        // picConvRlRoll
	        // 
	        this.tlpConvRl.SetColumnSpan(this.picConvRlRoll, 2);
	        this.picConvRlRoll.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.picConvRlRoll.Image = global::BlueBrick.Resource1.roll;
	        this.picConvRlRoll.Location = new System.Drawing.Point(108, 25);
	        this.picConvRlRoll.Margin = new System.Windows.Forms.Padding(2);
	        this.picConvRlRoll.Name = "picConvRlRoll";
	        this.tlpConvRl.SetRowSpan(this.picConvRlRoll, 5);
	        this.picConvRlRoll.Size = new System.Drawing.Size(85, 111);
	        this.picConvRlRoll.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
	        this.picConvRlRoll.TabIndex = 15;
	        this.picConvRlRoll.TabStop = false;
	        // 
	        // tpgConvBFoot
	        // 
	        this.tpgConvBFoot.Controls.Add(this.tlpConvBf);
	        this.tpgConvBFoot.Location = new System.Drawing.Point(4, 22);
	        this.tpgConvBFoot.Margin = new System.Windows.Forms.Padding(2);
	        this.tpgConvBFoot.Name = "tpgConvBFoot";
	        this.tpgConvBFoot.Padding = new System.Windows.Forms.Padding(2);
	        this.tpgConvBFoot.Size = new System.Drawing.Size(199, 222);
	        this.tpgConvBFoot.TabIndex = 3;
	        this.tpgConvBFoot.Text = "Board Ft";
	        this.tpgConvBFoot.UseVisualStyleBackColor = true;
	        // 
	        // tlpConvBf
	        // 
	        this.tlpConvBf.AutoSize = true;
	        this.tlpConvBf.ColumnCount = 5;
	        this.tlpConvBf.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20F));
	        this.tlpConvBf.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20F));
	        this.tlpConvBf.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20F));
	        this.tlpConvBf.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20F));
	        this.tlpConvBf.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20F));
	        this.tlpConvBf.Controls.Add(this.lblConvBfThk, 0, 0);
	        this.tlpConvBf.Controls.Add(this.lblConvBfWid, 0, 1);
	        this.tlpConvBf.Controls.Add(this.lblConvBfLen, 0, 2);
	        this.tlpConvBf.Controls.Add(this.lblConvBfQty, 0, 3);
	        this.tlpConvBf.Controls.Add(this.lblConvBfTot, 3, 4);
	        this.tlpConvBf.Controls.Add(this.txtConvBfThk, 1, 0);
	        this.tlpConvBf.Controls.Add(this.txtConvBfWid, 1, 1);
	        this.tlpConvBf.Controls.Add(this.txtConvBfLen, 1, 2);
	        this.tlpConvBf.Controls.Add(this.txtConvBfQty, 1, 3);
	        this.tlpConvBf.Controls.Add(this.btnConvBfAdd, 1, 4);
	        this.tlpConvBf.Controls.Add(this.btnConvBfDel, 2, 4);
	        this.tlpConvBf.Controls.Add(this.txtConvBfTot, 4, 4);
	        this.tlpConvBf.Controls.Add(this.lvwConvBf, 2, 0);
	        this.tlpConvBf.Controls.Add(this.lblConvBfMemo, 0, 5);
	        this.tlpConvBf.Controls.Add(this.btnConvBfClr, 4, 5);
	        this.tlpConvBf.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.tlpConvBf.Location = new System.Drawing.Point(2, 2);
	        this.tlpConvBf.Margin = new System.Windows.Forms.Padding(2);
	        this.tlpConvBf.Name = "tlpConvBf";
	        this.tlpConvBf.RowCount = 6;
	        this.tlpConvBf.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 11F));
	        this.tlpConvBf.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 11F));
	        this.tlpConvBf.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 11F));
	        this.tlpConvBf.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 11F));
	        this.tlpConvBf.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 16F));
	        this.tlpConvBf.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 40F));
	        this.tlpConvBf.Size = new System.Drawing.Size(195, 218);
	        this.tlpConvBf.TabIndex = 0;
	        // 
	        // lblConvBfThk
	        // 
	        this.lblConvBfThk.AutoSize = true;
	        this.lblConvBfThk.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lblConvBfThk.Location = new System.Drawing.Point(2, 0);
	        this.lblConvBfThk.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
	        this.lblConvBfThk.Name = "lblConvBfThk";
	        this.lblConvBfThk.Size = new System.Drawing.Size(35, 23);
	        this.lblConvBfThk.TabIndex = 0;
	        this.lblConvBfThk.Text = "Thk(in)";
	        // 
	        // lblConvBfWid
	        // 
	        this.lblConvBfWid.AutoSize = true;
	        this.lblConvBfWid.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lblConvBfWid.Location = new System.Drawing.Point(2, 23);
	        this.lblConvBfWid.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
	        this.lblConvBfWid.Name = "lblConvBfWid";
	        this.lblConvBfWid.Size = new System.Drawing.Size(35, 23);
	        this.lblConvBfWid.TabIndex = 2;
	        this.lblConvBfWid.Text = "Wid(in)";
	        // 
	        // lblConvBfLen
	        // 
	        this.lblConvBfLen.AutoSize = true;
	        this.lblConvBfLen.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lblConvBfLen.Location = new System.Drawing.Point(2, 46);
	        this.lblConvBfLen.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
	        this.lblConvBfLen.Name = "lblConvBfLen";
	        this.lblConvBfLen.Size = new System.Drawing.Size(35, 23);
	        this.lblConvBfLen.TabIndex = 4;
	        this.lblConvBfLen.Text = "Len(in)";
	        // 
	        // lblConvBfQty
	        // 
	        this.lblConvBfQty.AutoSize = true;
	        this.lblConvBfQty.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lblConvBfQty.Location = new System.Drawing.Point(2, 69);
	        this.lblConvBfQty.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
	        this.lblConvBfQty.Name = "lblConvBfQty";
	        this.lblConvBfQty.Size = new System.Drawing.Size(35, 23);
	        this.lblConvBfQty.TabIndex = 6;
	        this.lblConvBfQty.Text = "Qty";
	        // 
	        // lblConvBfTot
	        // 
	        this.lblConvBfTot.AutoSize = true;
	        this.lblConvBfTot.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lblConvBfTot.Location = new System.Drawing.Point(119, 92);
	        this.lblConvBfTot.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
	        this.lblConvBfTot.Name = "lblConvBfTot";
	        this.lblConvBfTot.Size = new System.Drawing.Size(35, 34);
	        this.lblConvBfTot.TabIndex = 10;
	        this.lblConvBfTot.Text = "Total (bf)";
	        // 
	        // txtConvBfThk
	        // 
	        this.txtConvBfThk.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.txtConvBfThk.Location = new System.Drawing.Point(41, 2);
	        this.txtConvBfThk.Margin = new System.Windows.Forms.Padding(2);
	        this.txtConvBfThk.Name = "txtConvBfThk";
	        this.txtConvBfThk.Size = new System.Drawing.Size(35, 20);
	        this.txtConvBfThk.TabIndex = 1;
	        // 
	        // txtConvBfWid
	        // 
	        this.txtConvBfWid.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.txtConvBfWid.Location = new System.Drawing.Point(41, 25);
	        this.txtConvBfWid.Margin = new System.Windows.Forms.Padding(2);
	        this.txtConvBfWid.Name = "txtConvBfWid";
	        this.txtConvBfWid.Size = new System.Drawing.Size(35, 20);
	        this.txtConvBfWid.TabIndex = 3;
	        // 
	        // txtConvBfLen
	        // 
	        this.txtConvBfLen.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.txtConvBfLen.Location = new System.Drawing.Point(41, 48);
	        this.txtConvBfLen.Margin = new System.Windows.Forms.Padding(2);
	        this.txtConvBfLen.Name = "txtConvBfLen";
	        this.txtConvBfLen.Size = new System.Drawing.Size(35, 20);
	        this.txtConvBfLen.TabIndex = 5;
	        // 
	        // txtConvBfQty
	        // 
	        this.txtConvBfQty.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.txtConvBfQty.Location = new System.Drawing.Point(41, 71);
	        this.txtConvBfQty.Margin = new System.Windows.Forms.Padding(2);
	        this.txtConvBfQty.Name = "txtConvBfQty";
	        this.txtConvBfQty.Size = new System.Drawing.Size(35, 20);
	        this.txtConvBfQty.TabIndex = 7;
	        // 
	        // txtConvBfTot
	        // 
	        this.txtConvBfTot.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.txtConvBfTot.Location = new System.Drawing.Point(158, 94);
	        this.txtConvBfTot.Margin = new System.Windows.Forms.Padding(2);
	        this.txtConvBfTot.Name = "txtConvBfTot";
	        this.txtConvBfTot.Size = new System.Drawing.Size(35, 20);
	        this.txtConvBfTot.TabIndex = 11;
	        // 
	        // lvwConvBf
	        // 
	        this.lvwConvBf.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] { this.colConvBfDims, this.colConvBfQty });
	        this.tlpConvBf.SetColumnSpan(this.lvwConvBf, 3);
	        this.lvwConvBf.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lvwConvBf.HideSelection = false;
	        this.lvwConvBf.Location = new System.Drawing.Point(80, 2);
	        this.lvwConvBf.Margin = new System.Windows.Forms.Padding(2);
	        this.lvwConvBf.Name = "lvwConvBf";
	        this.tlpConvBf.SetRowSpan(this.lvwConvBf, 4);
	        this.lvwConvBf.Size = new System.Drawing.Size(113, 88);
	        this.lvwConvBf.TabIndex = 13;
	        this.lvwConvBf.UseCompatibleStateImageBehavior = false;
	        this.lvwConvBf.View = System.Windows.Forms.View.Details;
	        // 
	        // lblConvBfMemo
	        // 
	        this.lblConvBfMemo.AutoSize = true;
	        this.tlpConvBf.SetColumnSpan(this.lblConvBfMemo, 3);
	        this.lblConvBfMemo.Dock = System.Windows.Forms.DockStyle.Top;
	        this.lblConvBfMemo.Location = new System.Drawing.Point(2, 126);
	        this.lblConvBfMemo.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
	        this.lblConvBfMemo.Name = "lblConvBfMemo";
	        this.lblConvBfMemo.Size = new System.Drawing.Size(113, 39);
	        this.lblConvBfMemo.TabIndex = 14;
	        this.lblConvBfMemo.Text = "Enter rough dimensions (ex. 2x4 is 2\"x4\", not 1.5\"x3.5\")";
	        // 
	        // cplMainOpp
	        // 
	        this.cplMainOpp.AutoScroll = true;
	        // 
	        // cplMainOpp.Content
	        // 
	        this.cplMainOpp.Content.AutoSize = true;
	        this.cplMainOpp.Content.Controls.Add(this.tlpMainOpp);
	        this.cplMainOpp.Content.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.cplMainOpp.Content.Location = new System.Drawing.Point(5, 24);
	        this.cplMainOpp.Content.Margin = new System.Windows.Forms.Padding(4);
	        this.cplMainOpp.Content.Name = "Content";
	        this.cplMainOpp.Content.Size = new System.Drawing.Size(211, 419);
	        this.cplMainOpp.Content.TabIndex = 1;
	        this.cplMainOpp.Dock = System.Windows.Forms.DockStyle.Top;
	        this.cplMainOpp.Location = new System.Drawing.Point(0, 1634);
	        this.cplMainOpp.Margin = new System.Windows.Forms.Padding(4);
	        this.cplMainOpp.MaximumSize = new System.Drawing.Size(750, 812);
	        this.cplMainOpp.MinimumSize = new System.Drawing.Size(90, 18);
	        this.cplMainOpp.Name = "cplMainOpp";
	        this.cplMainOpp.Size = new System.Drawing.Size(223, 450);
	        this.cplMainOpp.TabIndex = 5;
	        this.cplMainOpp.Title = "Epicor Opp Search";
	        // 
	        // tlpMainOpp
	        // 
	        this.tlpMainOpp.AutoSize = true;
	        this.tlpMainOpp.ColumnCount = 4;
	        this.tlpMainOpp.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 18F));
	        this.tlpMainOpp.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 30F));
	        this.tlpMainOpp.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 16F));
	        this.tlpMainOpp.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 36F));
	        this.tlpMainOpp.Controls.Add(this.btnEOSOpp, 0, 0);
	        this.tlpMainOpp.Controls.Add(this.lblEOSDue, 0, 1);
	        this.tlpMainOpp.Controls.Add(this.lblEOSDesc, 0, 2);
	        this.tlpMainOpp.Controls.Add(this.lblEOSCmt, 0, 3);
	        this.tlpMainOpp.Controls.Add(this.lblEOSMemo, 0, 4);
	        this.tlpMainOpp.Controls.Add(this.lblEOSCust, 2, 0);
	        this.tlpMainOpp.Controls.Add(this.txtEOSOpp, 1, 0);
	        this.tlpMainOpp.Controls.Add(this.txtEOSDue, 1, 1);
	        this.tlpMainOpp.Controls.Add(this.txtEOSDesc, 1, 2);
	        this.tlpMainOpp.Controls.Add(this.txtEOSCmt, 1, 3);
	        this.tlpMainOpp.Controls.Add(this.txtEOSCust, 3, 0);
	        this.tlpMainOpp.Controls.Add(this.txtEOSUser, 3, 1);
	        this.tlpMainOpp.Controls.Add(this.btnEOSUser, 2, 1);
	        this.tlpMainOpp.Controls.Add(this.btnEOSEmail, 2, 4);
	        this.tlpMainOpp.Controls.Add(this.txtEOSMemoBody, 0, 6);
	        this.tlpMainOpp.Controls.Add(this.lblEOSAttach, 0, 7);
	        this.tlpMainOpp.Controls.Add(this.lstEOSAttach, 0, 8);
	        this.tlpMainOpp.Controls.Add(this.cmbEOSMemoSel, 0, 5);
	        this.tlpMainOpp.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.tlpMainOpp.Location = new System.Drawing.Point(0, 0);
	        this.tlpMainOpp.Name = "tlpMainOpp";
	        this.tlpMainOpp.RowCount = 9;
	        this.tlpMainOpp.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 5.5F));
	        this.tlpMainOpp.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 5.5F));
	        this.tlpMainOpp.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 5.5F));
	        this.tlpMainOpp.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 5.5F));
	        this.tlpMainOpp.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 5.5F));
	        this.tlpMainOpp.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 6F));
	        this.tlpMainOpp.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 44F));
	        this.tlpMainOpp.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 5F));
	        this.tlpMainOpp.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 17.5F));
	        this.tlpMainOpp.Size = new System.Drawing.Size(211, 419);
	        this.tlpMainOpp.TabIndex = 13;
	        // 
	        // btnEOSOpp
	        // 
	        this.btnEOSOpp.AutoSize = true;
	        this.btnEOSOpp.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.btnEOSOpp.Location = new System.Drawing.Point(0, 0);
	        this.btnEOSOpp.Margin = new System.Windows.Forms.Padding(0);
	        this.btnEOSOpp.Name = "btnEOSOpp";
	        this.btnEOSOpp.Size = new System.Drawing.Size(37, 23);
	        this.btnEOSOpp.TabIndex = 0;
	        this.btnEOSOpp.Text = "Opp";
	        this.btnEOSOpp.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
	        this.tipTool.SetToolTip(this.btnEOSOpp, "Click to get opp number from open model");
	        this.btnEOSOpp.Click += new System.EventHandler(this.btnEOSOpp_Click);
	        // 
	        // lblEOSDue
	        // 
	        this.lblEOSDue.AutoSize = true;
	        this.lblEOSDue.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lblEOSDue.Location = new System.Drawing.Point(2, 23);
	        this.lblEOSDue.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
	        this.lblEOSDue.Name = "lblEOSDue";
	        this.lblEOSDue.Size = new System.Drawing.Size(33, 23);
	        this.lblEOSDue.TabIndex = 4;
	        this.lblEOSDue.Text = "Due";
	        // 
	        // lblEOSDesc
	        // 
	        this.lblEOSDesc.AutoSize = true;
	        this.lblEOSDesc.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lblEOSDesc.Location = new System.Drawing.Point(2, 46);
	        this.lblEOSDesc.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
	        this.lblEOSDesc.Name = "lblEOSDesc";
	        this.lblEOSDesc.Size = new System.Drawing.Size(33, 23);
	        this.lblEOSDesc.TabIndex = 8;
	        this.lblEOSDesc.Text = "Desc";
	        // 
	        // lblEOSCmt
	        // 
	        this.lblEOSCmt.AutoSize = true;
	        this.lblEOSCmt.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lblEOSCmt.Location = new System.Drawing.Point(2, 69);
	        this.lblEOSCmt.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
	        this.lblEOSCmt.Name = "lblEOSCmt";
	        this.lblEOSCmt.Size = new System.Drawing.Size(33, 23);
	        this.lblEOSCmt.TabIndex = 10;
	        this.lblEOSCmt.Text = "Cmt";
	        // 
	        // lblEOSMemo
	        // 
	        this.lblEOSMemo.AutoSize = true;
	        this.lblEOSMemo.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lblEOSMemo.Location = new System.Drawing.Point(2, 92);
	        this.lblEOSMemo.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
	        this.lblEOSMemo.Name = "lblEOSMemo";
	        this.lblEOSMemo.Size = new System.Drawing.Size(33, 23);
	        this.lblEOSMemo.TabIndex = 12;
	        this.lblEOSMemo.Text = "Memo";
	        // 
	        // lblEOSCust
	        // 
	        this.lblEOSCust.AutoSize = true;
	        this.lblEOSCust.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lblEOSCust.Location = new System.Drawing.Point(102, 0);
	        this.lblEOSCust.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
	        this.lblEOSCust.Name = "lblEOSCust";
	        this.lblEOSCust.Size = new System.Drawing.Size(29, 23);
	        this.lblEOSCust.TabIndex = 2;
	        this.lblEOSCust.Text = "Cust";
	        // 
	        // txtEOSOpp
	        // 
	        this.txtEOSOpp.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.txtEOSOpp.Location = new System.Drawing.Point(39, 2);
	        this.txtEOSOpp.Margin = new System.Windows.Forms.Padding(2);
	        this.txtEOSOpp.Name = "txtEOSOpp";
	        this.txtEOSOpp.Size = new System.Drawing.Size(59, 20);
	        this.txtEOSOpp.TabIndex = 1;
	        this.txtEOSOpp.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.txtEOSOpp_KeyPress);
	        this.txtEOSOpp.Leave += new System.EventHandler(this.txtEOSOpp_Leave);
	        // 
	        // txtEOSDue
	        // 
	        this.txtEOSDue.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.txtEOSDue.Location = new System.Drawing.Point(39, 25);
	        this.txtEOSDue.Margin = new System.Windows.Forms.Padding(2);
	        this.txtEOSDue.Name = "txtEOSDue";
	        this.txtEOSDue.ReadOnly = true;
	        this.txtEOSDue.Size = new System.Drawing.Size(59, 20);
	        this.txtEOSDue.TabIndex = 5;
	        // 
	        // txtEOSDesc
	        // 
	        this.tlpMainOpp.SetColumnSpan(this.txtEOSDesc, 3);
	        this.txtEOSDesc.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.txtEOSDesc.Location = new System.Drawing.Point(39, 48);
	        this.txtEOSDesc.Margin = new System.Windows.Forms.Padding(2);
	        this.txtEOSDesc.Name = "txtEOSDesc";
	        this.txtEOSDesc.ReadOnly = true;
	        this.txtEOSDesc.Size = new System.Drawing.Size(170, 20);
	        this.txtEOSDesc.TabIndex = 9;
	        // 
	        // txtEOSCmt
	        // 
	        this.tlpMainOpp.SetColumnSpan(this.txtEOSCmt, 3);
	        this.txtEOSCmt.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.txtEOSCmt.Location = new System.Drawing.Point(39, 71);
	        this.txtEOSCmt.Margin = new System.Windows.Forms.Padding(2);
	        this.txtEOSCmt.Name = "txtEOSCmt";
	        this.txtEOSCmt.ReadOnly = true;
	        this.txtEOSCmt.Size = new System.Drawing.Size(170, 20);
	        this.txtEOSCmt.TabIndex = 11;
	        // 
	        // txtEOSCust
	        // 
	        this.txtEOSCust.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.txtEOSCust.Location = new System.Drawing.Point(135, 2);
	        this.txtEOSCust.Margin = new System.Windows.Forms.Padding(2);
	        this.txtEOSCust.Name = "txtEOSCust";
	        this.txtEOSCust.ReadOnly = true;
	        this.txtEOSCust.Size = new System.Drawing.Size(74, 20);
	        this.txtEOSCust.TabIndex = 3;
	        // 
	        // txtEOSUser
	        // 
	        this.txtEOSUser.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.txtEOSUser.Location = new System.Drawing.Point(135, 25);
	        this.txtEOSUser.Margin = new System.Windows.Forms.Padding(2);
	        this.txtEOSUser.Name = "txtEOSUser";
	        this.txtEOSUser.ReadOnly = true;
	        this.txtEOSUser.Size = new System.Drawing.Size(74, 20);
	        this.txtEOSUser.TabIndex = 7;
	        // 
	        // txtEOSMemoBody
	        // 
	        this.txtEOSMemoBody.AcceptsReturn = true;
	        this.tlpMainOpp.SetColumnSpan(this.txtEOSMemoBody, 4);
	        this.txtEOSMemoBody.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.txtEOSMemoBody.Location = new System.Drawing.Point(2, 142);
	        this.txtEOSMemoBody.Margin = new System.Windows.Forms.Padding(2);
	        this.txtEOSMemoBody.MinimumSize = new System.Drawing.Size(4, 147);
	        this.txtEOSMemoBody.Multiline = true;
	        this.txtEOSMemoBody.Name = "txtEOSMemoBody";
	        this.txtEOSMemoBody.ReadOnly = true;
	        this.txtEOSMemoBody.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
	        this.txtEOSMemoBody.Size = new System.Drawing.Size(207, 180);
	        this.txtEOSMemoBody.TabIndex = 16;
	        // 
	        // lblEOSAttach
	        // 
	        this.lblEOSAttach.AutoSize = true;
	        this.tlpMainOpp.SetColumnSpan(this.lblEOSAttach, 2);
	        this.lblEOSAttach.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lblEOSAttach.Location = new System.Drawing.Point(2, 324);
	        this.lblEOSAttach.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
	        this.lblEOSAttach.Name = "lblEOSAttach";
	        this.lblEOSAttach.Size = new System.Drawing.Size(96, 20);
	        this.lblEOSAttach.TabIndex = 17;
	        this.lblEOSAttach.Text = "Attachments";
	        this.lblEOSAttach.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
	        // 
	        // lstEOSAttach
	        // 
	        this.lstEOSAttach.BackColor = System.Drawing.SystemColors.Control;
	        this.lstEOSAttach.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] { this.colEOSFile });
	        this.tlpMainOpp.SetColumnSpan(this.lstEOSAttach, 4);
	        this.lstEOSAttach.ContextMenuStrip = this.cmsOpAttach;
	        this.lstEOSAttach.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lstEOSAttach.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.None;
	        this.lstEOSAttach.HideSelection = false;
	        this.lstEOSAttach.Location = new System.Drawing.Point(2, 346);
	        this.lstEOSAttach.Margin = new System.Windows.Forms.Padding(2);
	        this.lstEOSAttach.MultiSelect = false;
	        this.lstEOSAttach.Name = "lstEOSAttach";
	        this.lstEOSAttach.ShowItemToolTips = true;
	        this.lstEOSAttach.Size = new System.Drawing.Size(207, 71);
	        this.lstEOSAttach.TabIndex = 18;
	        this.lstEOSAttach.UseCompatibleStateImageBehavior = false;
	        this.lstEOSAttach.View = System.Windows.Forms.View.Details;
	        this.lstEOSAttach.DoubleClick += new System.EventHandler(this.lstEOSAttach_DoubleClick);
	        this.lstEOSAttach.KeyUp += new System.Windows.Forms.KeyEventHandler(this.lstEOSAttach_KeyUp);
	        // 
	        // colEOSFile
	        // 
	        this.colEOSFile.Text = "File";
	        // 
	        // cmsOpAttach
	        // 
	        this.cmsOpAttach.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { this.tsiOpBrowse });
	        this.cmsOpAttach.Name = "cmsOpAttach";
	        this.cmsOpAttach.Size = new System.Drawing.Size(140, 26);
	        // 
	        // tsiOpBrowse
	        // 
	        this.tsiOpBrowse.Name = "tsiOpBrowse";
	        this.tsiOpBrowse.Size = new System.Drawing.Size(139, 22);
	        this.tsiOpBrowse.Text = "Open &Folder";
	        this.tsiOpBrowse.Click += new System.EventHandler(this.tsiOpBrowse_Click);
	        // 
	        // cmbEOSMemoSel
	        // 
	        this.tlpMainOpp.SetColumnSpan(this.cmbEOSMemoSel, 4);
	        this.cmbEOSMemoSel.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.cmbEOSMemoSel.FormattingEnabled = true;
	        this.cmbEOSMemoSel.Location = new System.Drawing.Point(3, 118);
	        this.cmbEOSMemoSel.Name = "cmbEOSMemoSel";
	        this.cmbEOSMemoSel.Size = new System.Drawing.Size(205, 21);
	        this.cmbEOSMemoSel.TabIndex = 14;
	        this.cmbEOSMemoSel.SelectedIndexChanged += new System.EventHandler(this.cmbEOSMemoSel_SelectedIndexChanged);
	        // 
	        // cplMainSfOpp
	        // 
	        this.cplMainSfOpp.AutoScroll = true;
	        // 
	        // cplMainSfOpp.Content
	        // 
	        this.cplMainSfOpp.Content.AutoSize = true;
	        this.cplMainSfOpp.Content.Controls.Add(this.tlpSfMain);
	        this.cplMainSfOpp.Content.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.cplMainSfOpp.Content.Location = new System.Drawing.Point(5, 24);
	        this.cplMainSfOpp.Content.Margin = new System.Windows.Forms.Padding(4);
	        this.cplMainSfOpp.Content.Name = "Content";
	        this.cplMainSfOpp.Content.Size = new System.Drawing.Size(211, 473);
	        this.cplMainSfOpp.Content.TabIndex = 1;
	        this.cplMainSfOpp.Dock = System.Windows.Forms.DockStyle.Top;
	        this.cplMainSfOpp.Location = new System.Drawing.Point(0, 1130);
	        this.cplMainSfOpp.Margin = new System.Windows.Forms.Padding(4);
	        this.cplMainSfOpp.MaximumSize = new System.Drawing.Size(750, 812);
	        this.cplMainSfOpp.MinimumSize = new System.Drawing.Size(90, 18);
	        this.cplMainSfOpp.Name = "cplMainSfOpp";
	        this.cplMainSfOpp.Size = new System.Drawing.Size(223, 504);
	        this.cplMainSfOpp.TabIndex = 5;
	        this.cplMainSfOpp.Title = "SalesForce";
	        // 
	        // tlpSfMain
	        // 
	        this.tlpSfMain.ColumnCount = 1;
	        this.tlpSfMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
	        this.tlpSfMain.Controls.Add(this.tlpSfUserInfo, 0, 0);
	        this.tlpSfMain.Controls.Add(this.tlpMainSfOpp, 0, 1);
	        this.tlpSfMain.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.tlpSfMain.Location = new System.Drawing.Point(0, 0);
	        this.tlpSfMain.Name = "tlpSfMain";
	        this.tlpSfMain.RowCount = 2;
	        this.tlpSfMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 73F));
	        this.tlpSfMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
	        this.tlpSfMain.Size = new System.Drawing.Size(211, 473);
	        this.tlpSfMain.TabIndex = 14;
	        // 
	        // tlpSfUserInfo
	        // 
	        this.tlpSfUserInfo.ColumnCount = 3;
	        this.tlpSfUserInfo.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 64F));
	        this.tlpSfUserInfo.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 35F));
	        this.tlpSfUserInfo.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 65F));
	        this.tlpSfUserInfo.Controls.Add(this.lblSfUserName, 1, 0);
	        this.tlpSfUserInfo.Controls.Add(this.picSfUser, 0, 0);
	        this.tlpSfUserInfo.Controls.Add(this.lblSfEmail, 1, 1);
	        this.tlpSfUserInfo.Controls.Add(this.btnSfLogin, 2, 2);
	        this.tlpSfUserInfo.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.tlpSfUserInfo.Location = new System.Drawing.Point(3, 3);
	        this.tlpSfUserInfo.Name = "tlpSfUserInfo";
	        this.tlpSfUserInfo.RowCount = 3;
	        this.tlpSfUserInfo.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
	        this.tlpSfUserInfo.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
	        this.tlpSfUserInfo.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
	        this.tlpSfUserInfo.Size = new System.Drawing.Size(205, 67);
	        this.tlpSfUserInfo.TabIndex = 14;
	        // 
	        // lblSfUserName
	        // 
	        this.tlpSfUserInfo.SetColumnSpan(this.lblSfUserName, 2);
	        this.lblSfUserName.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lblSfUserName.Location = new System.Drawing.Point(67, 0);
	        this.lblSfUserName.Name = "lblSfUserName";
	        this.lblSfUserName.Size = new System.Drawing.Size(135, 20);
	        this.lblSfUserName.TabIndex = 0;
	        this.lblSfUserName.Text = "Not Logged In";
	        // 
	        // picSfUser
	        // 
	        this.picSfUser.Dock = System.Windows.Forms.DockStyle.Top;
	        this.picSfUser.Location = new System.Drawing.Point(3, 3);
	        this.picSfUser.Name = "picSfUser";
	        this.tlpSfUserInfo.SetRowSpan(this.picSfUser, 3);
	        this.picSfUser.Size = new System.Drawing.Size(58, 46);
	        this.picSfUser.TabIndex = 1;
	        this.picSfUser.TabStop = false;
	        // 
	        // lblSfEmail
	        // 
	        this.tlpSfUserInfo.SetColumnSpan(this.lblSfEmail, 2);
	        this.lblSfEmail.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lblSfEmail.Location = new System.Drawing.Point(67, 20);
	        this.lblSfEmail.Name = "lblSfEmail";
	        this.lblSfEmail.Size = new System.Drawing.Size(135, 20);
	        this.lblSfEmail.TabIndex = 1;
	        // 
	        // btnSfLogin
	        // 
	        this.btnSfLogin.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.btnSfLogin.Location = new System.Drawing.Point(116, 43);
	        this.btnSfLogin.Name = "btnSfLogin";
	        this.btnSfLogin.Size = new System.Drawing.Size(86, 21);
	        this.btnSfLogin.TabIndex = 2;
	        this.btnSfLogin.Text = "Login";
	        this.btnSfLogin.UseVisualStyleBackColor = true;
	        this.btnSfLogin.Click += new System.EventHandler(this.btnSfLogin_Click);
	        // 
	        // tlpMainSfOpp
	        // 
	        this.tlpMainSfOpp.AutoSize = true;
	        this.tlpMainSfOpp.ColumnCount = 4;
	        this.tlpMainSfOpp.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 18F));
	        this.tlpMainSfOpp.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 30F));
	        this.tlpMainSfOpp.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 16F));
	        this.tlpMainSfOpp.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 36F));
	        this.tlpMainSfOpp.Controls.Add(this.btnSFOSOpp, 0, 0);
	        this.tlpMainSfOpp.Controls.Add(this.lblSFOSDue, 0, 1);
	        this.tlpMainSfOpp.Controls.Add(this.lblSFOSDesc, 0, 2);
	        this.tlpMainSfOpp.Controls.Add(this.lblSFOSTask, 0, 3);
	        this.tlpMainSfOpp.Controls.Add(this.lblSFOSCust, 2, 0);
	        this.tlpMainSfOpp.Controls.Add(this.txtSFOSOpp, 1, 0);
	        this.tlpMainSfOpp.Controls.Add(this.txtSFOSDue, 1, 1);
	        this.tlpMainSfOpp.Controls.Add(this.txtSFOSDesc, 1, 2);
	        this.tlpMainSfOpp.Controls.Add(this.txtSFOSCust, 3, 0);
	        this.tlpMainSfOpp.Controls.Add(this.txtSFOSUser, 3, 1);
	        this.tlpMainSfOpp.Controls.Add(this.btnSFOSUser, 2, 1);
	        this.tlpMainSfOpp.Controls.Add(this.btnSFOSEmail, 2, 3);
	        this.tlpMainSfOpp.Controls.Add(this.lblSFOSAttach, 0, 6);
	        this.tlpMainSfOpp.Controls.Add(this.lstSFOSAttach, 0, 7);
	        this.tlpMainSfOpp.Controls.Add(this.cmbSFOSMemoSel, 0, 4);
	        this.tlpMainSfOpp.Location = new System.Drawing.Point(3, 76);
	        this.tlpMainSfOpp.Name = "tlpMainSfOpp";
	        this.tlpMainSfOpp.RowCount = 8;
	        this.tlpMainSfOpp.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 6F));
	        this.tlpMainSfOpp.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 6F));
	        this.tlpMainSfOpp.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 19F));
	        this.tlpMainSfOpp.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 6F));
	        this.tlpMainSfOpp.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 7F));
	        this.tlpMainSfOpp.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 35F));
	        this.tlpMainSfOpp.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 6F));
	        this.tlpMainSfOpp.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 15F));
	        this.tlpMainSfOpp.Size = new System.Drawing.Size(205, 394);
	        this.tlpMainSfOpp.TabIndex = 13;
	        // 
	        // btnSFOSOpp
	        // 
	        this.btnSFOSOpp.AutoSize = true;
	        this.btnSFOSOpp.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.btnSFOSOpp.Location = new System.Drawing.Point(0, 0);
	        this.btnSFOSOpp.Margin = new System.Windows.Forms.Padding(0);
	        this.btnSFOSOpp.Name = "btnSFOSOpp";
	        this.btnSFOSOpp.Size = new System.Drawing.Size(36, 23);
	        this.btnSFOSOpp.TabIndex = 0;
	        this.btnSFOSOpp.Text = "Opp";
	        this.btnSFOSOpp.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
	        this.tipTool.SetToolTip(this.btnSFOSOpp, "Click to get opp number from open model");
	        this.btnSFOSOpp.Click += new System.EventHandler(this.btnSFOSOpp_Click);
	        // 
	        // lblSFOSDue
	        // 
	        this.lblSFOSDue.AutoSize = true;
	        this.lblSFOSDue.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lblSFOSDue.Location = new System.Drawing.Point(2, 23);
	        this.lblSFOSDue.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
	        this.lblSFOSDue.Name = "lblSFOSDue";
	        this.lblSFOSDue.Size = new System.Drawing.Size(32, 23);
	        this.lblSFOSDue.TabIndex = 4;
	        this.lblSFOSDue.Text = "Due";
	        // 
	        // lblSFOSDesc
	        // 
	        this.lblSFOSDesc.AutoSize = true;
	        this.lblSFOSDesc.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lblSFOSDesc.Location = new System.Drawing.Point(2, 46);
	        this.lblSFOSDesc.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
	        this.lblSFOSDesc.Name = "lblSFOSDesc";
	        this.lblSFOSDesc.Size = new System.Drawing.Size(32, 74);
	        this.lblSFOSDesc.TabIndex = 8;
	        this.lblSFOSDesc.Text = "Desc";
	        // 
	        // lblSFOSTask
	        // 
	        this.lblSFOSTask.AutoSize = true;
	        this.lblSFOSTask.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lblSFOSTask.Location = new System.Drawing.Point(2, 120);
	        this.lblSFOSTask.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
	        this.lblSFOSTask.Name = "lblSFOSTask";
	        this.lblSFOSTask.Size = new System.Drawing.Size(32, 23);
	        this.lblSFOSTask.TabIndex = 12;
	        this.lblSFOSTask.Text = "Task";
	        // 
	        // lblSFOSCust
	        // 
	        this.lblSFOSCust.AutoSize = true;
	        this.lblSFOSCust.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lblSFOSCust.Location = new System.Drawing.Point(99, 0);
	        this.lblSFOSCust.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
	        this.lblSFOSCust.Name = "lblSFOSCust";
	        this.lblSFOSCust.Size = new System.Drawing.Size(28, 23);
	        this.lblSFOSCust.TabIndex = 2;
	        this.lblSFOSCust.Text = "Cust";
	        // 
	        // txtSFOSOpp
	        // 
	        this.txtSFOSOpp.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.txtSFOSOpp.Location = new System.Drawing.Point(38, 2);
	        this.txtSFOSOpp.Margin = new System.Windows.Forms.Padding(2);
	        this.txtSFOSOpp.Name = "txtSFOSOpp";
	        this.txtSFOSOpp.Size = new System.Drawing.Size(57, 20);
	        this.txtSFOSOpp.TabIndex = 1;
	        this.txtSFOSOpp.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.txtSFOSOpp_KeyPress);
	        this.txtSFOSOpp.Leave += new System.EventHandler(this.txtSFOSOpp_Leave);
	        // 
	        // txtSFOSDue
	        // 
	        this.txtSFOSDue.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.txtSFOSDue.Location = new System.Drawing.Point(38, 25);
	        this.txtSFOSDue.Margin = new System.Windows.Forms.Padding(2);
	        this.txtSFOSDue.Name = "txtSFOSDue";
	        this.txtSFOSDue.ReadOnly = true;
	        this.txtSFOSDue.Size = new System.Drawing.Size(57, 20);
	        this.txtSFOSDue.TabIndex = 5;
	        // 
	        // txtSFOSDesc
	        // 
	        this.tlpMainSfOpp.SetColumnSpan(this.txtSFOSDesc, 3);
	        this.txtSFOSDesc.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.txtSFOSDesc.Location = new System.Drawing.Point(38, 48);
	        this.txtSFOSDesc.Margin = new System.Windows.Forms.Padding(2);
	        this.txtSFOSDesc.Multiline = true;
	        this.txtSFOSDesc.Name = "txtSFOSDesc";
	        this.txtSFOSDesc.ReadOnly = true;
	        this.txtSFOSDesc.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
	        this.txtSFOSDesc.Size = new System.Drawing.Size(165, 70);
	        this.txtSFOSDesc.TabIndex = 9;
	        // 
	        // txtSFOSCust
	        // 
	        this.txtSFOSCust.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.txtSFOSCust.Location = new System.Drawing.Point(131, 2);
	        this.txtSFOSCust.Margin = new System.Windows.Forms.Padding(2);
	        this.txtSFOSCust.Name = "txtSFOSCust";
	        this.txtSFOSCust.ReadOnly = true;
	        this.txtSFOSCust.Size = new System.Drawing.Size(72, 20);
	        this.txtSFOSCust.TabIndex = 3;
	        // 
	        // txtSFOSUser
	        // 
	        this.txtSFOSUser.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.txtSFOSUser.Location = new System.Drawing.Point(131, 25);
	        this.txtSFOSUser.Margin = new System.Windows.Forms.Padding(2);
	        this.txtSFOSUser.Name = "txtSFOSUser";
	        this.txtSFOSUser.ReadOnly = true;
	        this.txtSFOSUser.Size = new System.Drawing.Size(72, 20);
	        this.txtSFOSUser.TabIndex = 7;
	        // 
	        // btnSFOSUser
	        // 
	        this.btnSFOSUser.AutoSize = true;
	        this.btnSFOSUser.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.btnSFOSUser.Location = new System.Drawing.Point(97, 23);
	        this.btnSFOSUser.Margin = new System.Windows.Forms.Padding(0);
	        this.btnSFOSUser.Name = "btnSFOSUser";
	        this.btnSFOSUser.Size = new System.Drawing.Size(32, 23);
	        this.btnSFOSUser.TabIndex = 6;
	        this.btnSFOSUser.Text = "User";
	        this.btnSFOSUser.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
	        this.tipTool.SetToolTip(this.btnSFOSUser, "Launch tasks by user window");
	        this.btnSFOSUser.UseVisualStyleBackColor = true;
	        this.btnSFOSUser.Click += new System.EventHandler(this.btnSFOSUser_Click);
	        // 
	        // btnSFOSEmail
	        // 
	        this.btnSFOSEmail.AutoSize = true;
	        this.tlpMainSfOpp.SetColumnSpan(this.btnSFOSEmail, 2);
	        this.btnSFOSEmail.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.btnSFOSEmail.Location = new System.Drawing.Point(97, 120);
	        this.btnSFOSEmail.Margin = new System.Windows.Forms.Padding(0);
	        this.btnSFOSEmail.Name = "btnSFOSEmail";
	        this.btnSFOSEmail.Size = new System.Drawing.Size(108, 23);
	        this.btnSFOSEmail.TabIndex = 13;
	        this.btnSFOSEmail.Text = "Email Requestor";
	        this.tipTool.SetToolTip(this.btnSFOSEmail, "Create email about this opp");
	        this.btnSFOSEmail.UseVisualStyleBackColor = true;
	        this.btnSFOSEmail.Click += new System.EventHandler(this.btnSFOSEmail_Click);
	        // 
	        // lblSFOSAttach
	        // 
	        this.lblSFOSAttach.AutoSize = true;
	        this.tlpMainSfOpp.SetColumnSpan(this.lblSFOSAttach, 2);
	        this.lblSFOSAttach.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lblSFOSAttach.Location = new System.Drawing.Point(2, 307);
	        this.lblSFOSAttach.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
	        this.lblSFOSAttach.Name = "lblSFOSAttach";
	        this.lblSFOSAttach.Size = new System.Drawing.Size(93, 23);
	        this.lblSFOSAttach.TabIndex = 17;
	        this.lblSFOSAttach.Text = "Attachments";
	        this.lblSFOSAttach.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
	        // 
	        // lstSFOSAttach
	        // 
	        this.lstSFOSAttach.BackColor = System.Drawing.SystemColors.Control;
	        this.lstSFOSAttach.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] { this.colSFOSFile });
	        this.tlpMainSfOpp.SetColumnSpan(this.lstSFOSAttach, 4);
	        this.lstSFOSAttach.ContextMenuStrip = this.cmsOpAttach;
	        this.lstSFOSAttach.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lstSFOSAttach.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.None;
	        this.lstSFOSAttach.HideSelection = false;
	        this.lstSFOSAttach.Location = new System.Drawing.Point(2, 332);
	        this.lstSFOSAttach.Margin = new System.Windows.Forms.Padding(2);
	        this.lstSFOSAttach.MultiSelect = false;
	        this.lstSFOSAttach.Name = "lstSFOSAttach";
	        this.lstSFOSAttach.ShowItemToolTips = true;
	        this.lstSFOSAttach.Size = new System.Drawing.Size(201, 60);
	        this.lstSFOSAttach.TabIndex = 18;
	        this.lstSFOSAttach.UseCompatibleStateImageBehavior = false;
	        this.lstSFOSAttach.View = System.Windows.Forms.View.Details;
	        this.lstSFOSAttach.DoubleClick += new System.EventHandler(this.lstSFOSAttach_DoubleClick);
	        this.lstSFOSAttach.KeyUp += new System.Windows.Forms.KeyEventHandler(this.lstSFOSAttach_KeyUp);
	        // 
	        // colSFOSFile
	        // 
	        this.colSFOSFile.Text = "File";
	        // 
	        // cmbSFOSMemoSel
	        // 
	        this.tlpMainSfOpp.SetColumnSpan(this.cmbSFOSMemoSel, 4);
	        this.cmbSFOSMemoSel.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.cmbSFOSMemoSel.FormattingEnabled = true;
	        this.cmbSFOSMemoSel.Location = new System.Drawing.Point(3, 146);
	        this.cmbSFOSMemoSel.Name = "cmbSFOSMemoSel";
	        this.cmbSFOSMemoSel.Size = new System.Drawing.Size(199, 21);
	        this.cmbSFOSMemoSel.TabIndex = 14;
	        this.cmbSFOSMemoSel.SelectedIndexChanged += new System.EventHandler(this.cmbSFOSMemoSel_SelectedIndexChanged);
	        // 
	        // cmsSfOpAttach
	        // 
	        this.cmsSfOpAttach.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { this.tsiSfOpBrowse });
	        this.cmsSfOpAttach.Name = "cmsSfOpAttach";
	        this.cmsSfOpAttach.Size = new System.Drawing.Size(140, 26);
	        // 
	        // tsiSfOpBrowse
	        // 
	        this.tsiSfOpBrowse.Name = "tsiSfOpBrowse";
	        this.tsiSfOpBrowse.Size = new System.Drawing.Size(139, 22);
	        this.tsiSfOpBrowse.Text = "Open &Folder";
	        this.tsiSfOpBrowse.Click += new System.EventHandler(this.tsiSfOpBrowse_Click);
	        // 
	        // cplMainProp
	        // 
	        this.cplMainProp.AutoScroll = true;
	        // 
	        // cplMainProp.Content
	        // 
	        this.cplMainProp.Content.AutoSize = true;
	        this.cplMainProp.Content.Controls.Add(this.tlpMainProp);
	        this.cplMainProp.Content.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.cplMainProp.Content.Location = new System.Drawing.Point(5, 24);
	        this.cplMainProp.Content.Margin = new System.Windows.Forms.Padding(4);
	        this.cplMainProp.Content.Name = "Content";
	        this.cplMainProp.Content.Size = new System.Drawing.Size(211, 249);
	        this.cplMainProp.Content.TabIndex = 1;
	        this.cplMainProp.Dock = System.Windows.Forms.DockStyle.Top;
	        this.cplMainProp.Location = new System.Drawing.Point(0, 310);
	        this.cplMainProp.Margin = new System.Windows.Forms.Padding(4);
	        this.cplMainProp.MaximumSize = new System.Drawing.Size(750, 812);
	        this.cplMainProp.MinimumSize = new System.Drawing.Size(90, 18);
	        this.cplMainProp.Name = "cplMainProp";
	        this.cplMainProp.Size = new System.Drawing.Size(223, 280);
	        this.cplMainProp.TabIndex = 2;
	        this.cplMainProp.Title = "Custom Properties";
	        // 
	        // tlpMainProp
	        // 
	        this.tlpMainProp.ColumnCount = 1;
	        this.tlpMainProp.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
	        this.tlpMainProp.Controls.Add(this.tabMainProp, 0, 1);
	        this.tlpMainProp.Controls.Add(this.tlpPropCmds, 0, 0);
	        this.tlpMainProp.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.tlpMainProp.Location = new System.Drawing.Point(0, 0);
	        this.tlpMainProp.Name = "tlpMainProp";
	        this.tlpMainProp.RowCount = 2;
	        this.tlpMainProp.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 12.5F));
	        this.tlpMainProp.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 87.5F));
	        this.tlpMainProp.Size = new System.Drawing.Size(211, 249);
	        this.tlpMainProp.TabIndex = 1;
	        // 
	        // tabMainProp
	        // 
	        this.tabMainProp.Controls.Add(this.tpgPropGen);
	        this.tabMainProp.Controls.Add(this.tpgPropCut);
	        this.tabMainProp.Controls.Add(this.tpgPropCust);
	        this.tabMainProp.Controls.Add(this.tpgPropCmt);
	        this.tabMainProp.Controls.Add(this.tpgPropOrd);
	        this.tabMainProp.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.tabMainProp.Location = new System.Drawing.Point(2, 33);
	        this.tabMainProp.Margin = new System.Windows.Forms.Padding(2);
	        this.tabMainProp.Name = "tabMainProp";
	        this.tabMainProp.SelectedIndex = 0;
	        this.tabMainProp.Size = new System.Drawing.Size(207, 214);
	        this.tabMainProp.TabIndex = 3;
	        // 
	        // tpgPropGen
	        // 
	        this.tpgPropGen.Controls.Add(this.tlpPropGen);
	        this.tpgPropGen.Location = new System.Drawing.Point(4, 22);
	        this.tpgPropGen.Margin = new System.Windows.Forms.Padding(2);
	        this.tpgPropGen.Name = "tpgPropGen";
	        this.tpgPropGen.Padding = new System.Windows.Forms.Padding(2);
	        this.tpgPropGen.Size = new System.Drawing.Size(199, 188);
	        this.tpgPropGen.TabIndex = 0;
	        this.tpgPropGen.Text = "Gen";
	        this.tpgPropGen.UseVisualStyleBackColor = true;
	        // 
	        // tlpPropGen
	        // 
	        this.tlpPropGen.ColumnCount = 2;
	        this.tlpPropGen.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
	        this.tlpPropGen.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 75F));
	        this.tlpPropGen.Controls.Add(this.lblPropGenPCat, 0, 0);
	        this.tlpPropGen.Controls.Add(this.lblPropGenDocNo, 0, 1);
	        this.tlpPropGen.Controls.Add(this.lblPropGenPartNo, 0, 2);
	        this.tlpPropGen.Controls.Add(this.lblPropGenDesc, 0, 3);
	        this.tlpPropGen.Controls.Add(this.lblPropGenCustNo, 0, 4);
	        this.tlpPropGen.Controls.Add(this.lblPropGenOpp, 0, 5);
	        this.tlpPropGen.Controls.Add(this.lblPropGenRev, 0, 6);
	        this.tlpPropGen.Controls.Add(this.lblPropGenCust, 0, 7);
	        this.tlpPropGen.Controls.Add(this.cmbPropGenPCat, 1, 0);
	        this.tlpPropGen.Controls.Add(this.txtPropGenDocNo, 1, 1);
	        this.tlpPropGen.Controls.Add(this.txtPropGenPartNo, 1, 2);
	        this.tlpPropGen.Controls.Add(this.txtPropGenDesc, 1, 3);
	        this.tlpPropGen.Controls.Add(this.txtPropGenCustNo, 1, 4);
	        this.tlpPropGen.Controls.Add(this.txtPropGenOpp, 1, 5);
	        this.tlpPropGen.Controls.Add(this.txtPropGenRev, 1, 6);
	        this.tlpPropGen.Controls.Add(this.txtPropGenCust, 1, 7);
	        this.tlpPropGen.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.tlpPropGen.Location = new System.Drawing.Point(2, 2);
	        this.tlpPropGen.Margin = new System.Windows.Forms.Padding(2);
	        this.tlpPropGen.Name = "tlpPropGen";
	        this.tlpPropGen.RowCount = 8;
	        this.tlpPropGen.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 12.5F));
	        this.tlpPropGen.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 12.5F));
	        this.tlpPropGen.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 12.5F));
	        this.tlpPropGen.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 12.5F));
	        this.tlpPropGen.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 12.5F));
	        this.tlpPropGen.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 12.5F));
	        this.tlpPropGen.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 12.5F));
	        this.tlpPropGen.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 12.5F));
	        this.tlpPropGen.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 16F));
	        this.tlpPropGen.Size = new System.Drawing.Size(195, 184);
	        this.tlpPropGen.TabIndex = 0;
	        // 
	        // lblPropGenPCat
	        // 
	        this.lblPropGenPCat.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lblPropGenPCat.Location = new System.Drawing.Point(2, 0);
	        this.lblPropGenPCat.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
	        this.lblPropGenPCat.Name = "lblPropGenPCat";
	        this.lblPropGenPCat.Size = new System.Drawing.Size(44, 23);
	        this.lblPropGenPCat.TabIndex = 0;
	        this.lblPropGenPCat.Text = "Cat:";
	        // 
	        // lblPropGenDocNo
	        // 
	        this.lblPropGenDocNo.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lblPropGenDocNo.Location = new System.Drawing.Point(2, 23);
	        this.lblPropGenDocNo.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
	        this.lblPropGenDocNo.Name = "lblPropGenDocNo";
	        this.lblPropGenDocNo.Size = new System.Drawing.Size(44, 23);
	        this.lblPropGenDocNo.TabIndex = 2;
	        this.lblPropGenDocNo.Text = "Doc No:";
	        // 
	        // lblPropGenPartNo
	        // 
	        this.lblPropGenPartNo.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lblPropGenPartNo.Location = new System.Drawing.Point(2, 46);
	        this.lblPropGenPartNo.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
	        this.lblPropGenPartNo.Name = "lblPropGenPartNo";
	        this.lblPropGenPartNo.Size = new System.Drawing.Size(44, 23);
	        this.lblPropGenPartNo.TabIndex = 4;
	        this.lblPropGenPartNo.Text = "Part No:";
	        // 
	        // lblPropGenDesc
	        // 
	        this.lblPropGenDesc.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lblPropGenDesc.Location = new System.Drawing.Point(2, 69);
	        this.lblPropGenDesc.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
	        this.lblPropGenDesc.Name = "lblPropGenDesc";
	        this.lblPropGenDesc.Size = new System.Drawing.Size(44, 23);
	        this.lblPropGenDesc.TabIndex = 6;
	        this.lblPropGenDesc.Text = "Desc:";
	        // 
	        // lblPropGenCustNo
	        // 
	        this.lblPropGenCustNo.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lblPropGenCustNo.Location = new System.Drawing.Point(2, 92);
	        this.lblPropGenCustNo.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
	        this.lblPropGenCustNo.Name = "lblPropGenCustNo";
	        this.lblPropGenCustNo.Size = new System.Drawing.Size(44, 23);
	        this.lblPropGenCustNo.TabIndex = 8;
	        this.lblPropGenCustNo.Text = "Cust No:";
	        // 
	        // lblPropGenOpp
	        // 
	        this.lblPropGenOpp.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lblPropGenOpp.Location = new System.Drawing.Point(2, 115);
	        this.lblPropGenOpp.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
	        this.lblPropGenOpp.Name = "lblPropGenOpp";
	        this.lblPropGenOpp.Size = new System.Drawing.Size(44, 23);
	        this.lblPropGenOpp.TabIndex = 10;
	        this.lblPropGenOpp.Text = "Opp No:";
	        // 
	        // lblPropGenRev
	        // 
	        this.lblPropGenRev.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lblPropGenRev.Location = new System.Drawing.Point(2, 138);
	        this.lblPropGenRev.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
	        this.lblPropGenRev.Name = "lblPropGenRev";
	        this.lblPropGenRev.Size = new System.Drawing.Size(44, 23);
	        this.lblPropGenRev.TabIndex = 12;
	        this.lblPropGenRev.Text = "Rev:";
	        // 
	        // lblPropGenCust
	        // 
	        this.lblPropGenCust.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lblPropGenCust.Location = new System.Drawing.Point(2, 161);
	        this.lblPropGenCust.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
	        this.lblPropGenCust.Name = "lblPropGenCust";
	        this.lblPropGenCust.Size = new System.Drawing.Size(44, 23);
	        this.lblPropGenCust.TabIndex = 14;
	        this.lblPropGenCust.Text = "Cust:";
	        // 
	        // txtPropGenDocNo
	        // 
	        this.txtPropGenDocNo.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.txtPropGenDocNo.Location = new System.Drawing.Point(50, 25);
	        this.txtPropGenDocNo.Margin = new System.Windows.Forms.Padding(2);
	        this.txtPropGenDocNo.Name = "txtPropGenDocNo";
	        this.txtPropGenDocNo.ReadOnly = true;
	        this.txtPropGenDocNo.Size = new System.Drawing.Size(143, 20);
	        this.txtPropGenDocNo.TabIndex = 3;
	        this.txtPropGenDocNo.Leave += new System.EventHandler(this.txtPropGenDocNo_Leave);
	        // 
	        // txtPropGenPartNo
	        // 
	        this.txtPropGenPartNo.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.txtPropGenPartNo.Location = new System.Drawing.Point(50, 48);
	        this.txtPropGenPartNo.Margin = new System.Windows.Forms.Padding(2);
	        this.txtPropGenPartNo.Name = "txtPropGenPartNo";
	        this.txtPropGenPartNo.ReadOnly = true;
	        this.txtPropGenPartNo.Size = new System.Drawing.Size(143, 20);
	        this.txtPropGenPartNo.TabIndex = 5;
	        this.txtPropGenPartNo.Leave += new System.EventHandler(this.txtPropGenPartNo_Leave);
	        // 
	        // txtPropGenDesc
	        // 
	        this.txtPropGenDesc.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.txtPropGenDesc.Location = new System.Drawing.Point(50, 71);
	        this.txtPropGenDesc.Margin = new System.Windows.Forms.Padding(2);
	        this.txtPropGenDesc.Name = "txtPropGenDesc";
	        this.txtPropGenDesc.ReadOnly = true;
	        this.txtPropGenDesc.Size = new System.Drawing.Size(143, 20);
	        this.txtPropGenDesc.TabIndex = 7;
	        this.txtPropGenDesc.Leave += new System.EventHandler(this.txtPropGenDesc_Leave);
	        // 
	        // txtPropGenCustNo
	        // 
	        this.txtPropGenCustNo.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.txtPropGenCustNo.Location = new System.Drawing.Point(50, 94);
	        this.txtPropGenCustNo.Margin = new System.Windows.Forms.Padding(2);
	        this.txtPropGenCustNo.Name = "txtPropGenCustNo";
	        this.txtPropGenCustNo.ReadOnly = true;
	        this.txtPropGenCustNo.Size = new System.Drawing.Size(143, 20);
	        this.txtPropGenCustNo.TabIndex = 9;
	        this.txtPropGenCustNo.Leave += new System.EventHandler(this.txtPropGenCustNo_Leave);
	        // 
	        // txtPropGenOpp
	        // 
	        this.txtPropGenOpp.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.txtPropGenOpp.Location = new System.Drawing.Point(50, 117);
	        this.txtPropGenOpp.Margin = new System.Windows.Forms.Padding(2);
	        this.txtPropGenOpp.Name = "txtPropGenOpp";
	        this.txtPropGenOpp.ReadOnly = true;
	        this.txtPropGenOpp.Size = new System.Drawing.Size(143, 20);
	        this.txtPropGenOpp.TabIndex = 11;
	        this.txtPropGenOpp.Leave += new System.EventHandler(this.txtPropGenOpp_Leave);
	        // 
	        // txtPropGenRev
	        // 
	        this.txtPropGenRev.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.txtPropGenRev.Location = new System.Drawing.Point(50, 140);
	        this.txtPropGenRev.Margin = new System.Windows.Forms.Padding(2);
	        this.txtPropGenRev.Name = "txtPropGenRev";
	        this.txtPropGenRev.ReadOnly = true;
	        this.txtPropGenRev.Size = new System.Drawing.Size(143, 20);
	        this.txtPropGenRev.TabIndex = 13;
	        // 
	        // txtPropGenCust
	        // 
	        this.txtPropGenCust.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.txtPropGenCust.Location = new System.Drawing.Point(50, 163);
	        this.txtPropGenCust.Margin = new System.Windows.Forms.Padding(2);
	        this.txtPropGenCust.Name = "txtPropGenCust";
	        this.txtPropGenCust.ReadOnly = true;
	        this.txtPropGenCust.Size = new System.Drawing.Size(143, 20);
	        this.txtPropGenCust.TabIndex = 15;
	        // 
	        // tpgPropCut
	        // 
	        this.tpgPropCut.Location = new System.Drawing.Point(4, 22);
	        this.tpgPropCut.Margin = new System.Windows.Forms.Padding(2);
	        this.tpgPropCut.Name = "tpgPropCut";
	        this.tpgPropCut.Padding = new System.Windows.Forms.Padding(2);
	        this.tpgPropCut.Size = new System.Drawing.Size(199, 188);
	        this.tpgPropCut.TabIndex = 1;
	        this.tpgPropCut.Text = "Cut";
	        this.tpgPropCut.UseVisualStyleBackColor = true;
	        // 
	        // tpgPropCust
	        // 
	        this.tpgPropCust.Controls.Add(this.pnlPropCustomer);
	        this.tpgPropCust.Location = new System.Drawing.Point(4, 22);
	        this.tpgPropCust.Margin = new System.Windows.Forms.Padding(2);
	        this.tpgPropCust.Name = "tpgPropCust";
	        this.tpgPropCust.Padding = new System.Windows.Forms.Padding(2);
	        this.tpgPropCust.Size = new System.Drawing.Size(199, 188);
	        this.tpgPropCust.TabIndex = 2;
	        this.tpgPropCust.Text = "Cust";
	        this.tpgPropCust.UseVisualStyleBackColor = true;
	        // 
	        // pnlPropCustomer
	        // 
	        this.pnlPropCustomer.AutoScroll = true;
	        this.pnlPropCustomer.AutoSize = true;
	        this.pnlPropCustomer.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.pnlPropCustomer.Location = new System.Drawing.Point(2, 2);
	        this.pnlPropCustomer.Margin = new System.Windows.Forms.Padding(2);
	        this.pnlPropCustomer.Name = "pnlPropCustomer";
	        this.pnlPropCustomer.Size = new System.Drawing.Size(195, 184);
	        this.pnlPropCustomer.TabIndex = 0;
	        // 
	        // tpgPropCmt
	        // 
	        this.tpgPropCmt.Controls.Add(this.tlpPropCmts);
	        this.tpgPropCmt.Location = new System.Drawing.Point(4, 22);
	        this.tpgPropCmt.Margin = new System.Windows.Forms.Padding(2);
	        this.tpgPropCmt.Name = "tpgPropCmt";
	        this.tpgPropCmt.Padding = new System.Windows.Forms.Padding(2);
	        this.tpgPropCmt.Size = new System.Drawing.Size(199, 188);
	        this.tpgPropCmt.TabIndex = 5;
	        this.tpgPropCmt.Text = "Cmmts";
	        this.tpgPropCmt.UseVisualStyleBackColor = true;
	        // 
	        // tlpPropCmts
	        // 
	        this.tlpPropCmts.ColumnCount = 2;
	        this.tlpPropCmts.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 80F));
	        this.tlpPropCmts.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20F));
	        this.tlpPropCmts.Controls.Add(this.txtPropCmts, 0, 0);
	        this.tlpPropCmts.Controls.Add(this.txtPropCmtNew, 0, 1);
	        this.tlpPropCmts.Controls.Add(this.btnPropCmtAdd, 1, 1);
	        this.tlpPropCmts.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.tlpPropCmts.Location = new System.Drawing.Point(2, 2);
	        this.tlpPropCmts.Margin = new System.Windows.Forms.Padding(2);
	        this.tlpPropCmts.Name = "tlpPropCmts";
	        this.tlpPropCmts.RowCount = 2;
	        this.tlpPropCmts.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 70F));
	        this.tlpPropCmts.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 30F));
	        this.tlpPropCmts.Size = new System.Drawing.Size(195, 184);
	        this.tlpPropCmts.TabIndex = 0;
	        // 
	        // txtPropCmts
	        // 
	        this.tlpPropCmts.SetColumnSpan(this.txtPropCmts, 2);
	        this.txtPropCmts.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.txtPropCmts.Location = new System.Drawing.Point(2, 2);
	        this.txtPropCmts.Margin = new System.Windows.Forms.Padding(2);
	        this.txtPropCmts.Multiline = true;
	        this.txtPropCmts.Name = "txtPropCmts";
	        this.txtPropCmts.ReadOnly = true;
	        this.txtPropCmts.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
	        this.txtPropCmts.Size = new System.Drawing.Size(191, 124);
	        this.txtPropCmts.TabIndex = 0;
	        // 
	        // txtPropCmtNew
	        // 
	        this.txtPropCmtNew.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.txtPropCmtNew.Location = new System.Drawing.Point(2, 130);
	        this.txtPropCmtNew.Margin = new System.Windows.Forms.Padding(2);
	        this.txtPropCmtNew.Multiline = true;
	        this.txtPropCmtNew.Name = "txtPropCmtNew";
	        this.txtPropCmtNew.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
	        this.txtPropCmtNew.Size = new System.Drawing.Size(152, 52);
	        this.txtPropCmtNew.TabIndex = 1;
	        // 
	        // btnPropCmtAdd
	        // 
	        this.btnPropCmtAdd.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.btnPropCmtAdd.Location = new System.Drawing.Point(158, 130);
	        this.btnPropCmtAdd.Margin = new System.Windows.Forms.Padding(2);
	        this.btnPropCmtAdd.Name = "btnPropCmtAdd";
	        this.btnPropCmtAdd.Size = new System.Drawing.Size(35, 52);
	        this.btnPropCmtAdd.TabIndex = 2;
	        this.btnPropCmtAdd.Text = "Add New";
	        this.btnPropCmtAdd.UseVisualStyleBackColor = true;
	        this.btnPropCmtAdd.Click += new System.EventHandler(this.btnPropCmtAdd_Click);
	        // 
	        // tpgPropOrd
	        // 
	        this.tpgPropOrd.Controls.Add(this.lstPropOrd);
	        this.tpgPropOrd.Location = new System.Drawing.Point(4, 22);
	        this.tpgPropOrd.Margin = new System.Windows.Forms.Padding(2);
	        this.tpgPropOrd.Name = "tpgPropOrd";
	        this.tpgPropOrd.Padding = new System.Windows.Forms.Padding(2);
	        this.tpgPropOrd.Size = new System.Drawing.Size(199, 188);
	        this.tpgPropOrd.TabIndex = 4;
	        this.tpgPropOrd.Text = "Use";
	        this.tpgPropOrd.UseVisualStyleBackColor = true;
	        // 
	        // lstPropOrd
	        // 
	        this.lstPropOrd.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] { this.colPropOrd1, this.colPropOrd2, this.colPropOrd3, this.colPropOrd4, this.colPropOrd5, this.colPropOrd6, this.colPropOrd7, this.colPropOrd8 });
	        this.lstPropOrd.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lstPropOrd.HideSelection = false;
	        this.lstPropOrd.Location = new System.Drawing.Point(2, 2);
	        this.lstPropOrd.Margin = new System.Windows.Forms.Padding(2);
	        this.lstPropOrd.Name = "lstPropOrd";
	        this.lstPropOrd.Size = new System.Drawing.Size(195, 184);
	        this.lstPropOrd.TabIndex = 1;
	        this.lstPropOrd.UseCompatibleStateImageBehavior = false;
	        this.lstPropOrd.View = System.Windows.Forms.View.Details;
	        // 
	        // tlpPropCmds
	        // 
	        this.tlpPropCmds.ColumnCount = 9;
	        this.tlpPropCmds.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 12.4F));
	        this.tlpPropCmds.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 12.4F));
	        this.tlpPropCmds.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 12.4F));
	        this.tlpPropCmds.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 12.4F));
	        this.tlpPropCmds.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 12.4F));
	        this.tlpPropCmds.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 12.4F));
	        this.tlpPropCmds.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 12.4F));
	        this.tlpPropCmds.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 12.4F));
	        this.tlpPropCmds.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 0.8F));
	        this.tlpPropCmds.Controls.Add(this.chkPropSearch, 3, 0);
	        this.tlpPropCmds.Controls.Add(this.btnPropSave, 0, 0);
	        this.tlpPropCmds.Controls.Add(this.btnPropReset, 1, 0);
	        this.tlpPropCmds.Controls.Add(this.chkPropAll, 2, 0);
	        this.tlpPropCmds.Controls.Add(this.btnPropMdlSave, 4, 0);
	        this.tlpPropCmds.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.tlpPropCmds.Location = new System.Drawing.Point(2, 2);
	        this.tlpPropCmds.Margin = new System.Windows.Forms.Padding(2);
	        this.tlpPropCmds.Name = "tlpPropCmds";
	        this.tlpPropCmds.RowCount = 1;
	        this.tlpPropCmds.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
	        this.tlpPropCmds.Size = new System.Drawing.Size(207, 27);
	        this.tlpPropCmds.TabIndex = 1;
	        // 
	        // cplMainTls
	        // 
	        this.cplMainTls.AutoScroll = true;
	        // 
	        // cplMainTls.Content
	        // 
	        this.cplMainTls.Content.AutoSize = true;
	        this.cplMainTls.Content.Controls.Add(this.tlpMainTls);
	        this.cplMainTls.Content.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.cplMainTls.Content.Location = new System.Drawing.Point(5, 24);
	        this.cplMainTls.Content.Margin = new System.Windows.Forms.Padding(4);
	        this.cplMainTls.Content.Name = "Content";
	        this.cplMainTls.Content.Size = new System.Drawing.Size(211, 54);
	        this.cplMainTls.Content.TabIndex = 1;
	        this.cplMainTls.Dock = System.Windows.Forms.DockStyle.Top;
	        this.cplMainTls.Location = new System.Drawing.Point(0, 225);
	        this.cplMainTls.Margin = new System.Windows.Forms.Padding(4);
	        this.cplMainTls.MaximumSize = new System.Drawing.Size(750, 812);
	        this.cplMainTls.MinimumSize = new System.Drawing.Size(90, 18);
	        this.cplMainTls.Name = "cplMainTls";
	        this.cplMainTls.Size = new System.Drawing.Size(223, 85);
	        this.cplMainTls.TabIndex = 1;
	        this.cplMainTls.Title = "Tools";
	        // 
	        // tlpMainTls
	        // 
	        this.tlpMainTls.AutoSize = true;
	        this.tlpMainTls.ColumnCount = 9;
	        this.tlpMainTls.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 12.4F));
	        this.tlpMainTls.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 12.4F));
	        this.tlpMainTls.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 12.4F));
	        this.tlpMainTls.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 12.4F));
	        this.tlpMainTls.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 12.4F));
	        this.tlpMainTls.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 12.4F));
	        this.tlpMainTls.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 12.4F));
	        this.tlpMainTls.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 12.4F));
	        this.tlpMainTls.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 0.8F));
	        this.tlpMainTls.Controls.Add(this.btnTlsTaskQ, 5, 1);
	        this.tlpMainTls.Controls.Add(this.btnTlsChkIn, 0, 0);
	        this.tlpMainTls.Controls.Add(this.btnTlsSerial, 1, 0);
	        this.tlpMainTls.Controls.Add(this.btnTlsFinSch, 2, 0);
	        this.tlpMainTls.Controls.Add(this.btnTlsThru, 3, 0);
	        this.tlpMainTls.Controls.Add(this.btnTlsLights, 4, 0);
	        this.tlpMainTls.Controls.Add(this.btnTlsAppear, 5, 0);
	        this.tlpMainTls.Controls.Add(this.btnTlsFix, 6, 0);
	        this.tlpMainTls.Controls.Add(this.btnTlsCard, 7, 0);
	        this.tlpMainTls.Controls.Add(this.btnTlsFinSearch, 0, 1);
	        this.tlpMainTls.Controls.Add(this.btnTlsCopyDwg, 1, 1);
	        this.tlpMainTls.Controls.Add(this.btnTlsShowHidden, 2, 1);
	        this.tlpMainTls.Controls.Add(this.btnTlsDwgNum, 3, 1);
	        this.tlpMainTls.Controls.Add(this.btnTlsViki, 4, 1);
	        this.tlpMainTls.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.tlpMainTls.Location = new System.Drawing.Point(0, 0);
	        this.tlpMainTls.Name = "tlpMainTls";
	        this.tlpMainTls.RowCount = 2;
	        this.tlpMainTls.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
	        this.tlpMainTls.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
	        this.tlpMainTls.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
	        this.tlpMainTls.Size = new System.Drawing.Size(211, 54);
	        this.tlpMainTls.TabIndex = 0;
	        // 
	        // btnTlsTaskQ
	        // 
	        this.btnTlsTaskQ.AutoSize = true;
	        this.btnTlsTaskQ.BackColor = System.Drawing.Color.Transparent;
	        this.btnTlsTaskQ.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.btnTlsTaskQ.FlatAppearance.BorderSize = 0;
	        this.btnTlsTaskQ.FlatAppearance.MouseDownBackColor = System.Drawing.SystemColors.ButtonFace;
	        this.btnTlsTaskQ.FlatAppearance.MouseOverBackColor = System.Drawing.SystemColors.ButtonHighlight;
	        this.btnTlsTaskQ.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
	        this.btnTlsTaskQ.Image = global::BlueBrick.Resource1.icon_TlsQueue;
	        this.btnTlsTaskQ.Location = new System.Drawing.Point(130, 27);
	        this.btnTlsTaskQ.Margin = new System.Windows.Forms.Padding(0);
	        this.btnTlsTaskQ.Name = "btnTlsTaskQ";
	        this.btnTlsTaskQ.Size = new System.Drawing.Size(26, 27);
	        this.btnTlsTaskQ.TabIndex = 13;
	        this.tipTool.SetToolTip(this.btnTlsTaskQ, "Check PDM server task queue");
	        this.btnTlsTaskQ.UseVisualStyleBackColor = true;
	        this.btnTlsTaskQ.Click += new System.EventHandler(this.btnTlsTaskQ_Click);
	        // 
	        // btnTlsDwgNum
	        // 
	        this.btnTlsDwgNum.AutoSize = true;
	        this.btnTlsDwgNum.BackColor = System.Drawing.Color.Transparent;
	        this.btnTlsDwgNum.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.btnTlsDwgNum.FlatAppearance.BorderSize = 0;
	        this.btnTlsDwgNum.FlatAppearance.MouseDownBackColor = System.Drawing.SystemColors.ButtonFace;
	        this.btnTlsDwgNum.FlatAppearance.MouseOverBackColor = System.Drawing.SystemColors.ButtonHighlight;
	        this.btnTlsDwgNum.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
	        this.btnTlsDwgNum.Image = global::BlueBrick.Resource1.icon_TlsDrwRen;
	        this.btnTlsDwgNum.Location = new System.Drawing.Point(78, 27);
	        this.btnTlsDwgNum.Margin = new System.Windows.Forms.Padding(0);
	        this.btnTlsDwgNum.Name = "btnTlsDwgNum";
	        this.btnTlsDwgNum.Size = new System.Drawing.Size(26, 27);
	        this.btnTlsDwgNum.TabIndex = 11;
	        this.tipTool.SetToolTip(this.btnTlsDwgNum, "Re-number drawing sheets");
	        this.btnTlsDwgNum.UseVisualStyleBackColor = true;
	        this.btnTlsDwgNum.Click += new System.EventHandler(this.btnTlsDwgNum_Click);
	        // 
	        // lblMainStat
	        // 
	        this.lblMainStat.BackColor = System.Drawing.SystemColors.ControlDark;
	        this.lblMainStat.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.lblMainStat.Location = new System.Drawing.Point(1, 1);
	        this.lblMainStat.Margin = new System.Windows.Forms.Padding(0);
	        this.lblMainStat.Name = "lblMainStat";
	        this.lblMainStat.Size = new System.Drawing.Size(211, 23);
	        this.lblMainStat.TabIndex = 1;
	        this.lblMainStat.Text = "Status Output";
	        this.lblMainStat.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
	        // 
	        // pnlMain
	        // 
	        this.pnlMain.AutoScroll = true;
	        this.pnlMain.AutoSize = true;
	        this.pnlMain.Controls.Add(this.cplMainMisc);
	        this.pnlMain.Controls.Add(this.cplMainOpp);
	        this.pnlMain.Controls.Add(this.cplMainSfOpp);
	        this.pnlMain.Controls.Add(this.cplMainEPS);
	        this.pnlMain.Controls.Add(this.cplMainLky);
	        this.pnlMain.Controls.Add(this.cplMainProp);
	        this.pnlMain.Controls.Add(this.cplMainTls);
	        this.pnlMain.Controls.Add(this.cplMainGen);
	        this.pnlMain.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.pnlMain.Location = new System.Drawing.Point(2, 2);
	        this.pnlMain.Margin = new System.Windows.Forms.Padding(2);
	        this.pnlMain.Name = "pnlMain";
	        this.pnlMain.Size = new System.Drawing.Size(240, 605);
	        this.pnlMain.TabIndex = 0;
	        // 
	        // prgStatus
	        // 
	        this.tlpMainStat.SetColumnSpan(this.prgStatus, 2);
	        this.prgStatus.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.prgStatus.Location = new System.Drawing.Point(2, 181);
	        this.prgStatus.Margin = new System.Windows.Forms.Padding(1);
	        this.prgStatus.Name = "prgStatus";
	        this.prgStatus.Size = new System.Drawing.Size(234, 14);
	        this.prgStatus.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
	        this.prgStatus.TabIndex = 0;
	        // 
	        // tipTool
	        // 
	        this.tipTool.ShowAlways = true;
	        // 
	        // tlpMain
	        // 
	        this.tlpMain.ColumnCount = 1;
	        this.tlpMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
	        this.tlpMain.Controls.Add(this.pnlMain, 0, 0);
	        this.tlpMain.Controls.Add(this.tlpMainStat, 0, 1);
	        this.tlpMain.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.tlpMain.Location = new System.Drawing.Point(0, 0);
	        this.tlpMain.Name = "tlpMain";
	        this.tlpMain.RowCount = 2;
	        this.tlpMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
	        this.tlpMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 203F));
	        this.tlpMain.Size = new System.Drawing.Size(244, 812);
	        this.tlpMain.TabIndex = 0;
	        // 
	        // tlpMainStat
	        // 
	        this.tlpMainStat.BackColor = System.Drawing.SystemColors.Control;
	        this.tlpMainStat.CellBorderStyle = System.Windows.Forms.TableLayoutPanelCellBorderStyle.Single;
	        this.tlpMainStat.ColumnCount = 2;
	        this.tlpMainStat.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 90F));
	        this.tlpMainStat.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 10F));
	        this.tlpMainStat.Controls.Add(this.prgStatus, 0, 2);
	        this.tlpMainStat.Controls.Add(this.lblMainStat, 0, 0);
	        this.tlpMainStat.Controls.Add(this.txtStatus, 0, 1);
	        this.tlpMainStat.Controls.Add(this.btnOpt, 1, 0);
	        this.tlpMainStat.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.tlpMainStat.Location = new System.Drawing.Point(3, 612);
	        this.tlpMainStat.Name = "tlpMainStat";
	        this.tlpMainStat.RowCount = 3;
	        this.tlpMainStat.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 12F));
	        this.tlpMainStat.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 80F));
	        this.tlpMainStat.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 8F));
	        this.tlpMainStat.Size = new System.Drawing.Size(238, 197);
	        this.tlpMainStat.TabIndex = 1;
	        // 
	        // txtStatus
	        // 
	        this.txtStatus.BackColor = System.Drawing.SystemColors.ControlDarkDark;
	        this.tlpMainStat.SetColumnSpan(this.txtStatus, 2);
	        this.txtStatus.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.txtStatus.ForeColor = System.Drawing.Color.Lime;
	        this.txtStatus.Location = new System.Drawing.Point(4, 28);
	        this.txtStatus.Multiline = true;
	        this.txtStatus.Name = "txtStatus";
	        this.txtStatus.ReadOnly = true;
	        this.txtStatus.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
	        this.txtStatus.Size = new System.Drawing.Size(230, 148);
	        this.txtStatus.TabIndex = 2;
	        // 
	        // btnOpt
	        // 
	        this.btnOpt.Dock = System.Windows.Forms.DockStyle.Fill;
	        this.btnOpt.Font = new System.Drawing.Font("Segoe UI Emoji", 7.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
	        this.btnOpt.Location = new System.Drawing.Point(213, 1);
	        this.btnOpt.Margin = new System.Windows.Forms.Padding(0);
	        this.btnOpt.Name = "btnOpt";
	        this.btnOpt.Size = new System.Drawing.Size(24, 23);
	        this.btnOpt.TabIndex = 3;
	        this.btnOpt.Text = "⚙️";
	        this.btnOpt.UseVisualStyleBackColor = true;
	        this.btnOpt.Click += new System.EventHandler(this.btnOpt_Click);
	        // 
	        // FrmPane
	        // 
	        this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
	        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
	        this.AutoScroll = true;
	        this.AutoSize = true;
	        this.ClientSize = new System.Drawing.Size(244, 812);
	        this.ControlBox = false;
	        this.Controls.Add(this.tlpMain);
	        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
	        this.KeyPreview = true;
	        this.Margin = new System.Windows.Forms.Padding(2);
	        this.Name = "FrmPane";
	        this.Text = "FrmPane";
	        this.TopMost = true;
	        this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.FrmPane_KeyDown);
	        this.cmsLucky.ResumeLayout(false);
	        this.cmsRClick.ResumeLayout(false);
	        this.cplMainEPS.Content.ResumeLayout(false);
	        this.cplMainEPS.Content.PerformLayout();
	        this.tlpMainEPS.ResumeLayout(false);
	        this.tlpMainEPS.PerformLayout();
	        this.cplMainGen.Content.ResumeLayout(false);
	        this.cplMainGen.Content.PerformLayout();
	        this.tlpMainGen.ResumeLayout(false);
	        this.tlpMainGen.PerformLayout();
	        this.grpGenPre.ResumeLayout(false);
	        this.grpGenPre.PerformLayout();
	        this.tlpGenPre.ResumeLayout(false);
	        this.tlpGenPre.PerformLayout();
	        this.grpGenTyp.ResumeLayout(false);
	        this.grpGenTyp.PerformLayout();
	        this.tlpGenTyp.ResumeLayout(false);
	        this.tlpGenTyp.PerformLayout();
	        this.grpGenScope.ResumeLayout(false);
	        this.grpGenScope.PerformLayout();
	        this.tlpGenScope.ResumeLayout(false);
	        this.tlpGenScope.PerformLayout();
	        this.grpGenLoc.ResumeLayout(false);
	        this.grpGenLoc.PerformLayout();
	        this.tlpGenLoc.ResumeLayout(false);
	        this.tlpGenLoc.PerformLayout();
	        this.cplMainLky.Content.ResumeLayout(false);
	        this.cplMainLky.Content.PerformLayout();
	        this.tlpMainLky.ResumeLayout(false);
	        this.tlpMainLky.PerformLayout();
	        this.cplMainMisc.Content.ResumeLayout(false);
	        this.cplMainMisc.Content.PerformLayout();
	        this.tlpMainMisc.ResumeLayout(false);
	        this.tabMainConv.ResumeLayout(false);
	        this.tpgConvUnit.ResumeLayout(false);
	        this.tpgConvUnit.PerformLayout();
	        this.tlpConvUnit.ResumeLayout(false);
	        this.tlpConvUnit.PerformLayout();
	        this.tpgConvElec.ResumeLayout(false);
	        this.tpgConvElec.PerformLayout();
	        this.tlpConvEle.ResumeLayout(false);
	        this.tlpConvEle.PerformLayout();
	        this.tpgConvRoll.ResumeLayout(false);
	        this.tpgConvRoll.PerformLayout();
	        this.tlpConvRl.ResumeLayout(false);
	        this.tlpConvRl.PerformLayout();
	        ((System.ComponentModel.ISupportInitialize)(this.picConvRlRoll)).EndInit();
	        this.tpgConvBFoot.ResumeLayout(false);
	        this.tpgConvBFoot.PerformLayout();
	        this.tlpConvBf.ResumeLayout(false);
	        this.tlpConvBf.PerformLayout();
	        this.cplMainOpp.Content.ResumeLayout(false);
	        this.cplMainOpp.Content.PerformLayout();
	        this.tlpMainOpp.ResumeLayout(false);
	        this.tlpMainOpp.PerformLayout();
	        this.cmsOpAttach.ResumeLayout(false);
	        this.cplMainSfOpp.Content.ResumeLayout(false);
	        this.tlpSfMain.ResumeLayout(false);
	        this.tlpSfMain.PerformLayout();
	        this.tlpSfUserInfo.ResumeLayout(false);
	        ((System.ComponentModel.ISupportInitialize)(this.picSfUser)).EndInit();
	        this.tlpMainSfOpp.ResumeLayout(false);
	        this.tlpMainSfOpp.PerformLayout();
	        this.cmsSfOpAttach.ResumeLayout(false);
	        this.cplMainProp.Content.ResumeLayout(false);
	        this.tlpMainProp.ResumeLayout(false);
	        this.tabMainProp.ResumeLayout(false);
	        this.tpgPropGen.ResumeLayout(false);
	        this.tlpPropGen.ResumeLayout(false);
	        this.tlpPropGen.PerformLayout();
	        this.tpgPropCust.ResumeLayout(false);
	        this.tpgPropCust.PerformLayout();
	        this.tpgPropCmt.ResumeLayout(false);
	        this.tlpPropCmts.ResumeLayout(false);
	        this.tlpPropCmts.PerformLayout();
	        this.tpgPropOrd.ResumeLayout(false);
	        this.tlpPropCmds.ResumeLayout(false);
	        this.tlpPropCmds.PerformLayout();
	        this.cplMainTls.Content.ResumeLayout(false);
	        this.cplMainTls.Content.PerformLayout();
	        this.tlpMainTls.ResumeLayout(false);
	        this.tlpMainTls.PerformLayout();
	        this.pnlMain.ResumeLayout(false);
	        this.tlpMain.ResumeLayout(false);
	        this.tlpMain.PerformLayout();
	        this.tlpMainStat.ResumeLayout(false);
	        this.tlpMainStat.PerformLayout();
	        this.ResumeLayout(false);
        }

        private System.Windows.Forms.Label lblSfEmail;

        private System.Windows.Forms.Button btnSfLogin;

        private System.Windows.Forms.PictureBox picSfUser;

        private System.Windows.Forms.Label lblSfUserName;

        private System.Windows.Forms.TableLayoutPanel tlpSfUserInfo;

        private System.Windows.Forms.TableLayoutPanel tlpSfMain;

        private BlueBrick.CollapsePanel cplMainEPS;
        private BlueBrick.CollapsePanel cplMainGen;
        private BlueBrick.CollapsePanel cplMainLky;
        private BlueBrick.CollapsePanel cplMainMisc;
        private BlueBrick.CollapsePanel cplMainOpp;
        private BlueBrick.CollapsePanel cplMainProp;
        private BlueBrick.CollapsePanel cplMainSfOpp;
        private BlueBrick.CollapsePanel cplMainTls;
        private System.Windows.Forms.Button btnConvBfAdd;
        private System.Windows.Forms.Button btnConvBfClr;
        private System.Windows.Forms.Button btnConvBfDel;
        private System.Windows.Forms.Button btnConvEleClr;
        private System.Windows.Forms.Button btnConvRlCalc;
        private System.Windows.Forms.Button btnConvRlReset;
        private System.Windows.Forms.Button btnConvUnitCalc;
        private System.Windows.Forms.Button btnConvUnitSwap;
        private System.Windows.Forms.Button btnEOSEmail;
        private System.Windows.Forms.Button btnEOSOpp;
        private System.Windows.Forms.Button btnEOSUser;
        private System.Windows.Forms.Button btnEPSClear;
        private System.Windows.Forms.Button btnEPSSearch;
        private System.Windows.Forms.Button btnGenLocExe;
        private System.Windows.Forms.Button btnGenLocPath;
        private System.Windows.Forms.Button btnGenPreCol;
        private System.Windows.Forms.Button btnGenPreDXF;
        private System.Windows.Forms.Button btnGenPreDXFAll;
        private System.Windows.Forms.Button btnGenPreFull;
        private System.Windows.Forms.Button btnGenPrePDF;
        private System.Windows.Forms.Button btnGenPrePNG;
        private System.Windows.Forms.Button btnGenPreSTP;
        private System.Windows.Forms.Button btnGenPreSTPAll;
        private System.Windows.Forms.Button btnLkySrch;
        private System.Windows.Forms.Button btnOpt;
        private System.Windows.Forms.Button btnPropCmtAdd;
        private System.Windows.Forms.Button btnPropMdlSave;
        private System.Windows.Forms.Button btnPropReset;
        private System.Windows.Forms.Button btnPropSave;
        private System.Windows.Forms.Button btnSFOSEmail;
        private System.Windows.Forms.Button btnSFOSOpp;
        private System.Windows.Forms.Button btnSFOSUser;
        private System.Windows.Forms.Button btnTlsAppear;
        private System.Windows.Forms.Button btnTlsCard;
        private System.Windows.Forms.Button btnTlsChkIn;
        private System.Windows.Forms.Button btnTlsCopyDwg;
        private System.Windows.Forms.Button btnTlsDwgNum;
        private System.Windows.Forms.Button btnTlsFinSch;
        private System.Windows.Forms.Button btnTlsFinSearch;
        private System.Windows.Forms.Button btnTlsFix;
        private System.Windows.Forms.Button btnTlsLights;
        private System.Windows.Forms.Button btnTlsSerial;
        private System.Windows.Forms.Button btnTlsShowHidden;
        private System.Windows.Forms.Button btnTlsTaskQ;
        private System.Windows.Forms.Button btnTlsThru;
        private System.Windows.Forms.Button btnTlsViki;
        private System.Windows.Forms.CheckBox chkGenTypDXF;
        private System.Windows.Forms.CheckBox chkGenTypIGES;
        private System.Windows.Forms.CheckBox chkGenTypPDF;
        private System.Windows.Forms.CheckBox chkGenTypPNG;
        private System.Windows.Forms.CheckBox chkGenTypPkt;
        private System.Windows.Forms.CheckBox chkGenTypSTP;
        private System.Windows.Forms.CheckBox chkPropAll;
        private System.Windows.Forms.CheckBox chkPropSearch;
        private System.Windows.Forms.ColumnHeader colConvBfDims;
        private System.Windows.Forms.ColumnHeader colConvBfQty;
        private System.Windows.Forms.ColumnHeader colEOSFile;
        private System.Windows.Forms.ColumnHeader colEPSResultsAlloc;
        private System.Windows.Forms.ColumnHeader colEPSResultsDesc;
        private System.Windows.Forms.ColumnHeader colEPSResultsOH;
        private System.Windows.Forms.ColumnHeader colEPSResultsPart;
        private System.Windows.Forms.ColumnHeader colEPSResultsUM;
        private System.Windows.Forms.ColumnHeader colLkyResults1;
        private System.Windows.Forms.ColumnHeader colLkyResults2;
        private System.Windows.Forms.ColumnHeader colLkyResults3;
        private System.Windows.Forms.ColumnHeader colPropOrd1;
        private System.Windows.Forms.ColumnHeader colPropOrd2;
        private System.Windows.Forms.ColumnHeader colPropOrd3;
        private System.Windows.Forms.ColumnHeader colPropOrd4;
        private System.Windows.Forms.ColumnHeader colPropOrd5;
        private System.Windows.Forms.ColumnHeader colPropOrd6;
        private System.Windows.Forms.ColumnHeader colPropOrd7;
        private System.Windows.Forms.ColumnHeader colPropOrd8;
        private System.Windows.Forms.ColumnHeader colSFOSFile;
        private System.Windows.Forms.ComboBox cmbConvRlUnitFrom;
        private System.Windows.Forms.ComboBox cmbConvRlUnitTo;
        private System.Windows.Forms.ComboBox cmbConvUnitFrom;
        private System.Windows.Forms.ComboBox cmbConvUnitTo;
        private System.Windows.Forms.ComboBox cmbConvUnitTypes;
        private System.Windows.Forms.ComboBox cmbEOSMemoSel;
        private System.Windows.Forms.ComboBox cmbLkySrch;
        private System.Windows.Forms.ComboBox cmbPropGenPCat;
        private System.Windows.Forms.ComboBox cmbSFOSMemoSel;
        private System.Windows.Forms.ContextMenuStrip cmsLucky;
        private System.Windows.Forms.ContextMenuStrip cmsOpAttach;
        private System.Windows.Forms.ContextMenuStrip cmsRClick;
        private System.Windows.Forms.ContextMenuStrip cmsSfOpAttach;
        private System.Windows.Forms.FolderBrowserDialog fldrDiagOpen;
        private System.Windows.Forms.GroupBox grpGenLoc;
        private System.Windows.Forms.GroupBox grpGenPre;
        private System.Windows.Forms.GroupBox grpGenScope;
        private System.Windows.Forms.GroupBox grpGenTyp;
        internal System.Windows.Forms.ImageList imlThumbs;
        private System.Windows.Forms.Label lblConvBfLen;
        private System.Windows.Forms.Label lblConvBfMemo;
        private System.Windows.Forms.Label lblConvBfQty;
        private System.Windows.Forms.Label lblConvBfThk;
        private System.Windows.Forms.Label lblConvBfTot;
        private System.Windows.Forms.Label lblConvBfWid;
        private System.Windows.Forms.Label lblConvEleAmp;
        private System.Windows.Forms.Label lblConvEleMemo;
        private System.Windows.Forms.Label lblConvEleOhm;
        private System.Windows.Forms.Label lblConvEleVolt;
        private System.Windows.Forms.Label lblConvEleW50;
        private System.Windows.Forms.Label lblConvEleW80;
        private System.Windows.Forms.Label lblConvEleWatt;
        private System.Windows.Forms.Label lblConvRlCore;
        private System.Windows.Forms.Label lblConvRlID;
        private System.Windows.Forms.Label lblConvRlMat;
        private System.Windows.Forms.Label lblConvRlOD;
        private System.Windows.Forms.Label lblConvRlTotal;
        private System.Windows.Forms.Label lblConvRlUnitFrom;
        private System.Windows.Forms.Label lblConvUnitFDesc;
        private System.Windows.Forms.Label lblConvUnitFSym;
        private System.Windows.Forms.Label lblConvUnitTDesc;
        private System.Windows.Forms.Label lblConvUnitTSym;
        private System.Windows.Forms.Label lblConvUnits;
        private System.Windows.Forms.Label lblEOSAttach;
        private System.Windows.Forms.Label lblEOSCmt;
        private System.Windows.Forms.Label lblEOSCust;
        private System.Windows.Forms.Label lblEOSDesc;
        private System.Windows.Forms.Label lblEOSDue;
        private System.Windows.Forms.Label lblEOSMemo;
        private System.Windows.Forms.Label lblMainStat;
        private System.Windows.Forms.Label lblPropGenCust;
        private System.Windows.Forms.Label lblPropGenCustNo;
        private System.Windows.Forms.Label lblPropGenDesc;
        private System.Windows.Forms.Label lblPropGenDocNo;
        private System.Windows.Forms.Label lblPropGenOpp;
        private System.Windows.Forms.Label lblPropGenPCat;
        private System.Windows.Forms.Label lblPropGenPartNo;
        private System.Windows.Forms.Label lblPropGenRev;
        private System.Windows.Forms.Label lblSFOSAttach;
        private System.Windows.Forms.Label lblSFOSCust;
        private System.Windows.Forms.Label lblSFOSDesc;
        private System.Windows.Forms.Label lblSFOSDue;
        private System.Windows.Forms.Label lblSFOSTask;
        private System.Windows.Forms.ListView lstEOSAttach;
        internal System.Windows.Forms.ListView lstEPSResults;
        internal System.Windows.Forms.ListView lstLkyResults;
        private System.Windows.Forms.ListView lstPropOrd;
        private System.Windows.Forms.ListView lstSFOSAttach;
        private System.Windows.Forms.ListView lvwConvBf;
        private System.Windows.Forms.Panel pnlMain;
        private System.Windows.Forms.Panel pnlPropCustomer;
        private System.Windows.Forms.PictureBox picConvRlRoll;
        private System.Windows.Forms.ProgressBar prgStatus;
        private System.Windows.Forms.RadioButton radGenLocDesk;
        private System.Windows.Forms.RadioButton radGenLocPDM;
        private System.Windows.Forms.RadioButton radGenLocUser;
        private System.Windows.Forms.RadioButton radGenScopeAll;
        private System.Windows.Forms.RadioButton radGenScopeSingle;
        private System.Windows.Forms.TabControl tabMainConv;
        private System.Windows.Forms.TabControl tabMainProp;
        private System.Windows.Forms.TabPage tpgConvBFoot;
        private System.Windows.Forms.TabPage tpgConvElec;
        private System.Windows.Forms.TabPage tpgConvRoll;
        private System.Windows.Forms.TabPage tpgConvUnit;
        private System.Windows.Forms.TabPage tpgPropCmt;
        private System.Windows.Forms.TabPage tpgPropCust;
        private System.Windows.Forms.TabPage tpgPropCut;
        private System.Windows.Forms.TabPage tpgPropGen;
        private System.Windows.Forms.TabPage tpgPropOrd;
        private System.Windows.Forms.TableLayoutPanel tlpConvBf;
        private System.Windows.Forms.TableLayoutPanel tlpConvEle;
        private System.Windows.Forms.TableLayoutPanel tlpConvRl;
        private System.Windows.Forms.TableLayoutPanel tlpConvUnit;
        private System.Windows.Forms.TableLayoutPanel tlpGenLoc;
        private System.Windows.Forms.TableLayoutPanel tlpGenPre;
        private System.Windows.Forms.TableLayoutPanel tlpGenScope;
        private System.Windows.Forms.TableLayoutPanel tlpGenTyp;
        private System.Windows.Forms.TableLayoutPanel tlpMain;
        private System.Windows.Forms.TableLayoutPanel tlpMainEPS;
        private System.Windows.Forms.TableLayoutPanel tlpMainGen;
        private System.Windows.Forms.TableLayoutPanel tlpMainLky;
        private System.Windows.Forms.TableLayoutPanel tlpMainMisc;
        private System.Windows.Forms.TableLayoutPanel tlpMainOpp;
        private System.Windows.Forms.TableLayoutPanel tlpMainProp;
        private System.Windows.Forms.TableLayoutPanel tlpMainSfOpp;
        private System.Windows.Forms.TableLayoutPanel tlpMainStat;
        private System.Windows.Forms.TableLayoutPanel tlpMainTls;
        private System.Windows.Forms.TableLayoutPanel tlpPropCmds;
        private System.Windows.Forms.TableLayoutPanel tlpPropCmts;
        private System.Windows.Forms.TableLayoutPanel tlpPropGen;
        private System.Windows.Forms.TextBox txtConvBfLen;
        private System.Windows.Forms.TextBox txtConvBfQty;
        private System.Windows.Forms.TextBox txtConvBfThk;
        private System.Windows.Forms.TextBox txtConvBfTot;
        private System.Windows.Forms.TextBox txtConvBfWid;
        internal System.Windows.Forms.TextBox txtConvEleAmp;
        internal System.Windows.Forms.TextBox txtConvEleOhm;
        internal System.Windows.Forms.TextBox txtConvEleVolt;
        internal System.Windows.Forms.TextBox txtConvEleW50;
        internal System.Windows.Forms.TextBox txtConvEleW80;
        internal System.Windows.Forms.TextBox txtConvEleWatt;
        private System.Windows.Forms.TextBox txtConvRlCore;
        private System.Windows.Forms.TextBox txtConvRlID;
        private System.Windows.Forms.TextBox txtConvRlMat;
        private System.Windows.Forms.TextBox txtConvRlOD;
        private System.Windows.Forms.TextBox txtConvRlTotal;
        private System.Windows.Forms.TextBox txtConvUnitFVal;
        private System.Windows.Forms.TextBox txtConvUnitTVal;
        private System.Windows.Forms.TextBox txtEOSCmt;
        private System.Windows.Forms.TextBox txtEOSCust;
        private System.Windows.Forms.TextBox txtEOSDesc;
        private System.Windows.Forms.TextBox txtEOSDue;
        private System.Windows.Forms.TextBox txtEOSMemoBody;
        private System.Windows.Forms.TextBox txtEOSOpp;
        private System.Windows.Forms.TextBox txtEOSUser;
        private System.Windows.Forms.TextBox txtEPSDesc;
        private System.Windows.Forms.TextBox txtEPSPart;
        private System.Windows.Forms.TextBox txtGenLocPath;
        private System.Windows.Forms.TextBox txtPropCmtNew;
        private System.Windows.Forms.TextBox txtPropCmts;
        private System.Windows.Forms.TextBox txtPropGenCust;
        private System.Windows.Forms.TextBox txtPropGenCustNo;
        private System.Windows.Forms.TextBox txtPropGenDesc;
        private System.Windows.Forms.TextBox txtPropGenDocNo;
        private System.Windows.Forms.TextBox txtPropGenOpp;
        private System.Windows.Forms.TextBox txtPropGenPartNo;
        private System.Windows.Forms.TextBox txtPropGenRev;
        private System.Windows.Forms.TextBox txtSFOSCust;
        private System.Windows.Forms.TextBox txtSFOSDesc;
        private System.Windows.Forms.TextBox txtSFOSDue;
        private System.Windows.Forms.TextBox txtSFOSOpp;
        private System.Windows.Forms.TextBox txtSFOSUser;
        private System.Windows.Forms.TextBox txtStatus;
        private System.Windows.Forms.ToolStripMenuItem tsiCopyDesc;
        private System.Windows.Forms.ToolStripMenuItem tsiCopyPart;
        private System.Windows.Forms.ToolStripMenuItem tsiInsertPart;
        private System.Windows.Forms.ToolStripMenuItem tsiOpBrowse;
        private System.Windows.Forms.ToolStripMenuItem tsiOpenExplore;
        private System.Windows.Forms.ToolStripMenuItem tsiOpenFile;
        private System.Windows.Forms.ToolStripMenuItem tsiSearch;
        private System.Windows.Forms.ToolStripMenuItem tsiSfOpBrowse;
        private System.Windows.Forms.ToolTip tipTool;

        #endregion
    }
}