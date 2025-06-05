using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using EPDM.Interop.epdm;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace BlueBrick
{
    internal static class ClsLucky
    {
        internal static void FeelingLucky(FrmPane frmStat, string sSearch) //main method
        {
            try
            {
                //set parameters
                if (sSearch.Trim().Length == 0)
                {
                    frmStat.SetStat(iStat: 100, sStat: @"No search string specified.", bDone: true, bStart: true);
                    return;
                }

                sSearch = sSearch.Trim();

                //login to vault
                frmStat.SetStat(iStat: 0, sStat: @"Logging into vault...", bDone: false, bStart: true);
                var oVault = (IEdmVault20)new EdmVault5();
                if (!oVault.IsLoggedIn) oVault.LoginAuto(bsVaultName: @"_PDMVault", hParentWnd: 0);

                //search
                frmStat.SetStat(iStat: 20, sStat: @"Searching...");
                var oSearch = (IEdmSearch8)oVault.CreateUtility(eType: EdmUtility.EdmUtil_Search);
                oSearch.SetToken(eTok: EdmSearchToken.Edmstok_FindFiles, oValue: true);
                oSearch.SetToken(eTok: EdmSearchToken.Edmstok_FindFolders, oValue: false);
                oSearch.SetToken(eTok: EdmSearchToken.Edmstok_AllVersions, oValue: false);
                oSearch.FileName = @"%.SLD%";
                oSearch.BeginOR();
                oSearch.AddVariable2(poIdOrName: (int)ClsEnums.EnumPDMVars.Description, poValue: sSearch,
                    lEdmVarOp: (int)EdmVarOp.EdmVarOp_StringContains);
                oSearch.AddVariable2(poIdOrName: (int)ClsEnums.EnumPDMVars.Number, poValue: sSearch, lEdmVarOp: (int)EdmVarOp.EdmVarOp_StringContains);
                oSearch.AddVariable2(poIdOrName: (int)ClsEnums.EnumPDMVars.DocumentNumber, poValue: sSearch,
                    lEdmVarOp: (int)EdmVarOp.EdmVarOp_StringContains);
                oSearch.AddVariable2(poIdOrName: (int)ClsEnums.EnumPDMVars.PartNumber, poValue: sSearch,
                    lEdmVarOp: (int)EdmVarOp.EdmVarOp_StringContains);
                oSearch.AddVariable2(poIdOrName: (int)ClsEnums.EnumPDMVars.FileName, poValue: sSearch,
                    lEdmVarOp: (int)EdmVarOp.EdmVarOp_StringContains);
                oSearch.EndOR();
                var oResult = oSearch.GetFirstResult();

                //parse results and build collection
                var iCnt = 0;
                var colSearch = new List<EdmSelItem2>();
                while (oResult != null && iCnt < 12)
                {
                    var oSel = new EdmSelItem2
                    {
                        mlID = oResult.ID,
                        mlParentID = oResult.ParentFolderID,
                        meType = oResult.ObjectType,
                        mlVersion = oResult.Version
                    };
                    // ReSharper disable once UsageOfDefaultStructEquality
                    if (colSearch.IndexOf(oSel) == -1)
                    {
                        colSearch.Add(oSel);
                        iCnt++;
                    }

                    oResult = oSearch.GetNextResult();
                }

                if (iCnt == 0)
                {
                    frmStat.SetStat(iStat: 100, sStat: @"No files were found.", bDone: true);
                    return;
                }

                frmStat.SetStat(iStat: 60, sStat: iCnt + @" results returned. Adding to list...");

                //process results and add to list
                var lviResults = new ListViewItem[iCnt];
                iCnt = 0;
                foreach (var oSel in colSearch)
                {
                    var oFile = (IEdmFile18)oVault.GetObject(eType: EdmObjectType.EdmObject_File, lObjectID: oSel.mlID);
                    var oFolder = (IEdmFolder5)oVault.GetObject(eType: EdmObjectType.EdmObject_Folder, lObjectID: oSel.mlParentID);
                    var lstFile = new ListViewItem(text: oSel.mlID.ToString(), imageIndex: iCnt);
                    lstFile.SubItems.Add(text: oFile.Name);
                    lstFile.SubItems.Add(text: oFolder.LocalPath);

                    //get thumbnail
                    try
                    {
                        var oThumbPtr = (IntPtr)oFile.GetThumbnail3(lVersion: oFile.CurrentVersion);
                        var oThumb = Image.FromHbitmap(hbitmap: oThumbPtr);
                        frmStat.imlThumbs.Images.Add(value: oThumb);
                    }
                    catch
                    {
                        frmStat.imlThumbs.Images.Add(value: Resource1.thumb);
                    }

                    //add listview item to array and move to next
                    lviResults[iCnt] = lstFile;
                    iCnt++;
                }

                //finish and bail
                frmStat.lstLkyResults.Items.AddRange(items: lviResults);
                frmStat.SetStat(iStat: 100, sStat: @"Operation completed successfully.", bDone: true);
            }
            catch (COMException ex)
            {
                frmStat.SetStat(iStat: 100, sStat: @"HRESULT = 0x" + ex.ErrorCode.ToString(format: @"X") + @" " + ex.Message, bDone: true);
            }
            catch (Exception ex)
            {
                frmStat.SetStat(iStat: 100, sStat: ex.Message, bDone: true);
            }
        }

        //launches a file from PDM into current solidworks
        internal static void PdmFile(FrmPane frmStat, ISldWorks swApp, int iFileId, string sFile, bool bIns = false)
        {
            try
            {
                //login to vault
                var oVault = (IEdmVault20)new EdmVault5();
                if (!oVault.IsLoggedIn) oVault.LoginAuto(@"_PDMVault", 0);

                //determine file type
                frmStat.SetStat(0, @"Getting file type...", false, true);
                int iType;
                var sExt = sFile;
                var iSIdx = sExt.LastIndexOf('.');
                var iLen = sExt.Length - iSIdx - 1;
                sExt = sExt.Substring(iSIdx + 1, iLen).ToUpper();
                switch (sExt)
                {
                    case @"SLDASM":
                        iType = (int)swDocumentTypes_e.swDocASSEMBLY;
                        break;
                    case @"SLDDRW":
                        iType = (int)swDocumentTypes_e.swDocDRAWING;
                        if (bIns)
                        {
                            frmStat.SetStat(100, @"Cannot insert drawing.", true);
                            return;
                        }

                        break;
                    case @"SLDPRT":
                        iType = (int)swDocumentTypes_e.swDocPART;
                        break;
                    default: //not a solidworks file
                        frmStat.SetStat(100, @"Not a SolidWorks file, aborting...", true);
                        return;
                }

                //launch file
                var oFile = (IEdmFile5)oVault.GetObject(EdmObjectType.EdmObject_File, iFileId);
                try
                {
                    oFile.GetFileCopy(0, 0);
                }
                catch (Exception ex)
                {
                    if (ex.HResult == -2147220952) frmStat.SetStat(0, @"Failed to get new file copy from PDM.");
                }
                const int iOpt = (int)swOpenDocOptions_e.swOpenDocOptions_Silent;
                var iErr = 0;
                var iWarn = 0;
                if (bIns) //drag file
                {
                    var swMdl = (ModelDoc2)swApp.ActiveDoc;
                    if (swMdl != null)
                        if (swMdl.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY)
                        {
                            // ReSharper disable once SuspiciousTypeConversion.Global
                            var swAssm = (AssemblyDoc)swMdl;
                            swApp.DocumentVisible(false, (int)swDocumentTypes_e.swDocPART);
                            swApp.OpenDoc6(sFile, iType, iOpt, string.Empty, ref iErr, ref iWarn);
                            swApp.DocumentVisible(true, (int)swDocumentTypes_e.swDocPART);
                            var swComp = swAssm.AddComponent5(sFile, 0, string.Empty, false, string.Empty, 0.1, 0, 0);
                            swApp.CloseDoc(sFile);
                            var swSelMgr = (SelectionMgr)swMdl.SelectionManager;
                            var swSelData = swSelMgr.CreateSelectData();
                            swComp.Select4(false, swSelData, false);
                            swAssm.TranslateComponent();
                        }
                }
                else
                {
                    swApp.OpenDoc6(sFile, iType, iOpt, string.Empty, ref iErr, ref iWarn);
                }

                frmStat.SetStat(100, @"Operation completed.", true);
            }
            catch (COMException ex)
            {
                frmStat.SetStat(100, @"HRESULT = 0x" + ex.ErrorCode.ToString(@"X") + " " + ex.Message, true);
            }
            catch (Exception ex)
            {
                frmStat.SetStat(100, ex.Message, true);
            }
        }
    }
}