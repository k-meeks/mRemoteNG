using System;
using System.Drawing;
using System.Windows.Forms;
using mRemoteNG.Properties;

namespace mRemoteNG.Themes
{
    /// <summary>
    /// Applies the user-configured interface font (Options > Appearance)
    /// across open forms, controls and menus, without requiring a restart.
    /// </summary>
    public static class UIFontManager
    {
        public static Font Font
        {
            get
            {
                FontStyle style;
                if (!Enum.TryParse(OptionsAppearancePage.Default.UIFontStyle, out style))
                {
                    style = FontStyle.Regular;
                }

                return new Font(OptionsAppearancePage.Default.UIFontFamily, OptionsAppearancePage.Default.UIFontSize, style);
            }
        }

        public static void ApplyToOpenForms()
        {
            Font font = Font;

            foreach (Form form in Application.OpenForms)
            {
                Apply(form, font);
            }
        }

        public static void Apply(Control control)
        {
            Apply(control, Font);
        }

        private static void Apply(Control control, Font font)
        {
            ApplyFont(control, font);

            foreach (Control child in control.Controls)
            {
                Apply(child, font);
            }

            if (control is ToolStrip toolStrip)
            {
                Apply(toolStrip.Items, font);
            }
        }

        private static void Apply(ToolStripItemCollection items, Font font)
        {
            foreach (ToolStripItem item in items)
            {
                ApplyFont(item, font);

                if (item is ToolStripDropDownItem dropDownItem)
                {
                    Apply(dropDownItem.DropDownItems, font);
                }
            }
        }

        private static void ApplyFont(Control control, Font font)
        {
            if (FontsMatch(control.Font, font)) return;
            control.Font = font;
        }

        private static void ApplyFont(ToolStripItem item, Font font)
        {
            if (FontsMatch(item.Font, font)) return;
            item.Font = font;
        }

        private static bool FontsMatch(Font a, Font b)
        {
            return a.FontFamily.Equals(b.FontFamily)
                && a.Style == b.Style
                && Math.Abs(a.Size - b.Size) < 0.01f;
        }
    }
}
