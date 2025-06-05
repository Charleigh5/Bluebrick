using System.ComponentModel;

namespace BlueBrick
{
    partial class FrmTasks
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
            this.lblUserName = new System.Windows.Forms.Label();
            this.lstUserTasks = new System.Windows.Forms.ListView();
            this.colTaskOpp = new System.Windows.Forms.ColumnHeader();
            this.colTaskDesc = new System.Windows.Forms.ColumnHeader();
            this.colTaskCust = new System.Windows.Forms.ColumnHeader();
            this.colTaskDue = new System.Windows.Forms.ColumnHeader();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.btnUserCancel = new System.Windows.Forms.Button();
            this.btnUserOk = new System.Windows.Forms.Button();
            this.cmbPersonnel = new System.Windows.Forms.ComboBox();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // lblUserName
            // 
            this.lblUserName.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblUserName.Location = new System.Drawing.Point(9, 6);
            this.lblUserName.Name = "lblUserName";
            this.lblUserName.Size = new System.Drawing.Size(114, 30);
            this.lblUserName.TabIndex = 0;
            this.lblUserName.Text = "Epicor User Name";
            this.lblUserName.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lstUserTasks
            // 
            this.lstUserTasks.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] { this.colTaskOpp, this.colTaskDesc, this.colTaskCust, this.colTaskDue });
            this.tableLayoutPanel1.SetColumnSpan(this.lstUserTasks, 5);
            this.lstUserTasks.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lstUserTasks.HideSelection = false;
            this.lstUserTasks.Location = new System.Drawing.Point(9, 39);
            this.lstUserTasks.Name = "lstUserTasks";
            this.lstUserTasks.Size = new System.Drawing.Size(565, 352);
            this.lstUserTasks.TabIndex = 2;
            this.lstUserTasks.UseCompatibleStateImageBehavior = false;
            this.lstUserTasks.View = System.Windows.Forms.View.Details;
            // 
            // colTaskOpp
            // 
            this.colTaskOpp.Text = "Opp";
            // 
            // colTaskDesc
            // 
            this.colTaskDesc.Text = "Decription";
            this.colTaskDesc.Width = 380;
            // 
            // colTaskCust
            // 
            this.colTaskCust.Text = "Customer";
            this.colTaskCust.Width = 200;
            // 
            // colTaskDue
            // 
            this.colTaskDue.Text = "Due";
            this.colTaskDue.Width = 80;
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 5;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 120F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 150F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 100F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 100F));
            this.tableLayoutPanel1.Controls.Add(this.lblUserName, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.lstUserTasks, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.btnUserCancel, 4, 2);
            this.tableLayoutPanel1.Controls.Add(this.btnUserOk, 3, 2);
            this.tableLayoutPanel1.Controls.Add(this.cmbPersonnel, 1, 0);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.Padding = new System.Windows.Forms.Padding(6);
            this.tableLayoutPanel1.RowCount = 3;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 50F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(583, 450);
            this.tableLayoutPanel1.TabIndex = 3;
            // 
            // btnUserCancel
            // 
            this.btnUserCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnUserCancel.Location = new System.Drawing.Point(499, 418);
            this.btnUserCancel.Name = "btnUserCancel";
            this.btnUserCancel.Size = new System.Drawing.Size(75, 23);
            this.btnUserCancel.TabIndex = 4;
            this.btnUserCancel.Text = "Cancel";
            this.btnUserCancel.UseVisualStyleBackColor = true;
            this.btnUserCancel.Click += new System.EventHandler(this.btnUserCancel_Click);
            // 
            // btnUserOk
            // 
            this.btnUserOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnUserOk.Location = new System.Drawing.Point(399, 418);
            this.btnUserOk.Name = "btnUserOk";
            this.btnUserOk.Size = new System.Drawing.Size(75, 23);
            this.btnUserOk.TabIndex = 3;
            this.btnUserOk.Text = "OK";
            this.btnUserOk.UseVisualStyleBackColor = true;
            this.btnUserOk.Click += new System.EventHandler(this.btnUserOk_Click);
            // 
            // cmbPersonnel
            // 
            this.cmbPersonnel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cmbPersonnel.FormattingEnabled = true;
            this.cmbPersonnel.Location = new System.Drawing.Point(128, 8);
            this.cmbPersonnel.Margin = new System.Windows.Forms.Padding(2);
            this.cmbPersonnel.Name = "cmbPersonnel";
            this.cmbPersonnel.Size = new System.Drawing.Size(146, 21);
            this.cmbPersonnel.TabIndex = 1;
            this.cmbPersonnel.SelectedIndexChanged += new System.EventHandler(this.cmbPersonnel_SelectedIndexChanged);
            // 
            // FrmTasks
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(583, 450);
            this.Controls.Add(this.tableLayoutPanel1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FrmTasks";
            this.ShowIcon = false;
            this.Text = "List User Tasks";
            this.tableLayoutPanel1.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.ComboBox cmbPersonnel;

        private System.Windows.Forms.Button btnUserCancel;
        private System.Windows.Forms.Button btnUserOk;

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;

        private System.Windows.Forms.ColumnHeader colTaskOpp;
        private System.Windows.Forms.ColumnHeader colTaskDesc;
        private System.Windows.Forms.ColumnHeader colTaskCust;
        private System.Windows.Forms.ColumnHeader colTaskDue;

        private System.Windows.Forms.ListView lstUserTasks;

        private System.Windows.Forms.Label lblUserName;

        #endregion
    }
}