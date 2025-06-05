using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using forms = System.Windows.Forms;

namespace BlueBrick
{
    public static class ClsFinish
    {
        //globals
        private static ISldWorks _swApp; //active solidworks instance
        private static FrmPane _frm; //active form
        private static IModelDoc2 _swModel; //active model object
        private static bool _fin;
        private static readonly string[] Replace = new string[2]; //0 = find, 1 = replace

        public static void FinishRename(FrmPane frmStat, ISldWorks swAppL)
        {
            //init globals
            _swApp = swAppL;
            _frm = frmStat;

            //get top level doc
            _swModel = (ModelDoc2)_swApp.ActiveDoc;
            if (_swModel == null)
            {
                _frm.SetStat(100, @"No currently open document.", true);
                return;
            }
            
            //check for read only
            if (_swModel.IsOpenedReadOnly())
            {
                if (forms.MessageBox.Show(@"The current document is opened read only. Do you wish to continue?", @"Warning",
                        forms.MessageBoxButtons.OKCancel, forms.MessageBoxIcon.Warning) != forms.DialogResult.OK)
                {
                    _frm.SetStat(100, @"The current document is opened read-only.", true);
                    return;
                }
            }

            //ask for strings and validate
            if (FinReplace() != forms.DialogResult.OK)
            {
                frmStat.SetStat(100, @"Operation aborted.", true, true);
                return;
            }

            if (Replace[0].Length == 0)
            {
                frmStat.SetStat(100, @"Find not specified.", true, true);
                return;
            }

            if (Replace[1].Length == 0)
            {
                frmStat.SetStat(100, @"Replace not specified.", true, true);
                return;
            }

            //send top level to delegate and process components if bAll
            _frm.SetStat(0, @"Starting drawing audit...", false, true);
            var iErr = 0;
            var lstCad = new Dictionary<string, string>(); //used to collect list of drawing paths
            var sTop = new string[2]; //0 = model, 1 = draw
            var bTopIsMdl = AddCad(true, ref sTop, ref lstCad); //true if top level is model, false if drawing
            _swModel = (ModelDoc2)_swApp.ActivateDoc3(sTop[0], false,
                (int)swRebuildOnActivation_e.swRebuildActiveDoc, ref iErr);
            var swFeat = (Feature)_swModel.FirstFeature();
            while (swFeat != null)
            {
                CheckFeat(swFeat, ref sTop, ref lstCad);
                swFeat = (Feature)swFeat.GetNextFeature();
            }

            //process file list
            iErr = 0;
            foreach (var entry in lstCad)
            {
                //open drawing
                var swDocSpec = (DocumentSpecification)_swApp.GetOpenDocSpec(entry.Value);
                swDocSpec.DocumentType = (int)swDocumentTypes_e.swDocDRAWING;
                swDocSpec.ReadOnly = false;
                swDocSpec.Silent = true;
                _swModel = _swApp.OpenDoc7(swDocSpec);
                if (_swModel == null) continue;

                //activate drawing
                _swModel = (ModelDoc2)_swApp.ActivateDoc3(entry.Value, false,
                    (int)swRebuildOnActivation_e.swRebuildActiveDoc, ref iErr);
                if (iErr != 0)
                {
                    var sMsg = @"Error '" + iErr + @"' during drawing audit while activating drawing on filename: ";
                    sMsg += _swModel.GetPathName();
                    Status(sMsg);
                    iErr = 0;
                }

                //search drawing for finish tables
                var bSave = false;
                // ReSharper disable once SuspiciousTypeConversion.Global
                var swDraw = (DrawingDoc)_swModel;
                var swSheetNames = (string[])swDraw.GetSheetNames();
                foreach (var sht in swSheetNames)
                {
                    swDraw.ActivateSheet(sht);
                    var swView = (View)swDraw.GetFirstView();
                    var swNotes = (object[])swView.GetAnnotations();
                    if (swNotes == null || swNotes.Length == 0) continue;
                    foreach (var swObj in swNotes)
                    {
                        var swAnn = (Annotation)swObj;
                        if (swAnn.GetType() != (int)swAnnotationType_e.swTableAnnotation) continue;
                        var swTab = (TableAnnotation)swAnn.GetSpecificAnnotation();
                        if (swTab.Type != (int)swTableAnnotationType_e.swTableAnnotation_General) continue;
                        if ((swTab.RowCount < 3) | (swTab.ColumnCount < 2)) continue;
                        if (swTab.Text2[0, 0, true].ToUpper().Trim() != @"FINISH SCHEDULE") continue;
                        var sTell = swTab.Text2[1, 0, true].ToUpper().Trim();
                        switch (sTell)
                        {
                            case @"DWG NO.":
                                if (swTab.RowCount > 3)
                                {
                                    var sMsg = _swModel.GetTitle() +
                                               @" has a finish schedule, but it is of an incompatible type, skipping...";
                                    _frm.SetStat(0, sMsg);
                                }
                                else
                                {
                                    if (_fin)
                                    {
                                        if (FindCode(ref swTab, true)) bSave = true;
                                    }
                                    else
                                    {
                                        if (FindText(ref swTab, true)) bSave = true;
                                    }
                                }

                                break;
                            case @"CALL OUT":
                                if (_fin)
                                {
                                    if (FindCode(ref swTab)) bSave = true;
                                }
                                else
                                {
                                    if (FindText(ref swTab)) bSave = true;
                                }

                                break;
                        }
                    }
                }

                //check for changes and attempt save
                if (bSave)
                {
                    const int iOpt = (int)swSaveAsOptions_e.swSaveAsOptions_Silent;
                    var iWarn = 0;
                    _swModel.Save3(iOpt, ref iErr, ref iWarn);
                    if (iErr > 0)
                    {
                        var sMsg = _swModel.GetTitle() + @" had changes made, but failed to save...";
                        _frm.SetStat(0, sMsg);
                    }
                }

                //close drawing if not top level
                if (_swModel == null) continue;
                if ((_swModel.GetPathName() != sTop[1]) | bTopIsMdl) _swApp.QuitDoc(_swModel.GetTitle());
            }

            //close top level model if opened from drawing
            if (!bTopIsMdl) _swApp.QuitDoc(sTop[0]);

            //finish
            _frm.SetStat(0, @"Operation complete.", true);
        }

