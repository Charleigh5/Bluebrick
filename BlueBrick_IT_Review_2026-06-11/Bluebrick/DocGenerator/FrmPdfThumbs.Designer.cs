using System.ComponentModel;

namespace DocGenerator
{
    partial class FrmPdfThumbs
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
            this.imlThumbs = new System.Windows.Forms.ImageList(this.components);
            this.lstThumbs = new System.Windows.Forms.ListView();
            this.picThumb = new System.Windows.Forms.PictureBox();
            this.btnConfirm = new System.Windows.Forms.Button();
            this.lblDisclaim = new System.Windows.Forms.Label();
            this.btnAll = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.picThumb)).BeginInit();
            this.SuspendLayout();
            //
            // imlThumbs
            //
            this.imlThumbs.ColorDepth = System.Windows.Forms.ColorDepth.Depth4Bit;
            this.imlThumbs.ImageSize = new System.Drawing.Size(256, 175);
            this.imlThumbs.TransparentColor = System.Drawing.Color.Transparent;
            //
            // lstThumbs
            //
            this.lstThumbs.LargeImageList = this.imlThumbs;
            this.lstThumbs.Location = new System.Drawing.Point(16, 15);
            this.lstThumbs.Margin = new System.Windows.Forms.Padding(4);
            this.lstThumbs.Name = "lstThumbs";
            this.lstThumbs.Size = new System.Drawing.Size(360, 772);
            this.lstThumbs.TabIndex = 0;
            this.lstThumbs.UseCompatibleStateImageBehavior = false;
            this.lstThumbs.SelectedIndexChanged += new System.EventHandler(this.lstThumbs_SelectedIndexChanged);
            //
            // picThumb
            //
            this.picThumb.Enabled = false;
            this.picThumb.Location = new System.Drawing.Point(384, 15);
            this.picThumb.Margin = new System.Windows.Forms.Padding(4);
            this.picThumb.Name = "picThumb";
            this.picThumb.Size = new System.Drawing.Size(980, 772);
            this.picThumb.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.picThumb.TabIndex = 1;
            this.picThumb.TabStop = false;
            //
            // btnConfirm
            //
            this.btnConfirm.Location = new System.Drawing.Point(1251, 791);
            this.btnConfirm.Margin = new System.Windows.Forms.Padding(4);
            this.btnConfirm.Name = "btnConfirm";
            this.btnConfirm.Size = new System.Drawing.Size(116, 31);
            this.btnConfirm.TabIndex = 2;
            this.btnConfirm.Text = "Add Selected";
            this.btnConfirm.UseVisualStyleBackColor = true;
            this.btnConfirm.Click += new System.EventHandler(this.btnConfirm_Click);
            //
            // lblDisclaim
            //
            this.lblDisclaim.Location = new System.Drawing.Point(16, 791);
            this.lblDisclaim.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblDisclaim.Name = "lblDisclaim";
            this.lblDisclaim.Size = new System.Drawing.Size(1021, 21);
            this.lblDisclaim.TabIndex = 3;
            this.lblDisclaim.Text = "The displayed drawings will not be included in the final packet unless selected. " + "Close this window to ignore all.";
            //
            // btnAll
            //
            this.btnAll.Location = new System.Drawing.Point(1116, 791);
            this.btnAll.Name = "btnAll";
            this.btnAll.Size = new System.Drawing.Size(116, 31);
            this.btnAll.TabIndex = 1;
            this.btnAll.Text = "Add All";
            this.btnAll.UseVisualStyleBackColor = true;
            this.btnAll.Click += new System.EventHandler(this.btnAll_Click);
            //
            // FrmPdfThumbs
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1380, 830);
            this.Controls.Add(this.btnAll);
            this.Controls.Add(this.lblDisclaim);
            this.Controls.Add(this.btnConfirm);
            this.Controls.Add(this.picThumb);
            this.Controls.Add(this.lstThumbs);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Margin = new System.Windows.Forms.Padding(4);
            this.MaximizeBox = false;
            this.Name = "FrmPdfThumbs";
            this.ShowIcon = false;
            this.Text = "Select drawings to add to packet...";
            this.TopMost = true;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FrmPdfThumbs_FormClosing);
            ((System.ComponentModel.ISupportInitialize)(this.picThumb)).EndInit();
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.Button btnAll;

        private System.Windows.Forms.Button btnConfirm;

        private System.Windows.Forms.PictureBox picThumb;

        public System.Windows.Forms.ListView lstThumbs;

        public System.Windows.Forms.ImageList imlThumbs;

        public System.Windows.Forms.Label lblDisclaim;

        #endregion
    }
}