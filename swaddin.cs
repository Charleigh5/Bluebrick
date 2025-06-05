using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swcommands;
using SolidWorks.Interop.swconst;
using SolidWorks.Interop.swpublished;
using SolidWorksTools;
using SolidWorksTools.File;
using Attribute = System.Attribute;

// ReSharper disable LocalizableElement
namespace BlueBrick
{
    /// <summary>
    ///     Summary description for SolidWorksAddIn1.
    /// </summary>
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [Guid("C56E0AFF-0BD3-4364-90CB-1A581046CD7D")]
    [ComVisible(true)]
    [SwAddin(
        Description = "ViraInsight SolidWorks add-in",
        Title = "BlueBrick",
        LoadAtStartup = true
    )]
    public class SwAddin : ISwAddin
    {
        public string GetImage(string ResName, int Size)
        {
            var imgName = ResName + "_" + Size;
            var sPath = Path.GetTempPath() + imgName + ".png";
            var rm = Resource1.ResourceManager;
            var img = (Image)rm.GetObject(imgName);
            if (img == null) return "";
            img.Save(sPath, ImageFormat.Png);
            return sPath;
        }

        #region Local Variables

        private FrmPane TaskPanWinFormControl;
        private int addinID;

        private BitmapHandler iBmp;
        //private int registerID;

        public const int mainCmdGroupID = 2215;
        public const int mainItemID1 = 2300;
        public const int mainItemID2 = 2301;
        public const int mainItemID3 = 2302;
        public const int mainItemID4 = 2303;
        public const int mainItemID5 = 2304;
        public const int mainItemID6 = 2305;
        public const int mainItemID7 = 2306;
        public const int mainItemID8 = 2307;
        public const int mainItemID9 = 2308;
        public const int mainItemID10 = 2309;
        public const int mainItemID11 = 2310;
        public const int mainItemID12 = 2311;
        public const int mainItemID13 = 2312;
        public const int mainItemID14 = 2313;
        public const int mainItemID15 = 2314;
        public const int mainItemID16 = 2315;
        public const int mainItemID17 = 2316;
        public const int mainItemID18 = 2317;
        public const int mainItemID19 = 2318;
        public const int mainItemID20 = 2319;
        public const int mainItemID21 = 2320;
        public const int mainItemID22 = 2321;
        public const int mainItemID23 = 2322;
        public const int mainItemID24 = 2323;
        public const int mainItemID25 = 2324;
        public const int flyoutGroupID = 2436;

        private string[] mainIcons = new string[3];
        private string[] icons = new string[3];

        private SldWorks SwEventPtr;

        public ISldWorks SwApp { get; private set; }

        public ICommandManager CmdMgr { get; private set; }

        public Hashtable OpenDocs { get; private set; } = new Hashtable();

        #endregion

        #region SolidWorks Registration

