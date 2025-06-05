using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace BlueBrick
{
    internal class ClsProperties
    {
        private ICommentFolder _comments;
        private readonly FrmPane _frmStat;

        internal ClsProperties(FrmPane frmStat)
        {
            CustomerProps = new List<CustomerProp>();
            _frmStat = frmStat;
        }

        //properties
        internal bool Dirty { get; set; }

        internal string DocumentNum { get; set; }

        internal string PartNum { get; set; }

        internal string Description { get; set; }

        internal string CustomerNum { get; set; }

        internal string CustomerName { get; private set; }

        internal string Revision { get; private set; }

        internal string Opportunity { get; set; }

        internal string ProdCat { get; set; }

        internal List<CustomerProp> CustomerProps  { get; }

        internal IModelDoc2 Model { get; private set; }

        //methods
        internal bool SetModel(IModelDoc2 model, bool allCfg)
        {
            try
            {
                HandleDirty(allCfg);
                Model = model;
                if (Model == null)
                {
                    ClearProp();
                    return false;
                }
                ReadProp();
                return true;
            }
            catch (Exception e)
            {
                MessageBox.Show(@"Error occured during SetModel method of ClsProperties: " + e.Message, @"Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                Console.WriteLine(e);
                return false;
            }
        }

        private void HandleDirty(bool allCfg)
        {
            try
            {
                if (!Dirty) return;
                var res = MessageBox.Show(
                    @"Changes have been made to the custom properties. Do you want to apply them?",
                    @"Save Changes?", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);
                if (res == DialogResult.Yes) WriteProp(allCfg);
            }
            catch (Exception e)
            {
                _frmStat.SetStat(0, @"Error reading model properties: " + e.Message);
            }
        }

        private void ReadProp()
        {
            try
            {
                var propMgr = Model.Extension.CustomPropertyManager[""];
                ClearProp();

                propMgr.Get6(@"Document Number", false, out _, out var sProp, out _, out _);
                DocumentNum = sProp;
                propMgr.Get6(@"Part Number", false, out _, out sProp, out _, out _);
                PartNum = sProp;
                propMgr.Get6(@"Description", false, out _, out sProp, out _, out _);
                Description = sProp;
                propMgr.Get6(@"Number", false, out _, out sProp, out _, out _);
                CustomerNum = sProp;
                propMgr.Get6(@"Opp", false, out _, out sProp, out _, out _);
                Opportunity = sProp;
                propMgr.Get6(@"Revision", false, out _, out sProp, out _, out _);
                Revision = sProp;
                propMgr.Get6(@"Customer", false, out _, out sProp, out _, out _);
                CustomerName = sProp;
                propMgr.Get6(@"ProductCategory", false, out _, out sProp, out _, out _);
                ProdCat = sProp;

                //get customer specific
                var custCode = CustomerName.Trim().ToUpper();
                switch (custCode)
                {
                    case "GARMIN INTERNATIONAL, INC":
                        const string propName = @"UPC";
                        propMgr.Get6(propName, false, out _, out sProp, out _, out _);
                        var cProp = new CustomerProp
                        {
                            PropertyValue = sProp,
                            PropertyName = propName,
                            PropertyLabel = propName
                        };
                        CustomerProps.Add(cProp);
                        break;
                }
            }
            catch (Exception e)
            {
                _frmStat.SetStat(0, @"Error retrieving properties: " + e.Message);
            }

            //get comments folder object
            IFeature feat;
            switch (Model.GetType())
            {
                case (int)swDocumentTypes_e.swDocPART:
                    // ReSharper disable once SuspiciousTypeConversion.Global
                    var docP = (IPartDoc)Model;
                    feat = (IFeature)docP.FeatureByName("Comments") ?? (IFeature)docP.FeatureByName("TUBE GAUGE THICKNESS CHART");
                    break;
                case (int)swDocumentTypes_e.swDocASSEMBLY:
                    // ReSharper disable once SuspiciousTypeConversion.Global
                    var docA = (IAssemblyDoc)Model;
                    feat = (IFeature)docA.FeatureByName("Comments") ?? (IFeature)docA.FeatureByName("TUBE GAUGE THICKNESS CHART");
                    break;
                case (int)swDocumentTypes_e.swDocDRAWING:
                    // ReSharper disable once SuspiciousTypeConversion.Global
                    var docD = (IDrawingDoc)Model;
                    feat = (IFeature)docD.FeatureByName("Comments") ?? (IFeature)docD.FeatureByName("TUBE GAUGE THICKNESS CHART");
                    break;
                default:
                    return;
            }
            if (feat != null)
            {
                _comments = (CommentFolder)feat.GetSpecificFeature2();
            }
        }

        private void ClearProp()
        {
            DocumentNum = string.Empty;
            PartNum = string.Empty;
            Description = string.Empty;
            CustomerNum = string.Empty;
            Opportunity = string.Empty;
            Revision = string.Empty;
            CustomerName = string.Empty;
            ProdCat = string.Empty;
            _comments = null;
            CustomerProps.Clear();
            Dirty = false;
        }

        internal void WriteProp(bool allCfg)
        {
            if (Model == null) return;

            //set global properties
            var propMgr = Model.Extension.CustomPropertyManager[""];
            propMgr.Add3(@"Document Number", (int)swCustomInfoType_e.swCustomInfoText, DocumentNum,
                (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
            propMgr.Add3(@"Part Number", (int)swCustomInfoType_e.swCustomInfoText, PartNum,
                (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
            propMgr.Add3(@"Description", (int)swCustomInfoType_e.swCustomInfoText, Description,
                (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
            propMgr.Add3(@"Number", (int)swCustomInfoType_e.swCustomInfoText, CustomerNum,
                (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
            propMgr.Add3(@"Opp", (int)swCustomInfoType_e.swCustomInfoText, Opportunity,
                (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
            propMgr.Add3(@"ProductCategory", (int)swCustomInfoType_e.swCustomInfoText, ProdCat,
                (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);

            //set customer specific
            foreach (var c in CustomerProps)
            {
                propMgr.Add3(c.PropertyName, (int)swCustomInfoType_e.swCustomInfoText, c.PropertyValue,
                    (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
            }

            Dirty = false;
            Model.SetSaveFlag();

            //loop through configs and set config specific properties
            if (!allCfg) return;
            if (Model.GetType() != (int)swDocumentTypes_e.swDocPART)
                if (Model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
                    return;
            var configs = (string[])Model.GetConfigurationNames();
            foreach (var configName in configs)
            {
                propMgr = Model.Extension.CustomPropertyManager[configName];
                propMgr.Add3(@"Document Number", (int)swCustomInfoType_e.swCustomInfoText, DocumentNum,
                    (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                propMgr.Add3(@"Part Number", (int)swCustomInfoType_e.swCustomInfoText, PartNum,
                    (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                propMgr.Add3(@"Description", (int)swCustomInfoType_e.swCustomInfoText, Description,
                    (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                propMgr.Add3(@"Number", (int)swCustomInfoType_e.swCustomInfoText, CustomerNum,
                    (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                propMgr.Add3(@"Opp", (int)swCustomInfoType_e.swCustomInfoText, Opportunity,
                    (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                propMgr.Add3(@"ProductCategory", (int)swCustomInfoType_e.swCustomInfoText, ProdCat,
                    (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                propMgr.Add3(@"Customer", (int)swCustomInfoType_e.swCustomInfoText, CustomerName, (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                propMgr.Add3(@"Revision", (int)swCustomInfoType_e.swCustomInfoText, Revision, (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);

                //set customer specific
                foreach (var c in CustomerProps)
                {
                    propMgr.Add3(c.PropertyName, (int)swCustomInfoType_e.swCustomInfoText, c.PropertyValue,
                        (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                }
            }
        }

        internal string GetComments()
        {
            var comments = string.Empty;
            try
            {
                if (Model == null) return comments;
                if (_comments == null) return comments;
                if (_comments.GetCommentCount() == 0) return comments;
                var cmts = (object[])_comments.GetComments();
                if (cmts == null) return comments;
                for (var i = 0; i < cmts.Length; i++)
                {
                    var cmt = (Comment)cmts[i];
                    if (i != 0)
                        comments += "\r\n\r\n";
                    comments += cmt.Text;
                }
            }
            catch (Exception e)
            {
                _frmStat.SetStat(0, @"Error retrieving comments: " + e.Message);
            }
            return comments;
        }

        internal void AddComment(string comment)
        {
            if (Model == null) return;
            if (_comments == null) return;
            if (comment.Length == 0) return;
            comment = System.Environment.UserName + ": " + comment;
            comment = DateTime.Now.ToString("MM/dd/yyyy hh:mm tt") + " " + comment;
            _comments.AddComment(comment);
            Model.FeatureManager.UpdateFeatureTree();
            Model.SetSaveFlag();
        }

        internal struct CustomerProp : IEquatable<CustomerProp>
        {
            internal string PropertyName { get; set; }

            internal string PropertyValue { get; set; }

            internal string PropertyLabel { get; set; }

            public bool Equals(CustomerProp other)
            {
                return PropertyName == other.PropertyName && PropertyValue == other.PropertyValue && PropertyLabel == other.PropertyLabel;
            }

            public override bool Equals(object obj)
            {
                return obj is CustomerProp other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = PropertyName != null ? PropertyName.GetHashCode() : 0;
                    hashCode = (hashCode * 397) ^ (PropertyValue != null ? PropertyValue.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (PropertyLabel != null ? PropertyLabel.GetHashCode() : 0);
                    return hashCode;
                }
            }
        }
    }
}