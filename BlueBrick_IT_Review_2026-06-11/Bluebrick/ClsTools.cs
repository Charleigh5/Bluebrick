using EPDM.Interop.epdm;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BlueBrick
{
    internal static class ClsTools
    {
        internal static void CheckInAll(FrmPane frmStat) //checks in all files for the current user
        {
            //login to vault
            frmStat.SetStat(0, @"Logging into vault...", false, true);
            var oVault = (IEdmVault20)new EdmVault5();
            if (!oVault.IsLoggedIn) oVault.LoginAuto(@"_PDMVault", 0);

            //get user info
            var lUser = oVault.GetLoggedInWindowsUserID(@"_PDMVault");
            var oUMgr = (IEdmUserMgr5)oVault.CreateUtility(EdmUtility.EdmUtil_UserMgr);
            var oUser = (IEdmUser6)oUMgr.GetUser(lUser);
            var sUser = oUser.Name;
            frmStat.SetStat(20, @"User '" + sUser + @"' logged in. Searching...");

            //main procedure
            try
            {
                //search
                var oSearch = (IEdmSearch6)oVault.CreateUtility(EdmUtility.EdmUtil_Search);
                oSearch.SetToken(EdmSearchToken.Edmstok_FindFiles, true);
                oSearch.SetToken(EdmSearchToken.Edmstok_FindFolders, false);
                oSearch.SetToken(EdmSearchToken.Edmstok_LockedBy, sUser);
                var oResult = oSearch.GetFirstResult();
                var colFiles = new List<EdmSelItem>();
                while (oResult != null)
                {
                    var oSel = new EdmSelItem
                    {
                        mlDocID = oResult.ID,
                        mlProjID = oResult.ParentFolderID
                    };
                    colFiles.Add(oSel);
                    oResult = oSearch.GetNextResult();
                }

                if (colFiles.Count == 0)
                {
                    frmStat.SetStat(100, @"User has no files checked out.", true);
                    return;
                }

                frmStat.SetStat(50, @"User has " + colFiles.Count + @" files checked out.");

                //loop through and check in
                frmStat.SetStat(60, @"Generating file list...");
                var aoSel = new EdmSelItem[colFiles.Count];
                var oUnlock = (IEdmBatchUnlock2)oVault.CreateUtility(EdmUtility.EdmUtil_BatchUnlock);
                for (var iCnt = 0; iCnt < aoSel.Length; iCnt++)
                    aoSel[iCnt] = new EdmSelItem
                    {
                        mlDocID = colFiles[iCnt].mlDocID,
                        mlProjID = colFiles[iCnt].mlProjID
                    };
                oUnlock.AddSelection((EdmVault5)oVault, ref aoSel);
                oUnlock.Comment = @"Checked in via auto check in procedure";
                oUnlock.CreateTree(0, (int)EdmUnlockBuildTreeFlags.Eubtf_MayUnlock);
                var retVal = oUnlock.ShowDlg(frmStat.Handle.ToInt32());
                if (retVal)
                {
                    frmStat.SetStat(80, @"Checking in files...");
                    oUnlock.UnlockFiles(0);
                }

                frmStat.SetStat(100, @"Operation completed successfully.", true);
            }
            catch (COMException ex)
            {
                frmStat.SetStat(100, @"HRESULT = 0x" + ex.ErrorCode.ToString(@"X") + @" " + ex.Message, true);
            }
            catch (Exception ex)
            {
                frmStat.SetStat(100, ex.Message, true);
            }
        }

        internal static void ToggleSel(FrmPane frmStat, ISldWorks swApp) //toggle select hidden edges through faces
        {
            var bOpt = !swApp.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swEdgesHiddenEdgeSelectionInHLR);
            swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swEdgesHiddenEdgeSelectionInHLR, bOpt);
            swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swEdgesHiddenEdgeSelectionInWireframe, bOpt);
            frmStat.SetStat(100, @"Toggle hidden edges selection set to '" + bOpt + @"'.", true, true);
        }

        internal static void SetLights(FrmPane frmStat, ISldWorks swApp) //reset lights to default settings
        {
            var swPart = (ModelDoc2)swApp.ActiveDoc;
            if (swPart == null)
            {
                frmStat.SetStat(100, @"No open documents.", true, true);
                return;
            }

            //delete all current lights
            frmStat.SetStat(20, @"Removing current light sources...", false, true);
            for (var iCnt = swPart.GetLightSourceCount() - 1; iCnt >= 0; iCnt--) swPart.DeleteLightSource(iCnt);

            //add ambient
            frmStat.SetStat(40, @"Generating ambient light source...");
            swPart.SetLightSourcePropertyValuesVB(@"Ambient", 1, 1, 16777215, 1, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0.2, 0, 0, false);
            swPart.LockLightToModel(0, true);

            //add Direction1
            frmStat.SetStat(60, @"Generating directional light sources...");
            swPart.AddLightSource(@"SW#1", 4, @"Directional1");
            swPart.SetLightSourcePropertyValuesVB(@"SW#1", 4, 0.2, 16777215, 1, 0, 3.09129897044408E-02,
                -4.41483246288366E-02, 0, 0, 0, 0, 0, 0, 0, 0.2, 0.2, 0, false);
            swPart.LockLightToModel(1, true);

            //add Direction2
            swPart.AddLightSource(@"SW#2", 4, @"Directional2");
            swPart.SetLightSourcePropertyValuesVB(@"SW#2", 4, 0.2, 16777215, 1, -3.12175797230755E-02,
                3.09129897044408E-02, 3.12175797230753E-02, 0, 0, 0, 0, 0, 0, 0, 0.2, 0.2, 0, false);
            swPart.LockLightToModel(2, true);

            //add Direction3
            swPart.AddLightSource(@"SW#3", 4, @"Directional3");
            swPart.SetLightSourcePropertyValuesVB(@"SW#3", 4, 0.2, 16777215, 1, 3.12175797230753E-02,
                3.09129897044413E-02, 3.12175797230751E-02, 0, 0, 0, 0, 0, 0, 0, 0.2, 0.2, 0, false);
            swPart.LockLightToModel(3, true);

            //add default scene
            frmStat.SetStat(80, @"Setting default scene...");
            swPart.Extension.InsertScene(@"\scenes\01 basic scenes\00 3 point faded.p2s");

            //clean up and rebuild
            swPart.GraphicsRedraw();
            swPart.ClearSelection2(true);
            swPart.ForceRebuild3(true);
            frmStat.SetStat(100, @"Operation completed.", true);
        }

        //changes many preferences to Vira preferred settings
        internal static void SetStds(FrmPane frmStat, ISldWorks swApp)
        {
            var swModel = (ModelDoc2)swApp.ActiveDoc;
            if (swModel == null)
            {
                frmStat.SetStat(100, @"No open documents.", true, true);
                return;
            }

            //hide all the things
            frmStat.SetStat(20, @"Hiding all the things...", false, true);
            swModel.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swViewDisplayHideAllTypes, false);
            swModel.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDisplayPlanes, false);
            swModel.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDisplayLiveSections, false);
            swModel.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDisplayAxes, false);
            swModel.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDisplayTemporaryAxes, false);
            swModel.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDisplayOrigins, false);
            swModel.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDisplayCoordSystems, false);
            swModel.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDisplayAllAnnotations, false);
            swModel.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDisplayReferencePoints2, false);
            swModel.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDisplayRoutePoints, false);
            swModel.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swGridDisplay, false);
            swModel.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swShowDimensionNames, false);
            swModel.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDisplayLights, false);
            swModel.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDisplayCameras, false);
            switch (swModel.GetType())
            {
                case (int)swDocumentTypes_e.swDocDRAWING:
                    swModel.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDisplayCurves, false);
                    swModel.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDisplaySketches, true);
                    frmStat.SetStat(80, @"Done.");
                    break;
                case (int)swDocumentTypes_e.swDocPART:
                    swModel.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDisplayCurves, true);
                    swModel.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDisplaySketches, true);
                    frmStat.SetStat(80, @"Done. Fixing lights....");
                    SetLights(frmStat, swApp);
                    break;
                case (int)swDocumentTypes_e.swDocASSEMBLY:
                    swModel.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDisplayCurves, false);
                    swModel.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDisplaySketches, false);
                    frmStat.SetStat(80, @"Done. Fixing lights....");
                    SetLights(frmStat, swApp);
                    break;
            }

            frmStat.SetStat(100, @"All operations completed.", true);
        }

        //show all hidden components
        internal static void ShowHidden(FrmPane frmStat, ISldWorks swApp)
        {
            try
            {
                var swModel = (ModelDoc2)swApp.ActiveDoc;
                if (swModel == null)
                {
                    frmStat.SetStat(100, @"No open documents.", true, true);
                    return;
                }

                var top = false;
                if (swModel.GetType() == (int)swDocumentTypes_e.swDocDRAWING)
                {
                    //get model from drawing
                    // ReSharper disable once SuspiciousTypeConversion.Global
                    var swDraw = (DrawingDoc)swModel;
                    var swSheets = (object[])swDraw.GetSheetNames();
                    swDraw.ActivateSheet((string)swSheets[0]);
                    var swView = (SolidWorks.Interop.sldworks.View)swDraw.GetFirstView();
                    swView = (SolidWorks.Interop.sldworks.View)swView.GetNextView();
                    var sName = swView.GetReferencedModelName();
                    var iErr = 0;
                    swModel = (ModelDoc2)swApp.ActivateDoc3(sName, false,
                        (int)swRebuildOnActivation_e.swRebuildActiveDoc, ref iErr);
                    swModel.ShowConfiguration2(swView.ReferencedConfiguration);
                    top = true;
                }

                if (swModel.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
                {
                    if (top) swApp.CloseDoc(swModel.GetTitle());
                    frmStat.SetStat(100, @"Active document is not of type 'Assembly'.", true, true);
                    return;
                }

                // ReSharper disable once SuspiciousTypeConversion.Global
                // ReSharper disable once UseNegatedPatternMatching
                var swAssm = swModel as IAssemblyDoc;
                if (swAssm == null)
                {
                    //at this point it can't be null, but Rider still thinks it can /shrug
                    if (top) swApp.CloseDoc(swModel.GetTitle());
                    frmStat.SetStat(100, @"This error will never happen.", true, true);
                    return;
                }

                if (swModel.IsOpenedReadOnly())
                {
                    if (top) swApp.CloseDoc(swModel.GetTitle());
                    frmStat.SetStat(100, @"Model open read only. No changes made.", true, true);
                }
                var comps = (object[])swAssm.GetComponents(false);
                foreach (var c in comps)
                {
                    var swComp = (IComponent2)c;
                    if (swComp.Visible != (int)swComponentVisibilityState_e.swComponentVisible) 
                        swComp.Visible = (int)swComponentVisibilityState_e.swComponentVisible;
                }
                swModel.Extension.Rebuild((int)swRebuildOptions_e.swForceRebuildAll);
                var e = 0;
                var w = 0;
                swModel.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent + (int)swSaveAsOptions_e.swSaveAsOptions_AvoidRebuildOnSave, ref  e, ref w);
                if (top) swApp.CloseDoc(swModel.GetTitle());
                frmStat.SetStat(100, @"All operations completed.", true);
            }
            catch (Exception ex)
            {
                frmStat.SetStat(100, ex.Message, true);
            }
        }

        internal static void DrwTabRename(FrmPane frmStat, ISldWorks swApp)
        {
            var swModel = (ModelDoc2)swApp.ActiveDoc;
            if (swModel == null)
            {
                frmStat.SetStat(100, @"No open documents.", true, true);
                return;
            }

            if (swModel.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
            {
                frmStat.SetStat(100, @"No open drawing.", true, true);
                return;
            }

            // ReSharper disable once SuspiciousTypeConversion.Global
            var draw = (DrawingDoc)swModel;
            var cnt = draw.GetSheetCount();

            //first pass - temp names
            var sheets = (string[])draw.GetSheetNames();
            for (var i = 0; i < cnt; i++)
            {
                var swSheet = draw.Sheet[sheets[i]];
                swSheet.SetName(@"TempSheet" + i);
            }

            //second pass - actual names
            sheets = (string[])draw.GetSheetNames();
            for (var i = 0; i < cnt; i++)
            {
                var swSheet = draw.Sheet[sheets[i]];
                swSheet.SetName((i+1).ToString());
            }
            frmStat.SetStat(100, @"Operation complete.", true, true);
        }

        #region CopyDwg
        internal static void CopyDwg(FrmPane frmStat, ISldWorks swApp) //copy and save drawing under new number
        {
            try
            {
                //validation
                var swMdl = (ModelDoc2)swApp.ActiveDoc;
                if (swMdl == null)
                {
                    frmStat.SetStat(100, @"No open documents.", true, true);
                    return;
                }

                if (swMdl.GetType() == (int)swDocumentTypes_e.swDocDRAWING)
                {
                    frmStat.SetStat(100, @"Active document must be part/assembly.", true, true);
                    return;
                }

                //copy properties
                var oCad = new ModelInfo
                (
                    swMdl.GetPathName(),
                    swMdl.CustomInfo[@"Number"],
                    swMdl.CustomInfo[@"Document Number"],
                    swMdl.CustomInfo[@"Part Number"],
                    swMdl.CustomInfo[@"Description"],
                    swMdl.CustomInfo[@"Opp"]
                );

                //login to vault
                frmStat.SetStat(0, @"Logging into vault...", false, true);
                var oVault = (IEdmVault20)new EdmVault5();
                if (!oVault.IsLoggedIn) oVault.LoginAuto(@"_PDMVault", 0);

                //get drawing file to copy
                string fileName;
                var ofd = new OpenFileDialog();
                ofd.Multiselect = false;
                ofd.InitialDirectory = oCad.CadPath;
                ofd.Filter = @"Drawing files (*.slddrw)|*.slddrw";
                ofd.FilterIndex = 1;
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    fileName = ofd.FileName;
                }
                else
                {
                    frmStat.SetStat(100, @"No drawing file selected.", true);
                    return;
                }

                //copy files
                frmStat.SetStat(25, @"Copying over drawing...");
                var oFolder = (IEdmFolder11)oVault.GetFolderFromPath(oCad.CadPath);
                var iFile = oFolder.AddFile2(0, fileName, out _, oCad.DwgFile);

                //update drawing references to new model
                var oRawRef = (IEdmRawReferenceMgr)oVault.CreateUtility(EdmUtility.EdmUtil_RawReferenceMgr);
                if (!oRawRef.Open(oCad.CadPath + oCad.DwgFile))
                {
                    frmStat.SetStat(100, @"Error updating file references!", true);
                    return;
                }

                EdmRawReference[] aRefs = null;
                oRawRef.GetReferences(ref aRefs);
                aRefs[0].mbsIncludePath = oCad.CadPath + oCad.CadFile;
                aRefs[0].mbsRefName = oCad.CadFile;
                oRawRef.UpdateReferences(aRefs);
                oRawRef.Close();

                //get new file info and open
                frmStat.SetStat(35, @"Opening new file...");
                var oFile = (IEdmFile15)oVault.GetObject(EdmObjectType.EdmObject_File, iFile);
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

                //add properties to drawing
                frmStat.SetStat(70, @"Adding properties to drawing...");
                var swExt = swModel.Extension;
                var swMgr = swExt.CustomPropertyManager[string.Empty];
                swMgr.Add3(@"Description", (int)swCustomInfoType_e.swCustomInfoText, oCad.Description,
                    (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                swMgr.Add3(@"Opp", (int)swCustomInfoType_e.swCustomInfoText, oCad.OppNum,
                    (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                swMgr.Add3(@"Part Number", (int)swCustomInfoType_e.swCustomInfoText, oCad.PartNum,
                    (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                swMgr.Add3(@"Number", (int)swCustomInfoType_e.swCustomInfoText, oCad.CustNum,
                    (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                swMgr.Add3(@"Document Number", (int)swCustomInfoType_e.swCustomInfoText, oCad.DocNum,
                    (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);

                //save drawing
                frmStat.SetStat(75, @"Save drawing...");
                var iErr = 0;
                var iWarn = 0;
                swExt.Rebuild((int)swRebuildOptions_e.swForceRebuildAll);
                var bSave = swModel.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref iErr, ref iWarn);
                if (!bSave)
                {
                    if (iErr == 2) frmStat.SetStat(75, @"Drawing is read only!");
                    frmStat.SetStat(75, @"Error: " + iErr);
                }

                frmStat.SetStat(100, @"Operation complete!", true);
            }
            catch (Exception ex)
            {
                frmStat.SetStat(100, @"Error: " + ex.HResult + @" - " + ex.Message + @" occured during CopyDwg", true);
            }
        }

        private struct ModelInfo
        {
            internal ModelInfo(string file, string doc, string part, string cust, string description, string opp)
            {
                var len = file.LastIndexOf('\\') + 1;
                CadPath = file.Substring(0, len);
                CadFile = file.Substring(len, file.Length - len);
                DwgFile = CadFile.Substring(0, CadFile.LastIndexOf('.')) + @".SLDDRW";
                DocNum = doc;
                PartNum = part;
                CustNum = cust;
                Description = description;
                OppNum = opp;
            }
            internal string CadPath { get; }
            internal string CadFile { get; }
            internal string DwgFile { get; }
            internal string DocNum { get;}
            internal string PartNum { get;}
            internal string CustNum { get;}
            internal string OppNum { get;}
            internal string Description { get; }
        }
        
        #endregion CopyDwg
        
        #region ClrAppear

        internal static void ClrAppear(FrmPane frmStat, ISldWorks swApp) //removes all appearances from model
        {
            var swModel = (ModelDoc2)swApp.ActiveDoc;
            if (swModel == null)
            {
                frmStat.SetStat(100, @"No open documents.", true, true);
                return;
            }

            if (swModel.GetType() != (int)swDocumentTypes_e.swDocPART)
            {
                frmStat.SetStat(100, @"Can only be used on parts.", true, true);
                return;
            }

            // ReSharper disable once SuspiciousTypeConversion.Global
            var swPart = (PartDoc)swModel;
            const swInConfigurationOpts_e oCfg = swInConfigurationOpts_e.swAllConfiguration;
            try
            {
                frmStat.SetStat(0, @"Removing appearances from bodies/faces...", false, true);
                SubBodies((object[])swPart.GetBodies2((int)swBodyType_e.swAllBodies, false), true, oCfg);
                frmStat.SetStat(60, @"Removing appearances from features...");
                SubFeats((object[])swModel.FeatureManager.GetFeatures(false), oCfg);
                frmStat.SetStat(80, @"Re-applying part material...");
                var sMat = swPart.GetMaterialPropertyName2(string.Empty, out var sDb);
                swModel.Extension.RemoveMaterialProperty((int)oCfg, null);
                swPart.SetMaterialPropertyName2(string.Empty, string.Empty, string.Empty);
                swPart.SetMaterialPropertyName2(string.Empty, sDb, sMat);
                swModel.ForceRebuild3(true);
                frmStat.SetStat(100, @"Operation completed successfully.", true);
            }
            catch (COMException ex)
            {
                frmStat.SetStat(100, @"HRESULT = 0x" + ex.ErrorCode.ToString("X") + @" " + ex.Message, true);
            }
            catch (Exception ex)
            {
                frmStat.SetStat(100, ex.Message, true);
            }
        }

        //removes appearances from given body
        private static void SubBodies(object[] vBodies, bool bFace, swInConfigurationOpts_e oCfg)
        {
            if (vBodies == null) return;
            foreach (Body2 swBody in vBodies)
            {
                swBody.RemoveMaterialProperty((int)oCfg, null);
                if (!bFace) continue;
                var vFaces = (object[])swBody.GetFaces();
                SubFaces(vFaces, oCfg);
            }
        }

        //removes appearances from given feature
        private static void SubFeats(object[] vFeatures, swInConfigurationOpts_e oCfg)
        {
            if (vFeatures == null) return;
            foreach (Feature swFeat in vFeatures)
                swFeat.RemoveMaterialProperty2((int)oCfg, null);
        }

        //removes appearances from given face
        private static void SubFaces(object[] vFaces, swInConfigurationOpts_e oCfg)
        {
            if (vFaces == null) return;
            foreach (Face2 swFace in vFaces)
                swFace.RemoveMaterialProperty2((int)oCfg, null);
        }

        #endregion ClrAppear

        #region InsFinSched

        internal static void InsertFinSchedule(FrmPane frmStat, ISldWorks swApp)
        {
            //verify active drawing doc
            var swModel = (IModelDoc2)swApp.ActiveDoc;
            if (swModel == null)
            {
                frmStat.SetStat(100, @"No open documents.", true, true);
                return;
            }

            if (swModel.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
            {
                frmStat.SetStat(100, @"This feature can only be used on drawings.", true, true);
                return;
            }

            // ReSharper disable once SuspiciousTypeConversion.Global
            var swDraw = (DrawingDoc)swModel;

            //ask how many lines
            var iLines = 0;
            if (ShowInputDialog(ref iLines) != DialogResult.OK)
            {
                frmStat.SetStat(100, @"Operation aborted.", true, true);
                return;
            }

            //add table
            const string
                csTable = @"C:\_PDMVault\Templates\VI Templates\Tables\FINISH SCHEDULE.sldtbt"; //table template
            var oTable = swDraw.InsertTableAnnotation2(true, 0, 0, 1, csTable, 2, 6);
            if (oTable != null)
            {
                oTable.BorderLineWeight = 1;
                oTable.GridLineWeight = 1;
                for (var i = 1; i < iLines; i++)
                    oTable.InsertRow((int)swTableItemInsertPosition_e.swTableItemInsertPosition_Last, oTable.RowCount);
                var oTxtFrm = oTable.GetTextFormat();
                oTxtFrm.CharHeightInPts = 10;
                oTxtFrm.TypeFaceName = @"Arial";
                oTable.SetTextFormat(false, oTxtFrm);
            }

            frmStat.SetStat(100, @"Operation completed.", true, true);
        }

        private static DialogResult ShowInputDialog(ref int input)
        {
            try
            {
                var size = new Size(200, 70);
                var inputBox = new Form
                {
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    ClientSize = size,
                    Text = @"Lines"
                };

                var textBox = new TextBox
                {
                    Size = new Size(size.Width - 10, 23),
                    Location = new Point(5, 5),
                    Text = input.ToString()
                };
                inputBox.Controls.Add(textBox);

                var okButton = new Button
                {
                    DialogResult = DialogResult.OK,
                    Name = @"okButton",
                    Size = new Size(75, 23),
                    Text = @"&OK",
                    Location = new Point(size.Width - 80 - 80, 39)
                };
                inputBox.Controls.Add(okButton);
                inputBox.AcceptButton = okButton;

                var cancelButton = new Button
                {
                    DialogResult = DialogResult.Cancel,
                    Name = @"cancelButton",
                    Size = new Size(75, 23),
                    Text = @"&Cancel",
                    Location = new Point(size.Width - 80, 39)
                };
                inputBox.Controls.Add(cancelButton);
                inputBox.CancelButton = cancelButton;

                var result = inputBox.ShowDialog();
                input = int.Parse(textBox.Text, NumberStyles.None);
                return result;
            }
            catch
            {
                return DialogResult.Cancel;
            }
        }

        #endregion InsFinSched

        #region PullSerial

        internal static void GetSerial(FrmPane frmStat, string sSerial = "")
        {
            //verify properties loaded
            if (frmStat.Prop.Model == null) return;

            //setup and login to vault and create serial number generator
            var oVault = (IEdmVault20)new EdmVault5();
            if (!oVault.IsLoggedIn) oVault.LoginAuto(@"_PDMVault", 0);
            var serialNums = (IEdmSerNoGen7)oVault.CreateUtility(EdmUtility.EdmUtil_SerNoGen);

            //get serial series if none specified
            if (sSerial.Length == 0)
            {
                serialNums.GetSerialNumberNames(out var names);
                if (SelectSerial(ref sSerial, names) != DialogResult.OK)
                {
                    frmStat.SetStat(100, @"Operation aborted.", true, true);
                    return;
                }

                if (sSerial.Length == 0)
                {
                    frmStat.SetStat(100, @"Operation aborted.", true, true);
                    return;
                }
            }

            //get new serial number
            frmStat.SetStat(20, @"Getting serial number...");
            var oSer = serialNums.AllocSerNoValue(sSerial);
            sSerial = oSer.Value;

            //current config if not drawing
            if (frmStat.Prop.Model.GetType() == (int)swDocumentTypes_e.swDocDRAWING)
            {
                var sMsg = @"Pulling a new number from the drawing is not recommended. ";
                sMsg += @"If the model is not also updated, the part number will likely ";
                sMsg += @"be overwritten by the model part number at some point.";
                MessageBox.Show(sMsg, @"Warning!", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            //finish up
            frmStat.SetStat(100, @"Operation completed.", true, true);
            frmStat.SetSerial(sSerial);
        }

        private static DialogResult SelectSerial(ref string input, IEnumerable<string> names)
        {
            try
            {
                //create form
                var size = new Size(300, 70);
                var inputBox = new Form
                {
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    ClientSize = size,
                    Text = @"Select serial number"
                };

                //setup controls
                var cmbSerials = new ComboBox
                {
                    AutoCompleteMode = AutoCompleteMode.Suggest,
                    AutoCompleteSource = AutoCompleteSource.ListItems,
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    FormattingEnabled = true,
                    Location = new Point(5, 5),
                    Name = @"cmbSerials",
                    Size = new Size(267, 23),
                    Sorted = true,
                    TabIndex = 0
                };
                inputBox.Controls.Add(cmbSerials);

                var okButton = new Button
                {
                    DialogResult = DialogResult.OK,
                    Name = @"okButton",
                    Size = new Size(75, 23),
                    Text = @"&OK",
                    Location = new Point(size.Width - 80 - 80, 39)
                };
                inputBox.Controls.Add(okButton);
                inputBox.AcceptButton = okButton;

                var cancelButton = new Button
                {
                    DialogResult = DialogResult.Cancel,
                    Name = @"cancelButton",
                    Size = new Size(75, 23),
                    Text = @"&Cancel",
                    Location = new Point(size.Width - 80, 39)
                };
                inputBox.Controls.Add(cancelButton);
                inputBox.CancelButton = cancelButton;

                //add serial options
                foreach (var s in names) cmbSerials.Items.Add(s);
                if (cmbSerials.Items.Count > 0) cmbSerials.Text = (string)cmbSerials.Items[0];

                //return result
                var result = inputBox.ShowDialog();
                input = cmbSerials.Text;
                return result;
            }
            catch
            {
                return DialogResult.Cancel;
            }
        }

        #endregion PullSerial

    }
}