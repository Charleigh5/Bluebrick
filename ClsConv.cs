using System;
using System.Data;
using System.Globalization;
using System.IO;

namespace BlueBrick
{
    public class ClsConv
    {
        private readonly string _dataFile;
        private DataTable _ratios;
        private DataSet _import;
        private string _electTypeA = string.Empty;
        private string _electTypeB = string.Empty;
        private readonly FrmPane _frm;
        private bool _electDone;
        public object[] Types;
        public readonly object[] Rolls;

        internal ClsConv(FrmPane form, string path)
        {
            _frm = form;
            if (path != null) _dataFile = Path.Combine(path, @"DataFiles\qryUnitExport.xml");
            MakeRatioTable();
            GetTypes();
            Rolls = GetItems("Length", true);
        }

        private void MakeRatioTable()
        {
            _import = new DataSet();
            if (File.Exists(_dataFile)) _import.ReadXml(_dataFile);
            _ratios = _import.Tables["qryUnitExport"];
        }

        internal object[] GetItems(string type, bool roll = false)
        {
            if (string.IsNullOrEmpty(type)) return null;
            var exp = @"UTypeName = '" + type + "'";
            if (roll)
            {
                exp += @" AND UnitCustA = 1";
            }
            var rows = _ratios.Select(exp);
            var data = new object[rows.Length];
            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                var unit = new Unit(
                    row["UnitName"].ToString(),
                    row["UnitAbbrev"].ToString(),
                    row["UnitDesc"].ToString(),
                    row["UnitFrom"].ToString(),
                    row["UnitTo"].ToString(),
                    row["UnitCustA"].ToString(),
                    row["UnitCustB"].ToString(),
                    row["UnitID"].ToString(),
                    row["UTypeName"].ToString()
                );
                data[i] = unit;
            }
            return data;
        }

