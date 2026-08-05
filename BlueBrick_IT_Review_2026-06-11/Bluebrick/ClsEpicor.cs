using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Runtime.InteropServices;
using System.Data;
using System.Diagnostics;
using System.Windows.Forms;

namespace BlueBrick
{
    internal static class ClsEpicor
    {
        internal static void EpiSearch(FrmPane frmStat, string sPart, string sDesc)
        {
            try
            {
                //connection string
                const string sConn = @"Server=EPICOR10;Database=Vira;Trusted_Connection=True;";

                //build query
                var sSql = @"SELECT [Part].[PartNum], [Part].[PartDescription], [Part].[IUM], [PartOH].[OH], [PartOH].[Alloc] ";
                sSql += @"FROM [EpicorProd].[Erp].[Part] ";
                sSql += @"LEFT JOIN (" +
                        @"SELECT [PartNum], SUM([OnHandQty]) AS [OH], SUM([AllocatedQty]) AS [Alloc] " +
                        @"FROM [EpicorProd].[Erp].[PartWhse] " +
                        @"WHERE ([Company] = N'VIRAINS') " +
                        @"GROUP BY [PartNum]) AS [PartOH] " +
                        @"ON [Part].[PartNum] = [PartOH].[PartNum] ";
                sSql += @"WHERE (([Part].[Company] = N'VIRAINS') ";

                //build where criteria and add
                var sTemp = SearchCriteria(@"[Part].[PartNum]", sPart);
                if (sTemp.Length > 0) sSql += @"AND " + sTemp;
                sTemp = SearchCriteria(@"[Part].[PartDescription]", sDesc);
                if (sTemp.Length > 0) sSql += @"AND " + sTemp;
                sSql += @") ";

                //finish query string
                sSql += "ORDER BY [Part].[PartNum];";

                //grab records to list view items
                frmStat.SetStat(0, @"Searching Epicor for parts...", false, true);
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
                            lstRow.ToolTipText = sqlRead[1].ToString();
                            lstRow.SubItems.Add(sqlRead[2].ToString());
                            var f = float.Parse(sqlRead[3].ToString());
                            lstRow.SubItems.Add(f.ToString(@"G"));
                            f = float.Parse(sqlRead[4].ToString());
                            lstRow.SubItems.Add(f.ToString(@"G"));
                            lstResults.Add(lstRow);
                        }
                    sqlRead.Close();
                }

                //create array and add records
                frmStat.SetStat(50, @"Found " + lstResults.Count + @" records. Populating...");
                var iCnt = 0;
                var lviResults = new ListViewItem[lstResults.Count];
                foreach (var l in lstResults)
                {
                    lviResults[iCnt] = l;
                    iCnt++;
                }

