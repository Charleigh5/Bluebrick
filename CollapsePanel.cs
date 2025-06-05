using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.Design;

namespace BlueBrick
{
    [Designer(typeof(CustDesigner))]
    public partial class CollapsePanel : UserControl
    {
        private string _title = "Title";
        private bool _collapsed;
        private int _height;

        public CollapsePanel()
        {
            InitializeComponent();
            _height = tlpMain.Height;
        }

        // Note: property added
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        // ReSharper disable once ConvertToAutoProperty
        public Panel Content //do not change to auto-property, breaks designer
        {
            // ReSharper disable once ArrangeAccessorOwnerBody
            get { return pnlContent; } //do not change to expression bodied property, breaks designer
        }

        public bool Collapsed => _collapsed;

        [Category("Collapse Panel Options")]
        [Browsable(true)]
        public string Title
        {
            get => _title;
            set
            {
                _title = value;
                lblTitle.Text = _title;
            }
        }

        private void lblTitle_Click(object sender, EventArgs e)
        {
            CollapseMe();
        }

        private void picDirIcon_Click(object sender, EventArgs e)
        {
            CollapseMe();
        }

        private void tlpTitleBar_Click(object sender, EventArgs e)
        {
            CollapseMe();
        }

        private void CollapseMe()
        {
            _collapsed = !_collapsed;
            TabStop = !TabStop;
            picDirIcon.Image.RotateFlip(RotateFlipType.Rotate180FlipNone);
            picDirIcon.Refresh();
            if (_collapsed)
            {
                TabStop = false;
                _height = Height;
                var iHeight = (int)(AutoScaleFactor.Height * tlpMain.RowStyles[0].Height);
                Height = iHeight + 1;
            }
            else
            {
                Height = _height;
            }
        }

        public void Collapse()
        {
            if (_collapsed) return;
            CollapseMe();
        }

        public void Expand()
        {
            if (!_collapsed) return;
            CollapseMe();
        }

        public void ProcTab(bool forward)
        {
            ProcessTabKey(forward);
        }
    }

    public class CustDesigner : ParentControlDesigner
    {
        public override void Initialize(IComponent comp)
        {
            base.Initialize(comp);
            var uc = (CollapsePanel)comp;
            EnableDesignMode(uc.Content, "Content");
        }
    }
}