using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BlueBrick
{
    public partial class FrmQueue : Form
    {
        private readonly FrmPane _frmStat;
        public FrmQueue(FrmPane frmStat)
        {
            _frmStat = frmStat;
            InitializeComponent();
            GetTasks();
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            GetTasks();
        }

        private void GetTasks()
        {
            //connection string
            const string sConn = @"Server=PDMDBTX;Database=PDMVault;Trusted_Connection=True;";

            //get active tasks
            try
            {
                //build query
                var sSql = @"SELECT TaskName, UserName, StartTime, EndTime, ";
                sSql += @"CASE WHEN TaskStatus = 2 THEN 'Starting Up' WHEN TaskStatus = 3 THEN 'In process' ";
                sSql += @"ELSE CONVERT(nvarchar(25), TaskStatus) END AS 'Status', ";
                sSql += @"ProgressPos, DocString ";
                sSql += @"FROM TaskInstances ti ";
                sSql += @"INNER JOIN Tasks ON Tasks.TaskID = ti.TaskID ";
                sSql += @"INNER JOIN Users ON Users.UserID = ti.InitUserID ";
                sSql += @"WHERE TaskStatus BETWEEN 1 AND 6 ORDER BY TaskInstanceID DESC;";

                //grab records to list view items
                var lstResults = new List<ListViewItem>();
                using (var conn = new SqlConnection(sConn))
                {
                    var sqlCom = new SqlCommand(sSql, conn);
                    conn.Open();
                    var sqlRead = sqlCom.ExecuteReader();
                    if (sqlRead.HasRows)
                        while (sqlRead.Read())
                        {
                            var lstRow = new ListViewItem(sqlRead[0].ToString(), 0);
                            lstRow.SubItems.Add(sqlRead[1].ToString());
                            var val = sqlRead[2].ToString();
                            if (val != "")
                            {
                                var ts = DateTime.Parse(val);
                                ts = TimeZoneInfo.ConvertTimeFromUtc(ts, TimeZoneInfo.Local);
                                val = ts.ToString("g");
                            }
                            lstRow.SubItems.Add(val);
                            val = sqlRead[3].ToString();
                            if (val != "")
                            {
                                var ts = DateTime.Parse(val);
                                ts = TimeZoneInfo.ConvertTimeFromUtc(ts, TimeZoneInfo.Local);
                                val = ts.ToString("g");
                            }
                            lstRow.SubItems.Add(val);
                            lstRow.SubItems.Add(sqlRead[4].ToString());
                            lstRow.SubItems.Add(sqlRead[5].ToString());
                            lstRow.SubItems.Add(sqlRead[6].ToString());
                            lstResults.Add(lstRow);
                        }
                    sqlRead.Close();
                }

                //create array and add records
                var iCnt = 0;
                var lviResults = new ListViewItem[lstResults.Count];
                foreach (var l in lstResults)
                {
                    lviResults[iCnt] = l;
                    iCnt++;
                }

                //populate form listview with records
                lvwQueue.Items.Clear();
                lvwQueue.Items.AddRange(lviResults);
            }
            catch (COMException ex)
            {
                _frmStat.SetStat(100, @"HRESULT = 0x" + ex.ErrorCode.ToString(@"X") + @" " + ex.Message, true);
            }
            catch (Exception ex)
            {
                _frmStat.SetStat(100, ex.Message, true);
            }

            //get history tasks
            try
            {
                //build query
                var sSql = @"SELECT TOP 50 TaskName, UserName, StartTime, EndTime, ";
                sSql += @"CASE WHEN TaskStatus = 7 THEN 'OK' WHEN TaskStatus = 8 THEN 'Cancelled' ";
                sSql += @"WHEN TaskStatus = 9 THEN 'Failed' END AS 'Status', ";
                sSql += @"Hresult, CustomErrMsg, DocString ";
                sSql += @"FROM TaskInstances ti ";
                sSql += @"INNER JOIN Tasks ON Tasks.TaskID = ti.TaskID ";
                sSql += @"INNER JOIN Users ON Users.UserID = ti.InitUserID ";
                sSql += @"WHERE TaskStatus IN (7, 8, 9) ORDER BY TaskInstanceID DESC;";

                //grab records to list view items
                var lstResults = new List<ListViewItem>();
                using (var conn = new SqlConnection(sConn))
                {
                    var sqlCom = new SqlCommand(sSql, conn);
                    conn.Open();
                    var sqlRead = sqlCom.ExecuteReader();
                    if (sqlRead.HasRows)
                        while (sqlRead.Read())
                        {
                            var lstRow = new ListViewItem(sqlRead[0].ToString(), 0);
                            lstRow.SubItems.Add(sqlRead[1].ToString());
                            var val = "";
                            var chk = DateTime.TryParse(sqlRead[2].ToString(), out var ts);
                            if (chk)
                            {
                                ts = TimeZoneInfo.ConvertTimeFromUtc(ts, TimeZoneInfo.Local);
                                val = ts.ToString("g");
                            }
                            lstRow.SubItems.Add(val);
                            val = "";
                            chk = DateTime.TryParse(sqlRead[3].ToString(), out ts);
                            if (chk)
                            {
                                ts = TimeZoneInfo.ConvertTimeFromUtc(ts, TimeZoneInfo.Local);
                                val = ts.ToString("g");
                            }
                            lstRow.SubItems.Add(val);
                            lstRow.SubItems.Add(sqlRead[4].ToString());
                            lstRow.SubItems.Add(sqlRead[5].ToString());
                            lstRow.SubItems.Add(sqlRead[6].ToString());
                            lstRow.SubItems.Add(sqlRead[7].ToString());
                            lstResults.Add(lstRow);
                        }
                    sqlRead.Close();
                }

                //create array and add records
                var iCnt = 0;
                var lviResults = new ListViewItem[lstResults.Count];
                foreach (var l in lstResults)
                {
                    lviResults[iCnt] = l;
                    iCnt++;
                }

                //populate form listview with records
                lvwHist.Items.Clear();
                lvwHist.Items.AddRange(lviResults);
            }
            catch (COMException ex)
            {
                _frmStat.SetStat(100, @"HRESULT = 0x" + ex.ErrorCode.ToString(@"X") + @" " + ex.Message, true);
            }
            catch (Exception ex)
            {
                _frmStat.SetStat(100, ex.Message, true);
            }
        }
    }
}