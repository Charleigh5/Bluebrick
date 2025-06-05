using System;
using System.Data;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace BlueBrick
{
    internal class ClsSettings
    {
        private readonly string _dataFile;

        internal ClsSettings()
        {
            var uri = new UriBuilder(Assembly.GetExecutingAssembly().CodeBase);
            var path = Path.GetDirectoryName(Uri.UnescapeDataString(uri.Path));
            _dataFile = "";
            if (path != null) _dataFile = Path.Combine(path, @"DataFiles\settings.xml");
            ReadSettings();
        }

        private DataTable Settings { get; set; }

        private void ReadSettings()
        {
            var import = new DataSet();
            if (File.Exists(_dataFile))
            {
                import.ReadXml(_dataFile);
                Settings = import.Tables["settings"];
            }
            else
            {
                var dt = new DataTable();
                var col = new DataColumn("settingName");
                col.DataType = typeof(string);
                dt.Columns.Add(col);
                col = new DataColumn("settingValue");
                col.DataType = typeof(string);
                dt.Columns.Add(col);
                Settings = dt;
            }
        }

        internal void WriteSettings()
        {
            Settings.DataSet.WriteXml(_dataFile);
        }

        internal string GetSetting(string sVar, bool sec = false)
        {
            var rows = Settings.Select(@"settingName = '" + sVar + "'");
            if (rows.Length == 0) return null;
            var row = rows[0];

            var sVal = sec ? Decrypt(row["settingValue"].ToString()) : row["settingValue"].ToString();
            return sVal;
        }

        internal void SetSetting(string sVar, string sVal, bool sec = false)
        {
            for (var i = Settings.Rows.Count - 1; i >= 0; i--)
            {
                var dr = Settings.Rows[i];
                if (dr["settingName"].ToString() == sVar)
                    dr.Delete();
            }

            var row = Settings.NewRow();
            row["settingName"] = sVar;
            row["settingValue"] = sec ? Encrypt(sVal) : sVal;
            Settings.Rows.Add(row);
            Settings.AcceptChanges();
        }

        private static readonly byte[]
            EntropyKey = Encoding.Unicode.GetBytes("show me your kitties"); //lil salt for taste (>^.^<)

        //encrypt sensitive strings
        private static string Encrypt(string input)
        {
            var encryptedData = ProtectedData.Protect(Encoding.Unicode.GetBytes(input), EntropyKey,
                DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encryptedData);
        }

        //decrypt sensitive strings
        private static string Decrypt(string input)
        {
            string returnVal;
            try
            {
                var decryptedData = ProtectedData.Unprotect(Convert.FromBase64String(input), EntropyKey,
                    DataProtectionScope.CurrentUser);
                returnVal = Encoding.Unicode.GetString(decryptedData);
            }
            catch
            {
                returnVal = string.Empty;
            }

            return returnVal;
        }
    }
}