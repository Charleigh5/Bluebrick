using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using static System.Drawing.FontStyle;
using static System.Drawing.GraphicsUnit;

namespace BlueBrick
{
    partial class CollapsePanel
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

        #region Component Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CollapsePanel));
            this.tlpMain = new System.Windows.Forms.TableLayoutPanel();
            this.tlpTitleBar = new System.Windows.Forms.TableLayoutPanel();
            this.picDirIcon = new System.Windows.Forms.PictureBox();
            this.lblTitle = new System.Windows.Forms.Label();
            this.pnlContent = new System.Windows.Forms.Panel();
            this.tlpMain.SuspendLayout();
            this.tlpTitleBar.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picDirIcon)).BeginInit();
            this.SuspendLayout();
            // 
            // tlpMain
            // 
            this.tlpMain.AutoSize = true;
            this.tlpMain.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.tlpMain.CellBorderStyle = System.Windows.Forms.TableLayoutPanelCellBorderStyle.Single;
            this.tlpMain.ColumnCount = 1;
            this.tlpMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpMain.Controls.Add(this.tlpTitleBar, 0, 0);
            this.tlpMain.Controls.Add(this.pnlContent, 0, 1);
            this.tlpMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpMain.Location = new System.Drawing.Point(0, 0);
            this.tlpMain.Margin = new System.Windows.Forms.Padding(4);
            this.tlpMain.Name = "tlpMain";
            this.tlpMain.RowCount = 2;
            this.tlpMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 22F));
            this.tlpMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpMain.Size = new System.Drawing.Size(280, 292);
            this.tlpMain.TabIndex = 0;
            // 
            // tlpTitleBar
            // 
            this.tlpTitleBar.AutoSize = true;
            this.tlpTitleBar.BackColor = System.Drawing.SystemColors.ControlDark;
            this.tlpTitleBar.ColumnCount = 2;
            this.tlpTitleBar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpTitleBar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 22F));
            this.tlpTitleBar.Controls.Add(this.picDirIcon, 1, 0);
            this.tlpTitleBar.Controls.Add(this.lblTitle, 0, 0);
            this.tlpTitleBar.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpTitleBar.Location = new System.Drawing.Point(1, 1);
            this.tlpTitleBar.Margin = new System.Windows.Forms.Padding(0);
            this.tlpTitleBar.Name = "tlpTitleBar";
            this.tlpTitleBar.RowCount = 1;
            this.tlpTitleBar.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpTitleBar.Size = new System.Drawing.Size(276, 22);
            this.tlpTitleBar.TabIndex = 0;
            this.tlpTitleBar.Click += new System.EventHandler(this.tlpTitleBar_Click);
            // 
            // picDirIcon
            // 
            this.picDirIcon.Dock = System.Windows.Forms.DockStyle.Fill;
            this.picDirIcon.Image = ((System.Drawing.Image)(resources.GetObject("picDirIcon.Image")));
            this.picDirIcon.InitialImage = ((System.Drawing.Image)(resources.GetObject("picDirIcon.InitialImage")));
            this.picDirIcon.Location = new System.Drawing.Point(254, 0);
            this.picDirIcon.Margin = new System.Windows.Forms.Padding(0);
            this.picDirIcon.Name = "picDirIcon";
            this.picDirIcon.Size = new System.Drawing.Size(22, 22);
            this.picDirIcon.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
            this.picDirIcon.TabIndex = 1;
            this.picDirIcon.TabStop = false;
            this.picDirIcon.Click += new System.EventHandler(this.picDirIcon_Click);
            // 
            // lblTitle
            // 
            this.lblTitle.AutoSize = true;
            this.lblTitle.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblTitle.Location = new System.Drawing.Point(0, 0);
            this.lblTitle.Margin = new System.Windows.Forms.Padding(0);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(254, 22);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "Title";
            this.lblTitle.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.lblTitle.Click += new System.EventHandler(this.lblTitle_Click);
            // 
            // pnlContent
            // 
            this.pnlContent.AutoSize = true;
            this.pnlContent.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlContent.Location = new System.Drawing.Point(1, 24);
            this.pnlContent.Margin = new System.Windows.Forms.Padding(0);
            this.pnlContent.Name = "pnlContent";
            this.pnlContent.Size = new System.Drawing.Size(276, 265);
            this.pnlContent.TabIndex = 1;
            // 
            // CollapsePanel
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoScroll = true;
            this.Controls.Add(this.tlpMain);
            this.Margin = new System.Windows.Forms.Padding(0);
            this.MaximumSize = new System.Drawing.Size(1000, 999);
            this.MinimumSize = new System.Drawing.Size(120, 22);
            this.Name = "CollapsePanel";
            this.Size = new System.Drawing.Size(280, 292);
            this.tlpMain.ResumeLayout(false);
            this.tlpMain.PerformLayout();
            this.tlpTitleBar.ResumeLayout(false);
            this.tlpTitleBar.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picDirIcon)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.PictureBox picDirIcon;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.TableLayoutPanel tlpMain;
        private System.Windows.Forms.TableLayoutPanel tlpTitleBar;
        private System.Windows.Forms.Panel pnlContent;

        #endregion
    }
}