        private static bool
            AddCad(bool bTop, ref string[] sTop,
                ref Dictionary<string, string> lstCad) //add CAD file path to collection
        {
            try
            {
                //get all info to construct CAD item
                var sMdl = _swModel.GetPathName();
                var sDraw = string.Empty;
                var bTopIsMdl = bTop;
                switch (_swModel.GetType())
                {
                    case (int)swDocumentTypes_e.swDocDRAWING:
                        sDraw = _swModel.GetPathName();
                        sTop[1] = sDraw;
                        bTopIsMdl = false;

                        //get model path of top level drawing
                        // ReSharper disable once SuspiciousTypeConversion.Global
                        var swDraw = (DrawingDoc)_swModel;
                        var swView = (View)swDraw.GetFirstView();
                        swView = (View)swView.GetNextView();
                        var sName = swView.GetReferencedModelName();
                        var iErr = 0;
                        _swModel = (ModelDoc2)_swApp.ActivateDoc3(sName, false,
                            (int)swRebuildOnActivation_e.swRebuildActiveDoc, ref iErr);
                        _swModel.ShowConfiguration2(swView.ReferencedConfiguration);
                        sMdl = _swModel.GetPathName();
                        sTop[0] = sMdl;
                        break;
                    case (int)swDocumentTypes_e.swDocASSEMBLY:
                    case (int)swDocumentTypes_e.swDocPART:
                        sDraw = sMdl.Substring(0, sMdl.LastIndexOf('.')) + @".SLDDRW";
                        if (bTop) sTop[0] = sMdl;
                        break;
                }

                //add to list
                if (!lstCad.ContainsKey(sMdl)) //check for dupe
                    lstCad.Add(sMdl, sDraw);
                return bTopIsMdl;
            }
            catch (COMException ex)
            {
                Status(@"Error '" + ex.ErrorCode.ToString(@"X") + @"' during procedure AddCAD");
                return false;
            }
            catch (Exception ex)
            {
                Status(@"Error '" + ex.Message + @"' during procedure AddCAD");
                return false;
            }
        }

        //traverse each feature looking for models
        private static void CheckFeat(Feature swFeat, ref string[] sTop, ref Dictionary<string, string> lstCad)
        {
            try
            {
                if (swFeat == null) throw new ArgumentNullException(nameof(swFeat));
                if (swFeat.GetTypeName2() != @"Reference") return;
                var swComp = (Component2)swFeat.GetSpecificFeature2();
                if (swComp.GetSuppression2() != 0) //skip if suppressed
                {
                    _swModel = (ModelDoc2)swComp.GetModelDoc2(); //setting up for use in AddToCol
                    if (_swModel != null)
                    {
                        AddCad(false, ref sTop, ref lstCad);
                        _swApp.QuitDoc(_swModel.GetTitle());
                    }
                }

                var swSubFeat = swComp.FirstFeature(); //call recursively for sub components
                while (swSubFeat != null)
                {
                    CheckFeat(swSubFeat, ref sTop, ref lstCad);
                    swSubFeat = (Feature)swSubFeat.GetNextFeature();
                }
            }
            catch (COMException ex)
            {
                Status(@"Error '" + ex.ErrorCode.ToString(@"X") + @"' during procedure CheckFeat");
            }
            catch (Exception ex)
            {
                Status(@"Error '" + ex.Message + @"' during procedure CheckFeat");
            }
        }

