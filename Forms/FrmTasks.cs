using System;
using System.Data;
using System.IO;
using System.Windows.Forms;

namespace BlueBrick
{
    public partial class FrmTasks : Form
    {
        private readonly FrmPane _fstat;
        public string SOpp;
        private readonly bool _bSilent = true;

        public FrmTasks(FrmPane stat, string folder)
        {
            InitializeComponent();
            _fstat = stat;
            var eng = new DataTable();

            //import engineer xml file
            var dataFile = string.Empty;
            if (folder != null) dataFile = Path.Combine(folder, @"DataFiles\qryEngineers.xml");
            if (dataFile.Length != 0)
            {
                var import = new DataSet();
                if (File.Exists(dataFile))
                    import.ReadXml(dataFile);
                eng = import.Tables["qryEngineers"];
                eng.DefaultView.Sort = "FullName ASC";
                eng = eng.DefaultView.ToTable();
            }

            //retrieve people
            cmbPersonnel.ValueMember = "WorkForce";
            cmbPersonnel.DisplayMember = "FullName";
            cmbPersonnel.DataSource = eng;
            var user = Environment.GetEnvironmentVariable(@"username");
            var search = eng.Select("UserName ='" + user + "'");
            if (search.Length != 0)
                cmbPersonnel.Text = search[0]["FullName"].ToString();
            _bSilent = false;
            GetTask(_fstat);
        }

        private void btnUserOk_Click(object sender, EventArgs e)
        {
            if (lstUserTasks.SelectedItems.Count != 0)
                SOpp = lstUserTasks.SelectedItems[0].Text;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnUserCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void cmbPersonnel_SelectedIndexChanged(object sender, EventArgs e)
        {
            GetTask(_fstat);
        }

        private void GetTask(FrmPane stat)
        {
            var bClear = false;
            string[,] asRecord = default;
            var user = cmbPersonnel.SelectedValue.ToString();
            if (user.Length > 0)
            {
                asRecord = ClsEpicor.TaskSearch(stat, user, true);
                if (asRecord == null)
                {
                    bClear = true;
                    if (!_bSilent) stat.SetStat(100, @"No results for user.", true);
                }
            }
            else
            {
                bClear = true;
            }

            if (asRecord != null)
            {
                lstUserTasks.Items.Clear();
                for (var i = 0; i < asRecord.GetUpperBound(0) + 1; i++)
                {
                    var li = new ListViewItem(asRecord[i, 0]);
                    li.SubItems.Add(asRecord[i, 5]); //desc
                    li.SubItems.Add(asRecord[i, 4]); //customer
                    DateTime.TryParse(asRecord[i, 2], out var dt);
                    // ReSharper disable once LocalizableElement
                    li.SubItems.Add($"{dt:M/d/yyyy}"); //due
                    lstUserTasks.Items.Add(li);
                }
            }

            if (!bClear) return;
            lstUserTasks.Items.Clear();
        }
    }
}