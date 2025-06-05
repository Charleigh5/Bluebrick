using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorks.Interop.swpublished;

// ReSharper disable SuspiciousTypeConversion.Global

namespace BlueBrick
{
    // ReSharper disable once InconsistentNaming
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [ComVisible(true)]
    public class PMPHandler : IPropertyManagerPage2Handler9
    {
        // ReSharper disable once NotAccessedField.Local
        private readonly ISldWorks iSwApp;
        private readonly UserPMPage pPage;

        // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
        private readonly SwAddin userAddin;

        public PMPHandler(SwAddin addin, UserPMPage page)
        {
            userAddin = addin;
            iSwApp = userAddin.SwApp;
            pPage = page;
        }

        //Implement these methods from the interface
        public void AfterClose()
        {
            //This function must contain code, even if it does nothing, to prevent the
            //.NET runtime environment from doing garbage collection at the wrong time.
            var IndentSize = Debug.IndentSize;
            Debug.WriteLine(IndentSize);
        }

        public void OnCheckboxCheck(int id, bool status)
        {
        }

        public void OnClose(int reason)
        {
            //This function must contain code, even if it does nothing, to prevent the
            //.NET runtime environment from doing garbage collection at the wrong time.
            var IndentSize = Debug.IndentSize;
            Debug.WriteLine(IndentSize);
        }

        public void OnComboboxEditChanged(int id, string text)
        {
        }

        public int OnActiveXControlCreated(int id, bool status)
        {
            return -1;
        }

        public void OnButtonPress(int id)
        {
            if (id == UserPMPage.buttonID1) // Toggle the text box control visibility state
                ((IPropertyManagerPageControl)pPage.textbox2).Visible =
                    !((IPropertyManagerPageControl)pPage.textbox2).Visible;
            else if (id == UserPMPage.buttonID2) // Toggle the text box control enabled/disabled
                ((IPropertyManagerPageControl)pPage.textbox3).Enabled =
                    !((IPropertyManagerPageControl)pPage.textbox3).Enabled;
        }

        public void OnComboboxSelectionChanged(int id, int item)
        {
        }

        public void OnGroupCheck(int id, bool status)
        {
        }

        public void OnGroupExpand(int id, bool status)
        {
        }

        public bool OnHelp()
        {
            // Specify a url path or a path to a chm file
            const string helpPath = "https://help.solidworks.com/2019/English/api/sldworksapiprogguide/Welcome.htm";
            // ReSharper disable once CommentTypo
            // ex. const string helpPath = "C:\\Program Files\\SolidWorks Corp\\SOLIDWORKS (2)\\api\\apihelp.chm";

            var helpForm = new Form();
            Help.ShowHelp(helpForm, helpPath);
            return true;
        }

        public void OnListboxSelectionChanged(int id, int item)
        {
        }

        public bool OnNextPage()
        {
            return true;
        }

        public void OnNumberboxChanged(int id, double val)
        {
        }

        public void OnNumberBoxTrackingCompleted(int id, double val)
        {
        }

        public void OnOptionCheck(int id)
        {
        }

        public bool OnPreviousPage()
        {
            return true;
        }

        public void OnSelectionboxCalloutCreated(int id)
        {
        }

        public void OnSelectionboxCalloutDestroyed(int id)
        {
        }

        public void OnSelectionboxFocusChanged(int id)
        {
        }

        public void OnSelectionboxListChanged(int id, int item)
        {
            // When a user selects entities to populate the selection box, display a popup cursor.
            pPage.swPropertyPage.SetCursor((int)swPropertyManagerPageCursors_e.swPropertyManagerPageCursors_Advance);
        }

        public void OnTextboxChanged(int id, string text)
        {
        }

        public void AfterActivation()
        {
        }

        public bool OnKeystroke(int Wparam, int Message, int Lparam, int Id)
        {
            return true;
        }

        public void OnPopupMenuItem(int Id)
        {
        }

        public void OnPopupMenuItemUpdate(int Id, ref int retval)
        {
        }

        public bool OnPreview()
        {
            return true;
        }

        public void OnSliderPositionChanged(int Id, double Value)
        {
        }

        public void OnSliderTrackingCompleted(int Id, double Value)
        {
        }

        public bool OnSubmitSelection(int Id, object Selection, int SelType, ref string ItemText)
        {
            return true;
        }

        public bool OnTabClicked(int Id)
        {
            return true;
        }

        public void OnUndo()
        {
        }

        public void OnWhatsNew()
        {
        }


        public void OnGainedFocus(int Id)
        {
        }

        public void OnListboxRMBUp(int Id, int PosX, int PosY)
        {
        }

        public void OnLostFocus(int Id)
        {
        }

        public void OnRedo()
        {
        }

        public int OnWindowFromHandleControlCreated(int Id, bool Status)
        {
            return 0;
        }
    }
}