                //populate form listview with records
                frmStat.lstEPSResults.Items.AddRange(lviResults);
                frmStat.SetStat(100, @"Operation complete.", true);
            }
            catch (COMException ex)
            {
                frmStat.SetStat(100, @"HRESULT = 0x" + ex.ErrorCode.ToString(@"X") + @" " + ex.Message, true);
            }
            catch (System.Exception ex)
            {
                frmStat.SetStat(100, ex.Message, true);
            }
        }

        internal static ListViewItem[] UsageSearch(FrmPane frmStat, string sPart, string sCust)
        {
            try
            {
                //build query string
                const string sConn = @"Server=EPICOR10;Database=Vira;Trusted_Connection=True;";
                var sSql = @"EXECUTE [Vira].[dbo].[eng_UsageSearch]";
                if ((sPart == string.Empty) | (sPart == null))
                    sSql += @" null";
                else
                    sSql += @" '" + sPart + @"'";
                if ((sCust == string.Empty) | (sCust == null))
                    sSql += @";";
                else
                    sSql += @", '" + sCust + @"';";

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
                            var lstRow = new ListViewItem(sqlRead[0].ToString(), 0); //source ID
                            lstRow.SubItems.Add(sqlRead[1].ToString()); //source type
                            lstRow.SubItems.Add(sqlRead[2].ToString()); //part num
                            lstRow.SubItems.Add(sqlRead[3].ToString()); //rev
                            lstRow.SubItems.Add(sqlRead[4].ToString()); //draw
                            var f = float.Parse(sqlRead[5].ToString());
                            lstRow.SubItems.Add(f.ToString(@"G")); //qty
                            var d = DateTime.Parse(sqlRead[6].ToString());
                            lstRow.SubItems.Add(d.ToString(@"d")); //due
                            lstRow.SubItems.Add(sqlRead[7].ToString()); //status
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
                return lviResults;
            }
            catch (COMException ex)
            {
                frmStat.SetStat(100, @"HRESULT = 0x" + ex.ErrorCode.ToString(@"X") + @" " + ex.Message, true);
                return null;
            }
            catch (System.Exception ex)
            {
                frmStat.SetStat(100, ex.Message, true);
                return null;
            }
        }

        private static string SearchCriteria(string sField, string sWhere)
        {
            //sanitize that dirty string
            sWhere = sWhere.Replace(';', ' ').Trim();

            //create list to add criteria to
            var lstCrit = new List<string>();

            //split into sub criteria based on quotes
            try
            {
                while (sWhere.Length > 0)
                {
                    var i = sWhere.IndexOf('"');
                    if (i == -1) //no quotes found, split on whitespace
                    {
                        var subs = sWhere.Trim().Split();
                        lstCrit.AddRange(subs);
                        sWhere = string.Empty;
                    }
                    else //quotes found
                    {
                        var iEnd = sWhere.IndexOf('"', i + 1); //get next quote
                        if (iEnd == -1)
                        {
                            sWhere = sWhere.Replace('"', ' '); //no next quote, replace and continue
                        }
                        else
                        {
                            //isolate phrase between quotes and add
                            lstCrit.Add(sWhere.Substring(i + 1, iEnd - i - 1));

                            //remove phrase and continue
                            var num = sWhere.Length - iEnd - 1;
                            sWhere = sWhere.Substring(0, i).Trim() + @" " +
                                     sWhere.Substring(sWhere.Length - num, num).Trim();
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                MessageBox.Show(@"Error: " + e.HResult + @", " + e.Message, @"Error on process criteria.");
            }

            //see if any criteria were found
            if (lstCrit.Count <= 0) return string.Empty;
            {
                //loop through and build statement for each
                for (var i = 0; i < lstCrit.Count; i++)
                {
                    sWhere += @"(" + sField + @" Like N'%" + lstCrit[i].Replace(@"'", @"''") + @"%')";
                    if (i != lstCrit.Count - 1) sWhere += @" AND ";
                }

                return sWhere;
            }
        }

        internal static ListViewItem[] QuoteAttach(string sDemand)
        {
            try
            {
                //define parameters
                const string sConn = @"Server=EPICOR10;Database=Vira;Trusted_Connection=True;";
                var sSql = @"EXECUTE [Vira].[dbo].[eng_QuoteAttach] '" + sDemand + @"';";

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
                            lstRow.ToolTipText = sqlRead[1].ToString();
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
                return lviResults;
            }
            catch (System.Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return null;
            }
        }

        internal static DataTable ProductCat()
        {
            try
            {
                //define parameters
                const string sConn = @"Server=EPICOR10;Database=Vira;Trusted_Connection=True;";
                var sSql = @"SELECT Prefix AS 'ProdCat', [Description] FROM EpicorProd.Erp.RefCategory ";
                    sSql += @"WHERE Company = 'VIRAINS' ORDER BY Prefix;";

                //create datatable
                var results = new DataTable();
                results.Columns.Add(@"catVal", typeof(string));
                results.Columns.Add(@"catDisp", typeof(string));

                //grab records to datatable
                using (var conn = new SqlConnection(sConn))
                {
                    var sqlCom = new SqlCommand(sSql, conn);
                    conn.Open();
                    var sqlRead = sqlCom.ExecuteReader();
                    if (sqlRead.HasRows)
                        while (sqlRead.Read())
                        {
                            results.Rows.Add(sqlRead[0].ToString(), sqlRead[1].ToString());
                        }
                    sqlRead.Close();
                }

                //populate form listview with records
                return results;
            }
            catch (System.Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return null;
            }
        }

        internal static string[,] TaskSearch(FrmPane frmStat, string sSearch, bool bUser)
        {
            var lstRecords = new List<string[]>();
            try
            {
                //define parameters
                const string sConn = @"Server=EPICOR10;Database=Vira;Trusted_Connection=True;";
                var sSql = @"EXECUTE [Vira].[dbo].[eng_TaskSearch] '" + sSearch + @"', " + bUser + @";";

                //grab records to list
                using (var conn = new SqlConnection(sConn))
                {
                    var sqlCom = new SqlCommand(sSql, conn);
                    conn.Open();
                    var sqlRead = sqlCom.ExecuteReader();
                    if (sqlRead.HasRows)
                        while (sqlRead.Read())
                        {
                            var asRecord = new string[8];
                            for (var ic = 0; ic < 8; ic++) asRecord[ic] = sqlRead[ic].ToString();
                            lstRecords.Add(asRecord);
                        }

                    sqlRead.Close();
                }

                //create array and add records
                if (lstRecords.Count == 0) return null;
                var iCnt = 0;
                var asRecords = new string[lstRecords.Count, 8];
                foreach (var l in lstRecords)
                {
                    for (var ic = 0; ic < 8; ic++) asRecords[iCnt, ic] = l[ic];
                    iCnt++;
                }

                return asRecords;
            }
            catch (COMException ex)
            {
                frmStat.SetStat(100, @"HRESULT = 0x" + ex.ErrorCode.ToString(@"X") + @" " + ex.Message, true);
                return null;
            }
            catch (System.Exception ex)
            {
                frmStat.SetStat(100, ex.Message, true);
                return null;
            }
        }

        internal static void OppEmail(FrmPane frmStat, string sOpp, string sDesc = "")
        {
            try
            {
                //connection string
                const string sConn = @"Server=EPICOR10;Database=Vira;Trusted_Connection=True;";

                //build query
                var sSql = @"SELECT TOP 1 EMailAddress ";
                sSql += @"FROM EpicorProd.dbo.QuoteHed qh ";
                sSql += @"INNER JOIN EpicorProd.Erp.UserFile usr ON qh.strCreatedBy_c = usr.DcdUserID ";
                sSql += @"WHERE qh.QuoteNum = " + sOpp + ";";

                //search for email address
                var sEmail = "";
                frmStat.SetStat(0, @"Getting requestor email...", false, true);
                using (var conn = new SqlConnection(sConn))
                {
                    var sqlCom = new SqlCommand(sSql, conn);
                    conn.Open();
                    var sqlRead = sqlCom.ExecuteReader();
                    if (sqlRead.HasRows)
                        while (sqlRead.Read())
                        {
                            sEmail = sqlRead[0].ToString().Trim();
                        }
                    sqlRead.Close();
                }

                //validate address returned
                if (sEmail.Length == 0)
                {
                    var quit = MessageBox.Show(@"No valid email address found for requester. Do you want to create the email anyway?",
                        @"Email Not Found", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (quit != DialogResult.Yes)
                    {
                        frmStat.SetStat(100, @"Operation cancelled.", true);
                        return;
                    }
                }

                //create email and finish
                frmStat.SetStat(50, @"Launching email...");
                var url = $@"mailto:{sEmail}?Subject=Opp: {sOpp}- Desc: {sDesc}";
                Process.Start(new ProcessStartInfo() { FileName = url, UseShellExecute = true });
                frmStat.SetStat(100, @"Operation complete.", true);
            }
            catch (COMException ex)
            {
                frmStat.SetStat(100, @"HRESULT = 0x" + ex.ErrorCode.ToString(@"X") + @" " + ex.Message, true);
            }
            catch (System.Exception ex)
            {
                frmStat.SetStat(100, ex.Message, true);
            }
        }
    }
}