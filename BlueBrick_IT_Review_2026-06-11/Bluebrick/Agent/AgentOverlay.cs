using System;
using System.Drawing;
using System.Windows.Forms;

namespace BlueBrick.Agent
{
    internal class AgentOverlay : Form
    {
        internal AgentOverlay(Color color)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.LimeGreen;
            TransparencyKey = Color.LimeGreen;
            WindowState = FormWindowState.Maximized;
            Opacity = 0.6;
            _borderColor = color;
        }

        private readonly Color _borderColor;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(_borderColor, 8))
            {
                var rect = new Rectangle(4, 4, Width - 8, Height - 8);
                e.Graphics.DrawRectangle(pen, rect);
            }
        }

        internal void ShowOverlay()
        {
            if (!Visible) Show();
        }

        internal void HideOverlay()
        {
            if (Visible) Hide();
        }
    }
}
