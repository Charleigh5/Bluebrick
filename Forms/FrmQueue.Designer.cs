using System.ComponentModel;

namespace BlueBrick
{
    partial class FrmQueue
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
            this.tlpMain = new System.Windows.Forms.TableLayoutPanel();
            this.lblQueue = new System.Windows.Forms.Label();
            this.lblHist = new System.Windows.Forms.Label();
            this.lvwQueue = new System.Windows.Forms.ListView();
            this.colQTask = new System.Windows.Forms.ColumnHeader();
            this.colQUser = new System.Windows.Forms.ColumnHeader();
            this.colQStart = new System.Windows.Forms.ColumnHeader();
            this.colQEnd = new System.Windows.Forms.ColumnHeader();
            this.colQStat = new System.Windows.Forms.ColumnHeader();
            this.colQProg = new System.Windows.Forms.ColumnHeader();
            this.colQLog = new System.Windows.Forms.ColumnHeader();
            this.lvwHist = new System.Windows.Forms.ListView();
            this.colHName = new System.Windows.Forms.ColumnHeader();
            this.colHUser = new System.Windows.Forms.ColumnHeader();
            this.colHStart = new System.Windows.Forms.ColumnHeader();
            this.colHEnd = new System.Windows.Forms.ColumnHeader();
            this.colHStat = new System.Windows.Forms.ColumnHeader();
            this.colHErrNum = new System.Windows.Forms.ColumnHeader();
            this.colHErrMsg = new System.Windows.Forms.ColumnHeader();
            this.colHLog = new System.Windows.Forms.ColumnHeader();
            this.btnClose = new System.Windows.Forms.Button();
            this.btnRefresh = new System.Windows.Forms.Button();
            this.tlpMain.SuspendLayout();
            this.SuspendLayout();
            // 
            // tlpMain
            // 
            this.tlpMain.ColumnCount = 2;
            this.tlpMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 88F));
            this.tlpMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 12F));
            this.tlpMain.Controls.Add(this.lblQueue, 0, 0);
            this.tlpMain.Controls.Add(this.lblHist, 0, 2);
            this.tlpMain.Controls.Add(this.lvwQueue, 0, 1);
            this.tlpMain.Controls.Add(this.lvwHist, 0, 3);
            this.tlpMain.Controls.Add(this.btnClose, 1, 4);
            this.tlpMain.Controls.Add(this.btnRefresh, 0, 4);
            this.tlpMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpMain.Location = new System.Drawing.Point(0, 0);
            this.tlpMain.Name = "tlpMain";
            this.tlpMain.RowCount = 5;
            this.tlpMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 4F));
            this.tlpMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 42F));
            this.tlpMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 5F));
            this.tlpMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 42F));
            this.tlpMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 7F));
            this.tlpMain.Size = new System.Drawing.Size(800, 587);
            this.tlpMain.TabIndex = 0;
            // 
            // lblQueue
            // 
            this.lblQueue.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblQueue.Location = new System.Drawing.Point(3, 0);
            this.lblQueue.Name = "lblQueue";
            this.lblQueue.Size = new System.Drawing.Size(698, 23);
            this.lblQueue.TabIndex = 1;
            this.lblQueue.Text = "Queued:";
            this.lblQueue.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
            // 
            // lblHist
            // 
            this.lblHist.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblHist.Location = new System.Drawing.Point(3, 269);
            this.lblHist.Name = "lblHist";
            this.lblHist.Size = new System.Drawing.Size(698, 29);
            this.lblHist.TabIndex = 2;
            this.lblHist.Text = "History:";
            this.lblHist.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
            // 
            // lvwQueue
            // 
            this.lvwQueue.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] { this.colQTask, this.colQUser, this.colQStart, this.colQEnd, this.colQStat, this.colQProg, this.colQLog });
            this.tlpMain.SetColumnSpan(this.lvwQueue, 2);
            this.lvwQueue.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lvwQueue.HideSelection = false;
            this.lvwQueue.Location = new System.Drawing.Point(6, 29);
            this.lvwQueue.Margin = new System.Windows.Forms.Padding(6);
            this.lvwQueue.Name = "lvwQueue";
            this.lvwQueue.Size = new System.Drawing.Size(788, 234);
            this.lvwQueue.TabIndex = 4;
            this.lvwQueue.UseCompatibleStateImageBehavior = false;
            this.lvwQueue.View = System.Windows.Forms.View.Details;
            // 
            // colQTask
            // 
            this.colQTask.Text = "Name";
            this.colQTask.Width = 150;
            // 
            // colQUser
            // 
            this.colQUser.Text = "User";
            this.colQUser.Width = 90;
            // 
            // colQStart
            // 
            this.colQStart.Text = "Start";
            this.colQStart.Width = 120;
            // 
            // colQEnd
            // 
            this.colQEnd.Text = "End";
            this.colQEnd.Width = 120;
            // 
            // colQStat
            // 
            this.colQStat.Text = "Status";
            this.colQStat.Width = 85;
            // 
            // colQProg
            // 
            this.colQProg.Text = "Progress";
            // 
            // colQLog
            // 
            this.colQLog.Text = "Log";
            this.colQLog.Width = 250;
            // 
            // lvwHist
            // 
            this.lvwHist.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] { this.colHName, this.colHUser, this.colHStart, this.colHEnd, this.colHStat, this.colHErrNum, this.colHErrMsg, this.colHLog });
            this.tlpMain.SetColumnSpan(this.lvwHist, 2);
            this.lvwHist.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lvwHist.HideSelection = false;
            this.lvwHist.Location = new System.Drawing.Point(6, 304);
            this.lvwHist.Margin = new System.Windows.Forms.Padding(6);
            this.lvwHist.Name = "lvwHist";
            this.lvwHist.Size = new System.Drawing.Size(788, 234);
            this.lvwHist.TabIndex = 5;
            this.lvwHist.UseCompatibleStateImageBehavior = false;
            this.lvwHist.View = System.Windows.Forms.View.Details;
            // 
            // colHName
            // 
            this.colHName.Text = "Name";
            this.colHName.Width = 150;
            // 
            // colHUser
            // 
            this.colHUser.Text = "User";
            this.colHUser.Width = 90;
            // 
            // colHStart
            // 
            this.colHStart.Text = "Start";
            this.colHStart.Width = 120;
            // 
            // colHEnd
            // 
            this.colHEnd.Text = "End";
            this.colHEnd.Width = 120;
            // 
            // colHStat
            // 
            this.colHStat.Text = "Status";
            this.colHStat.Width = 85;
            // 
            // colHErrNum
            // 
            this.colHErrNum.Text = "Error";
            this.colHErrNum.Width = 75;
            // 
            // colHErrMsg
            // 
            this.colHErrMsg.Text = "Error Message";
            this.colHErrMsg.Width = 250;
            // 
            // colHLog
            // 
            this.colHLog.Text = "Log";
            this.colHLog.Width = 250;
            // 
            // btnClose
            // 
            this.btnClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClose.Location = new System.Drawing.Point(717, 556);
            this.btnClose.Margin = new System.Windows.Forms.Padding(8);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(75, 23);
            this.btnClose.TabIndex = 0;
            this.btnClose.Text = "Close";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            // 
            // btnRefresh
            // 
            this.btnRefresh.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnRefresh.Location = new System.Drawing.Point(621, 556);
            this.btnRefresh.Margin = new System.Windows.Forms.Padding(8);
            this.btnRefresh.Name = "btnRefresh";
            this.btnRefresh.Size = new System.Drawing.Size(75, 23);
            this.btnRefresh.TabIndex = 6;
            this.btnRefresh.Text = "Refresh";
            this.btnRefresh.UseVisualStyleBackColor = true;
            this.btnRefresh.Click += new System.EventHandler(this.btnRefresh_Click);
            // 
            // FrmQueue
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 587);
            this.Controls.Add(this.tlpMain);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FrmQueue";
            this.Text = "PDM Server Task Queue";
            this.tlpMain.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.ColumnHeader colHName;
        private System.Windows.Forms.ColumnHeader colHUser;
        private System.Windows.Forms.ColumnHeader colHStart;
        private System.Windows.Forms.ColumnHeader colHEnd;
        private System.Windows.Forms.ColumnHeader colHStat;
        private System.Windows.Forms.ColumnHeader colHErrNum;
        private System.Windows.Forms.ColumnHeader colHErrMsg;
        private System.Windows.Forms.ColumnHeader colHLog;

        private System.Windows.Forms.ColumnHeader colQTask;
        private System.Windows.Forms.ColumnHeader colQUser;
        private System.Windows.Forms.ColumnHeader colQStart;
        private System.Windows.Forms.ColumnHeader colQEnd;
        private System.Windows.Forms.ColumnHeader colQStat;
        private System.Windows.Forms.ColumnHeader colQProg;
        private System.Windows.Forms.ColumnHeader colQLog;

        private System.Windows.Forms.Button btnRefresh;

        private System.Windows.Forms.ListView lvwQueue;
        private System.Windows.Forms.ListView lvwHist;

        private System.Windows.Forms.Label lblQueue;
        private System.Windows.Forms.Label lblHist;

        private System.Windows.Forms.Button btnClose;

        private System.Windows.Forms.TableLayoutPanel tlpMain;

        #endregion
    }
}