        [ComRegisterFunction]
        [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
        public static void RegisterFunction(Type t)
        {
            #region Get Custom Attribute: SwAddinAttribute

            // ReSharper disable once IdentifierTypo
            SwAddinAttribute SWattr = null;
            var type = typeof(SwAddin);

            foreach (Attribute attr in type.GetCustomAttributes(false))
                if (attr is SwAddinAttribute attribute)
                {
                    SWattr = attribute;
                    break;
                }

            #endregion

            try
            {
                var hklm = Registry.LocalMachine;
                var hkcu = Registry.CurrentUser;
                var keyName = "SOFTWARE\\SolidWorks\\Addins\\{" + t.GUID + "}";
                var addInKey = hklm.CreateSubKey(keyName);
                if (addInKey != null)
                {
                    addInKey.SetValue(null, 0);
                    addInKey.SetValue("Description", SWattr.Description);
                    addInKey.SetValue("Title", SWattr.Title);
                    var oAssm = Assembly.GetExecutingAssembly();
                    var sPath = Path.GetDirectoryName(oAssm.Location);
                    if (sPath != null)
                    {
                        sPath = Path.Combine(sPath, "BlueBrick.png");
                        Resource1.BlueBrick.Save(sPath, ImageFormat.Png);
                        addInKey.SetValue("Icon Path", sPath);
                    }

                    keyName = "Software\\SolidWorks\\AddInsStartup\\{" + t.GUID + "}";
                }

                addInKey = hkcu.CreateSubKey(keyName);
                addInKey?.SetValue(null, Convert.ToInt32(SWattr.LoadAtStartup), RegistryValueKind.DWord);
            }
            catch (NullReferenceException nl)
            {
                Console.WriteLine("There was a problem registering this dll: SWattr is null. \n\"" + nl.Message + "\"");
                MessageBox.Show("There was a problem registering this dll: SWattr is null.\n\"" + nl.Message + "\"");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                MessageBox.Show("There was a problem registering the function: \n\"" + e.Message + "\"");
            }
        }

        [ComUnregisterFunction]
        public static void UnregisterFunction(Type t)
        {
            try
            {
                var hklm = Registry.LocalMachine;
                var hkcu = Registry.CurrentUser;

                var keyName = "SOFTWARE\\SolidWorks\\Addins\\{" + t.GUID + "}";
                hklm.DeleteSubKey(keyName);

                keyName = "Software\\SolidWorks\\AddInsStartup\\{" + t.GUID + "}";
                hkcu.DeleteSubKey(keyName);
            }
            catch (NullReferenceException nl)
            {
                Console.WriteLine("There was a problem unregistering this dll: " + nl.Message);
                MessageBox.Show("There was a problem unregistering this dll: \n\"" + nl.Message + "\"");
            }
            catch (Exception e)
            {
                Console.WriteLine("There was a problem unregistering this dll: " + e.Message);
                MessageBox.Show("There was a problem unregistering this dll: \n\"" + e.Message + "\"");
            }
        }

        #endregion

        #region ISwAddin Implementation

        public bool ConnectToSW(object ThisSW, int cookie)
        {
            SwApp = (ISldWorks)ThisSW;
            addinID = cookie;

            //Setup callbacks
            SwApp.SetAddinCallbackInfo(0, this, addinID);

            #region Setup the Command Manager

            CmdMgr = SwApp.GetCommandManager(cookie);
            AddCommandMgr();

            #endregion

            #region Setup the Event Handlers

            SwEventPtr = (SldWorks)SwApp;
            OpenDocs = new Hashtable();
            AttachEventHandlers();

            #endregion

            #region Setup Icons

            var bitmap = new string[6];
            var sTemp = Path.GetTempPath();
            var imgFmt = ImageFormat.Png;
            var sPath = sTemp + "ViraInsight_icon20.png";
            Resource1.ViraInsight_icon20.Save(sPath, imgFmt);
            bitmap[0] = sPath;
            sPath = sTemp + "ViraInsight_icon32.png";
            Resource1.ViraInsight_icon32.Save(sPath, imgFmt);
            bitmap[1] = sPath;
            sPath = sTemp + "ViraInsight_icon40.png";
            Resource1.ViraInsight_icon40.Save(sPath, imgFmt);
            bitmap[2] = sPath;
            sPath = sTemp + "ViraInsight_icon64.png";
            Resource1.ViraInsight_icon64.Save(sPath, imgFmt);
            bitmap[3] = sPath;
            sPath = sTemp + "ViraInsight_icon96.png";
            Resource1.ViraInsight_icon96.Save(sPath, imgFmt);
            bitmap[4] = sPath;
            sPath = sTemp + "ViraInsight_icon128.png";
            Resource1.ViraInsight_icon128.Save(sPath, imgFmt);
            bitmap[5] = sPath;

            #endregion Setup Icons

            #region Create task pane View

            const string toolTip = "ViraInsight BlueBrick";
            if (SwApp == null) return true;

            var ctrl = SwApp.CreateTaskpaneView3(bitmap, toolTip);
            TaskPanWinFormControl = new FrmPane(SwApp);
            ctrl.DisplayWindowFromHandle(TaskPanWinFormControl.Handle.ToInt32());

            #endregion Create task pane View

            return true;
        }


        public bool DisconnectFromSW()
        {
            RemoveCommandMgr();
            DetachEventHandlers();

            Marshal.ReleaseComObject(CmdMgr);
            CmdMgr = null;
            Marshal.ReleaseComObject(SwApp);
            SwApp = null;
            //The add-in _must_ call GC.Collect() here in order to retrieve all managed code pointers
            GC.Collect();
            GC.WaitForPendingFinalizers();

            GC.Collect();
            GC.WaitForPendingFinalizers();

            return true;
        }

        #endregion

        #region UI Methods

        public void AddCommandMgr()
        {
            if (iBmp == null)
                iBmp = new BitmapHandler();
            //Assembly thisAssembly;
            const string Title = "BlueBrick";
            const string ToolTip = "BlueBrick Tools";

            var docTypes = new[]
            {
                (int)swDocumentTypes_e.swDocASSEMBLY,
                (int)swDocumentTypes_e.swDocDRAWING,
                (int)swDocumentTypes_e.swDocPART
            };

            //thisAssembly = Assembly.GetAssembly(GetType());
            var cmdGroupErr = 0;
            var ignorePrevious = false;

            //get the ID information stored in the registry
            var getDataResult = CmdMgr.GetGroupDataFromRegistry(mainCmdGroupID, out var registryIDs);
            var knownIDs = new[]
            {
                mainItemID1,
                mainItemID2,
                mainItemID3,
                mainItemID4,
                mainItemID5,
                mainItemID6,
                mainItemID7,
                mainItemID8,
                mainItemID9,
                mainItemID10,
                mainItemID11,
                mainItemID12,
                mainItemID13,
                mainItemID14,
                mainItemID15,
                mainItemID16,
                mainItemID17,
                mainItemID18,
                mainItemID19,
                mainItemID20,
                mainItemID21,
                mainItemID22,
                mainItemID23,
                mainItemID24,
                mainItemID25
            };
            if (getDataResult)
                if (!CompareIDs((int[])registryIDs, knownIDs)) //if the IDs don't match, reset the commandGroup
                    ignorePrevious = true;

            var cmdGroup = CmdMgr.CreateCommandGroup2(mainCmdGroupID, Title, ToolTip, "BlueBrick Tools", -1,
                ignorePrevious, ref cmdGroupErr);

            #region Command Images

            //get proper sizes
            //20, 32, 40, 64, 96, 128 (.bmp or .png) Use a 256-color palette.
            SwApp.GetImageSize(out var iImgSm, out var iImgMed, out var iImgLrg);

            //load paths to correct sizes - small
            icons[0] = GetImage("iconStrip", iImgSm);
            mainIcons[0] = GetImage("icon_MenuSerial", iImgSm);

            //load paths to correct sizes - medium
            icons[1] = GetImage("iconStrip", iImgMed);
            mainIcons[1] = GetImage("icon_MenuSerial", iImgMed);

            //load paths to correct sizes - large
            icons[2] = GetImage("iconStrip", iImgLrg);
            mainIcons[2] = GetImage("icon_MenuSerial", iImgLrg);

            #endregion Command Images

            cmdGroup.MainIconList = mainIcons;
            cmdGroup.IconList = icons;

            // ReSharper disable once BitwiseOperatorOnEnumWithoutFlags
            const int menuToolbarOption = (int)(swCommandItemType_e.swMenuItem | swCommandItemType_e.swToolbarItem);
            var cmdIndex0 = cmdGroup.AddCommandItem2("Check In", -1, "Check in all parts checked out by user.",
                "Check In", 0, "barCheckIn", "",
                mainItemID1, menuToolbarOption);
            var cmdIndex1 = cmdGroup.AddCommandItem2("Pull Serial", -1, "Get a new serial number for the current part.",
                "Pull Serial", 1,
                "barPullSerial(0)", "", mainItemID2, menuToolbarOption);
            var cmdIndex2 = cmdGroup.AddCommandItem2("Finish Schedule", -1,
                @"Insert a finish schedule into the current drawing.", "Finish Schedule", 2,
                "barFinSchedule", "", mainItemID3, menuToolbarOption);
            var cmdIndex3 = cmdGroup.AddCommandItem2("Toggle Through", -1,
                "Toggle the ability to select lines thru faces.", "Toggle Through", 3,
                "barTogThru", "", mainItemID4, menuToolbarOption);
            var cmdIndex4 = cmdGroup.AddCommandItem2("Set Lights", -1, "Reset lighting to default options.",
                "Set Lights", 4,
                "barSetLights", "", mainItemID5, menuToolbarOption);
            var cmdIndex5 = cmdGroup.AddCommandItem2("Base Appearance", -1,
                "Remove all appearances and restore part material defaults.", "Base Appearance", 5,
                "barBaseAppear", "", mainItemID6, menuToolbarOption);
            var cmdIndex6 = cmdGroup.AddCommandItem2("Fix Crap", -1,
                "Reset multiple settings to ViraInsight preferred.",
                "Fix Crap", 6,
                "barFixCrap", "", mainItemID7, menuToolbarOption);
            var cmdIndex7 = cmdGroup.AddCommandItem2("Garmin Cards", -1, "Generate a new Garmin product card.",
                "Garmin Cards", 7,
                "barGarminCrd", "", mainItemID8, menuToolbarOption);
            var cmdIndex8 = cmdGroup.AddCommandItem2("Finish Table Search", -1,
                "Search and replace within finish schedules.",
                "Finish Table Search", 8,
                "barFinSearch", "", mainItemID22, menuToolbarOption);
            var cmdIndex9 = cmdGroup.AddCommandItem2("Copy Drawing", -1,
                "Copy existing drawing and reference current model.",
                "Copy Drawing", 9,
                "barCopyDwg", "", mainItemID23, menuToolbarOption);
            var cmdIndex10 = cmdGroup.AddCommandItem2("Show All", -1, "Open assembly model and un-hide all components.",
                "Show All", 10,
                "barShowHide", "", mainItemID24, menuToolbarOption);
            var cmdIndex11 = cmdGroup.AddCommandItem2("Sheet Rename", -1, "Renumber all sheets on drawing.",
                "Sheet Rename", 11,
                "barDrawRename", "", mainItemID25, menuToolbarOption);
            cmdGroup.AddCommandItem2("Get ASY", -1, "Get a new ASY number for the current part.", "Get ASY", 1,
                "barPullSerial(1)", "", mainItemID9, menuToolbarOption);
            cmdGroup.AddCommandItem2("Get SUB", -1, "Get a new SUB number for the current part.", "Get SUB", 1,
                "barPullSerial(2)", "", mainItemID10, menuToolbarOption);
            cmdGroup.AddCommandItem2("Get MPA", -1, "Get a new MPA number for the current part.", "Get MPA", 1,
                "barPullSerial(3)", "", mainItemID11, menuToolbarOption);
            cmdGroup.AddCommandItem2("Get MPM", -1, "Get a new MPM number for the current part.", "Get MPM", 1,
                "barPullSerial(4)", "", mainItemID12, menuToolbarOption);
            cmdGroup.AddCommandItem2("Get MPP", -1, "Get a new MPP number for the current part.", "Get MPP", 1,
                "barPullSerial(5)", "", mainItemID13, menuToolbarOption);
            cmdGroup.AddCommandItem2("Get MPW", -1, "Get a new MPW number for the current part.", "Get MPW", 1,
                "barPullSerial(6)", "", mainItemID14, menuToolbarOption);
            cmdGroup.AddCommandItem2("Get CSM", -1, "Get a new CSM number for the current part.", "Get CSM", 1,
                "barPullSerial(7)", "", mainItemID15, menuToolbarOption);
            cmdGroup.AddCommandItem2("Get CST", -1, "Get a new CST number for the current part.", "Get CST", 1,
                "barPullSerial(8)", "", mainItemID16, menuToolbarOption);
            cmdGroup.AddCommandItem2("Get ELE", -1, "Get a new ELE number for the current part.", "Get ELE", 1,
                "barPullSerial(9)", "", mainItemID17, menuToolbarOption);
            cmdGroup.AddCommandItem2("Get GLS", -1, "Get a new GLS number for the current part.", "Get GLS", 1,
                "barPullSerial(10)", "", mainItemID18, menuToolbarOption);
            cmdGroup.AddCommandItem2("Get GRA", -1, "Get a new GRA number for the current part.", "Get GRA", 1,
                "barPullSerial(11)", "", mainItemID19, menuToolbarOption);
            cmdGroup.AddCommandItem2("Get HWD", -1, "Get a new HWD number for the current part.", "Get HWD", 1,
                "barPullSerial(12)", "", mainItemID20, menuToolbarOption);
            cmdGroup.AddCommandItem2("Get LVL", -1, "Get a new LVL number for the current part.", "Get LVL", 1,
                "barPullSerial(13)", "", mainItemID21, menuToolbarOption);

            cmdGroup.HasToolbar = true;
            cmdGroup.HasMenu = true;
            cmdGroup.Activate();

            var flyGroup = CmdMgr.CreateFlyoutGroup2(flyoutGroupID, "Serials", "Quick Serial",
                "Get a new serial number for the current part.",
                cmdGroup.MainIconList, cmdGroup.IconList, "FlyoutCallback", "");
            flyGroup.FlyoutType = (int)swCommandFlyoutStyle_e.swCommandFlyoutStyle_Simple;

            foreach (var type in docTypes)
            {
                var cmdTab = CmdMgr.GetCommandTab(type, Title);
                if (((cmdTab != null) & !getDataResult) |
                    ignorePrevious) //if tab exists, but we have ignored the registry info (or changed command group ID), re-create the tab.  Otherwise the ids won't match up and the tab will be blank
                {
                    CmdMgr.RemoveCommandTab(cmdTab);
                    cmdTab = null;
                }

                //if cmdTab is null, must be first load (possibly after reset), add the commands to the tabs
                if (cmdTab != null) continue;
                cmdTab = CmdMgr.AddCommandTab(type, Title);
                var cmdBox = cmdTab.AddCommandTabBox();
                var cmdIDs = new int[13];
                var TextType = new int[13];
                cmdIDs[0] = cmdGroup.CommandID[cmdIndex0];
                TextType[0] = (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextBelow;
                cmdIDs[1] = cmdGroup.CommandID[cmdIndex1];
                TextType[1] = (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextBelow;
                cmdIDs[2] = cmdGroup.CommandID[cmdIndex2];
                TextType[2] = (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextBelow;
                cmdIDs[3] = cmdGroup.CommandID[cmdIndex8];
                TextType[3] = (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextBelow;
                cmdIDs[4] = cmdGroup.CommandID[cmdIndex3];
                TextType[4] = (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextBelow;
                cmdIDs[5] = cmdGroup.CommandID[cmdIndex4];
                TextType[5] = (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextBelow;
                cmdIDs[6] = cmdGroup.CommandID[cmdIndex5];
                TextType[6] = (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextBelow;
                cmdIDs[7] = cmdGroup.CommandID[cmdIndex6];
                TextType[7] = (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextBelow;
                cmdIDs[8] = cmdGroup.CommandID[cmdIndex7];
                TextType[8] = (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextBelow;
                cmdIDs[9] = cmdGroup.CommandID[cmdIndex8];
                TextType[9] = (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextBelow;
                cmdIDs[10] = cmdGroup.CommandID[cmdIndex9];
                TextType[10] = (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextBelow;
                cmdIDs[11] = cmdGroup.CommandID[cmdIndex10];
                TextType[11] = (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextBelow;
                cmdIDs[12] = cmdGroup.CommandID[cmdIndex11];
                TextType[12] = (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextBelow;
                cmdBox.AddCommands(cmdIDs, TextType);

                var cmdBox1 = cmdTab.AddCommandTabBox();
                cmdIDs = new int[1];
                TextType = new int[1];
                cmdIDs[0] = flyGroup.CmdID;
                TextType[0] = (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextBelow |
                              (int)swCommandTabButtonFlyoutStyle_e.swCommandTabButton_ActionFlyout;
                cmdBox1.AddCommands(cmdIDs, TextType);
                cmdTab.AddSeparator(cmdBox1, cmdIDs[0]);
            }
        }

        public void RemoveCommandMgr()
        {
            iBmp.Dispose();
            CmdMgr.RemoveCommandGroup(mainCmdGroupID);
            CmdMgr.RemoveFlyoutGroup(flyoutGroupID);
        }

        public bool CompareIDs(int[] storedIDs, int[] addinIDs)
        {
            var storedList = new List<int>(storedIDs);
            var addinList = new List<int>(addinIDs);

            addinList.Sort();
            storedList.Sort();

            if (addinList.Count != storedList.Count) return false;
            return !addinList.Where((t, i) => t != storedList[i]).Any();
        }

        #endregion

        #region UI Callbacks

        //this area is used for functions called by the commands - they will show as unused and need to be public

        public void barCheckIn()
        {
            ClsTools.CheckInAll(TaskPanWinFormControl);
        }

        public void barPullSerial(string data)
        {
            var cmd = int.Parse(data);
            switch (cmd)
            {
                case 0:
                    ClsTools.GetSerial(TaskPanWinFormControl);
                    break;
                case 1:
                    ClsTools.GetSerial(TaskPanWinFormControl, "01-ASY5");
                    break;
                case 2:
                    ClsTools.GetSerial(TaskPanWinFormControl, "02-SUB5");
                    break;
                case 3:
                    ClsTools.GetSerial(TaskPanWinFormControl, "20-MPA5");
                    break;
                case 4:
                    ClsTools.GetSerial(TaskPanWinFormControl, "03-MPM5");
                    break;
                case 5:
                    ClsTools.GetSerial(TaskPanWinFormControl, "04-MPP5");
                    break;
                case 6:
                    ClsTools.GetSerial(TaskPanWinFormControl, "05-MPW5");
                    break;
                case 7:
                    ClsTools.GetSerial(TaskPanWinFormControl, "12-CSM5");
                    break;
                case 8:
                    ClsTools.GetSerial(TaskPanWinFormControl, "11-CST5");
                    break;
                case 9:
                    ClsTools.GetSerial(TaskPanWinFormControl, "09-ELE5");
                    break;
                case 10:
                    ClsTools.GetSerial(TaskPanWinFormControl, "13-GLS5");
                    break;
                case 11:
                    ClsTools.GetSerial(TaskPanWinFormControl, "06-GRA5");
                    break;
                case 12:
                    ClsTools.GetSerial(TaskPanWinFormControl, "07-HWD5");
                    break;
                case 13:
                    ClsTools.GetSerial(TaskPanWinFormControl, "10-LVL5");
                    break;
            }
        }

        public void barFinSchedule()
        {
            ClsTools.InsertFinSchedule(TaskPanWinFormControl, SwApp);
        }

        public void barTogThru()
        {
            ClsTools.ToggleSel(TaskPanWinFormControl, SwApp);
        }

        public void barSetLights()
        {
            ClsTools.SetLights(TaskPanWinFormControl, SwApp);
        }

        public void barDrawRename()
        {
            ClsTools.DrwTabRename(TaskPanWinFormControl, SwApp);
        }

        public void barBaseAppear()
        {
            ClsTools.ClrAppear(TaskPanWinFormControl, SwApp);
        }

        public void barFixCrap()
        {
            ClsTools.SetStds(TaskPanWinFormControl, SwApp);
        }

        public void barGarminCrd()
        {
            ClsCardGen.GenCard(TaskPanWinFormControl, SwApp);
        }

        public void barFinSearch()
        {
            ClsFinish.FinishRename(TaskPanWinFormControl, SwApp);
        }

        public void barCopyDwg()
        {
            ClsTools.CopyDwg(TaskPanWinFormControl, SwApp);
        }

        public void barShowHide()
        {
            ClsTools.ShowHidden(TaskPanWinFormControl, SwApp);
        }

        public void FlyoutCallback()
        {
            var flyGroup = CmdMgr.GetFlyoutGroup(flyoutGroupID);
            flyGroup.RemoveAllCommandItems();
            flyGroup.AddCommandItem("ASY", "Get a new ASY number for the current part.", 1, "barPullSerial(1)", "");
            flyGroup.AddCommandItem("SUB", "Get a new SUB number for the current part.", 1, "barPullSerial(2)", "");
            flyGroup.AddCommandItem("MPA", "Get a new MPA number for the current part.", 1, "barPullSerial(3)", "");
            flyGroup.AddCommandItem("MPM", "Get a new MPM number for the current part.", 1, "barPullSerial(4)", "");
            flyGroup.AddCommandItem("MPP", "Get a new MPP number for the current part.", 1, "barPullSerial(5)", "");
            flyGroup.AddCommandItem("MPW", "Get a new MPW number for the current part.", 1, "barPullSerial(6)", "");
            flyGroup.AddCommandItem("CSM", "Get a new CSM number for the current part.", 1, "barPullSerial(7)", "");
            flyGroup.AddCommandItem("CST", "Get a new CST number for the current part.", 1, "barPullSerial(8)", "");
            flyGroup.AddCommandItem("ELE", "Get a new ELE number for the current part.", 1, "barPullSerial(9)", "");
            flyGroup.AddCommandItem("GLS", "Get a new GLS number for the current part.", 1, "barPullSerial(10)", "");
            flyGroup.AddCommandItem("GRA", "Get a new GRA number for the current part.", 1, "barPullSerial(11)", "");
            flyGroup.AddCommandItem("HWD", "Get a new HWD number for the current part.", 1, "barPullSerial(12)", "");
            flyGroup.AddCommandItem("LVL", "Get a new LVL number for the current part.", 1, "barPullSerial(13)", "");
        }

        #endregion UICallback

        #region Event Methods

        public bool AttachEventHandlers()
        {
            AttachSwEvents();
            //Listen for events on all currently open docs
            AttachEventsToAllDocuments();
            return true;
        }

        private bool AttachSwEvents()
        {
            try
            {
                SwEventPtr.ActiveModelDocChangeNotify += OnModelChange;
                SwEventPtr.FileOpenPostNotify += FileOpenPostNotify;
                SwEventPtr.OnIdleNotify += OnIdleNotify;
                SwEventPtr.CommandCloseNotify += CommandCloseNotify;
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }
        }

        private bool DetachSwEvents()
        {
            try
            {
                SwEventPtr.ActiveModelDocChangeNotify -= OnModelChange;
                SwEventPtr.FileOpenPostNotify -= FileOpenPostNotify;
                SwEventPtr.OnIdleNotify -= OnIdleNotify;
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }
        }

        public void AttachEventsToAllDocuments()
        {
            var modDoc = (ModelDoc2)SwApp.GetFirstDocument();
            while (modDoc != null)
            {
                if (!OpenDocs.Contains(modDoc))
                {
                    AttachModelDocEventHandler(modDoc);
                }
                else if (OpenDocs.Contains(modDoc))
                {
                    var docHandler = (DocumentEventHandler)OpenDocs[modDoc];
                    docHandler?.ConnectModelViews();
                }

                modDoc = (ModelDoc2)modDoc.GetNext();
            }
        }

        public bool AttachModelDocEventHandler(ModelDoc2 modDoc)
        {
            if (modDoc == null)
                return false;

            DocumentEventHandler docHandler;
            if (OpenDocs.Contains(modDoc)) return true;
            switch (modDoc.GetType())
            {
                case (int)swDocumentTypes_e.swDocPART:
                {
                    docHandler = new PartEventHandler(modDoc, this);
                    break;
                }
                case (int)swDocumentTypes_e.swDocASSEMBLY:
                {
                    docHandler = new AssemblyEventHandler(modDoc, this);
                    break;
                }
                case (int)swDocumentTypes_e.swDocDRAWING:
                {
                    docHandler = new DrawingEventHandler(modDoc, this);
                    break;
                }
                default:
                {
                    return false; //Unsupported document type
                }
            }

            docHandler.AttachEventHandlers();
            OpenDocs.Add(modDoc, docHandler);
            return true;
        }

        public bool DetachModelEventHandler(ModelDoc2 modDoc)
        {
            OpenDocs.Remove(modDoc);
            return true;
        }

        public bool DetachEventHandlers()
        {
            DetachSwEvents();

            //Close events on all currently open docs
            var numKeys = OpenDocs.Count;
            var keys = new object[numKeys];

            //Remove all document event handlers
            OpenDocs.Keys.CopyTo(keys, 0);
            foreach (ModelDoc2 key in keys)
            {
                var docHandler = (DocumentEventHandler)OpenDocs[key];
                docHandler.DetachEventHandlers(); //This also removes the pair from the hash
                //docHandler = null;
            }

            return true;
        }

        #endregion

        #region Event Handlers

        //Events
        public int CommandCloseNotify(int cmd, int userCmd)
        {
            try
            {
                switch (cmd)
                {
                    case (int)swCommands_e.swCommands_Delete_Comment:
                    {
                        TaskPanWinFormControl.RefreshComments();
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.HResult + " on CommandCloseNotify event. Message: " + ex.Message);
            }

            return 0;
        }

        public int FileOpenPostNotify(string FileName)
        {
            AttachEventsToAllDocuments();
            try
            {
                TaskPanWinFormControl.ReadPropFromPath(FileName);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.HResult + " on FileOpenPostNotify event. Message: " + ex.Message);
            }

            return 0;
        }

        public int OnIdleNotify()
        {
            try
            {
                if (TaskPanWinFormControl.Prop.Model == null) return 0;
                if (SwApp.GetDocumentCount() == 0) TaskPanWinFormControl.ReadPropFromModel(null);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.HResult + " on OnIdleNotify event. Message: " + ex.Message);
            }

            return 0;
        }

        public int OnModelChange()
        {
            try
            {
                TaskPanWinFormControl.ReadPropFromActive();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.HResult + " on OnModelChange event. Message: " + ex.Message);
            }

            return 0;
        }

        public int FromAssm(IModelDoc2 swModel)
        {
            try
            {
                TaskPanWinFormControl.ReadPropFromModel(swModel);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.HResult + " on FromAssm method. Message: " + ex.Message);
            }

            return 0;
        }

        public void ModelClosing()
        {
            try
            {
                TaskPanWinFormControl.ModelClosing();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.HResult + " on ModelClosing method. Message: " + ex.Message);
            }
        }

        #endregion
    }
}