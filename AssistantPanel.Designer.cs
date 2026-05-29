namespace BlueBrick
{
    partial class AssistantPanel
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        private void InitializeComponent()
        {
            this.tlpChatMain = new System.Windows.Forms.TableLayoutPanel();
            this.tlpChatButtons = new System.Windows.Forms.TableLayoutPanel();
            this.cmbModel = new System.Windows.Forms.ComboBox();
            this.cmbSearchTool = new System.Windows.Forms.ComboBox();
            this.btnNewSession = new System.Windows.Forms.Button();
            this.btnCapture = new System.Windows.Forms.Button();
            this.btnAttach = new System.Windows.Forms.Button();
            this.btnSearchVault = new System.Windows.Forms.Button();
            this.btnReindex = new System.Windows.Forms.Button();
            this.btnResetVault = new System.Windows.Forms.Button();
            this.btnOpenWorking = new System.Windows.Forms.Button();
            this.btnOpenChatGpt = new System.Windows.Forms.Button();
            this.btnTestConnection = new System.Windows.Forms.Button();
            this.btnToggleMode = new System.Windows.Forms.Button();
            this._webView = new Microsoft.Web.WebView2.WinForms.WebView2();
            this.pnlChatInput = new System.Windows.Forms.Panel();
            this.txtChatInput = new System.Windows.Forms.TextBox();
            this.btnSend = new System.Windows.Forms.Button();
            this.lblChatStatus = new System.Windows.Forms.Label();
            this.tlpChatMain.SuspendLayout();
            this.tlpChatButtons.SuspendLayout();
            this.pnlChatInput.SuspendLayout();
            this.SuspendLayout();
            //
            // tlpChatMain
            //
            this.tlpChatMain.ColumnCount = 1;
            this.tlpChatMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpChatMain.Controls.Add(this.tlpChatButtons, 0, 0);
            this.tlpChatMain.Controls.Add(this._webView, 0, 1);
            this.tlpChatMain.Controls.Add(this.pnlChatInput, 0, 2);
            this.tlpChatMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpChatMain.Location = new System.Drawing.Point(0, 0);
            this.tlpChatMain.Name = "tlpChatMain";
            this.tlpChatMain.RowCount = 3;
            this.tlpChatMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 142F));
            this.tlpChatMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpChatMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 48F));
            this.tlpChatMain.Size = new System.Drawing.Size(211, 360);
            this.tlpChatMain.TabIndex = 0;
            //
            // tlpChatButtons
            //
            this.tlpChatButtons.ColumnCount = 3;
            this.tlpChatButtons.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33F));
            this.tlpChatButtons.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33F));
            this.tlpChatButtons.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33F));
            this.tlpChatButtons.Controls.Add(this.cmbModel, 0, 0);
            this.tlpChatButtons.SetColumnSpan(this.cmbModel, 3);
            this.tlpChatButtons.Controls.Add(this.cmbSearchTool, 0, 1);
            this.tlpChatButtons.SetColumnSpan(this.cmbSearchTool, 3);
            this.tlpChatButtons.Controls.Add(this.btnNewSession, 0, 2);
            this.tlpChatButtons.Controls.Add(this.btnCapture, 1, 2);
            this.tlpChatButtons.Controls.Add(this.btnAttach, 2, 2);
            this.tlpChatButtons.Controls.Add(this.btnSearchVault, 0, 3);
            this.tlpChatButtons.Controls.Add(this.btnReindex, 1, 3);
            this.tlpChatButtons.Controls.Add(this.btnOpenWorking, 2, 3);
            this.tlpChatButtons.Controls.Add(this.btnOpenChatGpt, 0, 4);
            this.tlpChatButtons.Controls.Add(this.btnTestConnection, 1, 4);
            this.tlpChatButtons.Controls.Add(this.btnToggleMode, 2, 4);
            this.tlpChatButtons.Controls.Add(this.btnResetVault, 0, 5);
            this.tlpChatButtons.SetColumnSpan(this.btnResetVault, 3);
            this.tlpChatButtons.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpChatButtons.Location = new System.Drawing.Point(2, 2);
            this.tlpChatButtons.Name = "tlpChatButtons";
            this.tlpChatButtons.RowCount = 6;
            this.tlpChatButtons.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 24F));
            this.tlpChatButtons.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 24F));
            this.tlpChatButtons.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tlpChatButtons.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tlpChatButtons.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tlpChatButtons.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tlpChatButtons.Size = new System.Drawing.Size(207, 138);
            this.tlpChatButtons.TabIndex = 1;
            //
            // cmbModel
            //
            this.cmbModel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cmbModel.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbModel.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.cmbModel.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F);
            this.cmbModel.FormattingEnabled = true;
            this.cmbModel.Location = new System.Drawing.Point(1, 1);
            this.cmbModel.Margin = new System.Windows.Forms.Padding(1);
            this.cmbModel.Name = "cmbModel";
            this.cmbModel.Size = new System.Drawing.Size(205, 20);
            this.cmbModel.TabIndex = 9;
            this.cmbModel.SelectionChangeCommitted += new System.EventHandler(this.CmbModel_SelectionChangeCommitted);
            //
            // cmbSearchTool
            //
            this.cmbSearchTool.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cmbSearchTool.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbSearchTool.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.cmbSearchTool.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F);
            this.cmbSearchTool.FormattingEnabled = true;
            this.cmbSearchTool.Location = new System.Drawing.Point(1, 25);
            this.cmbSearchTool.Margin = new System.Windows.Forms.Padding(1);
            this.cmbSearchTool.Name = "cmbSearchTool";
            this.cmbSearchTool.Size = new System.Drawing.Size(205, 20);
            this.cmbSearchTool.TabIndex = 11;
            this.cmbSearchTool.SelectedIndexChanged += new System.EventHandler(this.CmbSearchTool_SelectedIndexChanged);
            //
            // btnNewSession
            //
            this.btnNewSession.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnNewSession.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(247)))), ((int)(((byte)(250)))));
            this.btnNewSession.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnNewSession.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F);
            this.btnNewSession.Location = new System.Drawing.Point(1, 49);
            this.btnNewSession.Name = "btnNewSession";
            this.btnNewSession.Size = new System.Drawing.Size(62, 18);
            this.btnNewSession.TabIndex = 0;
            this.btnNewSession.Text = "New";
            this.btnNewSession.UseVisualStyleBackColor = true;
            this.btnNewSession.Click += new System.EventHandler(this.BtnNewSession_Click);
            //
            // btnCapture
            //
            this.btnCapture.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnCapture.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(217)))), ((int)(((byte)(255)))), ((int)(((byte)(90)))));
            this.btnCapture.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnCapture.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F);
            this.btnCapture.Location = new System.Drawing.Point(69, 49);
            this.btnCapture.Name = "btnCapture";
            this.btnCapture.Size = new System.Drawing.Size(68, 18);
            this.btnCapture.TabIndex = 1;
            this.btnCapture.Text = "Capture";
            this.btnCapture.UseVisualStyleBackColor = true;
            this.btnCapture.Click += new System.EventHandler(this.BtnCapture_Click);
            //
            // btnAttach
            //
            this.btnAttach.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnAttach.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(247)))), ((int)(((byte)(250)))));
            this.btnAttach.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnAttach.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F);
            this.btnAttach.Location = new System.Drawing.Point(143, 49);
            this.btnAttach.Name = "btnAttach";
            this.btnAttach.Size = new System.Drawing.Size(63, 18);
            this.btnAttach.TabIndex = 2;
            this.btnAttach.Text = "Attach";
            this.btnAttach.UseVisualStyleBackColor = true;
            this.btnAttach.Click += new System.EventHandler(this.BtnAttach_Click);
            //
            // btnSearchVault
            //
            this.btnSearchVault.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnSearchVault.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(235)))), ((int)(((byte)(248)))), ((int)(((byte)(255)))));
            this.btnSearchVault.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSearchVault.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F);
            this.btnSearchVault.Location = new System.Drawing.Point(1, 72);
            this.btnSearchVault.Name = "btnSearchVault";
            this.btnSearchVault.Size = new System.Drawing.Size(62, 16);
            this.btnSearchVault.TabIndex = 10;
            this.btnSearchVault.Text = "Search";
            this.btnSearchVault.UseVisualStyleBackColor = true;
            this.btnSearchVault.Click += new System.EventHandler(this.BtnSearchVault_Click);
            //
            // btnReindex
            //
            this.btnReindex.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnReindex.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(247)))), ((int)(((byte)(250)))));
            this.btnReindex.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnReindex.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F);
            this.btnReindex.Location = new System.Drawing.Point(69, 72);
            this.btnReindex.Name = "btnReindex";
            this.btnReindex.Size = new System.Drawing.Size(68, 16);
            this.btnReindex.TabIndex = 3;
            this.btnReindex.Text = "Reindex";
            this.btnReindex.UseVisualStyleBackColor = true;
            this.btnReindex.Click += new System.EventHandler(this.BtnReindex_Click);
            //
            // btnResetVault
            //
            this.btnResetVault.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnResetVault.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(229)))), ((int)(((byte)(229)))));
            this.btnResetVault.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnResetVault.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F);
            this.btnResetVault.Location = new System.Drawing.Point(1, 118);
            this.btnResetVault.Name = "btnResetVault";
            this.btnResetVault.Size = new System.Drawing.Size(205, 18);
            this.btnResetVault.TabIndex = 4;
            this.btnResetVault.Text = "Reset Local Vault";
            this.btnResetVault.UseVisualStyleBackColor = true;
            this.btnResetVault.Click += new System.EventHandler(this.BtnResetVault_Click);
            //
            // btnOpenWorking
            //
            this.btnOpenWorking.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnOpenWorking.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(247)))), ((int)(((byte)(250)))));
            this.btnOpenWorking.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnOpenWorking.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F);
            this.btnOpenWorking.Location = new System.Drawing.Point(143, 72);
            this.btnOpenWorking.Name = "btnOpenWorking";
            this.btnOpenWorking.Size = new System.Drawing.Size(63, 16);
            this.btnOpenWorking.TabIndex = 5;
            this.btnOpenWorking.Text = "Work Dir";
            this.btnOpenWorking.UseVisualStyleBackColor = true;
            this.btnOpenWorking.Click += new System.EventHandler(this.BtnOpenWorking_Click);
            //
            // btnOpenChatGpt
            //
            this.btnOpenChatGpt.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnOpenChatGpt.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(229)))), ((int)(((byte)(231)))), ((int)(((byte)(255)))));
            this.btnOpenChatGpt.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnOpenChatGpt.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F);
            this.btnOpenChatGpt.Location = new System.Drawing.Point(1, 95);
            this.btnOpenChatGpt.Name = "btnOpenChatGpt";
            this.btnOpenChatGpt.Size = new System.Drawing.Size(62, 15);
            this.btnOpenChatGpt.TabIndex = 6;
            this.btnOpenChatGpt.Text = "ChatGPT";
            this.btnOpenChatGpt.UseVisualStyleBackColor = true;
            this.btnOpenChatGpt.Click += new System.EventHandler(this.BtnOpenChatGpt_Click);
            //
            // btnTestConnection
            //
            this.btnTestConnection.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnTestConnection.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(247)))), ((int)(((byte)(250)))));
            this.btnTestConnection.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnTestConnection.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F);
            this.btnTestConnection.Location = new System.Drawing.Point(69, 95);
            this.btnTestConnection.Name = "btnTestConnection";
            this.btnTestConnection.Size = new System.Drawing.Size(68, 15);
            this.btnTestConnection.TabIndex = 7;
            this.btnTestConnection.Text = "Test";
            this.btnTestConnection.UseVisualStyleBackColor = true;
            this.btnTestConnection.Click += new System.EventHandler(this.BtnTestConnection_Click);
            //
            // btnToggleMode
            //
            this.btnToggleMode.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnToggleMode.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(235)))), ((int)(((byte)(248)))), ((int)(((byte)(255)))));
            this.btnToggleMode.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnToggleMode.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F);
            this.btnToggleMode.Location = new System.Drawing.Point(143, 95);
            this.btnToggleMode.Name = "btnToggleMode";
            this.btnToggleMode.Size = new System.Drawing.Size(63, 15);
            this.btnToggleMode.TabIndex = 8;
            this.btnToggleMode.Text = "Mock";
            this.btnToggleMode.UseVisualStyleBackColor = true;
            this.btnToggleMode.Click += new System.EventHandler(this.BtnToggleMode_Click);
            //
            // _webView
            //
            this._webView.AllowExternalDrop = true;
            this._webView.CreationProperties = null;
            this._webView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(((int)(((byte)(15)))), ((int)(((byte)(18)))), ((int)(((byte)(22)))));
            this._webView.Dock = System.Windows.Forms.DockStyle.Fill;
            this._webView.Location = new System.Drawing.Point(2, 142);
            this._webView.Name = "_webView";
            this._webView.Size = new System.Drawing.Size(207, 168);
            this._webView.TabIndex = 2;
            this._webView.ZoomFactor = 1D;
            //
            // pnlChatInput
            //
            this.pnlChatInput.Controls.Add(this.txtChatInput);
            this.pnlChatInput.Controls.Add(this.btnSend);
            this.pnlChatInput.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlChatInput.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(15)))), ((int)(((byte)(18)))), ((int)(((byte)(22)))));
            this.pnlChatInput.Location = new System.Drawing.Point(2, 314);
            this.pnlChatInput.Name = "pnlChatInput";
            this.pnlChatInput.Size = new System.Drawing.Size(207, 44);
            this.pnlChatInput.TabIndex = 3;
            //
            // txtChatInput
            //
            this.txtChatInput.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtChatInput.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this.txtChatInput.Location = new System.Drawing.Point(0, 3);
            this.txtChatInput.Multiline = true;
            this.txtChatInput.Name = "txtChatInput";
            this.txtChatInput.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtChatInput.Size = new System.Drawing.Size(159, 38);
            this.txtChatInput.TabIndex = 0;
            this.txtChatInput.KeyDown += new System.Windows.Forms.KeyEventHandler(this.TxtChatInput_KeyDown);
            //
            // btnSend
            //
            this.btnSend.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSend.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(217)))), ((int)(((byte)(255)))), ((int)(((byte)(90)))));
            this.btnSend.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSend.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F);
            this.btnSend.Location = new System.Drawing.Point(163, 3);
            this.btnSend.Name = "btnSend";
            this.btnSend.Size = new System.Drawing.Size(43, 38);
            this.btnSend.TabIndex = 1;
            this.btnSend.Text = "Send";
            this.btnSend.UseVisualStyleBackColor = true;
            this.btnSend.Click += new System.EventHandler(this.BtnSend_Click);
            //
            // lblChatStatus
            //
            this.lblChatStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblChatStatus.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this.lblChatStatus.ForeColor = System.Drawing.SystemColors.ControlDark;
            this.lblChatStatus.Location = new System.Drawing.Point(0, 0);
            this.lblChatStatus.Name = "lblChatStatus";
            this.lblChatStatus.Size = new System.Drawing.Size(211, 20);
            this.lblChatStatus.TabIndex = 4;
            this.lblChatStatus.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // AssistantPanel
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.lblChatStatus);
            this.Controls.Add(this.tlpChatMain);
            this.Name = "AssistantPanel";
            this.Size = new System.Drawing.Size(211, 360);
            this.tlpChatMain.ResumeLayout(false);
            this.tlpChatButtons.ResumeLayout(false);
            this.pnlChatInput.ResumeLayout(false);
            this.pnlChatInput.PerformLayout();
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tlpChatMain;
        private System.Windows.Forms.TableLayoutPanel tlpChatButtons;
        private System.Windows.Forms.ComboBox cmbModel;
        private System.Windows.Forms.ComboBox cmbSearchTool;
        private System.Windows.Forms.Button btnNewSession;
        private System.Windows.Forms.Button btnCapture;
        private System.Windows.Forms.Button btnAttach;
        private System.Windows.Forms.Button btnSearchVault;
        private System.Windows.Forms.Button btnReindex;
        private System.Windows.Forms.Button btnResetVault;
        private System.Windows.Forms.Button btnOpenWorking;
        private System.Windows.Forms.Button btnOpenChatGpt;
        private System.Windows.Forms.Button btnTestConnection;
        private System.Windows.Forms.Button btnToggleMode;
        private Microsoft.Web.WebView2.WinForms.WebView2 _webView;
        private System.Windows.Forms.Panel pnlChatInput;
        private System.Windows.Forms.TextBox txtChatInput;
        private System.Windows.Forms.Button btnSend;
        private System.Windows.Forms.Label lblChatStatus;
    }
}
