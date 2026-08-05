
namespace BlueBrick
{
    partial class FrmCard
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FrmCard));
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.lblArtR = new System.Windows.Forms.Label();
            this.lblUPC = new System.Windows.Forms.Label();
            this.btnPickR = new System.Windows.Forms.Button();
            this.lblOpp = new System.Windows.Forms.Label();
            this.btnPickF = new System.Windows.Forms.Button();
            this.lblDesc = new System.Windows.Forms.Label();
            this.lblCust = new System.Windows.Forms.Label();
            this.lblArtF = new System.Windows.Forms.Label();
            this.txtOpp = new System.Windows.Forms.TextBox();
            this.txtUPC = new System.Windows.Forms.TextBox();
            this.txtDesc = new System.Windows.Forms.TextBox();
            this.txtCust = new System.Windows.Forms.TextBox();
            this.txtArtF = new System.Windows.Forms.TextBox();
            this.txtArtR = new System.Windows.Forms.TextBox();
            this.grpType = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this.radGBP = new System.Windows.Forms.RadioButton();
            this.radPCS = new System.Windows.Forms.RadioButton();
            this.radEDS = new System.Windows.Forms.RadioButton();
            this.radPCM = new System.Windows.Forms.RadioButton();
            this.radMGB = new System.Windows.Forms.RadioButton();
            this.btnOK = new System.Windows.Forms.Button();
            this.ofdPick = new System.Windows.Forms.OpenFileDialog();
            this.lstFiles = new System.Windows.Forms.ListView();
            this.colFile = new System.Windows.Forms.ColumnHeader();
            this.colDesc = new System.Windows.Forms.ColumnHeader();
            this.btnOpen = new System.Windows.Forms.Button();
            this.btnAttachF = new System.Windows.Forms.Button();
            this.btnAttachR = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.tableLayoutPanel3 = new System.Windows.Forms.TableLayoutPanel();
            this.tableLayoutPanel1.SuspendLayout();
            this.grpType.SuspendLayout();
            this.tableLayoutPanel2.SuspendLayout();
            this.tableLayoutPanel3.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 3;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 75F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 67F));
            this.tableLayoutPanel1.Controls.Add(this.lblArtR, 0, 5);
            this.tableLayoutPanel1.Controls.Add(this.lblUPC, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.btnPickR, 2, 5);
            this.tableLayoutPanel1.Controls.Add(this.lblOpp, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.btnPickF, 2, 4);
            this.tableLayoutPanel1.Controls.Add(this.lblDesc, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.lblCust, 0, 3);
            this.tableLayoutPanel1.Controls.Add(this.lblArtF, 0, 4);
            this.tableLayoutPanel1.Controls.Add(this.txtOpp, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.txtUPC, 1, 1);
            this.tableLayoutPanel1.Controls.Add(this.txtDesc, 1, 2);
            this.tableLayoutPanel1.Controls.Add(this.txtCust, 1, 3);
            this.tableLayoutPanel1.Controls.Add(this.txtArtF, 1, 4);
            this.tableLayoutPanel1.Controls.Add(this.txtArtR, 1, 5);
            this.tableLayoutPanel1.Location = new System.Drawing.Point(217, 12);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 6;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(375, 152);
            this.tableLayoutPanel1.TabIndex = 1;
            // 
            // lblArtR
            // 
            this.lblArtR.AutoSize = true;
            this.lblArtR.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblArtR.Location = new System.Drawing.Point(3, 125);
            this.lblArtR.Name = "lblArtR";
            this.lblArtR.Size = new System.Drawing.Size(71, 27);
            this.lblArtR.TabIndex = 4;
            this.lblArtR.Text = "Artwork (R)";
            this.lblArtR.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblUPC
            // 
            this.lblUPC.AutoSize = true;
            this.lblUPC.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblUPC.Location = new System.Drawing.Point(3, 25);
            this.lblUPC.Name = "lblUPC";
            this.lblUPC.Size = new System.Drawing.Size(71, 25);
            this.lblUPC.TabIndex = 2;
            this.lblUPC.Text = "UPC";
            this.lblUPC.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // btnPickR
            // 
            this.btnPickR.Dock = System.Windows.Forms.DockStyle.Top;
            this.btnPickR.Location = new System.Drawing.Point(311, 128);
            this.btnPickR.Name = "btnPickR";
            this.btnPickR.Size = new System.Drawing.Size(61, 19);
            this.btnPickR.TabIndex = 9;
            this.btnPickR.Text = "Pick";
            this.btnPickR.UseVisualStyleBackColor = true;
            this.btnPickR.Click += new System.EventHandler(this.btnPickR_Click);
            // 
            // lblOpp
            // 
            this.lblOpp.AutoSize = true;
            this.lblOpp.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblOpp.Location = new System.Drawing.Point(3, 0);
            this.lblOpp.Name = "lblOpp";
            this.lblOpp.Size = new System.Drawing.Size(71, 25);
            this.lblOpp.TabIndex = 0;
            this.lblOpp.Text = "Opp";
            this.lblOpp.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // btnPickF
            // 
            this.btnPickF.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnPickF.Location = new System.Drawing.Point(311, 103);
            this.btnPickF.Name = "btnPickF";
            this.btnPickF.Size = new System.Drawing.Size(61, 19);
            this.btnPickF.TabIndex = 7;
            this.btnPickF.Text = "Pick";
            this.btnPickF.UseVisualStyleBackColor = true;
            this.btnPickF.Click += new System.EventHandler(this.btnPickF_Click);
            // 
            // lblDesc
            // 
            this.lblDesc.AutoSize = true;
            this.lblDesc.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblDesc.Location = new System.Drawing.Point(3, 50);
            this.lblDesc.Name = "lblDesc";
            this.lblDesc.Size = new System.Drawing.Size(71, 25);
            this.lblDesc.TabIndex = 3;
            this.lblDesc.Text = "Description";
            this.lblDesc.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblCust
            // 
            this.lblCust.AutoSize = true;
            this.lblCust.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblCust.Location = new System.Drawing.Point(3, 75);
            this.lblCust.Name = "lblCust";
            this.lblCust.Size = new System.Drawing.Size(71, 25);
            this.lblCust.TabIndex = 5;
            this.lblCust.Text = "Customer PN";
            this.lblCust.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblArtF
            // 
            this.lblArtF.AutoSize = true;
            this.lblArtF.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblArtF.Location = new System.Drawing.Point(3, 100);
            this.lblArtF.Name = "lblArtF";
            this.lblArtF.Size = new System.Drawing.Size(71, 25);
            this.lblArtF.TabIndex = 1;
            this.lblArtF.Text = "Artwork (F)";
            this.lblArtF.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // txtOpp
            // 
            this.tableLayoutPanel1.SetColumnSpan(this.txtOpp, 2);
            this.txtOpp.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtOpp.Location = new System.Drawing.Point(80, 3);
            this.txtOpp.Name = "txtOpp";
            this.txtOpp.Size = new System.Drawing.Size(292, 20);
            this.txtOpp.TabIndex = 2;
            this.txtOpp.TextChanged += new System.EventHandler(this.txtOpp_TextChanged);
            // 
            // txtUPC
            // 
            this.tableLayoutPanel1.SetColumnSpan(this.txtUPC, 2);
            this.txtUPC.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtUPC.Location = new System.Drawing.Point(80, 28);
            this.txtUPC.Name = "txtUPC";
            this.txtUPC.Size = new System.Drawing.Size(292, 20);
            this.txtUPC.TabIndex = 3;
            // 
            // txtDesc
            // 
            this.tableLayoutPanel1.SetColumnSpan(this.txtDesc, 2);
            this.txtDesc.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtDesc.Location = new System.Drawing.Point(80, 53);
            this.txtDesc.Name = "txtDesc";
            this.txtDesc.Size = new System.Drawing.Size(292, 20);
            this.txtDesc.TabIndex = 4;
            // 
            // txtCust
            // 
            this.tableLayoutPanel1.SetColumnSpan(this.txtCust, 2);
            this.txtCust.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtCust.Location = new System.Drawing.Point(80, 78);
            this.txtCust.Name = "txtCust";
            this.txtCust.Size = new System.Drawing.Size(292, 20);
            this.txtCust.TabIndex = 5;
            // 
            // txtArtF
            // 
            this.txtArtF.BackColor = System.Drawing.SystemColors.Menu;
            this.txtArtF.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtArtF.Enabled = false;
            this.txtArtF.Location = new System.Drawing.Point(80, 103);
            this.txtArtF.Name = "txtArtF";
            this.txtArtF.Size = new System.Drawing.Size(225, 20);
            this.txtArtF.TabIndex = 6;
            this.txtArtF.TabStop = false;
            // 
            // txtArtR
            // 
            this.txtArtR.BackColor = System.Drawing.SystemColors.Menu;
            this.txtArtR.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtArtR.Enabled = false;
            this.txtArtR.Location = new System.Drawing.Point(80, 128);
            this.txtArtR.Name = "txtArtR";
            this.txtArtR.Size = new System.Drawing.Size(225, 20);
            this.txtArtR.TabIndex = 8;
            this.txtArtR.TabStop = false;
            // 
            // grpType
            // 
            this.grpType.Controls.Add(this.tableLayoutPanel2);
            this.grpType.Location = new System.Drawing.Point(12, 12);
            this.grpType.Name = "grpType";
            this.grpType.Size = new System.Drawing.Size(199, 147);
            this.grpType.TabIndex = 0;
            this.grpType.TabStop = false;
            this.grpType.Text = "Card Type";
            // 
            // tableLayoutPanel2
            // 
            this.tableLayoutPanel2.ColumnCount = 1;
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel2.Controls.Add(this.radGBP, 0, 4);
            this.tableLayoutPanel2.Controls.Add(this.radPCS, 0, 0);
            this.tableLayoutPanel2.Controls.Add(this.radEDS, 0, 3);
            this.tableLayoutPanel2.Controls.Add(this.radPCM, 0, 1);
            this.tableLayoutPanel2.Controls.Add(this.radMGB, 0, 2);
            this.tableLayoutPanel2.Location = new System.Drawing.Point(6, 19);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            this.tableLayoutPanel2.RowCount = 5;
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.tableLayoutPanel2.Size = new System.Drawing.Size(188, 122);
            this.tableLayoutPanel2.TabIndex = 4;
            // 
            // radGBP
            // 
            this.radGBP.AutoSize = true;
            this.radGBP.Location = new System.Drawing.Point(3, 99);
            this.radGBP.Name = "radGBP";
            this.radGBP.Size = new System.Drawing.Size(120, 17);
            this.radGBP.TabIndex = 5;
            this.radGBP.TabStop = true;
            this.radGBP.Text = "Graphic Back Panel";
            this.radGBP.UseVisualStyleBackColor = true;
            this.radGBP.CheckedChanged += new System.EventHandler(this.radGBP_CheckedChanged);
            // 
            // radPCS
            // 
            this.radPCS.AutoSize = true;
            this.radPCS.Location = new System.Drawing.Point(3, 3);
            this.radPCS.Name = "radPCS";
            this.radPCS.Size = new System.Drawing.Size(173, 17);
            this.radPCS.TabIndex = 1;
            this.radPCS.TabStop = true;
            this.radPCS.Text = "Product/Memo Card Small/Mini";
            this.radPCS.UseVisualStyleBackColor = true;
            this.radPCS.CheckedChanged += new System.EventHandler(this.radPCS_CheckedChanged);
            // 
            // radEDS
            // 
            this.radEDS.AutoSize = true;
            this.radEDS.Enabled = false;
            this.radEDS.Location = new System.Drawing.Point(3, 75);
            this.radEDS.Name = "radEDS";
            this.radEDS.Size = new System.Drawing.Size(142, 17);
            this.radEDS.TabIndex = 4;
            this.radEDS.TabStop = true;
            this.radEDS.Text = "Easel Display Sign (WIP)";
            this.radEDS.UseVisualStyleBackColor = true;
            this.radEDS.CheckedChanged += new System.EventHandler(this.radEDS_CheckedChanged);
            // 
            // radPCM
            // 
            this.radPCM.AutoSize = true;
            this.radPCM.Location = new System.Drawing.Point(3, 27);
            this.radPCM.Name = "radPCM";
            this.radPCM.Size = new System.Drawing.Size(161, 17);
            this.radPCM.TabIndex = 2;
            this.radPCM.TabStop = true;
            this.radPCM.Text = "Product/Memo Card Medium";
            this.radPCM.UseVisualStyleBackColor = true;
            this.radPCM.CheckedChanged += new System.EventHandler(this.radPCM_CheckedChanged);
            // 
            // radMGB
            // 
            this.radMGB.AutoSize = true;
            this.radMGB.Location = new System.Drawing.Point(3, 51);
            this.radMGB.Name = "radMGB";
            this.radMGB.Size = new System.Drawing.Size(146, 17);
            this.radMGB.TabIndex = 3;
            this.radMGB.TabStop = true;
            this.radMGB.Text = "Magnetic Graphic Backer";
            this.radMGB.UseVisualStyleBackColor = true;
            this.radMGB.CheckedChanged += new System.EventHandler(this.radMGB_CheckedChanged);
            // 
            // btnOK
            // 
            this.btnOK.Location = new System.Drawing.Point(517, 316);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 16;
            this.btnOK.Text = "Generate";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // ofdPick
            // 
            this.ofdPick.Filter = "Images (*.png; *.jpg; *.jpeg; *.pdf)|*.png; *.jpg; *.jpeg; *.pdf";
            this.ofdPick.Title = "Select artwork...";
            // 
            // lstFiles
            // 
            this.lstFiles.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] { this.colFile, this.colDesc });
            this.lstFiles.Location = new System.Drawing.Point(12, 170);
            this.lstFiles.Name = "lstFiles";
            this.lstFiles.Size = new System.Drawing.Size(580, 129);
            this.lstFiles.TabIndex = 12;
            this.lstFiles.UseCompatibleStateImageBehavior = false;
            this.lstFiles.View = System.Windows.Forms.View.Details;
            // 
            // colFile
            // 
            this.colFile.Text = "Opp Attachments";
            this.colFile.Width = 320;
            // 
            // colDesc
            // 
            this.colDesc.Text = "Description";
            this.colDesc.Width = 250;
            // 
            // btnOpen
            // 
            this.btnOpen.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnOpen.Location = new System.Drawing.Point(3, 3);
            this.btnOpen.Name = "btnOpen";
            this.btnOpen.Size = new System.Drawing.Size(61, 20);
            this.btnOpen.TabIndex = 13;
            this.btnOpen.Text = "Open";
            this.btnOpen.UseVisualStyleBackColor = true;
            this.btnOpen.Click += new System.EventHandler(this.btnOpen_Click);
            // 
            // btnAttachF
            // 
            this.btnAttachF.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnAttachF.Location = new System.Drawing.Point(191, 3);
            this.btnAttachF.Name = "btnAttachF";
            this.btnAttachF.Size = new System.Drawing.Size(61, 20);
            this.btnAttachF.TabIndex = 14;
            this.btnAttachF.Text = "Front";
            this.btnAttachF.UseVisualStyleBackColor = true;
            this.btnAttachF.Click += new System.EventHandler(this.btnAttachF_Click);
            // 
            // btnAttachR
            // 
            this.btnAttachR.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnAttachR.Location = new System.Drawing.Point(258, 3);
            this.btnAttachR.Name = "btnAttachR";
            this.btnAttachR.Size = new System.Drawing.Size(61, 20);
            this.btnAttachR.TabIndex = 15;
            this.btnAttachR.Text = "Rear";
            this.btnAttachR.UseVisualStyleBackColor = true;
            this.btnAttachR.Click += new System.EventHandler(this.btnAttachR_Click);
            // 
            // label1
            // 
            this.label1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label1.Location = new System.Drawing.Point(70, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(115, 26);
            this.label1.TabIndex = 16;
            this.label1.Text = "Assign artwork:";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // tableLayoutPanel3
            // 
            this.tableLayoutPanel3.ColumnCount = 4;
            this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 67F));
            this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 67F));
            this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 67F));
            this.tableLayoutPanel3.Controls.Add(this.btnOpen, 0, 0);
            this.tableLayoutPanel3.Controls.Add(this.label1, 1, 0);
            this.tableLayoutPanel3.Controls.Add(this.btnAttachF, 2, 0);
            this.tableLayoutPanel3.Controls.Add(this.btnAttachR, 3, 0);
            this.tableLayoutPanel3.Location = new System.Drawing.Point(12, 302);
            this.tableLayoutPanel3.Name = "tableLayoutPanel3";
            this.tableLayoutPanel3.RowCount = 1;
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel3.Size = new System.Drawing.Size(322, 26);
            this.tableLayoutPanel3.TabIndex = 17;
            // 
            // FrmCard
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(604, 351);
            this.Controls.Add(this.tableLayoutPanel3);
            this.Controls.Add(this.lstFiles);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.grpType);
            this.Controls.Add(this.tableLayoutPanel1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "FrmCard";
            this.Text = "Product Card Generator";
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.grpType.ResumeLayout(false);
            this.tableLayoutPanel2.ResumeLayout(false);
            this.tableLayoutPanel2.PerformLayout();
            this.tableLayoutPanel3.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.Button btnOpen;
        private System.Windows.Forms.Button btnAttachF;
        private System.Windows.Forms.Button btnAttachR;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel3;

        private System.Windows.Forms.ColumnHeader colFile;
        private System.Windows.Forms.ColumnHeader colDesc;

        private System.Windows.Forms.ListView lstFiles;

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Label lblArtR;
        private System.Windows.Forms.Label lblUPC;
        private System.Windows.Forms.Label lblOpp;
        private System.Windows.Forms.Label lblDesc;
        private System.Windows.Forms.Label lblCust;
        private System.Windows.Forms.Label lblArtF;
        private System.Windows.Forms.TextBox txtOpp;
        private System.Windows.Forms.TextBox txtUPC;
        private System.Windows.Forms.TextBox txtDesc;
        private System.Windows.Forms.TextBox txtCust;
        private System.Windows.Forms.TextBox txtArtF;
        private System.Windows.Forms.TextBox txtArtR;
        private System.Windows.Forms.GroupBox grpType;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private System.Windows.Forms.RadioButton radEDS;
        private System.Windows.Forms.RadioButton radMGB;
        private System.Windows.Forms.RadioButton radPCS;
        private System.Windows.Forms.RadioButton radPCM;
        private System.Windows.Forms.Button btnPickR;
        private System.Windows.Forms.Button btnPickF;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.OpenFileDialog ofdPick;
        private System.Windows.Forms.RadioButton radGBP;
    }
}

