using System.ComponentModel;

namespace BlueBrick
{
    partial class FrmOpts
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
            this.tlpOpts = new System.Windows.Forms.TableLayoutPanel();
            this.chkSalesForce = new System.Windows.Forms.CheckBox();
            this.lblSalesForce = new System.Windows.Forms.Label();
            this.lblSetting = new System.Windows.Forms.Label();
            this.tlpSfFields = new System.Windows.Forms.TableLayoutPanel();
            this.lblSfName = new System.Windows.Forms.Label();
            this.txtSfName = new System.Windows.Forms.TextBox();
            this.lblSfEmail = new System.Windows.Forms.Label();
            this.txtSfEmail = new System.Windows.Forms.TextBox();
            this.lblSfPic = new System.Windows.Forms.Label();
            this.txtSfPic = new System.Windows.Forms.TextBox();
            this.lblSfAccess = new System.Windows.Forms.Label();
            this.txtSfAccess = new System.Windows.Forms.TextBox();
            this.lblSfRefresh = new System.Windows.Forms.Label();
            this.txtSfRefresh = new System.Windows.Forms.TextBox();
            this.chkSfLogin = new System.Windows.Forms.CheckBox();
            this.lblSfHead = new System.Windows.Forms.Label();
            this.lblValue = new System.Windows.Forms.Label();
            this.lblViki = new System.Windows.Forms.Label();
            this.lblGen = new System.Windows.Forms.Label();
            this.lblTools = new System.Windows.Forms.Label();
            this.lblProp = new System.Windows.Forms.Label();
            this.lblLky = new System.Windows.Forms.Label();
            this.lblEpiSrch = new System.Windows.Forms.Label();
            this.lblEpiOpp = new System.Windows.Forms.Label();
            this.lblMisc = new System.Windows.Forms.Label();
            this.chkGen = new System.Windows.Forms.CheckBox();
            this.chkTools = new System.Windows.Forms.CheckBox();
            this.chkProp = new System.Windows.Forms.CheckBox();
            this.chkLky = new System.Windows.Forms.CheckBox();
            this.chkEpiSrch = new System.Windows.Forms.CheckBox();
            this.chkEpiOpp = new System.Windows.Forms.CheckBox();
            this.txtViki = new System.Windows.Forms.TextBox();
            this.chkMisc = new System.Windows.Forms.CheckBox();
            this.tlpMain = new System.Windows.Forms.TableLayoutPanel();
            this.btnSave = new System.Windows.Forms.Button();
            this.btnDiscard = new System.Windows.Forms.Button();
            this.btnVersion = new System.Windows.Forms.Button();
            this.tlpOpts.SuspendLayout();
            this.tlpSfFields.SuspendLayout();
            this.tlpMain.SuspendLayout();
            this.SuspendLayout();
            // 
            // tlpOpts
            // 
            this.tlpOpts.CellBorderStyle = System.Windows.Forms.TableLayoutPanelCellBorderStyle.Single;
            this.tlpOpts.ColumnCount = 2;
            this.tlpMain.SetColumnSpan(this.tlpOpts, 3);
            this.tlpOpts.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 31F));
            this.tlpOpts.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 69F));
            this.tlpOpts.Controls.Add(this.chkSalesForce, 1, 8);
            this.tlpOpts.Controls.Add(this.lblSalesForce, 0, 8);
            this.tlpOpts.Controls.Add(this.lblSetting, 0, 0);
            this.tlpOpts.Controls.Add(this.tlpSfFields, 0, 10);
            this.tlpOpts.Controls.Add(this.lblValue, 1, 0);
            this.tlpOpts.Controls.Add(this.lblViki, 0, 1);
            this.tlpOpts.Controls.Add(this.lblGen, 0, 2);
            this.tlpOpts.Controls.Add(this.lblTools, 0, 3);
            this.tlpOpts.Controls.Add(this.lblProp, 0, 4);
            this.tlpOpts.Controls.Add(this.lblLky, 0, 5);
            this.tlpOpts.Controls.Add(this.lblEpiSrch, 0, 6);
            this.tlpOpts.Controls.Add(this.lblEpiOpp, 0, 7);
            this.tlpOpts.Controls.Add(this.lblMisc, 0, 9);
            this.tlpOpts.Controls.Add(this.chkGen, 1, 2);
            this.tlpOpts.Controls.Add(this.chkTools, 1, 3);
            this.tlpOpts.Controls.Add(this.chkProp, 1, 4);
            this.tlpOpts.Controls.Add(this.chkLky, 1, 5);
            this.tlpOpts.Controls.Add(this.chkEpiSrch, 1, 6);
            this.tlpOpts.Controls.Add(this.chkEpiOpp, 1, 7);
            this.tlpOpts.Controls.Add(this.txtViki, 1, 1);
            this.tlpOpts.Controls.Add(this.chkMisc, 1, 9);
            this.tlpOpts.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpOpts.Location = new System.Drawing.Point(3, 3);
            this.tlpOpts.Name = "tlpOpts";
            this.tlpOpts.RowCount = 11;
            this.tlpOpts.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 6F));
            this.tlpOpts.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 6F));
            this.tlpOpts.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 6F));
            this.tlpOpts.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 6F));
            this.tlpOpts.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 6F));
            this.tlpOpts.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 6F));
            this.tlpOpts.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 6F));
            this.tlpOpts.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 6F));
            this.tlpOpts.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 6F));
            this.tlpOpts.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 6F));
            this.tlpOpts.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 40F));
            this.tlpOpts.Size = new System.Drawing.Size(432, 503);
            this.tlpOpts.TabIndex = 0;
            // 
            // chkSalesForce
            // 
            this.chkSalesForce.Location = new System.Drawing.Point(137, 244);
            this.chkSalesForce.Name = "chkSalesForce";
            this.chkSalesForce.Size = new System.Drawing.Size(104, 19);
            this.chkSalesForce.TabIndex = 17;
            this.chkSalesForce.Text = "Visible";
            this.chkSalesForce.UseVisualStyleBackColor = true;
            // 
            // lblSalesForce
            // 
            this.lblSalesForce.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblSalesForce.Location = new System.Drawing.Point(4, 241);
            this.lblSalesForce.Name = "lblSalesForce";
            this.lblSalesForce.Size = new System.Drawing.Size(126, 29);
            this.lblSalesForce.TabIndex = 16;
            this.lblSalesForce.Text = "SalesForce";
            // 
            // lblSetting
            // 
            this.lblSetting.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblSetting.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblSetting.Location = new System.Drawing.Point(4, 1);
            this.lblSetting.Name = "lblSetting";
            this.lblSetting.Size = new System.Drawing.Size(126, 29);
            this.lblSetting.TabIndex = 0;
            this.lblSetting.Text = "Setting";
            // 
            // tlpSfFields
            // 
            this.tlpSfFields.ColumnCount = 2;
            this.tlpOpts.SetColumnSpan(this.tlpSfFields, 2);
            this.tlpSfFields.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 31F));
            this.tlpSfFields.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 69F));
            this.tlpSfFields.Controls.Add(this.lblSfName, 0, 1);
            this.tlpSfFields.Controls.Add(this.txtSfName, 1, 1);
            this.tlpSfFields.Controls.Add(this.lblSfEmail, 0, 2);
            this.tlpSfFields.Controls.Add(this.txtSfEmail, 1, 2);
            this.tlpSfFields.Controls.Add(this.lblSfPic, 0, 3);
            this.tlpSfFields.Controls.Add(this.txtSfPic, 1, 3);
            this.tlpSfFields.Controls.Add(this.lblSfAccess, 0, 4);
            this.tlpSfFields.Controls.Add(this.txtSfAccess, 1, 4);
            this.tlpSfFields.Controls.Add(this.lblSfRefresh, 0, 5);
            this.tlpSfFields.Controls.Add(this.txtSfRefresh, 1, 5);
            this.tlpSfFields.Controls.Add(this.chkSfLogin, 1, 0);
            this.tlpSfFields.Controls.Add(this.lblSfHead, 0, 0);
            this.tlpSfFields.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpSfFields.Location = new System.Drawing.Point(4, 304);
            this.tlpSfFields.Name = "tlpSfFields";
            this.tlpSfFields.RowCount = 6;
            this.tlpSfFields.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 16.675F));
            this.tlpSfFields.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 16.665F));
            this.tlpSfFields.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 16.665F));
            this.tlpSfFields.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 16.665F));
            this.tlpSfFields.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 16.665F));
            this.tlpSfFields.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 16.665F));
            this.tlpSfFields.Size = new System.Drawing.Size(424, 195);
            this.tlpSfFields.TabIndex = 20;
            // 
            // lblSfName
            // 
            this.lblSfName.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblSfName.Location = new System.Drawing.Point(3, 32);
            this.lblSfName.Name = "lblSfName";
            this.lblSfName.Size = new System.Drawing.Size(125, 32);
            this.lblSfName.TabIndex = 2;
            this.lblSfName.Text = "SF Name";
            // 
            // txtSfName
            // 
            this.txtSfName.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtSfName.Location = new System.Drawing.Point(134, 35);
            this.txtSfName.Name = "txtSfName";
            this.txtSfName.ReadOnly = true;
            this.txtSfName.Size = new System.Drawing.Size(287, 20);
            this.txtSfName.TabIndex = 3;
            // 
            // lblSfEmail
            // 
            this.lblSfEmail.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblSfEmail.Location = new System.Drawing.Point(3, 64);
            this.lblSfEmail.Name = "lblSfEmail";
            this.lblSfEmail.Size = new System.Drawing.Size(125, 32);
            this.lblSfEmail.TabIndex = 4;
            this.lblSfEmail.Text = "SF Email";
            // 
            // txtSfEmail
            // 
            this.txtSfEmail.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtSfEmail.Location = new System.Drawing.Point(134, 67);
            this.txtSfEmail.Name = "txtSfEmail";
            this.txtSfEmail.ReadOnly = true;
            this.txtSfEmail.Size = new System.Drawing.Size(287, 20);
            this.txtSfEmail.TabIndex = 5;
            // 
            // lblSfPic
            // 
            this.lblSfPic.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblSfPic.Location = new System.Drawing.Point(3, 96);
            this.lblSfPic.Name = "lblSfPic";
            this.lblSfPic.Size = new System.Drawing.Size(125, 32);
            this.lblSfPic.TabIndex = 6;
            this.lblSfPic.Text = "SF Pic Url";
            // 
            // txtSfPic
            // 
            this.txtSfPic.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtSfPic.Location = new System.Drawing.Point(134, 99);
            this.txtSfPic.Name = "txtSfPic";
            this.txtSfPic.ReadOnly = true;
            this.txtSfPic.Size = new System.Drawing.Size(287, 20);
            this.txtSfPic.TabIndex = 7;
            // 
            // lblSfAccess
            // 
            this.lblSfAccess.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblSfAccess.Location = new System.Drawing.Point(3, 128);
            this.lblSfAccess.Name = "lblSfAccess";
            this.lblSfAccess.Size = new System.Drawing.Size(125, 32);
            this.lblSfAccess.TabIndex = 8;
            this.lblSfAccess.Text = "SF Acc Token";
            // 
            // txtSfAccess
            // 
            this.txtSfAccess.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtSfAccess.Location = new System.Drawing.Point(134, 131);
            this.txtSfAccess.Name = "txtSfAccess";
            this.txtSfAccess.PasswordChar = '*';
            this.txtSfAccess.ReadOnly = true;
            this.txtSfAccess.Size = new System.Drawing.Size(287, 20);
            this.txtSfAccess.TabIndex = 9;
            // 
            // lblSfRefresh
            // 
            this.lblSfRefresh.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblSfRefresh.Location = new System.Drawing.Point(3, 160);
            this.lblSfRefresh.Name = "lblSfRefresh";
            this.lblSfRefresh.Size = new System.Drawing.Size(125, 35);
            this.lblSfRefresh.TabIndex = 10;
            this.lblSfRefresh.Text = "SF Ref Token";
            // 
            // txtSfRefresh
            // 
            this.txtSfRefresh.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtSfRefresh.Location = new System.Drawing.Point(134, 163);
            this.txtSfRefresh.Name = "txtSfRefresh";
            this.txtSfRefresh.PasswordChar = '*';
            this.txtSfRefresh.ReadOnly = true;
            this.txtSfRefresh.Size = new System.Drawing.Size(287, 20);
            this.txtSfRefresh.TabIndex = 11;
            // 
            // chkSfLogin
            // 
            this.chkSfLogin.Dock = System.Windows.Forms.DockStyle.Fill;
            this.chkSfLogin.Enabled = false;
            this.chkSfLogin.Location = new System.Drawing.Point(134, 3);
            this.chkSfLogin.Name = "chkSfLogin";
            this.chkSfLogin.Size = new System.Drawing.Size(287, 26);
            this.chkSfLogin.TabIndex = 1;
            this.chkSfLogin.Text = "Logged In";
            this.chkSfLogin.UseVisualStyleBackColor = true;
            // 
            // lblSfHead
            // 
            this.lblSfHead.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblSfHead.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblSfHead.Location = new System.Drawing.Point(3, 0);
            this.lblSfHead.Name = "lblSfHead";
            this.lblSfHead.Size = new System.Drawing.Size(125, 32);
            this.lblSfHead.TabIndex = 0;
            this.lblSfHead.Text = "SalesForce Info";
            // 
            // lblValue
            // 
            this.lblValue.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblValue.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblValue.Location = new System.Drawing.Point(137, 1);
            this.lblValue.Name = "lblValue";
            this.lblValue.Size = new System.Drawing.Size(291, 29);
            this.lblValue.TabIndex = 1;
            this.lblValue.Text = "Value";
            // 
            // lblViki
            // 
            this.lblViki.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblViki.Location = new System.Drawing.Point(4, 31);
            this.lblViki.Name = "lblViki";
            this.lblViki.Size = new System.Drawing.Size(126, 29);
            this.lblViki.TabIndex = 2;
            this.lblViki.Text = "Viki Address";
            // 
            // lblGen
            // 
            this.lblGen.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblGen.Location = new System.Drawing.Point(4, 61);
            this.lblGen.Name = "lblGen";
            this.lblGen.Size = new System.Drawing.Size(126, 29);
            this.lblGen.TabIndex = 4;
            this.lblGen.Text = "Document Generators";
            // 
            // lblTools
            // 
            this.lblTools.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblTools.Location = new System.Drawing.Point(4, 91);
            this.lblTools.Name = "lblTools";
            this.lblTools.Size = new System.Drawing.Size(126, 29);
            this.lblTools.TabIndex = 6;
            this.lblTools.Text = "Tools";
            // 
            // lblProp
            // 
            this.lblProp.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblProp.Location = new System.Drawing.Point(4, 121);
            this.lblProp.Name = "lblProp";
            this.lblProp.Size = new System.Drawing.Size(126, 29);
            this.lblProp.TabIndex = 8;
            this.lblProp.Text = "Custom Properties";
            // 
            // lblLky
            // 
            this.lblLky.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblLky.Location = new System.Drawing.Point(4, 151);
            this.lblLky.Name = "lblLky";
            this.lblLky.Size = new System.Drawing.Size(126, 29);
            this.lblLky.TabIndex = 10;
            this.lblLky.Text = "Feelin\' Lucky";
            // 
            // lblEpiSrch
            // 
            this.lblEpiSrch.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblEpiSrch.Location = new System.Drawing.Point(4, 181);
            this.lblEpiSrch.Name = "lblEpiSrch";
            this.lblEpiSrch.Size = new System.Drawing.Size(126, 29);
            this.lblEpiSrch.TabIndex = 12;
            this.lblEpiSrch.Text = "Epicor Part Search";
            // 
            // lblEpiOpp
            // 
            this.lblEpiOpp.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblEpiOpp.Location = new System.Drawing.Point(4, 211);
            this.lblEpiOpp.Name = "lblEpiOpp";
            this.lblEpiOpp.Size = new System.Drawing.Size(126, 29);
            this.lblEpiOpp.TabIndex = 14;
            this.lblEpiOpp.Text = "Epicor Opp Search";
            // 
            // lblMisc
            // 
            this.lblMisc.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblMisc.Location = new System.Drawing.Point(4, 271);
            this.lblMisc.Name = "lblMisc";
            this.lblMisc.Size = new System.Drawing.Size(126, 29);
            this.lblMisc.TabIndex = 18;
            this.lblMisc.Text = "Misc. Utilities";
            // 
            // chkGen
            // 
            this.chkGen.Location = new System.Drawing.Point(137, 64);
            this.chkGen.Name = "chkGen";
            this.chkGen.Size = new System.Drawing.Size(104, 19);
            this.chkGen.TabIndex = 5;
            this.chkGen.Text = "Visible";
            this.chkGen.UseVisualStyleBackColor = true;
            // 
            // chkTools
            // 
            this.chkTools.Location = new System.Drawing.Point(137, 94);
            this.chkTools.Name = "chkTools";
            this.chkTools.Size = new System.Drawing.Size(104, 19);
            this.chkTools.TabIndex = 7;
            this.chkTools.Text = "Visible";
            this.chkTools.UseVisualStyleBackColor = true;
            // 
            // chkProp
            // 
            this.chkProp.Location = new System.Drawing.Point(137, 124);
            this.chkProp.Name = "chkProp";
            this.chkProp.Size = new System.Drawing.Size(104, 19);
            this.chkProp.TabIndex = 9;
            this.chkProp.Text = "Visible";
            this.chkProp.UseVisualStyleBackColor = true;
            // 
            // chkLky
            // 
            this.chkLky.Location = new System.Drawing.Point(137, 154);
            this.chkLky.Name = "chkLky";
            this.chkLky.Size = new System.Drawing.Size(104, 19);
            this.chkLky.TabIndex = 11;
            this.chkLky.Text = "Visible";
            this.chkLky.UseVisualStyleBackColor = true;
            // 
            // chkEpiSrch
            // 
            this.chkEpiSrch.Location = new System.Drawing.Point(137, 184);
            this.chkEpiSrch.Name = "chkEpiSrch";
            this.chkEpiSrch.Size = new System.Drawing.Size(104, 19);
            this.chkEpiSrch.TabIndex = 13;
            this.chkEpiSrch.Text = "Visible";
            this.chkEpiSrch.UseVisualStyleBackColor = true;
            // 
            // chkEpiOpp
            // 
            this.chkEpiOpp.Location = new System.Drawing.Point(137, 214);
            this.chkEpiOpp.Name = "chkEpiOpp";
            this.chkEpiOpp.Size = new System.Drawing.Size(104, 19);
            this.chkEpiOpp.TabIndex = 15;
            this.chkEpiOpp.Text = "Visible";
            this.chkEpiOpp.UseVisualStyleBackColor = true;
            // 
            // txtViki
            // 
            this.txtViki.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtViki.Location = new System.Drawing.Point(137, 34);
            this.txtViki.Name = "txtViki";
            this.txtViki.Size = new System.Drawing.Size(291, 20);
            this.txtViki.TabIndex = 3;
            // 
            // chkMisc
            // 
            this.chkMisc.Location = new System.Drawing.Point(137, 274);
            this.chkMisc.Name = "chkMisc";
            this.chkMisc.Size = new System.Drawing.Size(104, 21);
            this.chkMisc.TabIndex = 19;
            this.chkMisc.Text = "Visible";
            this.chkMisc.UseVisualStyleBackColor = true;
            // 
            // tlpMain
            // 
            this.tlpMain.ColumnCount = 3;
            this.tlpMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 60F));
            this.tlpMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.tlpMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.tlpMain.Controls.Add(this.tlpOpts, 0, 0);
            this.tlpMain.Controls.Add(this.btnSave, 1, 1);
            this.tlpMain.Controls.Add(this.btnDiscard, 2, 1);
            this.tlpMain.Controls.Add(this.btnVersion, 0, 1);
            this.tlpMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpMain.Location = new System.Drawing.Point(0, 0);
            this.tlpMain.Name = "tlpMain";
            this.tlpMain.RowCount = 2;
            this.tlpMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 90F));
            this.tlpMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 10F));
            this.tlpMain.Size = new System.Drawing.Size(438, 566);
            this.tlpMain.TabIndex = 1;
            // 
            // btnSave
            // 
            this.btnSave.Location = new System.Drawing.Point(265, 512);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(75, 23);
            this.btnSave.TabIndex = 1;
            this.btnSave.Text = "Save";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // btnDiscard
            // 
            this.btnDiscard.Location = new System.Drawing.Point(352, 512);
            this.btnDiscard.Name = "btnDiscard";
            this.btnDiscard.Size = new System.Drawing.Size(75, 23);
            this.btnDiscard.TabIndex = 2;
            this.btnDiscard.Text = "Discard";
            this.btnDiscard.UseVisualStyleBackColor = true;
            this.btnDiscard.Click += new System.EventHandler(this.btnDiscard_Click);
            // 
            // btnVersion
            // 
            this.btnVersion.Location = new System.Drawing.Point(3, 512);
            this.btnVersion.Name = "btnVersion";
            this.btnVersion.Size = new System.Drawing.Size(75, 23);
            this.btnVersion.TabIndex = 3;
            this.btnVersion.Text = "Info";
            this.btnVersion.UseVisualStyleBackColor = true;
            this.btnVersion.Click += new System.EventHandler(this.btnVersion_Click);
            // 
            // FrmOpts
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(438, 566);
            this.Controls.Add(this.tlpMain);
            this.Name = "FrmOpts";
            this.Text = "Options";
            this.tlpOpts.ResumeLayout(false);
            this.tlpOpts.PerformLayout();
            this.tlpSfFields.ResumeLayout(false);
            this.tlpSfFields.PerformLayout();
            this.tlpMain.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.Button btnVersion;

        private System.Windows.Forms.CheckBox chkSfLogin;
        private System.Windows.Forms.Label lblSfHead;

        private System.Windows.Forms.TableLayoutPanel tlpSfFields;

        private System.Windows.Forms.Label lblSfName;
        private System.Windows.Forms.Label lblSfEmail;
        private System.Windows.Forms.Label lblSfPic;
        private System.Windows.Forms.TextBox txtSfEmail;
        private System.Windows.Forms.TextBox txtSfPic;
        private System.Windows.Forms.TextBox txtSfName;

        private System.Windows.Forms.Label lblSalesForce;
        private System.Windows.Forms.CheckBox chkSalesForce;
        private System.Windows.Forms.Label lblSfAccess;
        private System.Windows.Forms.Label lblSfRefresh;
        private System.Windows.Forms.TextBox txtSfAccess;
        private System.Windows.Forms.TextBox txtSfRefresh;

        private System.Windows.Forms.Button btnDiscard;

        private System.Windows.Forms.Button btnSave;

        private System.Windows.Forms.TableLayoutPanel tlpOpts;

        private System.Windows.Forms.Label lblSetting;
        private System.Windows.Forms.Label lblValue;
        private System.Windows.Forms.Label lblViki;
        private System.Windows.Forms.Label lblGen;
        private System.Windows.Forms.Label lblTools;
        private System.Windows.Forms.Label lblProp;
        private System.Windows.Forms.Label lblLky;
        private System.Windows.Forms.Label lblEpiSrch;
        private System.Windows.Forms.Label lblEpiOpp;
        private System.Windows.Forms.Label lblMisc;
        private System.Windows.Forms.CheckBox chkGen;
        private System.Windows.Forms.CheckBox chkTools;
        private System.Windows.Forms.CheckBox chkProp;
        private System.Windows.Forms.CheckBox chkLky;
        private System.Windows.Forms.CheckBox chkEpiSrch;
        private System.Windows.Forms.CheckBox chkEpiOpp;
        private System.Windows.Forms.CheckBox chkMisc;
        private System.Windows.Forms.TextBox txtViki;

        private System.Windows.Forms.TableLayoutPanel tlpMain;

        #endregion
    }
}