        private void GetTypes()
        {
            //define datatable
            var dtTypes = new DataTable();
            dtTypes.Columns.Add("Type");

            //add records
            var rows = _ratios.Select("","UTypeName ASC");
            foreach (var row in rows)
            {
                var itemData = (string)row["UTypeName"];
                var rs = dtTypes.Select("Type = '" + itemData + "'");
                if (rs.GetUpperBound(0) == 0) continue;
                object[] type = {itemData};
                dtTypes.Rows.Add(type);
            }

            //retrieve types
            rows = dtTypes.Select();
            Types = new object[rows.Length];
            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                var itemData = (string)row["Type"];
                Types[i] = itemData;
            }
        }

        internal struct Unit
        {
            public Unit(string name, string symbol, string desc, string from, string to, string custA, string custB, string id, string type)
            {
                const NumberStyles style = NumberStyles.AllowExponent | NumberStyles.AllowDecimalPoint;
                var culture = new CultureInfo("en-US");
                double.TryParse(from, style, culture, out var ratioF);
                double.TryParse(to, style, culture, out var ratioT);
                double.TryParse(custA, style, culture, out var cust);
                int.TryParse(id, style, culture, out var idNum);
                Id = idNum;
                Type = type;
                Name = name;
                Symbol = symbol;
                Description = desc;
                FromRatio = ratioF;
                ToRatio = ratioT;
                CustomA = cust;
                CustomB = Convert.ToBoolean(Convert.ToInt16(custB));
            }

            public int Id  { get; }

            public string Type  { get; }

            private string Name { get; }

            public string Symbol { get; }

            public string Description { get; }

            public double FromRatio { get; }

            public double ToRatio { get; }

            public double CustomA { get; }

            public bool CustomB { get; }

            public override string ToString()
            {
                return Name + " (" + Symbol + ")";
            }
        }

        internal double UnitConvert(object fromUnit, object toUnit, double value)
        {
            try
            {
                //casting
                var from = (Unit)fromUnit;
                var to = (Unit)toUnit;

                //normal
                if (from.Type != @"Temp") return RoundSig(value * from.FromRatio * to.ToRatio);

                //temperature scales
                if (from.CustomB) value *= -1; //inverted
                value = (value + from.CustomA) * from.FromRatio;
                value = value * to.ToRatio - to.CustomA;
                if (to.CustomB) value *= -1;
                return RoundSig(value);
            }
            catch
            {
                return 0;
            }
        }

        internal static double RollCalc(string rollId, string rollOd, string tubeWall, string matThk, object fromUnit, object toUnit)
        {
            try
            {
                //casting
                var from = (Unit)fromUnit;
                var to = (Unit)toUnit;

                //validate
                if (!double.TryParse(rollId, out var id)) return 0;
                if (id == 0) return 0;
                if (!double.TryParse(rollOd, out var od)) return 0;
                if (od == 0) return 0;
                if (!double.TryParse(tubeWall, out var tube)) return 0;
                if (!double.TryParse(matThk, out var thick)) return 0;
                if (thick == 0) return 0;

                //do the math (unit agnostic)
                var dPi = 4 * Math.Atan(1);
                var dWork = dPi * od / 2 * od / 2;
                if (tube == 0)
                {
                    dWork -= dPi * id / 2 * id / 2;
                }
                else
                {
                    dWork -= dPi * ((id / 2 + tube) * (id / 2 + tube));
                }
                dWork /= thick;

                //convert if different format selected
                if (from.Id == to.Id) return Math.Round(dWork, 3);
                dWork *= from.FromRatio;
                dWork *= to.ToRatio;
                return Math.Round(dWork, 3);
            }
            catch
            {
                return 0;
            }
        }

        private double RoundSig(double orig)
        {
            try
            {
                const int ciDigits = 5;
                var number = orig;
                var neg = false;
                if (number < 0)
                {
                    number *= -1;
                    neg = true;
                }

                if (number == 0) return number;
                if (number >= 1)
                {
                    number = Math.Round(number, 5);
                    if (neg) number *= -1;
                    return Math.Round(number, 5);
                }

                var sNum = number.ToString(@"G");
                int count;
                if (sNum.Contains("E"))
                {
                    int.TryParse(sNum.Substring(sNum.IndexOf('E') + 2, sNum.Length - (sNum.IndexOf('E') + 2)),
                        out count);
                    sNum = sNum.Substring(0, sNum.Length - (count.ToString().Length + 2));
                    sNum = new string('0', count - 1) + sNum.Replace(@".", string.Empty);
                }
                else
                {
                    sNum = sNum.Substring(2, sNum.Length - 2);
                }

                for (count = 0; count < sNum.Length; count++)
                {
                    var digit = sNum.Substring(count, 1);
                    if (digit == @"0") continue;
                    count += ciDigits;
                    break;
                }

                sNum = @"0." + sNum.Substring(0, count);
                if (neg) sNum = @"-" + sNum;
                double.TryParse(sNum, out number);
                return number;
            }
            catch (Exception ex)
            {
                _frm.SetStat(0,"Error: '" + ex.Message + "' during rounding.");
                return orig;
            }
        }

        #region Electrical

        public void ElectUpdate(string type, string value)
        {
            if (_electDone) return;

            //verify a good value was passed
            var chk = float.TryParse(value, out _);
            if (!chk) return;

            //update last types and verify there are two
            if (_electTypeA != type)
            {
                _electTypeB = _electTypeA;
                _electTypeA = type;
            }
            if (_electTypeB.Length == 0) return;

            //get selected types
            var types = 0;
            for (var i = 0; i < 2; i++)
            {
                var s = i==0 ? _electTypeA : _electTypeB;
                switch (s)
                {
                    case "watt":
                        types += (int)ElectType.Watt;
                        break;
                    case "volt":
                        types += (int)ElectType.Volt;
                        break;
                    case "amp":
                        types += (int)ElectType.Amp;
                        break;
                    case "ohm":
                        types += (int)ElectType.Ohm;
                        break;
                }
            }

            //get all four values
            var values = new float[4];
            float.TryParse(_frm.txtConvEleWatt.Text, out values[0]);
            float.TryParse(_frm.txtConvEleVolt.Text, out values[1]);
            float.TryParse(_frm.txtConvEleAmp.Text, out values[2]);
            float.TryParse(_frm.txtConvEleOhm.Text, out values[3]);

            //do the maths!
            switch (types)
            {
                case (int)ElectType.Watt + (int)ElectType.Volt:
                    values[2] = values[0] / values[1];
                    values[3] = values[1] / values[2];
                    break;
                case (int)ElectType.Watt + (int)ElectType.Amp:
                    values[1] = values[0] / values[2];
                    values[3] = values[1] / values[2];
                    break;
                case (int)ElectType.Watt + (int)ElectType.Ohm:
                    values[1] = (float)Math.Sqrt(values[0] * values[3]);
                    values[2] = values[0] / values[1];
                    break;
                case (int)ElectType.Volt + (int)ElectType.Amp:
                    values[3] = values[1] / values[2];
                    values[0] = values[1] * values[2];
                    break;
                case (int)ElectType.Volt + (int)ElectType.Ohm:
                    values[0] = values[1] * values[1] / values[3];
                    values[2] = values[0] / values[1];
                    break;
                case (int)ElectType.Amp + (int)ElectType.Ohm:
                    values[1] = values[2] * values[3];
                    values[0] = values[1] * values[2];
                    break;
            }

            //populate the controls
            _frm.txtConvEleWatt.Text = Math.Round(values[0],3).ToString(CultureInfo.CurrentCulture);
            _frm.txtConvEleVolt.Text = Math.Round(values[1],3).ToString(CultureInfo.CurrentCulture);
            _frm.txtConvEleAmp.Text = Math.Round(values[2],3).ToString(CultureInfo.CurrentCulture);
            _frm.txtConvEleOhm.Text = Math.Round(values[3],3).ToString(CultureInfo.CurrentCulture);
            _frm.txtConvEleW80.Text = Math.Round(values[0]*0.8,3).ToString(CultureInfo.CurrentCulture);
            _frm.txtConvEleW50.Text = Math.Round(values[0]*0.5,3).ToString(CultureInfo.CurrentCulture);
            _frm.txtConvEleWatt.ReadOnly = true;
            _frm.txtConvEleVolt.ReadOnly = true;
            _frm.txtConvEleAmp.ReadOnly = true;
            _frm.txtConvEleOhm.ReadOnly = true;
            _electDone = true;
        }

        [Flags]
        private enum ElectType
        {
            Watt = 1,
            Volt = 2,
            Amp = 4,
            Ohm = 8
        }

        public void ElectReset()
        {
            _electTypeA = string.Empty;
            _electTypeB = string.Empty;
            _frm.txtConvEleWatt.Text = string.Empty;
            _frm.txtConvEleVolt.Text = string.Empty;
            _frm.txtConvEleAmp.Text = string.Empty;
            _frm.txtConvEleOhm.Text = string.Empty;
            _frm.txtConvEleW80.Text = string.Empty;
            _frm.txtConvEleW50.Text = string.Empty;
            _frm.txtConvEleWatt.ReadOnly = false;
            _frm.txtConvEleVolt.ReadOnly = false;
            _frm.txtConvEleAmp.ReadOnly = false;
            _frm.txtConvEleOhm.ReadOnly = false;
            _electDone = false;
        }

        #endregion Electrical
    }
}