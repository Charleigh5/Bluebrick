using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swpublished;

// ReSharper disable LocalizableElement

namespace BlueBrick
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    // ReSharper disable once ClassNeverInstantiated.Global
    public class TaskPane
    {
        [ComVisible(true)]
        [Guid("C56E0AFF-0BD3-4364-90CB-1A581046CD7D")]
        [DisplayName("BlueBrick")]
        [Description("ViraInsight SOLIDWORKS add-in")]
        public class MainAddIn : ISwAddin
        {
            private ISldWorks swApp;
            private FrmMain TaskPanWinFormControl;

            public bool ConnectToSW(object ThisSW, int Cookie)
            {
                swApp = ThisSW as ISldWorks;

                //setup task view icons
                var bitmap = new string[6];
                var sTemp = Path.GetTempPath();
                var imgFmt = ImageFormat.Png;
                var sPath = sTemp + "ViraInsight_icon20.png";
                Resource1.ViraInsight_icon20.Save(sPath, imgFmt);
                bitmap[0] = sPath;
                sPath = sTemp + "ViraInsight_icon32.png";
                Resource1.ViraInsight_icon20.Save(sPath, imgFmt);
                bitmap[1] = sPath;
                sPath = sTemp + "ViraInsight_icon40.png";
                Resource1.ViraInsight_icon20.Save(sPath, imgFmt);
                bitmap[2] = sPath;
                sPath = sTemp + "ViraInsight_icon64.png";
                Resource1.ViraInsight_icon20.Save(sPath, imgFmt);
                bitmap[3] = sPath;
                sPath = sTemp + "ViraInsight_icon96.png";
                Resource1.ViraInsight_icon20.Save(sPath, imgFmt);
                bitmap[4] = sPath;
                sPath = sTemp + "ViraInsight_icon128.png";
                Resource1.ViraInsight_icon20.Save(sPath, imgFmt);
                bitmap[5] = sPath;

                //create Task pane View
                const string toolTip = "ViraInsight BlueBrick";
                if (swApp != null)
                {
                    var ctrl = swApp.CreateTaskpaneView3(bitmap, toolTip);
                    TaskPanWinFormControl = new FrmMain(swApp);
                    ctrl.DisplayWindowFromHandle(TaskPanWinFormControl.Handle.ToInt32());
                }

                //setup command manager
                if (swApp != null) cMgr = swApp.GetCommandManager(Cookie);
                //if (!AddCommands()) { MessageBox.Show("Something went wrong initializing toolbar commands."); }
                return true;
            }

            public bool DisconnectFromSW()
            {
                //RemCommands();
                Marshal.ReleaseComObject(cMgr);
                cMgr = null;
                Marshal.ReleaseComObject(swApp);
                swApp = null;

                //the add-in must call GC.Collect() here in order to retrieve all managed code pointers 
                GC.Collect();
                GC.WaitForPendingFinalizers();
                return true;
            }

            private bool AddCommands() //add custom toolbars and buttons
            {
                try
                {
                    #region Images

                    //get proper sizes
                    //20, 32, 40, 64, 96, 128 (.bmp or .png) Use a 256-color palette.
                    swApp.GetImageSize(out var iImgSm, out var iImgMed, out var iImgLrg);
                    var mainIcons = new string[3];
                    var icons = new string[3];
                    var sTemp = Path.GetTempPath();
                    var imgFmt = ImageFormat.Png;
                    string sPath;

                    //load paths to correct sizes - small
                    switch (iImgSm)
                    {
                        case 20:
                            sPath = sTemp + "iconStrip_20.png";
                            Resource1.iconStrip_20.Save(sPath, imgFmt);
                            icons[0] = sPath;
                            mainIcons[0] = sPath;
                            break;
                        case 32:
                            sPath = sTemp + "iconStrip_32.png";
                            Resource1.iconStrip_32.Save(sPath, imgFmt);
                            icons[0] = sPath;
                            mainIcons[0] = sPath;
                            break;
                        case 40:
                            sPath = sTemp + "iconStrip_40.png";
                            Resource1.iconStrip_40.Save(sPath, imgFmt);
                            icons[0] = sPath;
                            mainIcons[0] = sPath;
                            break;
                        case 64:
                            sPath = sTemp + "iconStrip_64.png";
                            Resource1.iconStrip_64.Save(sPath, imgFmt);
                            icons[0] = sPath;
                            mainIcons[0] = sPath;
                            break;
                        case 96:
                            sPath = sTemp + "iconStrip_96.png";
                            Resource1.iconStrip_96.Save(sPath, imgFmt);
                            icons[0] = sPath;
                            mainIcons[0] = sPath;
                            break;
                        case 128:
                            sPath = sTemp + "iconStrip_128.png";
                            Resource1.iconStrip_128.Save(sPath, imgFmt);
                            icons[0] = sPath;
                            mainIcons[0] = sPath;
                            break;
                    }

                    //load paths to correct sizes - medium
                    switch (iImgMed)
                    {
                        case 20:
                            sPath = sTemp + "iconStrip_20.png";
                            Resource1.iconStrip_20.Save(sPath, imgFmt);
                            icons[1] = sPath;
                            mainIcons[1] = sPath;
                            break;
                        case 32:
                            sPath = sTemp + "iconStrip_32.png";
                            Resource1.iconStrip_32.Save(sPath, imgFmt);
                            icons[1] = sPath;
                            mainIcons[1] = sPath;
                            break;
                        case 40:
                            sPath = sTemp + "iconStrip_40.png";
                            Resource1.iconStrip_40.Save(sPath, imgFmt);
                            icons[1] = sPath;
                            mainIcons[1] = sPath;
                            break;
                        case 64:
                            sPath = sTemp + "iconStrip_64.png";
                            Resource1.iconStrip_64.Save(sPath, imgFmt);
                            icons[1] = sPath;
                            mainIcons[1] = sPath;
                            break;
                        case 96:
                            sPath = sTemp + "iconStrip_96.png";
                            Resource1.iconStrip_96.Save(sPath, imgFmt);
                            icons[1] = sPath;
                            mainIcons[1] = sPath;
                            break;
                        case 128:
                            sPath = sTemp + "iconStrip_128.png";
                            Resource1.iconStrip_128.Save(sPath, imgFmt);
                            icons[1] = sPath;
                            mainIcons[1] = sPath;
                            break;
                    }

                    //load paths to correct sizes - large
                    switch (iImgLrg)
                    {
                        case 20:
                            sPath = sTemp + "iconStrip_20.png";
                            Resource1.iconStrip_20.Save(sPath, imgFmt);
                            icons[2] = sPath;
                            mainIcons[2] = sPath;
                            break;
                        case 32:
                            sPath = sTemp + "iconStrip_32.png";
                            Resource1.iconStrip_32.Save(sPath, imgFmt);
                            icons[2] = sPath;
                            mainIcons[2] = sPath;
                            break;
                        case 40:
                            sPath = sTemp + "iconStrip_40.png";
                            Resource1.iconStrip_40.Save(sPath, imgFmt);
                            icons[2] = sPath;
                            mainIcons[2] = sPath;
                            break;
                        case 64:
                            sPath = sTemp + "iconStrip_64.png";
                            Resource1.iconStrip_64.Save(sPath, imgFmt);
                            icons[2] = sPath;
                            mainIcons[2] = sPath;
                            break;
                        case 96:
                            sPath = sTemp + "iconStrip_96.png";
                            Resource1.iconStrip_96.Save(sPath, imgFmt);
                            icons[2] = sPath;
                            mainIcons[2] = sPath;
                            break;
                        case 128:
                            sPath = sTemp + "iconStrip_128.png";
                            Resource1.iconStrip_128.Save(sPath, imgFmt);
                            icons[2] = sPath;
                            mainIcons[2] = sPath;
                            break;
                    }

                    #endregion Images

                    #region ToolsGroup

                    //validate
                    var bIgn = false;
                    var knownIDs = new[] { iCmdID1, iCmdID2, iCmdID3, iCmdID4, iCmdID5, iCmdID6, iCmdID7, iCmdID8 };
                    var bID = cMgr.GetGroupDataFromRegistry(iGrpTlsID, out var oIDs);
                    if (bID)
                        if (!CompareIDs((int[])oIDs, knownIDs))
                            bIgn = true;

                    //setup command group
                    var iErr = 0;
                    ICommandGroup cGrpTls = cMgr.CreateCommandGroup2(iGrpTlsID, "BlueBrick", "BlueBrick Tools",
                        "BlueBrick Tools", 0, bIgn, ref iErr);
                    if (iErr != 1) MessageBox.Show(iErr.ToString());
                    cGrpTls.IconList = icons;
                    cGrpTls.MainIconList = mainIcons;

                    //add items
                    const int iOpt = 3;
                    cGrpTls.AddCommandItem2("Check In", -1, "Check In", "Tooltip", 0, "barCheckIn", "", iCmdID1, iOpt);
                    cGrpTls.AddCommandItem2("Pull Serial", -1, "Pull Serial", "Tooltip", 1, "barPullSerial", "",
                        iCmdID2, iOpt);
                    cGrpTls.AddCommandItem2("Finish Schedule", -1, "Finish Schedule", "Tooltip", 2, "barFinSchedule",
                        "", iCmdID3, iOpt);
                    cGrpTls.AddCommandItem2("Toggle Through", -1, "Toggle Through", "Tooltip", 3, "barTogThru", "",
                        iCmdID4, iOpt);
                    cGrpTls.AddCommandItem2("Set Lights", -1, "Set Lights", "Tooltip", 4, "barSetLights", "", iCmdID5,
                        iOpt);
                    cGrpTls.AddCommandItem2("Base Appearance", -1, "Base Appearance", "Tooltip", 5, "barBaseAppear", "",
                        iCmdID6, iOpt);
                    cGrpTls.AddCommandItem2("Fix Crap", -1, "Fix Crap", "Tooltip", 6, "barFixCrap", "", iCmdID7, iOpt);
                    cGrpTls.AddCommandItem2("Garmin Cards", -1, "Garmin Cards", "Tooltip", 7, "barGarminCrd", "",
                        iCmdID8, iOpt);

                    //initialize
                    cGrpTls.HasMenu = false;
                    cGrpTls.HasToolbar = false;
                    cGrpTls.Activate();

                    #endregion ToolsGroup

                    return true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                    return false;
                }
            }

            private void RemCommands()
            {
                cMgr.RemoveCommandGroup(iGrpTlsID);
            }

            public bool CompareIDs(int[] aiStore, int[] aiIDs) //from SW api examples
            {
                var lstStore = new List<int>(aiStore);
                var lstIDs = new List<int>(aiIDs);
                lstIDs.Sort();
                lstStore.Sort();
                if (lstIDs.Count != lstStore.Count)
                    return false;
                for (var i = 0; i < lstIDs.Count; i++)
                    if (lstIDs[i] != lstStore[i])
                        return false;
                return true;
            }

            #region Registration

            // ReSharper disable once IdentifierTypo
            private const string ADDIN_KEY_TEMPLATE = @"SOFTWARE\SolidWorks\Addins\{{{0}}}";

            // ReSharper disable once IdentifierTypo
            private const string ADDIN_STARTUP_KEY_TEMPLATE = @"Software\SolidWorks\AddInsStartup\{{{0}}}";

            //command manager - these IDs need to be static (read/write from Registry)
            public const int iGrpTlsID = 1;
            public const int iCmdID1 = 13;
            public const int iCmdID2 = 14;
            public const int iCmdID3 = 15;
            public const int iCmdID4 = 16;
            public const int iCmdID5 = 17;
            public const int iCmdID6 = 18;
            public const int iCmdID7 = 19;
            public const int iCmdID8 = 20;
            private ICommandManager cMgr;

            [ComRegisterFunction]
            public static void RegisterFunction(Type t)
            {
                try
                {
                    //var addInTitle = "";
                    var dspNameAtt = t.GetCustomAttributes(false).OfType<DisplayNameAttribute>().FirstOrDefault();

                    var sTitle = dspNameAtt != null ? dspNameAtt.DisplayName : t.ToString();

                    var descAtt = t.GetCustomAttributes(false).OfType<DescriptionAttribute>().FirstOrDefault();
                    var sDesc = descAtt != null ? descAtt.Description : t.ToString();

                    var key = Registry.LocalMachine.CreateSubKey(string.Format(ADDIN_KEY_TEMPLATE, t.GUID));
                    if (key != null)
                    {
                        key.SetValue(null, 0);
                        key.SetValue("Title", sTitle);
                        key.SetValue("Description", sDesc);

                        var oAssm = Assembly.GetExecutingAssembly();
                        var sPath = Path.GetDirectoryName(oAssm.Location);
                        if (sPath != null)
                        {
                            sPath = Path.Combine(sPath, "BlueBrick.png");
                            Resource1.BlueBrick.Save(sPath, ImageFormat.Png);
                            key.SetValue("Icon Path", sPath);
                        }
                    }

                    var keyStart = Registry.CurrentUser.CreateSubKey(string.Format(ADDIN_STARTUP_KEY_TEMPLATE, t.GUID));
                    keyStart?.SetValue(null, Convert.ToInt32(true), RegistryValueKind.DWord);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error while registering the add-in: " + ex.Message);
                }
            }

            [ComUnregisterFunction]
            public static void UnregisterFunction(Type t)
            {
                try
                {
                    Registry.LocalMachine.DeleteSubKey(
                        string.Format(ADDIN_KEY_TEMPLATE, t.GUID));

                    Registry.CurrentUser.DeleteSubKey(
                        string.Format(ADDIN_STARTUP_KEY_TEMPLATE, t.GUID));
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error while unregistering the add-in: " + e.Message);
                }
            }

            #endregion

            #region UICallback

            //this area is used for functions called by the commands - they will show as unused

            private void barCheckIn()
            {
                ClsTools.CheckInAll(TaskPanWinFormControl);
            }

            private void barPullSerial()
            {
                ClsTools.GetSerial(TaskPanWinFormControl, swApp);
            }

            private void barFinSchedule()
            {
                ClsTools.InsertFinSchedule(TaskPanWinFormControl, swApp);
            }

            private void barTogThru()
            {
                ClsTools.ToggleSel(TaskPanWinFormControl, swApp);
            }

            private void barSetLights()
            {
                ClsTools.SetLights(TaskPanWinFormControl, swApp);
            }

            private void barBaseAppear()
            {
                ClsTools.ClrAppear(TaskPanWinFormControl, swApp);
            }

            private void barFixCrap()
            {
                ClsTools.SetStds(TaskPanWinFormControl, swApp);
            }

            private void barGarminCrd()
            {
                ClsCardGen.GenCard(TaskPanWinFormControl, swApp);
            }

            #endregion UICallback
        }
    }
}