        private static void Status(string sMsg, int iPrg = 0) //increment progress bar and add status message
        {
            var giPrg = _frm.GetPrg(); //current progress bar value
            giPrg += iPrg;
            giPrg = giPrg > 99 ? 99 : giPrg;
            _frm.SetStat(giPrg, sMsg);
        }

        [SuppressMessage(@"ReSharper", @"LocalizableElement")]
        private static forms.DialogResult FinReplace()
        {
            try
            {
                //create form
                var size = new Size(375, 115);
                var inputBox = new forms.Form
                {
                    FormBorderStyle = forms.FormBorderStyle.FixedDialog,
                    ClientSize = size,
                    Text = @"Finish Schedule Update"
                };

                //setup controls
                var lblFind = new forms.Label
                {
                    Location = new Point(5, 8),
                    Name = @"lblFind",
                    Text = @"Find",
                    Size = new Size(50, 23),
                    TabIndex = 0
                };
                inputBox.Controls.Add(lblFind);

                var txtFind = new forms.TextBox
                {
                    Location = new Point(58, 5),
                    Name = @"txtFind",
                    Size = new Size(267, 23),
                    TabIndex = 1
                };
                inputBox.Controls.Add(txtFind);

                var lblReplace = new forms.Label
                {
                    Location = new Point(5, 35),
                    Name = @"lblReplace",
                    Text = @"Replace",
                    Size = new Size(50, 23),
                    TabIndex = 2
                };
                inputBox.Controls.Add(lblReplace);

                var txtReplace = new forms.TextBox
                {
                    Location = new Point(58, 32),
                    Name = @"txtReplace",
                    Size = new Size(267, 23),
                    TabIndex = 3
                };
                inputBox.Controls.Add(txtReplace);

                var chkFinName = new forms.CheckBox
                {
                    Checked = true,
                    Location = new Point(58, 58),
                    Name = @"chkFinName",
                    Size = new Size(250, 20),
                    Text = @"Search By Finish Code",
                    TabIndex = 5
                };
                inputBox.Controls.Add(chkFinName);

                var okButton = new forms.Button
                {
                    DialogResult = forms.DialogResult.OK,
                    Name = @"okButton",
                    Size = new Size(75, 23),
                    Text = @"&OK",
                    Location = new Point(size.Width - 80 - 80, 88),
                    TabIndex = 6
                };
                inputBox.Controls.Add(okButton);
                inputBox.AcceptButton = okButton;

                var cancelButton = new forms.Button
                {
                    DialogResult = forms.DialogResult.Cancel,
                    Name = @"cancelButton",
                    Size = new Size(75, 23),
                    Text = @"&Cancel",
                    Location = new Point(size.Width - 80, 88),
                    TabIndex = 7
                };
                inputBox.Controls.Add(cancelButton);
                inputBox.CancelButton = cancelButton;

                //return result
                var result = inputBox.ShowDialog();
                Replace[0] = txtFind.Text.ToUpper().Trim();
                Replace[1] = txtReplace.Text.ToUpper().Trim();
                _fin = chkFinName.Checked;
                return result;
            }
            catch
            {
                return forms.DialogResult.Cancel;
            }
        }

        private static bool FindCode(ref TableAnnotation swTab, bool bH = false)
        {
            var bChk = false;
            if (!bH)
                for (var iRow = 2; iRow <= swTab.RowCount - 1; iRow++)
                {
                    var sComp = swTab.Text2[iRow, 0, false].ToUpper().Trim();
                    if (sComp != Replace[0]) continue;
                    swTab.Text2[iRow, 1, false] = Replace[1];
                    bChk = true;
                }
            else
                for (var iCol = 1; iCol <= swTab.ColumnCount - 1; iCol++)
                {
                    var sComp = swTab.Text2[1, iCol, false].ToUpper().Trim();
                    if (sComp != Replace[0]) continue;
                    swTab.Text2[2, iCol, false] = Replace[1];
                    bChk = true;
                }

            return bChk;
        }

        private static bool FindText(ref TableAnnotation swTab, bool bH = false)
        {
            var bChk = false;
            if (!bH)
                for (var iRow = 2; iRow <= swTab.RowCount - 1; iRow++)
                {
                    var sComp = swTab.Text2[iRow, 1, false].ToUpper().Trim();
                    if (sComp.IndexOf(Replace[0], StringComparison.OrdinalIgnoreCase) < 0) continue;
                    swTab.Text2[iRow, 1, false] = sComp.Replace(Replace[0], Replace[1]);
                    bChk = true;
                }
            else
                for (var iCol = 1; iCol <= swTab.ColumnCount - 1; iCol++)
                {
                    var sComp = swTab.Text2[2, iCol, false].ToUpper().Trim();
                    if (sComp.IndexOf(Replace[0], StringComparison.OrdinalIgnoreCase) < 0) continue;
                    swTab.Text2[2, iCol, false] = sComp.Replace(Replace[0], Replace[1]);
                    bChk = true;
                }

            return bChk;
        }
    }
}