using System.ComponentModel;

namespace DocGenerator
{
    partial class FrmThumbs
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
            this.lstThumbs.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.lstThumbs.MultiSelect = false;
            this.lstThumbs.Name = "lstThumbs";
            this.lstThumbs.Size = new System.Drawing.Size(352, 741);
            this.lstThumbs.TabIndex = 0;
            this.lstThumbs.UseCompatibleStateImageBehavior = false;
            this.lstThumbs.SelectedIndexChanged += new System.EventHandler(this.lstThumbs_SelectedIndexChanged);
            this.lstThumbs.DoubleClick += new System.EventHandler(this.lstThumbs_DoubleClick);
            //
            // picThumb
            //
            this.picThumb.Enabled = false;
            this.picThumb.Location = new System.Drawing.Point(376, 15);
            this.picThumb.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.picThumb.Name = "picThumb";
            this.picThumb.Size = new System.Drawing.Size(988, 741);
            this.picThumb.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.picThumb.TabIndex = 1;
            this.picThumb.TabStop = false;
            //
            // FrmThumbs
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1380, 763);
            this.Controls.Add(this.picThumb);
            this.Controls.Add(this.lstThumbs);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.MaximizeBox = false;
            this.Name = "FrmThumbs";
            this.ShowIcon = false;
            this.Text = "DXF Preview";
            this.TopMost = true;
            ((System.ComponentModel.ISupportInitialize)(this.picThumb)).EndInit();
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.PictureBox picThumb;

        public System.Windows.Forms.ListView lstThumbs;

        public System.Windows.Forms.ImageList imlThumbs;

        #endregion
    }
}