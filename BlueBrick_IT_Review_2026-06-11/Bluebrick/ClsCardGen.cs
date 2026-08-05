using System;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using EPDM.Interop.epdm;
using PdfiumViewer;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using View = SolidWorks.Interop.sldworks.View;
using DocGenerator;

namespace BlueBrick
{
    internal static class ClsCardGen
    {
        private static FrmPane _frm;

        internal static void GenCard(FrmPane frmStat, ISldWorks swApp)
        {
            _frm = frmStat;
            try
            {
                //open form to allow serial selection
                CardProp oCard;
                using (var formA = new FrmCard())
                {
                    var result = formA.ShowDialog();
                    if (result != DialogResult.OK) return;
                    oCard = new CardProp(formA.Type, formA.Opp.Trim(), formA.Upc.Trim(), formA.Desc.Trim().ToUpper(),
                        formA.Cust.Trim().ToUpper(), formA.ArtF, formA.ArtR);
                }

                //check art for PDF and convert
                if (oCard.FrontArtFile.Length != 0 &&
                    oCard.FrontArtFile.Substring(oCard.FrontArtFile.Length - 3, 3).ToUpper() == @"PDF")
                    //oCard.FrontArtFile.IndexOf(@".pdf", StringComparison.OrdinalIgnoreCase) != -1)
                    oCard.FrontArtFile = PdfConvert(oCard.FrontArtFile, oCard.ArtWidth, oCard.ArtHeight);

                if (oCard.RearArtFile.Length != 0 &&
                    oCard.RearArtFile.Substring(oCard.RearArtFile.Length - 3, 3).ToUpper() == @"PDF")
                    //oCard.RearArtFile.IndexOf(@".pdf", StringComparison.OrdinalIgnoreCase) != -1)
                    oCard.RearArtFile = PdfConvert(oCard.RearArtFile, oCard.ArtWidth, oCard.ArtHeight);

                //login to vault
                frmStat.SetStat(0, @"Logging into vault...", false, true);
                var oVault = (IEdmVault20)new EdmVault5();
                if (!oVault.IsLoggedIn) oVault.LoginAuto(@"_PDMVault", 0);

                //copy front artwork and check in if checked out
                int iFile;
                IEdmFile15 oFile;
                IEdmFolder11 oFolder;
                if (oCard.FrontArtFile.Length != 0)
                {
                    frmStat.SetStat(0, @"Adding artwork to vault...");
                    var s = oCard.MiscPath + Right(oCard.FrontArtFile,
                        oCard.FrontArtFile.Length - oCard.FrontArtFile.LastIndexOf('\\'));
                    if (oCard.FrontArtFile != s)
                    {
                        //todo -- error on file already exists
                        oFolder = (IEdmFolder11)oVault.GetFolderFromPath(oCard.MiscPath);
                        iFile = oFolder.AddFile2(0, oCard.FrontArtFile, out _);
                        oFile = (IEdmFile15)oVault.GetObject(EdmObjectType.EdmObject_File, iFile);
                        oCard.FrontArtFile = oFile.GetLocalPath(oFolder.ID); //update to new path
                    }

                    oFile = (IEdmFile15)oVault.GetFileFromPath(oCard.FrontArtFile, out _);
                    if (oFile.IsLocked)
                    {
                        if (oFile.LockedByUserID == oVault.GetLoggedInWindowsUserID(oVault.Name))
                        {
                            oFile.UnlockFile(0, @"Checked in by Garmin card generator");
                        }
                        else
                        {
                            var oState = oFile.CurrentState;
                            if (oState.Name == @"Private State")
                            {
                                frmStat.SetStat(100, @"Artwork file(front) is in Private State!", true);
                                return;
                            }

                            oFile.GetFileCopy(0);
                        }
                    }
                }

                //copy rear artwork and check in if checked out
                if (oCard.HasRearArt)
                {
                    frmStat.SetStat(10, @"Adding secondary artwork to vault...");
                    if (oCard.RearArtFile.Length != 0)
                    {
                        var s = oCard.MiscPath + Right(oCard.RearArtFile,
                            oCard.RearArtFile.Length - oCard.RearArtFile.LastIndexOf('\\'));
                        if (oCard.RearArtFile != s)
                        {
                            oFolder = (IEdmFolder11)oVault.GetFolderFromPath(oCard.MiscPath);
                            iFile = oFolder.AddFile2(0, oCard.RearArtFile, out _);
                            oFile = (IEdmFile15)oVault.GetObject(EdmObjectType.EdmObject_File, iFile);
                            oCard.RearArtFile = oFile.GetLocalPath(oFolder.ID);
                        }

                        oFile = (IEdmFile15)oVault.GetFileFromPath(oCard.RearArtFile, out _);
                        if (oFile.IsLocked)
                        {
                            if (oFile.LockedByUserID == oVault.GetLoggedInWindowsUserID(oVault.Name))
                            {
                                oFile.UnlockFile(0, @"Checked in by Garmin card generator");
                            }
                            else
                            {
                                var oState = oFile.CurrentState;
                                if (oState.Name == @"Private State")
                                {
                                    frmStat.SetStat(100, @"Artwork file(front) is in Private State!", true);
                                    return;
                                }

                                oFile.GetFileCopy(0);
                            }
                        }
                    }
                }

                //get new serial number
                frmStat.SetStat(15, @"Getting serial number...");
                var oSerGen = (IEdmSerNoGen7)oVault.CreateUtility(EdmUtility.EdmUtil_SerNoGen);
                var oSer = oSerGen.AllocSerNoValue(@"DocNumbers"); //need 800 serial name
                oCard.SerialNum = oSer.Value;

                //get new mpp number
                frmStat.SetStat(20, @"Assigning part numbers...");
                oSerGen = (IEdmSerNoGen7)oVault.CreateUtility(EdmUtility.EdmUtil_SerNoGen);
                oSer = oSerGen.AllocSerNoValue(@"04-MPP5");
                oCard.PartNum = oSer.Value;

                //copy files - manual because the built-in copy tree api is bad
                frmStat.SetStat(25, @"Copying over template...");
                var oDest = (IEdmFolder11)oVault.GetFolderFromPath(oCard.CadPath);
                var sMdl = oCard.SerialNum + @".SLDPRT";
                oCard.ModelFileId = oDest.AddFile2(0, oCard.TempMdl, out _, sMdl);
                var sFile = oCard.SerialNum + @".SLDDRW";
                oCard.DrawFileId = oDest.AddFile2(0, oCard.TempDraw, out _, sFile);

                //update drawing references to new model
                var oRawRef = (IEdmRawReferenceMgr)oVault.CreateUtility(EdmUtility.EdmUtil_RawReferenceMgr);
                if (!oRawRef.Open(oCard.CadPath + sFile))
                {
                    frmStat.SetStat(100, @"Error updating file references!", true);
                    return;
                }
                EdmRawReference[] aRefs = null;
                oRawRef.GetReferences(ref aRefs);
                aRefs[0].mbsIncludePath = oCard.CadPath + sMdl;
                aRefs[0].mbsRefName = sMdl;
                oRawRef.UpdateReferences(aRefs);
                oRawRef.Close();

                //get new file info and open
                frmStat.SetStat(35, @"Opening new files...");
                oFile = (IEdmFile15)oVault.GetObject(EdmObjectType.EdmObject_File, oCard.DrawFileId);
                oFolder = oDest;
                var oDSpec = (DocumentSpecification)swApp.GetOpenDocSpec(oFolder.LocalPath + @"\" + oFile.Name);
                oDSpec.LightWeight = false;
                oDSpec.ReadOnly = true;
                oDSpec.Silent = true;
                var swModel = swApp.OpenDoc7(oDSpec);
                if (swModel == null)
                {
                    frmStat.SetStat(100, @"Error opening drawing file!", true);
                    return;
                }

                //test drawing for read only and attempt to reopen X times to get write access
                if (swModel.IsOpenedReadOnly())
                {
                    frmStat.SetStat(40, @"File is read only, unlocking...");
                    var sMsg = string.Empty;
                    var b = false;
                    for (var c = 0; c < 3; c++)
                    {
                        sMsg += @"Attempt " + c + @": " + @"Reopen Code ";
                        sMsg += swApp.CloseAndReopen2(
                            swModel, (int)swCloseReopenOption_e.swCloseReopenOption_DiscardChanges,
                            out var temp) + @", ";
                        swModel = (ModelDoc2)temp;
                        if (swModel.IsOpenedReadOnly()) continue;
                        b = true;
                        break;
                    }

                    if (!b)
                    {
                        sMsg += @"Failed to open new drawing file with write access! " +
                                @"Changes likely won't save. Output: " + sMsg;
                        frmStat.SetStat(40, sMsg);
                    }
                }

                //open underlying model
                frmStat.SetStat(45, @"Getting model...");
                // ReSharper disable once SuspiciousTypeConversion.Global
                var swDraw = (DrawingDoc)swModel;
                var swView = (View)swDraw.GetFirstView();
                swView = (View)swView.GetNextView();
                var sName = swView.GetReferencedModelName();
                var sConf = swView.ReferencedConfiguration;
                var i = 0;
                swModel = (ModelDoc2)swApp.ActivateDoc3(
                    sName, false, (int)swRebuildOnActivation_e.swRebuildActiveDoc, ref i);
                if (i != 0) frmStat.SetStat(45, @"Error: (" + i + @") during Release, section 'check if drawing file'");

                //test model for read only and attempt to get write access
                if (swModel.IsOpenedReadOnly())
                {
                    frmStat.SetStat(50, @"File is read only, unlocking...");
                    var sMsg = string.Empty;
                    var b = false;
                    for (var c = 0; c < 3; c++)
                    {
                        sMsg += @"Attempt " + c + ": ";
                        sMsg += @"Release Code " + swModel.ForceReleaseLocks() + @", ";
                        sMsg += @"Reload Code " + swModel.Extension.ReloadOrReplace(false, swModel.GetPathName(), true, true) + @", ";
                        if (swModel.IsOpenedReadOnly()) continue;
                        b = true;
                        break;
                    }

                    if (!b)
                    {
                        sMsg += @"Failed to open new model file with write access! " +
                                @"Changes likely won't save. Output: " + sMsg;
                        frmStat.SetStat(50, sMsg);
                    }
                }

                swModel.ShowConfiguration2(sConf);

                //todo if iType = assem, get proper component, assign component to swModel
                //iType = swModel.GetType(); //determine type of model
                //probably need lots of other stuff to implement assembly templates; not happening soon

                //find decals and replace art
                var swExt = swModel.Extension;
                if (oCard.FrontArtFile.Length != 0)
                {
                    frmStat.SetStat(55, @"Replacing art...");
                    // ReSharper disable once SuspiciousTypeConversion.Global
                    var swMat = (RenderMaterial)swExt.GetDecal(1, "1");
                    swMat.TextureFilename = oCard.FrontArtFile;
                    swMat.LinkToFile = false;
                    swMat.IgnoreMissingFile = true;
                    if (oCard.HasRearArt)
                        if (oCard.RearArtFile.Length != 0) //replace rear art if used
                        {
                            // ReSharper disable once SuspiciousTypeConversion.Global
                            swMat = (RenderMaterial)swExt.GetDecal(2, "1");
                            swMat.TextureFilename = oCard.RearArtFile;
                            swMat.LinkToFile = false;
                            swMat.IgnoreMissingFile = true;
                        }
                }

                //add properties to model
                frmStat.SetStat(60, @"Adding properties to model...");
                var swMgr = swExt.CustomPropertyManager[string.Empty]; //global
                swMgr.Add3(@"Description", (int)swCustomInfoType_e.swCustomInfoText, oCard.Description,
                    (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                swMgr.Add3(@"Opp", (int)swCustomInfoType_e.swCustomInfoText, oCard.OppNum,
                    (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                swMgr.Add3(@"UPC", (int)swCustomInfoType_e.swCustomInfoText, oCard.Upc,
                    (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                swMgr.Add3(@"Part Number", (int)swCustomInfoType_e.swCustomInfoText, oCard.PartNum,
                    (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                swMgr.Add3(@"Number", (int)swCustomInfoType_e.swCustomInfoText, oCard.CustomerNum,
                    (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                swMgr.Add3(@"Document Number", (int)swCustomInfoType_e.swCustomInfoText, oCard.SerialNum,
                    (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);

                var swCfg = (Configuration)swModel.GetActiveConfiguration(); //default config
                swMgr = swCfg.CustomPropertyManager;
                swMgr.Add3(@"Description", (int)swCustomInfoType_e.swCustomInfoText, oCard.Description,
                    (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                swMgr.Add3(@"Opp", (int)swCustomInfoType_e.swCustomInfoText, oCard.OppNum,
                    (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                swMgr.Add3(@"Part Number", (int)swCustomInfoType_e.swCustomInfoText, oCard.PartNum,
                    (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                swMgr.Add3(@"Number", (int)swCustomInfoType_e.swCustomInfoText, oCard.CustomerNum,
                    (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                swMgr.Add3(@"Document Number", (int)swCustomInfoType_e.swCustomInfoText, oCard.SerialNum,
                    (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);

                swMgr = swExt.CustomPropertyManager[@"1SM-FLAT-PATTERN"]; //flat pattern if exists
                if (swMgr != null)
                {
                    swMgr.Add3(@"Description", (int)swCustomInfoType_e.swCustomInfoText, oCard.Description,
                        (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                    swMgr.Add3(@"Opp", (int)swCustomInfoType_e.swCustomInfoText, oCard.OppNum,
                        (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                    swMgr.Add3(@"Part Number", (int)swCustomInfoType_e.swCustomInfoText, oCard.PartNum,
                        (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                    swMgr.Add3(@"Number", (int)swCustomInfoType_e.swCustomInfoText, oCard.CustomerNum,
                        (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                    swMgr.Add3(@"Document Number", (int)swCustomInfoType_e.swCustomInfoText, oCard.SerialNum,
                        (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                }

                //save model and close
                frmStat.SetStat(65, @"Save model and close...");
                var iErr = 0;
                var iWarn = 0;
                swExt.Rebuild((int)swRebuildOptions_e.swForceRebuildAll);
                var bSave = swModel.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref iErr, ref iWarn);
                if (!bSave)
                {
                    if ((iErr & 2) == 2) frmStat.SetStat(65, @"Model is read only!");
                    frmStat.SetStat(65, @"Error: " + iErr);
                }
                swApp.QuitDoc(swModel.GetTitle());

                //add properties to drawing
                frmStat.SetStat(70, @"Adding properties to drawing...");
                swModel = (ModelDoc2)swApp.ActivateDoc3(oDSpec.FileName, false,
                    (int)swRebuildOnActivation_e.swRebuildActiveDoc, ref i);
                swExt = swModel.Extension;
                swMgr = swExt.CustomPropertyManager[string.Empty]; //global
                swMgr.Add3(@"Description", (int)swCustomInfoType_e.swCustomInfoText, oCard.Description,
                    (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                swMgr.Add3(@"Opp", (int)swCustomInfoType_e.swCustomInfoText, oCard.OppNum,
                    (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                swMgr.Add3(@"Part Number", (int)swCustomInfoType_e.swCustomInfoText, oCard.PartNum,
                    (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                swMgr.Add3(@"Number", (int)swCustomInfoType_e.swCustomInfoText, oCard.CustomerNum,
                    (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                swMgr.Add3(@"Document Number", (int)swCustomInfoType_e.swCustomInfoText, oCard.SerialNum,
                    (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);

                //save drawing
                frmStat.SetStat(75, @"Save drawing...");
                swExt.Rebuild((int)swRebuildOptions_e.swForceRebuildAll);
                bSave = swModel.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref iErr, ref iWarn);
                if (!bSave)
                {
                    if (iErr == 2) frmStat.SetStat(75, @"Drawing is read only!");
                    frmStat.SetStat(75, @"Error: " + iErr);
                }

                //save PDF and add to vault
                frmStat.SetStat(80, @"Generating PDF...");
                var iOpt = (int)ClsEnums.EnumGenOptions.Packet;
                iOpt += (int)ClsEnums.EnumGenOptions.One;
                iOpt += (int)ClsEnums.EnumGenOptions.SaveToPdm;
                iOpt += (int)ClsEnums.EnumGenOptions.Silent;

                //call doc generator
                frmStat.PropActive(false);
                var docGen = new ClsGenerators();
                docGen.SetStatus += c_SetStatus;
                docGen.GenerateDoc(swApp, iOpt, frmStat.SFolder);
                frmStat.SetStat(100);
                frmStat.PropActive(true);

                //close drawing
                frmStat.SetStat(85, @"Closing drawing...");
                swApp.QuitDoc(swModel.GetTitle());

                //update data cards
                frmStat.SetStat(90, @"Updating model data cards...");
                var oVarBatch = (IEdmBatchUpdate2)oVault.CreateUtility(EdmUtility.EdmUtil_BatchUpdate); //model
                oFile = (IEdmFile15)oVault.GetObject(EdmObjectType.EdmObject_File, oCard.ModelFileId);
                oVarBatch.SetVar(oFile.ID, (int)ClsEnums.EnumPDMVars.PartNumber, oCard.PartNum, string.Empty,
                    (int)EdmBatchFlags.EdmBatch_AllConfigs);
                oVarBatch.SetVar(oFile.ID, (int)ClsEnums.EnumPDMVars.DocumentNumber, oCard.SerialNum, string.Empty,
                    (int)EdmBatchFlags.EdmBatch_AllConfigs);
                oVarBatch.SetVar(oFile.ID, (int)ClsEnums.EnumPDMVars.Number, oCard.CustomerNum, string.Empty,
                    (int)EdmBatchFlags.EdmBatch_AllConfigs);
                oVarBatch.SetVar(oFile.ID, (int)ClsEnums.EnumPDMVars.Description, oCard.Description, string.Empty,
                    (int)EdmBatchFlags.EdmBatch_AllConfigs);
                oVarBatch.SetVar(oFile.ID, (int)ClsEnums.EnumPDMVars.Opp, oCard.OppNum, string.Empty,
                    (int)EdmBatchFlags.EdmBatch_AllConfigs);

                oFile = (IEdmFile15)oVault.GetObject(EdmObjectType.EdmObject_File, oCard.DrawFileId); //drawing
                oVarBatch.SetVar(oFile.ID, (int)ClsEnums.EnumPDMVars.PartNumber, oCard.PartNum, string.Empty,
                    (int)EdmBatchFlags.EdmBatch_AllConfigs);
                oVarBatch.SetVar(oFile.ID, (int)ClsEnums.EnumPDMVars.DocumentNumber, oCard.SerialNum, string.Empty,
                    (int)EdmBatchFlags.EdmBatch_AllConfigs);
                oVarBatch.SetVar(oFile.ID, (int)ClsEnums.EnumPDMVars.Number, oCard.CustomerNum, string.Empty,
                    (int)EdmBatchFlags.EdmBatch_AllConfigs);
                oVarBatch.SetVar(oFile.ID, (int)ClsEnums.EnumPDMVars.Description, oCard.Description, string.Empty,
                    (int)EdmBatchFlags.EdmBatch_AllConfigs);
                oVarBatch.SetVar(oFile.ID, (int)ClsEnums.EnumPDMVars.Opp, oCard.OppNum, string.Empty,
                    (int)EdmBatchFlags.EdmBatch_AllConfigs);

                var iErrCnt = oVarBatch.CommitUpdate(out var aoErrs); //save changes
                for (var iCnt = 0; iCnt <= iErrCnt - 1; iCnt++)
                    frmStat.SetStat(90,
                        @"Error: (" + aoErrs[iCnt].mlErrorCode + @") during Release, section 'update card variables'");

                //check in files
                frmStat.SetStat(95, @"Checking in models...");
                oDest = (IEdmFolder11)oVault.GetFolderFromPath(oCard.CadPath);
                var aoSel = new EdmSelItem[3];
                var oUnlock = (IEdmBatchUnlock2)oVault.CreateUtility(EdmUtility.EdmUtil_BatchUnlock);

                oFile = (IEdmFile15)oVault.GetObject(EdmObjectType.EdmObject_File, oCard.ModelFileId); //model
                aoSel[0].mlDocID = oFile.ID;
                aoSel[0].mlProjID = oDest.ID;

                oFile = (IEdmFile15)oVault.GetObject(EdmObjectType.EdmObject_File, oCard.DrawFileId); //drawing
                aoSel[1].mlDocID = oFile.ID;
                aoSel[1].mlProjID = oDest.ID;

                oUnlock.AddSelection((EdmVault5)oVault, aoSel);
                oUnlock.Comment = @"Checked in via Garmin Product Card Generator";
                oUnlock.CreateTree(0, (int)EdmUnlockBuildTreeFlags.Eubtf_MayUnlock);
                oUnlock.UnlockFiles(0);

                //finish
                Process.Start(oCard.PdfPath);
                frmStat.SetStat(100, @"Operation completed successfully.", true);
            }
            catch (Exception ex)
            {
                Status(@"Error '" + ex.Message + @"' during main procedure");
            }
        }

        private static void c_SetStatus(object sender, SetStatusEventArgs e)
        {
            _frm.SetStat(e.Progress, e.Message);
        }

        private static string PdfConvert(string sFile, int iWidth, int iHeight)
        {
            var csTemp = Path.GetTempPath(); //temp folder
            try
            {
                using (var oPdfView = PdfDocument.Load(sFile))
                {
                    var sOut = Right(sFile, sFile.Length - sFile.LastIndexOf('\\') - 1);
                    sOut = csTemp + sOut.ToLower().Replace(@".pdf", @".png");
                    var oImg = oPdfView.Render(0, iWidth, iHeight, 600, 600, true);
                    oImg.Save(sOut, ImageFormat.Png);
                    return sOut;
                }
            }
            catch (Exception ex)
            {
                Status(@"Error '" + ex.Message + @"' during main procedure");
                return string.Empty;
            }
        }

        private static void Status(string sMsg, int iPrg = 0) //increment progress bar and add status message
        {
            var giPrg = _frm.GetPrg(); //current progress bar value
            giPrg += iPrg;
            giPrg = giPrg > 99 ? 99 : giPrg;
            _frm.SetStat(giPrg, sMsg);
        }

        private static string Right(string sString, int iNumChar) //vba 'Right' function
        {
            try
            {
                sString = sString.Substring(sString.Length - iNumChar, iNumChar);
                return sString;
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    internal class CardProp
    {
        internal CardProp(int type, string opp = "", string upc = "", string desc = "", string cust = "",
            string artF = "", string artR = "")
        {
            string sFld;
            string sSer;
            const string sGarmin = @"C:\_PDMVault\Engineering Data Base\GARMIN INTERNATIONAL, INC\";
            const string sTemp = @" CARD TEMPLATES\";
            const string sCad = @"CAD Files\";
            const string sMisc = @"Misc\";
            // ReSharper disable once StringLiteralTypo
            const string sPdf = @"Drawing PDF's\";
            switch (type)
            {
                case 1: //pcs
                    sFld = @" SMALL PRODUCT CARDS\";
                    sSer = @"80094317";
                    ArtHeight = 820;
                    ArtWidth = 1000;
                    break;
                case 2: //pcm
                    sFld = @"MEDIUM PRODUCT CARDS\";
                    sSer = @"80094429";
                    ArtHeight = 820;
                    ArtWidth = 1000;
                    HasRearArt = true;
                    break;
                case 3: //mgb
                    sFld = @" MAGNETIC GRAPHIC BACKER\";
                    sSer = @"80097583";
                    ArtHeight = 820;
                    ArtWidth = 1000;
                    break;
                case 4: //eds TODO
                    sFld = @"EASEL DISPLAY SIGNS\";
                    sSer = @"";
                    ArtHeight = 820;
                    ArtWidth = 1000;
                    break;
                case 5: //gbp
                    sFld = @" GRAPHIC BACK PANEL FOR TRAY KITS\";
                    sSer = @"80097498";
                    ArtHeight = 820;
                    ArtWidth = 1000;
                    HasRearArt = true;
                    break;
                default:
                    return;
            }

            TempDraw = sGarmin + sTemp + sCad + sSer + @".SLDDRW";
            TempMdl = sGarmin + sTemp + sCad + sSer + @".SLDPRT";
            CadPath = sGarmin + sFld + sCad;
            MiscPath = sGarmin + sFld + sMisc;
            PdfPath = sGarmin + sFld + sPdf;
            OppNum = opp;
            Upc = upc;
            Description = desc;
            CustomerNum = cust;
            FrontArtFile = artF;
            RearArtFile = artR;
        }

        internal string TempDraw { get; }
        internal string CadPath { get; }
        internal string MiscPath { get; }
        internal string PdfPath { get; }
        internal string TempMdl { get; }
        internal string OppNum { get; }
        internal string Upc { get; }
        internal string Description { get; }
        internal string CustomerNum { get; }
        internal string FrontArtFile { get; set; }
        internal string RearArtFile { get; set; }
        internal string SerialNum { get; set; }
        internal string PartNum { get; set; }
        internal bool HasRearArt { get; }
        internal int ModelFileId { get; set; }
        internal int DrawFileId { get; set; }
        internal int ArtHeight { get; }
        internal int ArtWidth { get; }
    }
}