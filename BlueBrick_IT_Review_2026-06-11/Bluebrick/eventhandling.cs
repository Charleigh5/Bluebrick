using System;
using System.Collections;
using System.Diagnostics;
using SolidWorks.Interop.sldworks;

// ReSharper disable SuspiciousTypeConversion.Global
// ReSharper disable InconsistentNaming
namespace BlueBrick
{
    public class DocumentEventHandler
    {
        protected readonly ModelDoc2 document;
        private readonly Hashtable openModelViews;
        protected readonly SwAddin userAddin;

        internal DocumentEventHandler(ModelDoc2 modDoc, SwAddin addin)
        {
            document = modDoc;
            userAddin = addin;
            openModelViews = new Hashtable();
        }

        internal virtual void AttachEventHandlers()
        {

        }

        internal virtual void DetachEventHandlers()
        {

        }

        internal void ConnectModelViews()
        {
            var mView = (IModelView)document.GetFirstModelView();
            while (mView != null)
            {
                if (!openModelViews.Contains(mView))
                {
                    var dView = new DocView(userAddin, mView, this);
                    DocView.AttachEventHandlers();
                    openModelViews.Add(mView, dView);
                }
                mView = (IModelView)mView.GetNext();
            }
        }

        internal void DisconnectModelViews()
        {
            //Close events on all currently open docs
            var numKeys = openModelViews.Count;
            if (numKeys == 0) return;
            var keys = new object[numKeys];

            //Remove all ModelView event handlers
            openModelViews.Keys.CopyTo(keys, 0);
            foreach (ModelView key in keys)
            {
                var dView = (DocView)openModelViews[key];
                dView.DetachEventHandlers();
                openModelViews.Remove(key);
            }
        }

        internal void DetachModelViewEventHandler(ModelView mView)
        {
            if (!openModelViews.Contains(mView)) return;
            openModelViews.Remove(mView);
        }
    }

    public class PartEventHandler : DocumentEventHandler
    {
        private readonly PartDoc doc;
        private readonly SwAddin swAddin;

        public PartEventHandler(ModelDoc2 modDoc, SwAddin addin)
            : base(modDoc, addin)
        {
            doc = (PartDoc)document;
            swAddin = addin;
        }

        internal override void AttachEventHandlers()
        {
            doc.DestroyNotify2 += OnDestroy;
            ConnectModelViews();
        }

        internal override void DetachEventHandlers()
        {
            doc.DestroyNotify2 -= OnDestroy;
            DisconnectModelViews();
            userAddin.DetachModelEventHandler(document);
        }

        //Event Handlers
        private int OnDestroy(int type)
        {
            try
            {
                swAddin.ModelClosing();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error: " + ex.HResult + " on OnDestroy event. Message: " + ex.Message);
            }
            DetachEventHandlers();
            return 0;
        }
    }

    public class AssemblyEventHandler : DocumentEventHandler
    {
        private readonly AssemblyDoc doc;
        private readonly SwAddin swAddin;

        public AssemblyEventHandler(ModelDoc2 modDoc, SwAddin addin)
            : base(modDoc, addin)
        {
            doc = (AssemblyDoc)document;
            swAddin = addin;
        }

        internal override void AttachEventHandlers()
        {
            doc.DestroyNotify += OnDestroy;
            doc.NewSelectionNotify += OnNewSelection;
            ConnectModelViews();
        }

        internal override void DetachEventHandlers()
        {
            doc.DestroyNotify -= OnDestroy;
            doc.NewSelectionNotify -= OnNewSelection;
            DisconnectModelViews();
            userAddin.DetachModelEventHandler(document);
        }

        //Event Handlers
        private int OnDestroy()
        {
            try
            {
                swAddin.ModelClosing();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error: " + ex.HResult + " on OnDestroy event. Message: " + ex.Message);
            }
            DetachEventHandlers();
            return 0;
        }

        private int OnNewSelection()
        {
            var swModel = (IModelDoc2)doc;
            try
            {
                var active = (IModelDoc2)swAddin.SwApp.ActiveDoc;
                if (swModel.GetTitle() != active.GetTitle()) return 0;
                var selMgr = (SelectionMgr)swModel.SelectionManager;

                //check for/count selected components
                var selCount = selMgr.GetSelectedObjectCount2(0);
                if (selCount != 1)
                {
                    swAddin.FromAssm(swModel); //none or multiple - return top assembly
                    return 0;
                }

                //one - return top component
                var comp = (Component2)selMgr.GetSelectedObjectsComponent4(1, -1);
                if (comp == null) return 0;
                swModel = (IModelDoc2)comp.GetModelDoc2();
                swAddin.FromAssm(swModel); 
                return 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error: " + ex.HResult + " on OnNewSelection event. Message: " + ex.Message);
                swModel = (IModelDoc2)doc;
                swAddin.FromAssm(swModel);
                return 0;
            }
        }
    }

    public class DrawingEventHandler : DocumentEventHandler
    {
        private readonly DrawingDoc doc;
        private readonly SwAddin swAddin;

        public DrawingEventHandler(ModelDoc2 modDoc, SwAddin addin)
            : base(modDoc, addin)
        {
            doc = (DrawingDoc)document;
            swAddin = addin;
        }

        internal override void AttachEventHandlers()
        {
            doc.DestroyNotify += OnDestroy;
            ConnectModelViews();
        }

        internal override void DetachEventHandlers()
        {
            doc.DestroyNotify -= OnDestroy;
            DisconnectModelViews();
            userAddin.DetachModelEventHandler(document);
        }

        //Event Handlers
        private int OnDestroy()
        {
            try
            {
                swAddin.ModelClosing();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error: " + ex.HResult + " on OnDestroy event. Message: " + ex.Message);
            }
            DetachEventHandlers();
            return 0;
        }
    }

    public class DocView
    {
        // ReSharper disable once NotAccessedField.Local
        private readonly ISldWorks iSwApp;
        private readonly ModelView mView;
        private readonly DocumentEventHandler parent;

        public DocView(SwAddin addin, IModelView mv, DocumentEventHandler doc)
        {
            mView = (ModelView)mv;
            iSwApp = addin.SwApp;
            parent = doc;
        }

        // ReSharper disable once UnusedMethodReturnValue.Global
        public static bool AttachEventHandlers()
        {
            return true;
        }

        // ReSharper disable once UnusedMethodReturnValue.Global
        public bool DetachEventHandlers()
        {
            parent.DetachModelViewEventHandler(mView);
            return true;
        }
    }
}