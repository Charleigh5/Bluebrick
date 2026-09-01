using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BlueBrick.UI.Tests
{
    [TestClass]
    public class CollapsePanelTests
    {
        [TestMethod]
        public void CollapseAndExpand_AreIdempotentRestoreHeightAndNotifyOncePerTransition()
        {
            using (var panel = new CollapsePanel())
            {
                panel.Size = new Size(280, 292);
                var expandedHeight = panel.Height;
                var transitionCount = 0;
                panel.ExpandedStateChanged += () => transitionCount++;

                var direction = GetDirectionIcon(panel);
                using (var expandedIcon = Clone(direction.Image))
                {
                    panel.Collapse();
                    var collapsedHeight = panel.Height;
                    Assert.IsTrue(panel.Collapsed, "Collapse must change the state.");
                    Assert.AreNotEqual(expandedHeight, collapsedHeight, "Collapse must reduce the panel height.");
                    Assert.AreEqual(1, transitionCount, "First collapse must notify exactly once.");
                    Assert.IsFalse(ImagesEqual(expandedIcon, direction.Image), "Collapse must reverse the direction arrow.");

                    panel.Collapse();
                    Assert.AreEqual(collapsedHeight, panel.Height, "Second collapse must be a no-op.");
                    Assert.AreEqual(1, transitionCount, "Second collapse must not notify.");

                    panel.Expand();
                    Assert.IsFalse(panel.Collapsed, "Expand must change the state.");
                    Assert.AreEqual(expandedHeight, panel.Height, "Expand must restore the prior expanded height.");
                    Assert.AreEqual(2, transitionCount, "First expand must notify exactly once.");
                    Assert.IsTrue(ImagesEqual(expandedIcon, direction.Image), "Expand must restore the direction arrow.");

                    panel.Expand();
                    Assert.AreEqual(expandedHeight, panel.Height, "Second expand must be a no-op.");
                    Assert.AreEqual(2, transitionCount, "Second expand must not notify.");
                }
            }
        }

        private static PictureBox GetDirectionIcon(CollapsePanel panel)
        {
            var field = typeof(CollapsePanel).GetField("picDirIcon", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "CollapsePanel must retain its native direction icon.");
            var icon = field.GetValue(panel) as PictureBox;
            Assert.IsNotNull(icon, "CollapsePanel direction icon must be available.");
            Assert.IsNotNull(icon.Image, "CollapsePanel direction icon must have an image.");
            return icon;
        }

        private static Bitmap Clone(Image image)
        {
            return new Bitmap(image);
        }

        private static bool ImagesEqual(Image expected, Image actual)
        {
            using (var expectedCopy = new Bitmap(expected))
            using (var actualCopy = new Bitmap(actual))
            {
                if (expectedCopy.Width != actualCopy.Width || expectedCopy.Height != actualCopy.Height)
                    return false;

                for (var x = 0; x < expectedCopy.Width; x++)
                {
                    for (var y = 0; y < expectedCopy.Height; y++)
                    {
                        if (expectedCopy.GetPixel(x, y) != actualCopy.GetPixel(x, y))
                            return false;
                    }
                }
                return true;
            }
        }
    }
}
