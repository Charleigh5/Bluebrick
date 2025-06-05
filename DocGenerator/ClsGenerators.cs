using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using EPDM.Interop.epdm;
using Microsoft.Win32;
//using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using SolidWorks.Interop.swconst;
using sw = SolidWorks.Interop.sldworks; //to resolve ambiguities with System.Windows.Forms.View

namespace DocGenerator
{
    internal class ClsGenerators
    {
        //constants
        private const string
            DxfFmt = @"C:\_PDMVault\Templates\VI Templates\VI DRAWING FOR DXF.drwdot"; //template for DXF

        private const string PdfFolderPath = @"\Drawing PDF's\"; //PDF subfolder
        private const string DocFolderPath = @"\DXF-STP's\"; //DXF/STEP subfolder
        private const string MiscFolderPath = @"\Misc\"; //png subfolder
        private readonly string _tempPath = Path.GetTempPath(); //temp folder
        private string _sFolder = "";
        private int _progBar;
        private bool _silent;
        private object _return;

        //stats for packet
        //private string _topTemplate;

        //globals
        private sw.ISldWorks _swApp; //active solidworks instance
        private sw.IModelDoc2 _swModel; //active model object

        internal object GenerateDoc(sw.ISldWorks swAppL, int iOpt, string sFolder = "") //initial caller
        {
            try
            {
                //init globals
                _swApp = swAppL;
                _sFolder = sFolder;
                if ((iOpt & (int)ClsEnums.EnumGenOptions.Silent) == (int)ClsEnums.EnumGenOptions.Silent)
                    _silent = true;

                //get top level doc
                _swModel = (sw.ModelDoc2)_swApp.ActiveDoc;
                if (_swModel == null)
                {
                    _progBar = 100;
                    OnSetStatus(
                        new SetStatusEventArgs { Message = @"No currently open document.", Progress = _progBar });
                    return _return;
                }

                _progBar = 0;
                OnSetStatus(
                    new SetStatusEventArgs { Message = @"Starting document generation...", Progress = _progBar });

                //send top level to delegate and process components if bAll
                var iErr = 0;
                var lstCad = new Dictionary<string, CadItem>(); //used to collect a list of model full paths
                var sTop = new string[2]; //0 = model, 1 = draw
                var bTopIsMdl = AddCad(true, ref sTop, ref lstCad); //return true if model, else dwg
                if ((iOpt & (int)ClsEnums.EnumGenOptions.All) == (int)ClsEnums.EnumGenOptions.All)
                {
                    _swModel = (sw.ModelDoc2)_swApp.ActivateDoc3(sTop[0], false,
                        (int)swRebuildOnActivation_e.swRebuildActiveDoc, ref iErr);
                    var swFeat = (sw.Feature)_swModel.FirstFeature();
                    while (swFeat != null)
                    {
                        CheckFeat(swFeat, ref sTop, ref lstCad);
                        swFeat = (sw.Feature)swFeat.GetNextFeature();
                    }
                }

                //save and set user settings to preferred
                var setSaved = ChangeSettings();
                ChangeSettings(true);

                //login to vault
                var oVault = (IEdmVault20)new EdmVault5();
                if (!oVault.IsLoggedIn) oVault.LoginAuto(@"_PDMVault", 0);

                //get latest version of dxf template
                var oFile = (IEdmFile15)oVault.GetFileFromPath(DxfFmt, out var oTemp);
                var oFolder = (IEdmFolder8)oTemp;
                if (oFile.CurrentVersion != oFile.GetLocalVersionNo2(oFolder.ID, out _)) oFile.GetFileCopy(0);
                var dxfThumbs = new Dictionary<string, DxfThumb>();
                var pdfThumbs = new Dictionary<string, PdfThumb>();

                #region ProcessCAD

                //check whitelist and mark ignore
                var keys = new List<string>(lstCad.Keys);
                var parts = new string[lstCad.Count];
                var i = 0;
                foreach (var key in keys)
                {
                    parts[i] = lstCad[key].Properties.PartNo;
                    i++;
                    if ((iOpt & (int)ClsEnums.EnumGenOptions.One) == (int)ClsEnums.EnumGenOptions.One) continue;
                    if (!Skip(lstCad[key].Properties.PartNo)) continue;
                    var temp = lstCad[key];
                    temp.Ignore = true;
                    lstCad[key] = temp;
                }

                //check for duplicate part number properties
                var duplicates = parts.GroupBy(x => x)
                    .Where(g => g.Count() > 1)
                    .Select(y => y.Key)
                    .ToList();
                if (duplicates.Count != 0)
                {
                    var quit = false;
                    if (!_silent) //prompt user
                    {
                        var msg = @"The following part number(s) are used for multiple files.";
                        msg += @"Do you want to continue?" + Environment.NewLine;
                        msg += string.Join(Environment.NewLine, duplicates);
                        var diag = MessageBox.Show(msg, @"Duplicates Found",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                        if (diag != DialogResult.Yes) quit = true;
                    }
                    else
                    {
                        //silent, check for whitelisted parts that are dupes
                        foreach (var num in duplicates)
                        {
                            if (Skip(num)) continue;
                            quit = true;
                            break;
                        }
                    }

                    //handle results of checks
                    if (quit)
                    {
                        Status(
                            @"The following part number(s) are used for multiple files. "
                            + @"Please update the numbers and try again.");
                        Status(string.Join(Environment.NewLine, duplicates));
                        _swModel = null;
                        return _return;
                    }

                    Status(
                        @"The following part number(s) are used for multiple files. "
                        + @"The process will attempt to continue.");
                    Status(string.Join(Environment.NewLine, duplicates));
                }

                //check for locked documents
                if ((iOpt & (int)ClsEnums.EnumGenOptions.SaveToPdm) ==
                    (int)ClsEnums.EnumGenOptions.SaveToPdm)
                {
                    var lstCheck = new Dictionary<string, string>();
                    foreach (var entry in lstCad)
                    {
                        var ent = entry.Value;

                        //add PDF
                        var iChk = (int)ClsEnums.EnumGenOptions.Pdf + (int)ClsEnums.EnumGenOptions.Packet;
                        if ((iOpt & iChk) != 0 && !(_silent && ent.Ignore)) //needs PDF
                        {
                            var sFldr = ent.Drawing.Substring(0, ent.Drawing.LastIndexOf('\\'));
                            sFldr = sFldr.Substring(0, sFldr.LastIndexOf('\\'));
                            sFldr += PdfFolderPath;
                            sFldr += ent.Properties.PartNo + @".pdf";
                            lstCheck.Add(sFldr, sFldr);
                        }

                        //add the packet
                        if ((iOpt & (int)ClsEnums.EnumGenOptions.Packet) == (int)ClsEnums.EnumGenOptions.Packet)
                        {
                            if (ent.TopLevel != (int)ClsEnums.EnumGenTop.No)
                            {
                                var sFldr = ent.Drawing.Substring(0, ent.Drawing.LastIndexOf('\\'));
                                sFldr = sFldr.Substring(0, sFldr.LastIndexOf('\\'));
                                sFldr += PdfFolderPath;
                                if (ent.Properties.Customer == @"KOH" && ent.Properties.CustNo != "")
                                    sFldr += ent.Properties.CustNo + @"-" + ent.Properties.PartNo + @".PDF";
                                else
                                    sFldr += ent.Properties.PartNo + @"-" + ent.Properties.DocNo + @".PDF";
                                lstCheck.Add(sFldr, sFldr);
                            }
                        }

                        //add STEP
                        if ((iOpt & (int)ClsEnums.EnumGenOptions.Step) == (int)ClsEnums.EnumGenOptions.Step)
                        {
                            var bChk = ent.TopLevel == (int)ClsEnums.EnumGenTop.No &&
                                       (iOpt & (int)ClsEnums.EnumGenOptions.Packet) ==
                                       (int)ClsEnums.EnumGenOptions.Packet;
                            if (!bChk) //skip STEP if not top level and packet
                            {
                                var sFldr = ent.Model.Substring(0, ent.Model.LastIndexOf('\\'));
                                sFldr = sFldr.Substring(0, sFldr.LastIndexOf('\\'));
                                sFldr += DocFolderPath;
                                sFldr += ent.Properties.PartNo + @".STEP";
                                lstCheck.Add(sFldr, sFldr);
                            }
                        }

                        //add PNG
                        if ((iOpt & (int)ClsEnums.EnumGenOptions.Png) == (int)ClsEnums.EnumGenOptions.Png)
                        {
                            var sFldr = ent.Model.Substring(0, ent.Model.LastIndexOf('\\'));
                            sFldr = sFldr.Substring(0, sFldr.LastIndexOf('\\'));
                            sFldr += MiscFolderPath;
                            sFldr += ent.Properties.PartNo + @".png";
                            lstCheck.Add(sFldr, sFldr);
                        }

                        //add IGES
                        if ((iOpt & (int)ClsEnums.EnumGenOptions.Iges) == (int)ClsEnums.EnumGenOptions.Iges)
                        {
                            var bChk = !ent.IsAssm || (ent.IsAssm &&
                                                       (iOpt & (int)ClsEnums.EnumGenOptions.One) ==
                                                       (int)ClsEnums.EnumGenOptions.One);
                            if (bChk) //skip IGES for top level if assem and 'All'
                            {
                                var sFldr = ent.Model.Substring(0, ent.Model.LastIndexOf('\\'));
                                sFldr = sFldr.Substring(0, sFldr.LastIndexOf('\\'));
                                sFldr += DocFolderPath;
                                sFldr += ent.Properties.PartNo + @".igs";
                                lstCheck.Add(sFldr, sFldr);
                            }
                        }

                        //add DXF
                        if ((iOpt & (int)ClsEnums.EnumGenOptions.Dxf) == (int)ClsEnums.EnumGenOptions.Dxf)
                        {
                            var bChk = !ent.IsAssm || (ent.IsAssm &&
                                                       (iOpt & (int)ClsEnums.EnumGenOptions.One) ==
                                                       (int)ClsEnums.EnumGenOptions.One);
                            if (bChk) //skip DXF for top level if assem and 'All'
                            {
                                var sFldr = ent.Model.Substring(0, ent.Model.LastIndexOf('\\'));
                                sFldr = sFldr.Substring(0, sFldr.LastIndexOf('\\'));
                                sFldr += DocFolderPath;
                                sFldr += ent.Properties.PartNo + @".dxf";
                                lstCheck.Add(sFldr, sFldr);
                            }
                        }
                    }

                    //parse the file list and check for locked files
                    var lstLocked = new Dictionary<string, string>();
                    foreach (var entry in lstCheck)
                    {
                        //verify the file exists and isn't checked out
                        var file = oVault.GetFileFromPath(entry.Value, out _);
                        if (!(file is { IsLocked: true })) continue;

                        //the file is checked out - verify the user
                        var userMgr = (IEdmUserMgr5)oVault;
                        if (file.LockedByUser == userMgr.GetLoggedInUser())
                        {
                            //verify locked on this machine
                            if (file.LockedOnComputer == Environment.MachineName)
                            {
                                //verify write access to delete the file
                                var local = new FileInfo(entry.Value);
                                if (!local.IsReadOnly) continue;
                            }
                        }

                        //the file is locked and cannot be deleted by packet generator
                        lstLocked.Add(file.Name, file.Name);
                    }

                    //report locked documents
                    if (lstLocked.Count != 0)
                    {
                        var sErr = @"The following documents are checked out and will cause this procedure to fail:";
                        sErr += Environment.NewLine;
                        foreach (var entry in lstLocked)
                        {
                            sErr += entry.Value + Environment.NewLine;
                        }

                        Status(sErr);
                        return _return;
                    }
                }

                //process file list
                iErr = 0;
                string sMsg;
                var sPath = string.Empty;
                var lstDocs = new Dictionary<string, DocItem>();
                foreach (var entry in lstCad)
                {
                    //process file and gen docs
                    var ent = entry.Value;
                    var iChk = (int)ClsEnums.EnumGenOptions.Pdf + (int)ClsEnums.EnumGenOptions.Packet;
                    if ((iOpt & iChk) != 0 && !(_silent && ent.Ignore)) //needs PDF
                    {
                        //get the latest version of drawing if PDM
                        var oFileDrw = (IEdmFile15)oVault.GetFileFromPath(ent.Drawing, out var oTempDrw);
                        if (oFileDrw != null)
                        {
                            oFileDrw.GetLocalVersionNo2(oTempDrw.ID, out var bObs);
                            if (bObs)
                            {
                                try
                                {
                                    oFileDrw.GetFileCopy(0);
                                }
                                catch
                                {
                                    Status(@"You're version of '" + ent.Drawing +
                                           "' is outdated. Failed to get latest version of file from PDM.");
                                    continue;
                                }
                            }
                        }

                        //open drawing
                        var swDocSpec = (sw.DocumentSpecification)_swApp.GetOpenDocSpec(ent.Drawing);
                        swDocSpec.DocumentType = (int)swDocumentTypes_e.swDocDRAWING;
                        swDocSpec.ReadOnly = true;
                        swDocSpec.Silent = true;
                        _swModel = _swApp.OpenDoc7(swDocSpec);
                        if (_swModel != null)
                        {
                            //activate drawing
                            _swModel = (sw.ModelDoc2)_swApp.ActivateDoc3(ent.Drawing, false,
                                (int)swRebuildOnActivation_e.swRebuildActiveDoc, ref iErr);
                            if (iErr != 0)
                            {
                                sMsg = @"Error '" + iErr;
                                sMsg += @"' during PDF creation while activating drawing on filename: ";
                                sMsg += _swModel.GetPathName();
                                Status(sMsg);
                                iErr = 0;
                            }

                            //get doc template
                            // ReSharper disable once SuspiciousTypeConversion.Global
                            //var swDraw = (sw.DrawingDoc)_swModel;
                            //var swSheet = (sw.Sheet)swDraw.GetCurrentSheet();
                            //var sTemp = swSheet.GetTemplateName();

                            //generate PDF
                            if ((iOpt & (int)ClsEnums.EnumGenOptions.SaveToPdm) ==
                                (int)ClsEnums.EnumGenOptions.SaveToPdm)
                            {
                                sPath = ent.Drawing.Substring(0, ent.Drawing.LastIndexOf('\\'));
                                sPath = sPath.Substring(0, sPath.LastIndexOf('\\'));
                                sPath += PdfFolderPath;
                            }

                            var sFile = ent.Properties.PartNo + @".pdf";
                            var sOut = GenPdf(_tempPath + sFile);

                            //stamp metal weight - remove stamping function for now
                            var stamp = (sOut != string.Empty && ent.Weight > 0);
                            if (ent.TopLevel != (int)ClsEnums.EnumGenTop.No &&
                                (iOpt & (int)ClsEnums.EnumGenOptions.Packet) == (int)ClsEnums.EnumGenOptions.Packet)
                                stamp = false;
                            if (stamp)
                            {
                                LogWeight(ent.Properties.PartNo, ent.Weight, ent.Weight);
                                //    var doc = PdfReader.Open(sOut);
                                //    doc = StampPdf(doc, ent.Weight, sTemp);
                                //    doc.Save(sOut);
                            }

                            //save template for packet later
                            //if (ent.TopLevel != (int)ClsEnums.EnumGenTop.No) _topTemplate = sTemp;

                            //build doc record and add to collection
                            if (sOut.Length > 0)
                            {
                                var iDrwOpt = (int)ClsEnums.EnumGenOptions.Pdf;
                                if (ent.Ignore)
                                {
                                    //save screenshot for preview
                                    _swApp.SetUserPreferenceIntegerValue(
                                        (int)swUserPreferenceIntegerValue_e.swTiffImageType,
                                        (int)swTiffImageType_e.swTiffImageBlackAndWhite);
                                    _swModel.ViewZoomtofit2();
                                    var thumb = new PdfThumb
                                    {
                                        ImgPath = sOut.ToUpper().Replace(@".PDF", @"_PdfThumb.PNG"),
                                        PartNo = _swModel.CustomInfo[@"Part Number"],
                                        PdfPath = sOut
                                    };
                                    var swExt = _swModel.Extension;
                                    var iWarn = 0;
                                    try
                                    {
                                        var bSave = swExt.SaveAs3(thumb.ImgPath, 0, 0, null, null,
                                            ref iErr,
                                            ref iWarn);
                                        if (bSave) pdfThumbs.Add(thumb.PartNo, thumb);
                                    }
                                    catch
                                    {
                                        Status(@"Failed to save thumbnail of PDF for '" + ent.Drawing + "'.");
                                    }

                                    _swApp.SetUserPreferenceIntegerValue(
                                        (int)swUserPreferenceIntegerValue_e.swTiffImageType,
                                        (int)swTiffImageType_e.swTiffImageRGB);
                                    iDrwOpt += (int)ClsEnums.EnumGenOptions.Skip;
                                }

                                //create the doc and add
                                var docTemp = new DocItem(sFile, sPath, iDrwOpt, ent.Properties);
                                lstDocs.Add(sOut, docTemp);
                            }

                            //close drawing
                            if (_swModel != null)
                                if ((_swModel.GetPathName() != sTop[1]) | bTopIsMdl)
                                    _swApp.QuitDoc(_swModel.GetTitle());
                        }
                    }

                    //model section
                    if (ent.Ignore) continue;
                    iChk = (int)ClsEnums.EnumGenOptions.Png + (int)ClsEnums.EnumGenOptions.Dxf +
                           (int)ClsEnums.EnumGenOptions.Step + (int)ClsEnums.EnumGenOptions.Iges;
                    if ((iOpt & iChk) == 0) continue;
                    {
                        //open model
                        var swDocSpec = (sw.DocumentSpecification)_swApp.GetOpenDocSpec(ent.Model);
                        if (ent.IsAssm)
                            swDocSpec.DocumentType = (int)swDocumentTypes_e.swDocASSEMBLY;
                        else
                            swDocSpec.DocumentType = (int)swDocumentTypes_e.swDocPART;
                        swDocSpec.ReadOnly = true;
                        swDocSpec.Silent = true;
                        _swModel = _swApp.OpenDoc7(swDocSpec);
                        if (_swModel == null) continue;
                        //activate model
                        _swModel = (sw.ModelDoc2)_swApp.ActivateDoc3(ent.Model, false,
                            (int)swRebuildOnActivation_e.swRebuildActiveDoc, ref iErr);
                        if (iErr != 0)
                        {
                            sMsg = @"Error '" + iErr + @"' during Doc creation while activating model on filename: ";
                            sMsg += _swModel.GetPathName();
                            Status(sMsg);
                            iErr = 0;
                        }

                        _swModel = (sw.ModelDoc2)_swApp.ActiveDoc;
                        _swModel.ShowNamedView2(string.Empty, (int)swStandardViews_e.swTrimetricView);
                        _swModel.ViewZoomtofit2();

                        //collapse if exploded
                        if (ent.IsAssm)
                        {
                            sw.IModelDocExtension swExt = _swModel.Extension;
                            if (swExt.IsExploded(out var sExp))
                            {
                                // ReSharper disable once SuspiciousTypeConversion.Global
                                var swAssm = (sw.AssemblyDoc)_swModel;
                                swAssm.ShowExploded2(false, sExp);
                            }
                        }

                        //suppress all grain marks
                        var lstFeats = new List<string>();
                        var swFeat = (sw.Feature)_swModel.FirstFeature();
                        while (swFeat != null)
                        {
                            var sName = swFeat.Name;
                            if (sName.Length > 9)
                                if (sName.Substring(0, 9) == @"GRAIN MRK")
                                {
                                    swFeat.SetSuppression2((int)swFeatureSuppressionAction_e.swSuppressFeature,
                                        (int)swInConfigurationOpts_e.swThisConfiguration, null);
                                    lstFeats.Add(swFeat.Name);
                                }

                            swFeat = (sw.Feature)swFeat.GetNextFeature();
                        }

                        //generate STEP
                        if ((iOpt & (int)ClsEnums.EnumGenOptions.Step) == (int)ClsEnums.EnumGenOptions.Step)
                        {
                            var bChk = ent.TopLevel == (int)ClsEnums.EnumGenTop.No &&
                                       (iOpt & (int)ClsEnums.EnumGenOptions.Packet) ==
                                       (int)ClsEnums.EnumGenOptions.Packet;
                            if (!bChk) //skip STEP if not top level and packet
                            {
                                if ((iOpt & (int)ClsEnums.EnumGenOptions.SaveToPdm) ==
                                    (int)ClsEnums.EnumGenOptions.SaveToPdm)
                                {
                                    sPath = ent.Model.Substring(0, ent.Model.LastIndexOf('\\'));
                                    sPath = sPath.Substring(0, sPath.LastIndexOf('\\'));
                                    sPath += DocFolderPath;
                                }

                                var sFile = ent.Properties.PartNo + @".STEP";
                                var sOut = GenStep(_tempPath + sFile);

                                //build doc record and add to collection
                                if (sOut.Length > 0)
                                {
                                    var docTemp = new DocItem(sFile, sPath, (int)ClsEnums.EnumGenOptions.Step,
                                        ent.Properties);
                                    lstDocs.Add(sOut, docTemp);
                                }
                            }
                        }

                        //generate PNG
                        if ((iOpt & (int)ClsEnums.EnumGenOptions.Png) == (int)ClsEnums.EnumGenOptions.Png)
                        {
                            if ((iOpt & (int)ClsEnums.EnumGenOptions.SaveToPdm) ==
                                (int)ClsEnums.EnumGenOptions.SaveToPdm)
                            {
                                sPath = ent.Model.Substring(0, ent.Model.LastIndexOf('\\'));
                                sPath = sPath.Substring(0, sPath.LastIndexOf('\\'));
                                sPath += MiscFolderPath;
                            }

                            var sFile = ent.Properties.PartNo + @".png";
                            var sOut = GenPng(_tempPath + sFile);

                            //build doc record and add to collection
                            if (sOut.Length > 0)
                            {
                                var docTemp = new DocItem(sFile, sPath, (int)ClsEnums.EnumGenOptions.Png,
                                    ent.Properties);
                                lstDocs.Add(sOut, docTemp);
                            }
                        }

                        //generate IGES
                        if ((iOpt & (int)ClsEnums.EnumGenOptions.Iges) == (int)ClsEnums.EnumGenOptions.Iges)
                        {
                            var bChk = !ent.IsAssm || (ent.IsAssm &&
                                                       (iOpt & (int)ClsEnums.EnumGenOptions.One) ==
                                                       (int)ClsEnums.EnumGenOptions.One);
                            if (bChk) //skip IGES for top level if assem and 'All'
                            {
                                if ((iOpt & (int)ClsEnums.EnumGenOptions.SaveToPdm) ==
                                    (int)ClsEnums.EnumGenOptions.SaveToPdm)
                                {
                                    sPath = ent.Model.Substring(0, ent.Model.LastIndexOf('\\'));
                                    sPath = sPath.Substring(0, sPath.LastIndexOf('\\'));
                                    sPath += DocFolderPath;
                                }

                                var sFile = ent.Properties.PartNo + @".igs";
                                var sOut = GenIges(_tempPath + sFile);

                                //build doc record and add to collection
                                if (sOut.Length > 0)
                                {
                                    var docTemp = new DocItem(sFile, sPath, (int)ClsEnums.EnumGenOptions.Iges,
                                        ent.Properties);
                                    lstDocs.Add(sOut, docTemp);
                                }
                            }
                        }

                        //generate DXF
                        if ((iOpt & (int)ClsEnums.EnumGenOptions.Dxf) == (int)ClsEnums.EnumGenOptions.Dxf)
                        {
                            var bChk = !ent.IsAssm || (ent.IsAssm &&
                                                       (iOpt & (int)ClsEnums.EnumGenOptions.One) ==
                                                       (int)ClsEnums.EnumGenOptions.One);
                            if (bChk) //skip DXF for top level if assem and 'All'
                            {
                                if ((iOpt & (int)ClsEnums.EnumGenOptions.SaveToPdm) ==
                                    (int)ClsEnums.EnumGenOptions.SaveToPdm)
                                {
                                    sPath = ent.Model.Substring(0, ent.Model.LastIndexOf('\\'));
                                    sPath = sPath.Substring(0, sPath.LastIndexOf('\\'));
                                    sPath += DocFolderPath;
                                }

                                var sFile = ent.Properties.PartNo + @".dxf";
                                var sOut = GenDxf(_tempPath + sFile, ref dxfThumbs);

                                //build doc record and add to collection
                                if (sOut.Length > 0)
                                {
                                    var docTemp = new DocItem(sFile, sPath, (int)ClsEnums.EnumGenOptions.Dxf,
                                        ent.Properties);
                                    lstDocs.Add(sOut, docTemp);
                                }
                            }
                        }

                        //unsuppress features
                        swFeat = (sw.Feature)_swModel.FirstFeature();
                        while (swFeat != null)
                        {
                            if (lstFeats.Contains(swFeat.Name))
                                swFeat.SetSuppression2((int)swFeatureSuppressionAction_e.swUnSuppressFeature,
                                    (int)swInConfigurationOpts_e.swThisConfiguration, null);
                            swFeat = (sw.Feature)swFeat.GetNextFeature();
                        }

                        //close model
                        if (_swModel != null && _swModel.GetPathName() != sTop[0]) _swApp.QuitDoc(_swModel.GetTitle());
                    }
                }

                //close the top level model if opened from drawing
                if (!bTopIsMdl) _swApp.QuitDoc(sTop[0]);

                #endregion ProcessCAD

                #region Packet

                //get the list of PDF thumbnails
                if (!_silent)
                {
                    if (pdfThumbs.Count != 0)
                    {
                        var frmP = new FrmPdfThumbs();
                        var thumb = new ListViewItem[pdfThumbs.Count];
                        var iP = 0;
                        foreach (var oT in pdfThumbs)
                        {
                            var imgT = Image.FromFile(oT.Value.ImgPath);
                            frmP.imlThumbs.Images.Add(imgT);
                            var lstT = new ListViewItem(oT.Value.PartNo, iP);
                            lstT.SubItems.Add(oT.Value.ImgPath);
                            lstT.SubItems.Add(oT.Value.PdfPath);
                            thumb[iP] = lstT;
                            iP++;
                        }

                        frmP.lstThumbs.Items.AddRange(thumb);
                        using (frmP)
                        {
                            var result = frmP.ShowDialog();
                            foreach (var key in lstDocs.Keys.ToList())
                            {
                                if (result == DialogResult.OK && frmP.DrwAdd != null && frmP.DrwAdd.Length != 0 &&
                                    frmP.DrwAdd.Contains(key)) continue;
                                var temp = lstDocs[key];
                                if ((temp.DocType & (int)ClsEnums.EnumGenOptions.Skip) !=
                                    (int)ClsEnums.EnumGenOptions.Skip)
                                    continue;
                                try
                                {
                                    File.Delete(key);
                                }
                                catch
                                {
                                    Status(@"Failed to delete PDF thumbnails.");
                                }

                                lstDocs.Remove(key);
                            }
                        }
                    }
                }

                //generate packet
                var pdfCnt = lstDocs.Count(x => x.Value.DocType == (int)ClsEnums.EnumGenOptions.Pdf);
                if (pdfCnt > 0 && (iOpt & (int)ClsEnums.EnumGenOptions.Packet) == (int)ClsEnums.EnumGenOptions.Packet)
                {
                    //get packet details
                    DocItem docPkt = default;
                    foreach (var ent in lstCad.Select(entry => entry.Value)
                                 .Where(ent => ent.TopLevel != (int)ClsEnums.EnumGenTop.No))
                    {
                        //get file name
                        string sFile;
                        if (ent.Properties.Customer == @"KOH" && ent.Properties.CustNo != "")
                            sFile = ent.Properties.CustNo + @"-" + ent.Properties.PartNo + @".PDF";
                        else
                            sFile = ent.Properties.PartNo + @"-" + ent.Properties.DocNo + @".PDF";

                        //get the path
                        if ((iOpt & (int)ClsEnums.EnumGenOptions.SaveToPdm) == (int)ClsEnums.EnumGenOptions.SaveToPdm)
                        {
                            sPath = ent.Drawing.Substring(0, ent.Drawing.LastIndexOf('\\'));
                            sPath = sPath.Substring(0, sPath.LastIndexOf('\\'));
                            sPath += PdfFolderPath;
                        }

                        //construct packet object
                        docPkt = new DocItem(sFile, sPath, (int)ClsEnums.EnumGenOptions.Packet, ent.Properties);
                        break;
                    }

                    //concatenate PDFs into packet
                    var oOutDoc = new PdfDocument();
                    foreach (var entry in lstDocs)
                    {
                        var ent = entry.Value;
                        if ((ent.DocType & (int)ClsEnums.EnumGenOptions.Pdf) == 0) continue;
                        var oInDoc = PdfReader.Open(_tempPath + ent.FileName, PdfDocumentOpenMode.Import);
                        var count = oInDoc.PageCount;
                        for (var iPage = 0; iPage < count; iPage++)
                        {
                            var oPage = oInDoc.Pages[iPage];
                            oOutDoc.AddPage(oPage);
                        }

                        oInDoc.Dispose();
                    }

                    //create metal weight property info
                    double metal = 0;
                    double total = 0;
                    foreach (var item in lstCad.Values)
                    {
                        if (item.TopLevel != (int)ClsEnums.EnumGenTop.No)
                            total = item.Weight;
                        else
                            metal += (item.Occur * item.Weight);
                    }

                    //maybe linq is better? 2 loops though
                    //var tot2 = lstCad.Values.First(x => x.TopLevel != (int)ClsEnums.EnumGenTop.No).Weight;
                    //var met2 = lstCad.Values.Where(r => r.TopLevel == (int)ClsEnums.EnumGenTop.No).Sum(x => x.Weight * x.Occur);

                    //stamp the PDF
                    //oOutDoc = StampPdf(oOutDoc, metal, _topTemplate, true, total); -remove stamping for now
                    LogWeight(docPkt.Properties.PartNo, total, metal);

                    //save the packet out
                    var sPkt = _tempPath + docPkt.FileName;
                    try
                    {
                        oOutDoc.Save(sPkt);
                    }
                    catch
                    {
                        Status(@"Failed to save PDF packet to temp folder.");
                    }

                    oOutDoc.Dispose();
                    lstDocs.Add(sPkt, docPkt);
                }

                #endregion Packet

                //rollback any changed user preferences
                ChangeSettings(true, setSaved);

                //verify any docs were created
                if (lstDocs.Count == 0)
                {
                    _progBar = 100;
                    OnSetStatus(new SetStatusEventArgs
                        { Message = @"No documents were created.", Progress = _progBar });
                    return _return;
                }

                #region ProcessDocs

                //remove unused PDFs if only the packet is selected
                foreach (var entry in lstDocs.ToList())
                {
                    var ent = entry.Value;
                    var bChk = (ent.DocType & (int)ClsEnums.EnumGenOptions.Pdf) != 0
                               && (iOpt & (int)ClsEnums.EnumGenOptions.Pdf) != (int)ClsEnums.EnumGenOptions.Pdf;
                    if (!bChk) continue;
                    try
                    {
                        File.Delete(_tempPath + ent.FileName);
                    }
                    catch
                    {
                        Status(@"Failed to delete unused PDFs from temp folder.");
                    }

                    lstDocs.Remove(entry.Key);
                }

                //process documents
                if ((iOpt & (int)ClsEnums.EnumGenOptions.SaveToPdm) !=
                    (int)ClsEnums.EnumGenOptions.SaveToPdm) //outside PDM
                {
                    //get the path depending on options
                    if ((iOpt & (int)ClsEnums.EnumGenOptions.SaveToDesktop) ==
                        (int)ClsEnums.EnumGenOptions.SaveToDesktop)
                    {
                        sPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    }
                    else
                    {
                        sPath = _sFolder; //will be assigned on the form
                        if (!Directory.Exists(sPath)) Directory.CreateDirectory(sPath);
                    }

                    if (sPath.LastIndexOf('\\') != sPath.Length - 1) sPath += @"\";
                    //get new subdirectory based on top level part number if not single file
                    if ((iOpt & (int)ClsEnums.EnumGenOptions.One) != (int)ClsEnums.EnumGenOptions.One)
                        foreach (var entry in lstCad)
                        {
                            var ent = entry.Value;
                            if (ent.TopLevel == (int)ClsEnums.EnumGenTop.No) continue;
                            sPath += RemBad(ent.Properties.PartNo) + @"\";
                            if (!Directory.Exists(sPath)) Directory.CreateDirectory(sPath);
                        }

                    //move docs to the final location
                    foreach (var entry in lstDocs)
                    {
                        var ent = entry.Value;
                        try
                        {
                            File.Copy(_tempPath + ent.FileName, sPath + ent.FileName, true);
                            try
                            {
                                File.Delete(_tempPath + ent.FileName);
                            }
                            catch
                            {
                                Status(@"Failed to delete PDFs from temp folder.");
                            }
                        }
                        catch
                        {
                            Status(@"Failed to move PDFs to destination folder.");
                        }
                    }
                }
                else //inside PDM
                {
                    //delete existing files first
                    EdmBatchDelErrInfo[] oErr = null;
                    var oDel = (IEdmBatchDelete3)oVault.CreateUtility(EdmUtility.EdmUtil_BatchDelete);
                    foreach (var entry in lstDocs)
                    {
                        oDel.AddFileByPath(entry.Value.PathFinal + entry.Value.FileName);
                        if (entry.Value.DocType != (int)ClsEnums.EnumGenOptions.Packet) continue;

                        //if the packet option is selected, add all possible file names to be deleted
                        var temp = entry.Value.Properties.PartNo + @"-" + entry.Value.Properties.CustNo + @".PDF";
                        oDel.AddFileByPath(entry.Value.PathFinal + temp);
                        temp = entry.Value.Properties.PartNo + @"-" + entry.Value.Properties.DocNo + @".PDF";
                        oDel.AddFileByPath(entry.Value.PathFinal + temp);
                        temp = entry.Value.Properties.CustNo + @"-" + entry.Value.Properties.PartNo + @".PDF";
                        oDel.AddFileByPath(entry.Value.PathFinal + temp);
                        temp = entry.Value.Properties.DocNo + @"-" + entry.Value.Properties.PartNo + @".PDF";
                        oDel.AddFileByPath(entry.Value.PathFinal + temp);
                    }

                    if (oDel != null)
                    {
                        var bStat = oDel.ComputePermissions(false);
                        if (!bStat)
                        {
                            sMsg = @"Error: failed to get permission to delete document files from PDM.";
                            Status(sMsg);
                        }
                        else
                        {
                            bStat = oDel.CommitDelete(0);
                            if (!bStat)
                            {
                                oDel.GetCommitErrors(oErr);
                                if (oErr != null)
                                    foreach (var error in oErr)
                                    {
                                        oVault.GetErrorString(error.mlErrorCode, out var errName, out var errDesc);
                                        sMsg = @"Error: (" + errName + " - " + errDesc;
                                        sMsg += @") while attempting to delete '" + error.mbsPathName + @"' from PDM.";
                                        Status(sMsg);
                                    }
                            }
                        }
                    }

                    //loop through created files add to vault if not already
                    var oBatch = (IEdmBatchAdd)oVault.CreateUtility(EdmUtility.EdmUtil_BatchAdd);
                    var aoFile = new EdmFileInfo[lstDocs.Count];
                    foreach (var ent in lstDocs.Select(entry => entry.Value))
                    {
                        if (sPath.Length == 0) sPath = ent.PathFinal; //for open folder location
                        oBatch.AddFileFromPathToPath(_tempPath + ent.FileName, ent.PathFinal);
                    }

                    oBatch.CommitAdd(0, ref aoFile, 1);

                    //create an array of added files for task notification
                    int iCnt;
                    if (_silent)
                    {
                        var list = new EdmSelItem2[aoFile.Length];
                        for (iCnt = 0; iCnt < aoFile.Length; iCnt++)
                        {
                            if (aoFile[iCnt].mhResult == 0)
                            {
                                list[iCnt] = new EdmSelItem2
                                {
                                    meType = EdmObjectType.EdmObject_File,
                                    mlID = aoFile[iCnt].mlFileID,
                                    mlParentID = aoFile[iCnt].mlFolderID
                                };
                            }
                            else
                            {
                                oVault.GetErrorString(aoFile[iCnt].mhResult, out var errName, out var errDesc);
                                sMsg = @"Error: (" + errName + @" - " + errDesc;
                                sMsg += @") while adding file '" + aoFile[iCnt].mbsPath + @"' to vault.";
                                Status(sMsg);
                            }
                        }

                        _return = list;
                    }

                    //loop through and update card variables
                    var oVarBatch = (IEdmBatchUpdate2)oVault.CreateUtility(EdmUtility.EdmUtil_BatchUpdate);
                    foreach (var ent in lstDocs.Select(entry => entry.Value))
                    {
                        oFile = (IEdmFile15)oVault.GetFileFromPath(ent.PathFinal + ent.FileName, out oTemp);
                        if (oFile is null) continue;
                        oFolder = (IEdmFolder8)oTemp;

                        //make sure it isn't checked out
                        if (!oFile.IsLocked)
                        {
                            oFile.LockFile(oFolder.ID, 0);
                            if (!oFile.IsLocked) continue;
                        }

                        //update variables
                        oVarBatch.SetVar(oFile.ID, (int)ClsEnums.EnumPDMVars.PartNumber, ent.Properties.PartNo,
                            string.Empty, (int)EdmBatchFlags.EdmBatch_AllConfigs);
                        oVarBatch.SetVar(oFile.ID, (int)ClsEnums.EnumPDMVars.DocumentNumber,
                            ent.Properties.DocNo, string.Empty, (int)EdmBatchFlags.EdmBatch_AllConfigs);
                        oVarBatch.SetVar(oFile.ID, (int)ClsEnums.EnumPDMVars.Number, ent.Properties.CustNo,
                            string.Empty,
                            (int)EdmBatchFlags.EdmBatch_AllConfigs);
                        oVarBatch.SetVar(oFile.ID, (int)ClsEnums.EnumPDMVars.Description,
                            ent.Properties.Description, string.Empty, (int)EdmBatchFlags.EdmBatch_AllConfigs);
                        oVarBatch.SetVar(oFile.ID, (int)ClsEnums.EnumPDMVars.ProductCategory,
                            ent.Properties.ProductCategory, string.Empty, (int)EdmBatchFlags.EdmBatch_AllConfigs);
                    }

                    var iErrCnt = oVarBatch.CommitUpdate(out var aoErrs);
                    for (iCnt = 0; iCnt <= iErrCnt - 1; iCnt++) //report any errors
                    {
                        oVault.GetErrorString(aoErrs[iCnt].mlErrorCode, out var errName, out var errDesc);
                        sMsg = @"Error: (" + errName + @" - " + errDesc + @") while attempting to update var '";
                        sMsg += (ClsEnums.EnumPDMVars)aoErrs[iCnt].mlVariableID + @"' for ";
                        if (aoErrs[iCnt].mlFileID != 0)
                        {
                            var o = (IEdmFile5)oVault.GetObject(EdmObjectType.EdmObject_File, aoErrs[iCnt].mlFileID);
                            sMsg += @"file '" + o.Name + @"'.";
                        }
                        else
                            sMsg += @"unknown folder/object.";

                        Status(sMsg);
                    }

                    //loop through and check in
                    var aoSel = new EdmSelItem[lstDocs.Count];
                    var oUnlock = (IEdmBatchUnlock2)oVault.CreateUtility(EdmUtility.EdmUtil_BatchUnlock);
                    iCnt = 0;
                    foreach (var ent in lstDocs.Select(entry => entry.Value))
                    {
                        oFile = (IEdmFile15)oVault.GetFileFromPath(ent.PathFinal + ent.FileName, out oTemp);
                        oFolder = (IEdmFolder8)oTemp;
                        aoSel[iCnt].mlDocID = oFile.ID;
                        aoSel[iCnt].mlProjID = oFolder.ID;
                        iCnt++;
                    }

                    oUnlock.AddSelection((EdmVault5)oVault, aoSel);
                    oUnlock.Comment = @"Checked in via Generate Product Data procedure";
                    oUnlock.CreateTree(0, (int)EdmUnlockBuildTreeFlags.Eubtf_MayUnlock);
                    oUnlock.UnlockFiles(0);
                }

                #endregion ProcessDocs

                //cleanup and finish
                _swModel = null;
                lstCad.Clear();
                _progBar = 100;
                OnSetStatus(new SetStatusEventArgs { Message = @"Operation completed.", Progress = _progBar });

                //end here if silent
                if (_silent) return _return;

                //offer to open location
                var oRes = MessageBox.Show(@"Do you want to open the file location?", @"Operation Complete",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (oRes == DialogResult.Yes) Process.Start(sPath, @"explorer.exe");

                //get the list of DXF thumbnails
                if (dxfThumbs.Count == 0) return _return;
                var frmT = new FrmThumbs(_swApp);
                var lviT = new ListViewItem[dxfThumbs.Count];
                i = 0;
                foreach (var oT in dxfThumbs)
                {
                    var imgT = Image.FromFile(oT.Value.ImgPath);
                    frmT.imlThumbs.Images.Add(imgT);
                    var lstT = new ListViewItem(oT.Value.PartNo, i);
                    lstT.SubItems.Add(oT.Value.PartPath);
                    lstT.SubItems.Add(oT.Value.ImgPath);
                    lviT[i] = lstT;
                    i++;
                }

                frmT.lstThumbs.Items.AddRange(lviT);
                frmT.Show();
            }
            catch (COMException ex)
            {
                var msg = @"Com error '" + ex.ErrorCode.ToString(@"X") + @" - " + ex.Message;
                msg += @"' during main procedure.";
                Status(msg);
                _swModel = null;
            }
            catch (Exception ex)
            {
                //oVault.GetErrorString(aoErrs[iCnt].mlErrorCode, out var errName, out var errDesc);
                var msg = @"Exception '" + ex.HResult + @" - " + ex.Message;
                msg += @"' during main procedure.";
                Status(msg);
                _swModel = null;
            }

            return _return;
        }

        // private static PdfDocument StampPdf(PdfDocument doc, double metal, string template,
        //     bool top = false, double total = 0) //metal weight stamp
        // {
        //     //get page settings
        //     var page = doc.Pages[0];
        //     var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
        //
        //     //stamp metal
        //     double x;
        //     double y;
        //     var weight = @"Total Metal Weight: " + Math.Round(metal, 2) + @" lbs";
        //     XFont font;
        //     switch (template)
        //     {
        //         case @"C:\_PDMVault\Templates\Customer Templates\CVS TEMPLATE.slddrt": //cvs
        //         case @"d:\设计原始图稿\小刘2020\cvs\opp33805\template\cvs template-china.slddrt": //cvs china
        //             x = page.Width.Point - 160;
        //             y = 528;
        //             font = new XFont("Arial", 8, XFontStyle.Bold);
        //             break;
        //         default:
        //             x = page.Width.Point - 170;
        //             y = 15;
        //             font = new XFont("Arial", 10, XFontStyle.Regular);
        //             break;
        //     }
        //
        //     gfx.DrawString(weight, font, XBrushes.Black,
        //         new XRect(x, y, 300, 30), XStringFormats.CenterLeft);
        //
        //     //stamp total
        //     if (top)
        //     {
        //         weight = @"Total Weight: " + Math.Round(total, 2) + @" lbs";
        //         switch (template)
        //         {
        //             case @"C:\_PDMVault\Templates\Customer Templates\CVS TEMPLATE.slddrt": //cvs
        //             case @"d:\设计原始图稿\小刘2020\cvs\opp33805\template\cvs template-china.slddrt": //cvs china
        //                 x = page.Width.Point - 160;
        //                 y = 540;
        //                 font = new XFont("Arial", 8, XFontStyle.Bold);
        //                 break;
        //             default:
        //                 x = page.Width.Point - 320;
        //                 y = 15;
        //                 font = new XFont("Arial", 10, XFontStyle.Regular);
        //                 break;
        //         }
        //
        //         gfx.DrawString(weight, font, XBrushes.Black,
        //             new XRect(x, y, 300, 30), XStringFormats.CenterLeft);
        //     }
        //
        //     //set pdf options
        //     doc.Options.UseFlateDecoderForJpegImages = PdfUseFlateDecoderForJpegImages.Automatic;
        //     doc.Options.NoCompression = false;
        //     doc.Options.CompressContentStreams = true;
        //     doc.Options.FlateEncodeMode = PdfFlateEncodeMode.BestCompression;
        //     doc.Options.EnableCcittCompressionForBilevelImages = true;
        //
        //     return doc;
        // }

        private string GenPdf(string sOut) //generate PDF file
        {
            try
            {
                //set PDF export options
                sw.IModelDocExtension swExt = _swModel.Extension;
                var swExData = (sw.ExportPdfData)_swApp.GetExportFileData((int)swExportDataFileType_e.swExportPdfData);
                swExData.SetSheets(1, null);
                swExData.ViewPdfAfterSaving = false;

                //attempt save
                var iErr = 0;
                var iWarn = 0;
                _swModel.ClearSelection2(true);
                Application.DoEvents();
                var bSave = swExt.SaveAs3(sOut, 0, 0, swExData, null, ref iErr, ref iWarn);
                if (bSave || iErr + iWarn != 0) return sOut;
                var sMsg = @"Error '" + iErr + @"'/Warning: '" + iWarn;
                sMsg += @"' during procedure GenPDF, section 'attempt save' on filename: " + _swModel.GetPathName();
                Status(sMsg);
                sOut = string.Empty;
                return sOut;
            }
            catch (COMException ex)
            {
                Status(@"Error '" + ex.ErrorCode.ToString("X") + @"' during procedure GenPDF");
                return string.Empty;
            }
            catch (Exception ex)
            {
                Status(@"Error '" + ex.Message + @"' during procedure GenPDF");
                return string.Empty;
            }
        }

        private string GenDxf(string sOut, ref Dictionary<string, DxfThumb> lstThumbs) //generate DXF file
        {
            try
            {
                //check for the flat pattern and set config
                var sIn = _swModel.GetPathName();
                var asCfg = (string[])_swModel.GetConfigurationNames();
                var swConf = (sw.Configuration)_swModel.GetActiveConfiguration();
                var swOrig = swConf;
                foreach (var sCfg in asCfg)
                    if (sCfg.IndexOf(@"FLAT-PATTERN", StringComparison.Ordinal) != -1)
                    {
                        swConf = (sw.Configuration)_swModel.GetConfigurationByName(sCfg);
                        break;
                    }
                    else if (sCfg.IndexOf(@"UNFOLDED", StringComparison.Ordinal) != -1)
                    {
                        swConf = (sw.Configuration)_swModel.GetConfigurationByName(sCfg);
                        break;
                    }

                _swModel.ShowConfiguration2(swConf.Name);

                //hide sketches
                _swModel.ClearSelection2(true);
                var swFeat = (sw.Feature)_swModel.FirstFeature();
                while (swFeat != null)
                {
                    HideSketch(swFeat);
                    swFeat = (sw.Feature)swFeat.GetNextFeature();
                }

                //create the drawing and activate
                var swDraw = (sw.DrawingDoc)_swApp.NewDocument(
                    DxfFmt, (int)swDwgPaperSizes_e.swDwgPaperEsize, 0, 0); //make new dwg
                if (swDraw is null)
                {
                    var sMsg = @"Failed to create drawing for DXF output during procedure GenDXF, ";
                    sMsg += @"section 'create drawing and activate' on filename: ";
                    sMsg += sOut;
                    Status(sMsg);
                    return string.Empty;
                }

                var swSheets = (string[])swDraw.GetSheetNames();
                var swSheet = swDraw.Sheet[swSheets[0]]; //select the first sheet
                swDraw.SetupSheet6(swSheet.GetName(), (int)swDwgPaperSizes_e.swDwgPaperEsize,
                    (int)swDwgTemplates_e.swDwgTemplateCustom,
                    1, 1, false, DxfFmt, 0, 0, @"Default", true, 0.5, 0.5,
                    0.5, 0.5, 1, 1); //set template
                // ReSharper disable once SuspiciousTypeConversion.Global
                sw.IModelDoc2 swTemp = (sw.ModelDoc2)swDraw;
                sw.IModelDocExtension swExt = swTemp.Extension;
                var swLyrMgr = (sw.LayerMgr)swTemp.GetLayerManager();

                //set doc options
                swLyrMgr.SetCurrentLayer(@"TEXT"); //view names will go here
                swExt.SetUserPreferenceToggle(
                    (int)swUserPreferenceToggle_e.swDetailingOrthoView_AddViewLabelOnViewCreation,
                    (int)swUserPreferenceOption_e.swDetailingNoOptionSpecified, false);
                swExt.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDetailingOrthoViewLabels_PerStandard,
                    (int)swUserPreferenceOption_e.swDetailingNoOptionSpecified, false);
                swExt.SetUserPreferenceInteger((int)swUserPreferenceIntegerValue_e.swDetailingOrthoView_Name,
                    (int)swUserPreferenceOption_e.swDetailingNoOptionSpecified,
                    (int)swDetailingViewLabelsName_e.swDetailingViewLabelsName_DrawingTree);
                swExt.SetUserPreferenceInteger((int)swUserPreferenceIntegerValue_e.swDetailingOrthoViewLabels_Scale,
                    (int)swUserPreferenceOption_e.swDetailingNoOptionSpecified,
                    (int)swDetailingViewLabelsScale_e.swDetailingViewLabelsScale_none);
                swExt.SetUserPreferenceInteger((int)swUserPreferenceIntegerValue_e.swDetailingOrthoViewLabels_Delimiter,
                    (int)swUserPreferenceOption_e.swDetailingNoOptionSpecified,
                    (int)swDetailingViewLabelsDelimiter_e.swDetailingViewLabelsDelimiter_none);
                swExt.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDetailingOrthoView_RemoveSpaceInScale,
                    (int)swUserPreferenceOption_e.swDetailingNoOptionSpecified, true);
                swExt.SetUserPreferenceString((int)swUserPreferenceStringValue_e.swDetailingLayer,
                    (int)swUserPreferenceOption_e.swDetailingOrthoView, @"TEXT");
                swExt.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDetailingAutoInsertBalloons,
                    (int)swUserPreferenceOption_e.swDetailingNoOptionSpecified, false);
                swExt.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDetailingAutoInsertCenterLines,
                    (int)swUserPreferenceOption_e.swDetailingNoOptionSpecified, false);
                swExt.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDetailingAutoInsertCenterMarks,
                    (int)swUserPreferenceOption_e.swDetailingNoOptionSpecified, false);
                swExt.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDetailingAutoInsertCenterMarksForFillets,
                    (int)swUserPreferenceOption_e.swDetailingNoOptionSpecified, false);
                swExt.SetUserPreferenceToggle(
                    (int)swUserPreferenceToggle_e.swDetailingAutoInsertCenterMarksForFilletsAsm,
                    (int)swUserPreferenceOption_e.swDetailingNoOptionSpecified, false);
                swExt.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDetailingAutoInsertCenterMarksForHoles,
                    (int)swUserPreferenceOption_e.swDetailingNoOptionSpecified, false);
                swExt.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDetailingAutoInsertCenterMarksForHolesAsm,
                    (int)swUserPreferenceOption_e.swDetailingNoOptionSpecified, false);
                swExt.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDetailingAutoInsertCenterMarksForSlots,
                    (int)swUserPreferenceOption_e.swDetailingNoOptionSpecified, false);
                swExt.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDetailingAutoInsertCenterMarksForSlotsAsm,
                    (int)swUserPreferenceOption_e.swDetailingNoOptionSpecified, false);
                swExt.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDetailingAutoInsertDimsMarkedForDrawing,
                    (int)swUserPreferenceOption_e.swDetailingNoOptionSpecified, false);
                swExt.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDetailingAutoInsertDowelSymbols,
                    (int)swUserPreferenceOption_e.swDetailingNoOptionSpecified, false);
                swExt.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDetailingAutoInsertDowelSymbolsAsm,
                    (int)swUserPreferenceOption_e.swDetailingNoOptionSpecified, false);
                swExt.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDetailingAutoInsertDowelSymForHolesAsm,
                    (int)swUserPreferenceOption_e.swDetailingNoOptionSpecified, false);
                swExt.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDetailingAutoInsertDowelSymForHolesPart,
                    (int)swUserPreferenceOption_e.swDetailingNoOptionSpecified, false);

                //set DXF output quality options
                swExt.SetUserPreferenceInteger((int)swUserPreferenceIntegerValue_e.swImageQualityWireframe,
                    (int)swUserPreferenceOption_e.swDetailingNoOptionSpecified,
                    (int)swImageQualityWireframe_e.swWireframeImageQualityCustom);
                swExt.SetUserPreferenceInteger((int)swUserPreferenceIntegerValue_e.swImageQualityWireframeValue,
                    (int)swUserPreferenceOption_e.swDetailingNoOptionSpecified, 100);
                swExt.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swPreciseRenderingOfOverlappingGeometry,
                    (int)swUserPreferenceOption_e.swDetailingNoOptionSpecified, true);
                swExt.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swImageQualityWireframeHighCurveQuality,
                    (int)swUserPreferenceOption_e.swDetailingNoOptionSpecified, true);

                //override units for DXF output
                swExt.SetUserPreferenceInteger((int)swUserPreferenceIntegerValue_e.swUnitSystem,
                    (int)swUserPreferenceOption_e.swDetailingNoOptionSpecified, (int)swUnitSystem_e.swUnitSystem_IPS);

                //create the FRONT view
                var swView = swDraw.CreateDrawViewFromModelView3(sIn, @"*Front", 0.5, 0.5, 0);
                swView.SetName2(@"FRONT VIEW");
                swView.SetDisplayMode4(false, (int)swDisplayMode_e.swHIDDEN_GREYED, false, false, false);
                var swFirst = swView; //save the initial view for later

                //create the RIGHT view
                var adPos = (double[])swView.GetOutline();
                var dOld = adPos[2];
                swView = swDraw.CreateDrawViewFromModelView3(sIn, @"*Right", 0.5, 0.5, 0);
                swView.SetName2(@"RIGHT VIEW");
                swView.SetDisplayMode4(false, (int)swDisplayMode_e.swHIDDEN_GREYED, false, false, false);
                adPos = (double[])swView.GetOutline();
                var adNew = new double[2];
                adNew[0] = dOld + (adPos[2] - adPos[0]) / 2;
                adNew[1] = 0.5;
                swView.Position = adNew;

                //create the BACK view
                adPos = (double[])swView.GetOutline();
                dOld = adPos[2];
                var adViewPos = (double[])swView.Position;
                swView = swDraw.CreateDrawViewFromModelView3(sIn, @"*Back", adViewPos[0], adViewPos[1], 0);
                swView.SetName2(@"BACK VIEW");
                swView.SetDisplayMode4(false, (int)swDisplayMode_e.swHIDDEN_GREYED, false, false, false);
                adPos = (double[])swView.GetOutline();
                adNew[0] = dOld + (adPos[2] - adPos[0]) / 2;
                adNew[1] = 0.5;
                swView.Position = adNew;

                //create the LEFT view
                adPos = (double[])swFirst.GetOutline();
                dOld = adPos[0];
                swView = swDraw.CreateDrawViewFromModelView3(sIn, @"*Left", 0.5, 0.5, 0);
                swView.SetName2(@"LEFT VIEW");
                swView.SetDisplayMode4(false, (int)swDisplayMode_e.swHIDDEN_GREYED, false, false, false);
                adPos = (double[])swView.GetOutline();
                adNew[0] = dOld - (adPos[2] - adPos[0]) / 2;
                adNew[1] = 0.5;
                swView.Position = adNew;

                //create the TOP view
                adPos = (double[])swFirst.GetOutline();
                dOld = adPos[3];
                swView = swDraw.CreateDrawViewFromModelView3(sIn, @"*Top", 0.5, 0.5, 0);
                swView.SetName2(@"TOP VIEW");
                swView.SetDisplayMode4(false, (int)swDisplayMode_e.swHIDDEN_GREYED, false, false, false);
                adPos = (double[])swView.GetOutline();
                adNew[0] = 0.5;
                adNew[1] = dOld + (adPos[3] - adPos[1]) / 2;
                swView.Position = adNew;

                //create the BOTTOM view
                adPos = (double[])swFirst.GetOutline();
                dOld = adPos[1];
                swView = swDraw.CreateDrawViewFromModelView3(sIn, @"*Bottom", 0.5, 0.5, 0);
                swView.SetName2(@"BOTTOM VIEW");
                swView.SetDisplayMode4(false, (int)swDisplayMode_e.swHIDDEN_GREYED, false, false, false);
                adPos = (double[])swView.GetOutline();
                adNew[0] = 0.5;
                adNew[1] = dOld - (adPos[3] - adPos[1]) / 2 - 0.05;
                swView.Position = adNew;

                //attempt to save
                var iErr = 0;
                _swApp.ActivateDoc3(swTemp.GetTitle(), true, 0, ref iErr);
                if (iErr != 0)
                {
                    var sMsg = @"Error '" + iErr + @"' during procedure GenDXF, section 'attempt save', on filename: ";
                    sMsg += swTemp.GetPathName();
                    Status(sMsg);
                    iErr = 0;
                }

                swTemp.ClearSelection2(true);
                var iWarn = 0;
                var bSave = swExt.SaveAs3(sOut, 0, 0, null, null, ref iErr, ref iWarn);
                if (!bSave)
                    if (iErr + iWarn != 0)
                    {
                        var sMsg = @"Error '" + iErr + @"'/Warning: '" + iWarn;
                        sMsg += @"' during procedure GenDXF, section 'attempt save', on filename: ";
                        sMsg += swTemp.GetPathName();
                        Status(sMsg);
                        sOut = string.Empty;
                    }

                //save screenshot for preview
                if (!_silent)
                {
                    _swApp.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swTiffImageType,
                        (int)swTiffImageType_e.swTiffImageBlackAndWhite);
                    swTemp.ViewZoomtofit2();
                    var thumb = new DxfThumb
                    {
                        ImgPath = sOut.ToUpper().Replace(@".DXF", @"_thumb.PNG"),
                        PartNo = _swModel.CustomInfo[@"Part Number"],
                        PartPath = _swModel.GetPathName()
                    };
                    bSave = swExt.SaveAs3(thumb.ImgPath, 0, 0, null, null, ref iErr, ref iWarn);
                    if (bSave) lstThumbs.Add(thumb.PartNo, thumb);
                    _swApp.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swTiffImageType,
                        (int)swTiffImageType_e.swTiffImageRGB);
                }

                //close out of temp drawing
                _swApp.QuitDoc(swTemp.GetTitle());

                //reset model
                _swModel = (sw.ModelDoc2)_swApp.ActivateDoc3(_swModel.GetTitle(), false,
                    (int)swRebuildOnActivation_e.swRebuildActiveDoc, ref iErr);
                if (iErr != 0)
                {
                    var sMsg = @"Error '" + iErr + @"' during GenDXF while reactivating model on filename: ";
                    sMsg += _swModel.GetPathName();
                    Status(sMsg);
                }

                _swModel.ShowConfiguration2(swOrig.Name);
                _swModel.ShowNamedView2(string.Empty, (int)swStandardViews_e.swTrimetricView);
                _swModel.ViewZoomtofit2();
                return sOut;
            }
            catch (COMException ex)
            {
                Status(@"Error '" + ex.ErrorCode.ToString(@"X") + @"' during procedure GenDXF");
                return string.Empty;
            }
            catch (Exception ex)
            {
                Status(@"Error '" + ex.Message + @"' during procedure GenDXF");
                return string.Empty;
            }
        }

        private string GenStep(string sOut) //generate STEP file
        {
            try
            {
                //activate model - redundant, but STEP REALLY needs this part
                var iErr = 0;
                _swModel = (sw.ModelDoc2)_swApp.ActivateDoc3(_swModel.GetPathName(), false,
                    (int)swRebuildOnActivation_e.swRebuildActiveDoc, ref iErr);
                if (iErr != 0)
                {
                    var sMsg = @"Error '" + iErr + @"' during STEP procedure while activating model on filename: ";
                    sMsg += _swModel.GetPathName();
                    Status(sMsg);
                    iErr = 0;
                }

                _swModel = (sw.ModelDoc2)_swApp.ActiveDoc;

                //attempt save
                sw.IModelDocExtension swExt = _swModel.Extension;
                var iWarn = 0;
                _swModel.ClearSelection2(true);
                var bSave = swExt.SaveAs3(sOut, 0, 0, null, null, ref iErr, ref iWarn);
                if (bSave) return sOut;
                {
                    string sMsg;
                    if (iErr + iWarn != 0)
                    {
                        sMsg = @"Error '" + iErr + @"'/Warning: '" + iWarn;
                        sMsg += @"' during procedure GenSTP, section 'attempt save' on filename: ";
                        sMsg += _swModel.GetPathName();
                        Status(sMsg);
                        sOut = string.Empty;
                    }
                    else
                    {
                        sMsg = @"Failed to save '" + _swModel.GetPathName() + @"' as STEP file '" + sOut;
                        sMsg += @"' with no error message.";
                        Status(sMsg);
                        sOut = string.Empty;
                    }
                }
                return sOut;
            }
            catch (COMException ex)
            {
                Status(@"Error '" + ex.ErrorCode.ToString(@"X") + @"' during procedure GenSTP");
                return string.Empty;
            }
            catch (Exception ex)
            {
                Status(@"Error '" + ex.Message + @"' during procedure GenSTP");
                return string.Empty;
            }
        }

        private string GenIges(string sOut) //generate IGES file
        {
            try
            {
                //activate model - redundant, but STEP REALLY needs this part - maybe IGES too?
                var iErr = 0;
                _swModel = (sw.ModelDoc2)_swApp.ActivateDoc3(_swModel.GetPathName(), false,
                    (int)swRebuildOnActivation_e.swRebuildActiveDoc, ref iErr);
                if (iErr != 0)
                {
                    var sMsg = @"Error '" + iErr + @"' during IGES procedure while activating model on filename: ";
                    sMsg += _swModel.GetPathName();
                    Status(sMsg);
                    iErr = 0;
                }

                _swModel = (sw.ModelDoc2)_swApp.ActiveDoc;

                //change units
                sw.IModelDocExtension swExt = _swModel.Extension;
                var iUnits = swExt.GetUserPreferenceInteger((int)swUserPreferenceIntegerValue_e.swUnitSystem,
                    (int)swUserPreferenceOption_e.swDetailingNoOptionSpecified);
                swExt.SetUserPreferenceInteger((int)swUserPreferenceIntegerValue_e.swUnitSystem,
                    (int)swUserPreferenceOption_e.swDetailingNoOptionSpecified, (int)swUnitSystem_e.swUnitSystem_MMGS);

                //attempt save
                var iWarn = 0;
                _swModel.ClearSelection2(true);
                var bSave = swExt.SaveAs3(sOut, 0, 0, null, null, ref iErr, ref iWarn);
                if (!bSave)
                {
                    string sMsg;
                    if (iErr + iWarn != 0)
                    {
                        sMsg = @"Error '" + iErr + @"'/Warning: '" + iWarn;
                        sMsg += @"' during procedure GenIGES, section 'attempt save' on filename: ";
                        sMsg += _swModel.GetPathName();
                        Status(sMsg);
                        sOut = string.Empty;
                    }
                    else
                    {
                        sMsg = @"Failed to save '" + _swModel.GetPathName() + @"' as IGES file '" + sOut;
                        sMsg += @"' with no error message.";
                        Status(sMsg);
                        sOut = string.Empty;
                    }
                }

                //reset units and exit
                swExt.SetUserPreferenceInteger((int)swUserPreferenceIntegerValue_e.swUnitSystem,
                    (int)swUserPreferenceOption_e.swDetailingNoOptionSpecified, iUnits);
                return sOut;
            }
            catch (COMException ex)
            {
                Status(@"Error '" + ex.ErrorCode.ToString(@"X") + @"' during procedure GenIGES");
                return string.Empty;
            }
            catch (Exception ex)
            {
                Status(@"Error '" + ex.Message + @"' during procedure GenIGES");
                return string.Empty;
            }
        }

        private string GenPng(string sOut) //generate PNG file
        {
            try
            {
                //attempt save
                sw.IModelDocExtension swExt = _swModel.Extension;
                var iErr = 0;
                var iWarn = 0;
                var bSave = swExt.SaveAs3(sOut, 0, 0, null, null, ref iErr, ref iWarn);
                if (bSave) return sOut;
                if (iErr + iWarn == 0) return sOut;
                var sMsg = @"Error '" + iErr + @"'/Warning: '" + iWarn;
                sMsg += @"' during procedure GenPNG, section 'attempt save' on filename: " + _swModel.GetPathName();
                Status(sMsg);
                sOut = string.Empty;
                return sOut;
            }
            catch (COMException ex)
            {
                Status(@"Error '" + ex.ErrorCode.ToString(@"X") + @"' during procedure GenPNG");
                return string.Empty;
            }
            catch (Exception ex)
            {
                Status(@"Error '" + ex.Message + @"' during procedure GenPNG");
                return string.Empty;
            }
        }

        //add CAD file path to the collection
        private bool AddCad(bool bTop, ref string[] sTop, ref Dictionary<string, CadItem> lstCad)
        {
            try
            {
                //get all info to construct the CAD item
                var sMdl = _swModel.GetPathName();
                var sDraw = string.Empty;
                var iTop = 0;
                var bAssm = false;
                var bTopIsMdl = bTop;
                switch (_swModel.GetType()) //set options based on the file type
                {
                    case (int)swDocumentTypes_e.swDocDRAWING:
                        sDraw = _swModel.GetPathName();
                        sTop[1] = sDraw;
                        bTopIsMdl = false;
                        iTop = (int)ClsEnums.EnumGenTop.Drawing;

                        //go get the model path of the top level drawing
                        // ReSharper disable once SuspiciousTypeConversion.Global
                        var swDraw = (sw.DrawingDoc)_swModel;
                        var swSheets = (object[])swDraw.GetSheetNames();
                        swDraw.ActivateSheet((string)swSheets[0]);
                        var swView = (sw.View)swDraw.GetFirstView();
                        swView = (sw.View)swView.GetNextView();
                        var sName = swView.GetReferencedModelName();
                        var iErr = 0;
                        _swModel = (sw.ModelDoc2)_swApp.ActivateDoc3(sName, false,
                            (int)swRebuildOnActivation_e.swRebuildActiveDoc, ref iErr);
                        _swModel.ShowConfiguration2(swView.ReferencedConfiguration);
                        sMdl = _swModel.GetPathName();
                        sTop[0] = sMdl;
                        switch (_swModel.GetType()) //set options based on the underlying model
                        {
                            case (int)swDocumentTypes_e.swDocASSEMBLY:
                                bAssm = true;
                                break;
                            case (int)swDocumentTypes_e.swDocPART:
                                break;
                        }

                        break;
                    case (int)swDocumentTypes_e.swDocASSEMBLY:
                        sDraw = sMdl.Substring(0, sMdl.LastIndexOf('.')) + @".SLDDRW";
                        iTop = bTop ? (int)ClsEnums.EnumGenTop.Model : (int)ClsEnums.EnumGenTop.No;
                        bAssm = true;
                        if (bTop) sTop[0] = sMdl;
                        break;
                    case (int)swDocumentTypes_e.swDocPART:
                        sDraw = sMdl.Substring(0, sMdl.LastIndexOf('.')) + @".SLDDRW";
                        iTop = bTop ? (int)ClsEnums.EnumGenTop.Model : (int)ClsEnums.EnumGenTop.No;
                        if (bTop) sTop[0] = sMdl;
                        break;
                }

                //build properties and verify key fields are not empty
                var fpProps = new FileProp
                {
                    CustNo = _swModel.CustomInfo[@"Number"],
                    DocNo = _swModel.CustomInfo[@"Document Number"],
                    PartNo = _swModel.CustomInfo[@"Part Number"],
                    Description = _swModel.CustomInfo[@"Description"],
                    // ReSharper disable once StringLiteralTypo
                    Customer = _swModel.CustomInfo[@"CustAbbrev"],
                    ProductCategory = _swModel.CustomInfo[@"ProductCategory"]
                };
                if (fpProps.DocNo.Length == 0) //if document number is empty then use file name
                {
                    var sFName = sMdl.Substring(0, sMdl.LastIndexOf('.') + 1);
                    sFName = sFName.Substring(sFName.LastIndexOf('\\') + 1,
                        sFName.Length - (sFName.LastIndexOf('\\') + 1));
                    fpProps.DocNo = sFName;
                }

                if (fpProps.PartNo.Length == 0) //if part number is empty then use DocNo property
                    fpProps.PartNo = fpProps.DocNo;

                //get weight
                double weight = 0;
                if (bTop || (!bAssm && DoWeight(fpProps.PartNo)))
                {
                    var mass = (sw.IMassProperty2)_swModel.Extension.CreateMassProperty2();
                    mass.UseSystemUnits = false;
                    mass.Recalculate();
                    weight = mass.Mass;
                }

                //build CAD item and add
                var oCad = new CadItem(sMdl, sDraw, iTop, bAssm, fpProps, weight, 1);
                if (!lstCad.ContainsKey(sMdl)) //check for dupe
                    lstCad.Add(sMdl, oCad);
                else
                {
                    var cadItem = lstCad[sMdl];
                    cadItem.Occur++;
                    lstCad[sMdl] = cadItem;
                }

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

        //traverse each feature looking for sketches and hide them
        private void HideSketch(sw.Feature swFeat)
        {
            try
            {
                if (swFeat == null) throw new ArgumentNullException(nameof(swFeat));

                //hide if sketch
                switch (swFeat.GetTypeName2())
                {
                    case @"ProfileFeature":
                        _swModel.Extension.SelectByID2(swFeat.Name, @"SKETCH", 0, 0, 0, false, 0, null, 0);
                        _swModel.BlankSketch();
                        _swModel.ClearSelection2(true);
                        break;
                    case @"OriginProfileFeature":
                        // ReSharper disable once StringLiteralTypo
                        _swModel.Extension.SelectByID2(swFeat.Name, @"EXTSKETCHPOINT", 0, 0, 0, false, 0, null, 0);
                        _swModel.BlankSketch();
                        _swModel.ClearSelection2(true);
                        break;
                    case @"RefPlane":
                        _swModel.Extension.SelectByID2(swFeat.Name, @"PLANE", 0, 0, 0, false, 0, null, 0);
                        _swModel.BlankRefGeom();
                        _swModel.ClearSelection2(true);
                        break;
                }

                //check for sub features
                var swChild = (sw.Feature)swFeat.GetFirstSubFeature();
                while (swChild != null)
                {
                    HideSketch(swChild);
                    swChild = (sw.Feature)swChild.GetNextSubFeature();
                }
            }
            catch (COMException ex)
            {
                Status(@"Error '" + ex.ErrorCode.ToString(@"X") + @"' during procedure HideSketch");
            }
            catch (Exception ex)
            {
                Status(@"Error '" + ex.Message + @"' during procedure HideSketch");
            }
        }

        //traverse each feature looking for models
        private void CheckFeat(sw.Feature swFeat, ref string[] sTop, ref Dictionary<string, CadItem> lstCad)
        {
            try
            {
                if (swFeat == null) throw new ArgumentNullException(nameof(swFeat));
                var type = swFeat.GetTypeName2();
                switch (type)
                {
                    case "Reference":
                    case "ReferencePattern":
                        break;
                    default:
                        return;
                }

                var swComp = (sw.Component2)swFeat.GetSpecificFeature2();

                //skip if excluded
                var b = (bool[])swComp.GetExcludeFromBOM2((int)swInConfigurationOpts_e.swThisConfiguration, null);
                if (b[0]) return;

                //skip if suppressed
                if (swComp.GetSuppression2() == 0) return;

                _swModel = (sw.ModelDoc2)swComp.GetModelDoc2(); //setting up for use in AddToCol
                if (_swModel != null)
                {
                    var conf = _swModel.ConfigurationManager.ActiveConfiguration;
                    if (conf.ChildComponentDisplayInBOM !=
                        (int)swChildComponentInBOMOption_e.swChildComponent_Promote)
                    {
                        AddCad(false, ref sTop, ref lstCad);
                        _swApp.QuitDoc(_swModel.GetTitle());
                    }
                }

                var swSubFeat = swComp.FirstFeature(); //call recursively for subcomponents
                while (swSubFeat != null)
                {
                    CheckFeat(swSubFeat, ref sTop, ref lstCad);
                    swSubFeat = (sw.Feature)swSubFeat.GetNextFeature();
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

        //get or set user settings
        private UserSettings ChangeSettings(bool bSet = false, UserSettings usrSet = default)
        {
            const string keyPath =
                // ReSharper disable once StringLiteralTypo
                @"HKEY_CURRENT_USER\SOFTWARE\Solidworks\Applications\PDMWorks Enterprise\PDMSW\Options";
            if (bSet) //set values
            {
                if (!usrSet.Init) //set preferred values
                {
                    _swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swExtRefNoPromptOrSave, true);
                    _swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swAutomaticScaling3ViewDrawings, true);
                    _swApp.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swDxfOutputNoScale, 1);
                    _swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDxfExportSplinesAsSplines, false);
                    _swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swStepExportFaceEdgeProps, true);
                    _swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swStepExportSplitPeriodic, true);
                    _swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swStepExport3DCurveFeatures, false);
                    _swApp.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swStepExportPreference, 0);
                    _swApp.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swTiffImageType,
                        (int)swTiffImageType_e.swTiffImageRGB);
                    _swApp.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swTiffScreenOrPrintCapture,
                        0);
                    _swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swIGESExportAsWireframe, false);
                    _swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swIGESExportSolidAndSurface, true);
                    _swApp.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swIGESRepresentation,
                        (int)swIGESRepresentation_e.swIGES_TRMSRF);
                    _swApp.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swStepAP, 214);
                    _swApp.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swDxfVersion,
                        (int)swDxfFormat_e.swDxfFormat_R2000);
                    _swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swLargeAsmModeEnabled, false);
                    _swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDXFExportHiddenLayersOn, false);
                    _swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDXFExportHiddenLayersWarnIsOn,
                        false);
                    Registry.SetValue(keyPath, @"ModifyWarning", 0, RegistryValueKind.DWord);
                    Registry.SetValue(keyPath, @"ReadOnlyWarning", 0, RegistryValueKind.DWord);
                    Registry.SetValue(keyPath, @"ShowFileDataCard", 0, RegistryValueKind.DWord);
                }
                else //set values to previously saved user settings
                {
                    _swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swExtRefNoPromptOrSave,
                        usrSet.ExtRefNoPromptOrSave);
                    _swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swAutomaticScaling3ViewDrawings,
                        usrSet.AutomaticScaling3ViewDrawings);
                    _swApp.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swDxfOutputNoScale,
                        usrSet.DxfOutputNoScale);
                    _swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDxfExportSplinesAsSplines,
                        usrSet.DxfExportSplinesAsSplines);
                    _swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swStepExportFaceEdgeProps,
                        usrSet.StepExportFaceEdgeProps);
                    _swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swStepExportSplitPeriodic,
                        usrSet.StepExportSplitPeriodic);
                    _swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swStepExport3DCurveFeatures,
                        usrSet.StepExport3DCurveFeatures);
                    _swApp.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swStepExportPreference,
                        usrSet.StepExportPreference);
                    _swApp.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swTiffImageType,
                        usrSet.TiffImageType);
                    _swApp.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swTiffScreenOrPrintCapture,
                        usrSet.TiffScreenOrPrintCapture);
                    _swApp.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swIGESRepresentation,
                        usrSet.IgesRepresentation);
                    _swApp.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swIGESCurveRepresentation,
                        usrSet.IgesCurveRepresentation);
                    _swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swIGESExportAsWireframe,
                        !usrSet.IgesExportSolidOrWireFrame);
                    _swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swIGESExportSolidAndSurface,
                        usrSet.IgesExportSolidOrWireFrame);
                    _swApp.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swStepAP,
                        usrSet.StepAp);
                    _swApp.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swDxfVersion,
                        usrSet.DxfVersion);
                    _swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swLargeAsmModeEnabled,
                        usrSet.LargeAsmModeEnabled);
                    _swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDXFExportHiddenLayersOn,
                        usrSet.DxfExportHiddenLayersOn);
                    _swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDXFExportHiddenLayersWarnIsOn,
                        usrSet.DxfExportHiddenLayersWarnIsOn);
                    Registry.SetValue(keyPath, @"ModifyWarning", usrSet.PdmWarnModify, RegistryValueKind.DWord);
                    Registry.SetValue(keyPath, @"ReadOnlyWarning", usrSet.PdmWarnCheckOut, RegistryValueKind.DWord);
                    Registry.SetValue(keyPath, @"ShowFileDataCard", usrSet.PdmShowDataCard, RegistryValueKind.DWord);
                }
            }
            else
            {
                usrSet = new UserSettings(
                    _swApp.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swExtRefNoPromptOrSave),
                    _swApp.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swAutomaticScaling3ViewDrawings),
                    _swApp.GetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swDxfOutputNoScale),
                    _swApp.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDxfExportSplinesAsSplines),
                    _swApp.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swStepExportFaceEdgeProps),
                    _swApp.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swStepExportSplitPeriodic),
                    _swApp.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swStepExport3DCurveFeatures),
                    _swApp.GetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swStepExportPreference),
                    _swApp.GetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swTiffImageType),
                    _swApp.GetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e
                        .swTiffScreenOrPrintCapture),
                    (int)Registry.GetValue(keyPath, @"ModifyWarning", RegistryValueKind.DWord),
                    (int)Registry.GetValue(keyPath, @"ReadOnlyWarning", RegistryValueKind.DWord),
                    (int)Registry.GetValue(keyPath, @"ShowFileDataCard", RegistryValueKind.DWord),
                    _swApp.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swIGESExportSolidAndSurface),
                    _swApp.GetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swIGESRepresentation),
                    _swApp.GetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swIGESCurveRepresentation),
                    _swApp.GetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swStepAP),
                    _swApp.GetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swDxfVersion),
                    _swApp.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swLargeAsmModeEnabled),
                    _swApp.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDXFExportHiddenLayersOn),
                    _swApp.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDXFExportHiddenLayersWarnIsOn)
                );
            }

            return usrSet;
        }

        //remove characters that cannot be in Windows folder names
        private string RemBad(string sPart)
        {
            var sBlack = string.Empty;
            try
            {
                //validation
                sPart = sPart.Trim();
                if (sPart.Length == 0) return sPart;

                //populate the blacklist with Windows forbidden chars
                int iCnt;
                char cAsc;
                for (iCnt = 0; iCnt <= 31; iCnt++) //add ASCII non-printable chars
                {
                    cAsc = (char)iCnt;
                    sBlack += cAsc.ToString();
                }

                cAsc = (char)60; // <
                sBlack += cAsc.ToString();
                cAsc = (char)62; // >
                sBlack += cAsc.ToString();
                cAsc = (char)58; // :
                sBlack += cAsc.ToString();
                cAsc = (char)34; // "
                sBlack += cAsc.ToString();
                cAsc = (char)47; // /
                sBlack += cAsc.ToString();
                cAsc = (char)92; // \
                sBlack += cAsc.ToString();
                cAsc = (char)124; // |
                sBlack += cAsc.ToString();
                cAsc = (char)63; // ?
                sBlack += cAsc.ToString();
                cAsc = (char)42; // *
                sBlack += cAsc.ToString();

                //check each character against blacklist and rebuild string with only valid chars
                var asChar = sPart.ToCharArray();
                sPart = string.Empty;
                var iMax = asChar.GetUpperBound(0);
                for (iCnt = 0; iCnt <= iMax; iCnt++)
                    if (sBlack.IndexOf(asChar[iCnt].ToString(), 0, StringComparison.Ordinal) == -1)
                        sPart += asChar[iCnt].ToString();

                //cannot end in a period
                cAsc = (char)46; // .
                if (sPart.Substring(sPart.Length - 1, 1) == cAsc.ToString())
                    sPart = sPart.Substring(0, sPart.Length - 1);
                return sPart;
            }
            catch (COMException ex)
            {
                Status(@"Error '" + ex.ErrorCode.ToString(@"X") + @"' during procedure RemBad");
                return sPart;
            }
            catch (Exception ex)
            {
                Status(@"Error '" + ex.Message + @"' during procedure RemBad");
                return sPart;
            }
        }

        private bool Skip(string sPart) //verify the part prefix is allowed
        {
            string[] asWht =
                { @"ASY", @"SUB", @"MPA", @"MPP", @"MPM", @"MPW", @"GLS", @"GRA" }; //allowed part categories
            sPart = sPart.Trim();
            try
            {
                if (sPart.Length == 0) return true;

                int iCnt;
                for (iCnt = 0; iCnt < asWht.Length; iCnt++)
                    if (sPart.IndexOf(asWht[iCnt], 0, StringComparison.OrdinalIgnoreCase) == 0)
                        return false;
                return true;
            }
            catch (COMException ex)
            {
                Status(@"Error '" + ex.ErrorCode.ToString(@"X") + @"' during procedure Skip");
                return false;
            }
            catch (Exception ex)
            {
                Status(@"Error '" + ex.Message + @"' during procedure Skip");
                return false;
            }
        }

        private bool DoWeight(string sPart) //verify part prefix needs to be weighed
        {
            string[] asWht =
                { @"MPA", @"MPM" }; //allowed part categories
            sPart = sPart.Trim();
            try
            {
                if (sPart.Length == 0) return false;

                int iCnt;
                for (iCnt = 0; iCnt < asWht.Length; iCnt++)
                    if (sPart.IndexOf(asWht[iCnt], 0, StringComparison.OrdinalIgnoreCase) == 0)
                        return true;
                return false;
            }
            catch (COMException ex)
            {
                Status(@"Error '" + ex.ErrorCode.ToString(@"X") + @"' during procedure DoWeight");
                return false;
            }
            catch (Exception ex)
            {
                Status(@"Error '" + ex.Message + @"' during procedure DoWeight");
                return false;
            }
        }

        private void Status(string sMsg, int iPrg = 0) //increment progress bar and add a status message
        {
            var giPrg = _progBar; //current progress bar value
            giPrg += iPrg;
            giPrg = giPrg > 99 ? 99 : giPrg;
            _progBar = giPrg;
            OnSetStatus(new SetStatusEventArgs { Message = sMsg, Progress = _progBar });
        }

        private static void LogWeight(string part, double total, double metal) //log the weights to a SQL table
        {
            //connection string
            const string sConn = @"Server=EPICOR10;Database=Vira;Trusted_Connection=True;";
            const string sTable = @"[dbo].[eng_PartWeight]";

            try
            {
                //upload records
                using var conn = new SqlConnection(sConn);
                conn.Open();

                //erase existing data
                var sSql = $@"DELETE FROM {sTable} WHERE [PartNUm] = '{part}';";
                var sqlCom = new SqlCommand(sSql, conn);
                sqlCom.ExecuteNonQuery();

                //upload new data
                sSql = $@"INSERT INTO {sTable} ([PartNum], [TotalWeight], [MetalWeight], [LastUpdated])
                        VALUES ('{part}', {Math.Round(total, 2)}, {Math.Round(metal, 2)}, GetDate());";
                sqlCom = new SqlCommand(sSql, conn);
                sqlCom.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private struct CadItem //solidworks item struct
        {
            public CadItem(string sMdl, string sDraw, int iTop, bool bAssm, FileProp fpProps, double dWeight, int iOcc)
            {
                Model = sMdl;
                Drawing = sDraw;
                TopLevel = iTop;
                IsAssm = bAssm;
                Properties = fpProps;
                Ignore = false;
                Weight = dWeight;
                Occur = iOcc;
            }

            public string Model { get; } //full path to the model file
            public string Drawing { get; } //full path to drawing file
            public int TopLevel { get; } //top level info (from ClsEnums.EnumGenTop)
            public bool IsAssm { get; } //is the CAD file an assembly
            public bool Ignore { get; set; } //flag part as should be skipped
            public FileProp Properties { get; } //custom properties from the CAD file
            public double Weight { get; } //weight
            public int Occur { get; set; } //number of occurrences in the model
        }

        private struct DocItem //document item struct
        {
            public DocItem(string sFile, string sPath, int iType, FileProp fpProps)
            {
                FileName = sFile;
                PathFinal = sPath;
                DocType = iType;
                Properties = fpProps;
            }

            public string FileName { get; } //file name without the path
            public string PathFinal { get; } //final resting place without filename
            public int DocType { get; } //type from ClsEnums.EnumGenOptions
            public FileProp Properties { get; } //custom properties from the CAD file
        }

        private struct FileProp //data card properties
        {
            public string DocNo { get; set; }
            public string PartNo { get; set; }
            public string CustNo { get; set; }
            public string Description { get; set; }
            public string Customer { get; set; }
            public string ProductCategory { get; set; }
        }

        private struct DxfThumb //DXF preview thumbnail
        {
            public string PartNo { get; set; }
            public string PartPath { get; set; }
            public string ImgPath { get; set; }
        }

        private struct PdfThumb //PDF preview thumbnail
        {
            public string PartNo { get; set; }
            public string ImgPath { get; set; }
            public string PdfPath { get; set; }
        }

        private struct UserSettings //user settings
        {
            public UserSettings(bool bFlag, bool bAuto, int iScale, bool bSpline, bool bEdge, bool bFace, bool bCurve,
                int iOut, int iType, int iCap, int iMod, int iChk, int iCard, bool bSolid, int iSolidType,
                int iWireType, int iStep, int iDxf, bool bLrgAsm, bool bLayer, bool bHidDiag)
            {
                ExtRefNoPromptOrSave = bFlag;
                AutomaticScaling3ViewDrawings = bAuto;
                DxfOutputNoScale = iScale;
                DxfExportSplinesAsSplines = bSpline;
                StepExportFaceEdgeProps = bEdge;
                StepExportSplitPeriodic = bFace;
                StepExport3DCurveFeatures = bCurve;
                StepExportPreference = iOut;
                TiffImageType = iType;
                TiffScreenOrPrintCapture = iCap;
                PdmWarnModify = iMod;
                PdmWarnCheckOut = iChk;
                PdmShowDataCard = iCard;
                IgesExportSolidOrWireFrame = bSolid;
                IgesRepresentation = iSolidType;
                IgesCurveRepresentation = iWireType;
                StepAp = iStep;
                DxfVersion = iDxf;
                LargeAsmModeEnabled = bLrgAsm;
                DxfExportHiddenLayersOn = bLayer;
                DxfExportHiddenLayersWarnIsOn = bHidDiag;
                Init = true;
            }

            public bool ExtRefNoPromptOrSave { get; } //whether to prompt on close to save
            public bool AutomaticScaling3ViewDrawings { get; } //whether to scale the sheet to accomodate views
            public int DxfOutputNoScale { get; } //whether to output DXF at 1:1 scale
            public bool DxfExportSplinesAsSplines { get; } //whether to output DXF splines as splines or poly-lines
            public bool StepExportFaceEdgeProps { get; } //export face/edge properties
            public bool StepExportSplitPeriodic { get; } //split periodic faces
            public bool StepExport3DCurveFeatures { get; } //export 3D curve features
            public int StepExportPreference { get; } //whether to output solid or wireframe
            public int TiffImageType { get; } //image type for PNG output
            public int TiffScreenOrPrintCapture { get; } //screen print or print capture
            public int PdmWarnModify { get; } //warn if modifying file not checked out in PDM
            public int PdmWarnCheckOut { get; } //prompt to check out the file if open as read-only
            public int PdmShowDataCard { get; } //show data card when saving the file into PDM
            public bool Init { get; } //if this struct has values
            public bool IgesExportSolidOrWireFrame { get; } //export IGES as solid or wireframe
            public int IgesRepresentation { get; } //export IGES solid type
            public int IgesCurveRepresentation { get; } //export IGES solid type
            public int StepAp { get; } //step export file format
            public int DxfVersion { get; } //dxf export file format
            public bool LargeAsmModeEnabled { get; } //enable large assembly mode
            public bool DxfExportHiddenLayersOn { get; } //export hidden layers on dxf
            public bool DxfExportHiddenLayersWarnIsOn { get; } //do not show the hidden layer dialog
        }

        public event EventHandler<SetStatusEventArgs> SetStatus;

        private void OnSetStatus(SetStatusEventArgs e)
        {
            SetStatus?.Invoke(this, e);
        }
    }

    public class SetStatusEventArgs : EventArgs
    {
        public int Progress { get; set; }
        public string Message { get; set; }
    }
}