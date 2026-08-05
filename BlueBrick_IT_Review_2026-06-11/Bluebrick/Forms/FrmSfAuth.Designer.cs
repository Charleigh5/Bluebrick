using System.ComponentModel;

namespace BlueBrick
{
    partial class FrmSfAuth
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
            this.wvwSfLogin = new Microsoft.Web.WebView2.WinForms.WebView2();
            ((System.ComponentModel.ISupportInitialize)(this.wvwSfLogin)).BeginInit();
            this.SuspendLayout();
            // 
            // wvwSfLogin
            // 
            this.wvwSfLogin.AllowExternalDrop = true;
            this.wvwSfLogin.CreationProperties = null;
            this.wvwSfLogin.DefaultBackgroundColor = System.Drawing.Color.White;
            this.wvwSfLogin.Dock = System.Windows.Forms.DockStyle.Fill;
            this.wvwSfLogin.Location = new System.Drawing.Point(0, 0);
            this.wvwSfLogin.Name = "wvwSfLogin";
            this.wvwSfLogin.Size = new System.Drawing.Size(514, 771);
            this.wvwSfLogin.TabIndex = 0;
            this.wvwSfLogin.ZoomFactor = 1D;
            this.wvwSfLogin.NavigationStarting += new System.EventHandler<Microsoft.Web.WebView2.Core.CoreWebView2NavigationStartingEventArgs>(this.wvwSfLogin_NavigationStarting);
            // 
            // FrmSfAuth
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(514, 771);
            this.Controls.Add(this.wvwSfLogin);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FrmSfAuth";
            this.Text = "SalesForce Login";
            ((System.ComponentModel.ISupportInitialize)(this.wvwSfLogin)).EndInit();
            this.ResumeLayout(false);
        }

        private Microsoft.Web.WebView2.WinForms.WebView2 wvwSfLogin;

        #endregion
    }
}