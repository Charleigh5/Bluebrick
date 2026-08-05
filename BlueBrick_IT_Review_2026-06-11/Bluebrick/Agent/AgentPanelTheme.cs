using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

namespace BlueBrick.Agent
{
    internal static class AgentPanelTheme
    {
        public static readonly AgentPanelThemeInstance Current = new AgentPanelThemeInstance();

        public static readonly Color Base = ColorTranslator.FromHtml("#0F1418");
        public static readonly Color BaseAlt = ColorTranslator.FromHtml("#161D24");
        public static readonly Color Surface = ColorTranslator.FromHtml("#1E2731");
        public static readonly Color SurfaceAlt = ColorTranslator.FromHtml("#252F3A");
        public static readonly Color Accent = ColorTranslator.FromHtml("#C46B3A");
        public static readonly Color AccentAlt = ColorTranslator.FromHtml("#3BA7A4");
        public static readonly Color TextPrimary = ColorTranslator.FromHtml("#E6ECF2");
        public static readonly Color TextSecondary = ColorTranslator.FromHtml("#AAB6C3");
        public static readonly Color StatusSuccess = ColorTranslator.FromHtml("#3BA7A4");
        public static readonly Color StatusWarning = ColorTranslator.FromHtml("#D9A441");
        public static readonly Color StatusError = ColorTranslator.FromHtml("#D15C4D");

        public static Font HeaderFont(float size = 20f, FontStyle style = FontStyle.Bold)
        {
            return new Font(GetFontFamily("Space Grotesk", "Segoe UI"), size, style);
        }

        public static Font BodyFont(float size = 12f, FontStyle style = FontStyle.Regular)
        {
            return new Font(GetFontFamily("IBM Plex Sans", "Segoe UI"), size, style);
        }

        private static FontFamily GetFontFamily(string preferred, string fallback)
        {
            // Delegate to the new font loader which handles:
            // 1. Private fonts loaded from TTF files
            // 2. System-installed fonts
            // 3. Fallback fonts
            return AgentFontLoader.GetFamily(preferred, fallback);
        }

        public static void ApplyPanel(Control control)
        {
            control.BackColor = Surface;
            control.ForeColor = TextPrimary;
            control.Font = BodyFont();
        }

        public static void ApplyHeaderLabel(Label label)
        {
            label.ForeColor = TextPrimary;
            label.Font = HeaderFont(18f, FontStyle.Bold);
        }

        public static void ApplySubtleLabel(Label label)
        {
            label.ForeColor = TextSecondary;
            label.Font = BodyFont(9.5f, FontStyle.Regular);
        }

        public static void ApplyTextBox(TextBox box)
        {
            box.BorderStyle = BorderStyle.FixedSingle;
            box.BackColor = SurfaceAlt;
            box.ForeColor = TextPrimary;
            box.Font = BodyFont(10.5f, FontStyle.Regular);
        }

        public static void ApplyListView(ListView list)
        {
            list.BorderStyle = BorderStyle.None;
            list.BackColor = Surface;
            list.ForeColor = TextPrimary;
            list.Font = BodyFont(9.5f, FontStyle.Regular);
            list.HideSelection = false;
            list.FullRowSelect = true;
        }

        public static void ApplyNavButton(Button button, bool active)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Height = 40;
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.Padding = new Padding(12, 0, 0, 0);
            button.Font = BodyFont(11f, FontStyle.Bold);
            button.BackColor = active ? SurfaceAlt : Surface;
            button.ForeColor = active ? TextPrimary : TextSecondary;
        }

        public static void ApplyPill(Button button, bool active)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Height = 28;
            button.Width = 70;
            button.Font = BodyFont(9.5f, FontStyle.Bold);
            button.BackColor = active ? Accent : SurfaceAlt;
            button.ForeColor = active ? TextPrimary : TextSecondary;
        }

        public static void ApplyPrimaryButton(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Height = 32;
            button.BackColor = Accent;
            button.ForeColor = TextPrimary;
            button.Font = BodyFont(10.5f, FontStyle.Bold);
        }

        public static void ApplySecondaryButton(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Height = 32;
            button.BackColor = SurfaceAlt;
            button.ForeColor = TextPrimary;
            button.Font = BodyFont(10.5f, FontStyle.Bold);
        }
    }

    internal class AgentPanelThemeInstance
    {
        public Color BackgroundColor => AgentPanelTheme.Base;
        public Color SurfaceColor => AgentPanelTheme.Surface;
        public Font BodyFont => AgentPanelTheme.BodyFont();
        public Font HeaderFont => AgentPanelTheme.HeaderFont();
    }
}
