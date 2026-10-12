using System.Reflection;
using LumenDeck;

internal static partial class Program
{
    private static void CheckButtonAppearance(string renderDirectory)
    {
        static double Luminance(Color color)
        {
            static double Channel(byte value) => value / 255d <= 0.04045
                ? value / 255d / 12.92 : Math.Pow((value / 255d + 0.055) / 1.055, 2.4);
            return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
        }
        static double Ratio(Color a, Color b)
        {
            double x = Luminance(a), y = Luminance(b);
            return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
        }
        Assert(Ratio(Theme.InkFaint, Theme.CardTop) >= 4.5 && Ratio(Theme.InkMuted, Theme.CardHover) >= 4.5,
            "secondary text remains readable on the lightest material and hover surface");
        Assert(Ratio(Theme.OnAmber, Theme.Amber) >= 4.5 && Ratio(Theme.OnAmber, Theme.AmberPressed) >= 4.5 &&
            Ratio(Theme.AmberLight, Theme.CardHover) >= 3,
            "primary labels and primary/secondary focus indicators retain sufficient token contrast");
        foreach (float scale in new[] { 1f, 1.5f, 2f })
        {
            using var form = new PreviewForm { AutoScaleMode = AutoScaleMode.None, BackColor = Theme.Base };
            using var font = new Font(Theme.Body.FontFamily, Theme.Body.Size * scale);
            Size size = new((int)(200 * scale), (int)(40 * scale));
            using var primary = new FlatButton("Apply preset") { Size = size, Font = font, Primary = true };
            using var secondary = new FlatButton("Apply preset") { Size = size, Font = font };
            form.Controls.Add(primary);
            form.Controls.Add(secondary);
            form.Show();
            using var enabledPrimary = new Bitmap(size.Width, size.Height);
            primary.DrawToBitmap(enabledPrimary, new Rectangle(Point.Empty, size));
            using var enabledSecondary = new Bitmap(size.Width, size.Height);
            secondary.DrawToBitmap(enabledSecondary, new Rectangle(Point.Empty, size));
            primary.Enabled = secondary.Enabled = false;
            using var disabledPrimary = new Bitmap(size.Width, size.Height);
            primary.DrawToBitmap(disabledPrimary, new Rectangle(Point.Empty, size));
            using var disabledSecondary = new Bitmap(size.Width, size.Height);
            secondary.DrawToBitmap(disabledSecondary, new Rectangle(Point.Empty, size));
            bool Equal(Bitmap a, Bitmap b)
            {
                for (int y = 0; y < a.Height; y++)
                    for (int x = 0; x < a.Width; x++)
                        if (a.GetPixel(x, y) != b.GetPixel(x, y)) return false;
                return true;
            }
            if (renderDirectory != null)
            {
                Directory.CreateDirectory(renderDirectory);
                using var sheet = new Bitmap(size.Width + 170, (size.Height + 12) * 4 + 12);
                using var graphics = Graphics.FromImage(sheet);
                graphics.Clear(Theme.Base);
                using var ink = new SolidBrush(Theme.Ink);
                var rows = new[] { ("Enabled primary", enabledPrimary), ("Enabled secondary", enabledSecondary),
                    ("Disabled primary", disabledPrimary), ("Disabled secondary", disabledSecondary) };
                for (int row = 0; row < rows.Length; row++)
                {
                    int y = 8 + row * (size.Height + 12);
                    graphics.DrawString(rows[row].Item1, Theme.Body, ink, 8, y + 8);
                    graphics.DrawImageUnscaled(rows[row].Item2, 164, y);
                }
                sheet.Save(Path.Combine(renderDirectory, $"button-states-{scale}.png"));
            }
            Assert(Equal(disabledPrimary, disabledSecondary), "disabled primary and secondary buttons share readable native rendering");
            Assert(enabledPrimary.GetPixel(size.Width / 2, 3).ToArgb() == Theme.Amber.ToArgb(),
                "enabled primary retains its amber face");
            Assert(!Equal(enabledPrimary, disabledPrimary), "disabled primary remains visually distinct from its enabled action");
            typeof(FlatButton).GetMethod("OnMouseEnter", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(primary, [EventArgs.Empty]);
            using var disabledHover = new Bitmap(size.Width, size.Height);
            primary.DrawToBitmap(disabledHover, new Rectangle(Point.Empty, size));
            Assert(Equal(disabledPrimary, disabledHover), "disabled hover cannot advertise an available action");
            typeof(FlatButton).GetMethod("OnMouseEnter", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(secondary, [EventArgs.Empty]);
            using var disabledSecondaryHover = new Bitmap(size.Width, size.Height);
            secondary.DrawToBitmap(disabledSecondaryHover, new Rectangle(Point.Empty, size));
            Assert(Equal(disabledSecondary, disabledSecondaryHover), "disabled secondary hover retains its neutral border");
            primary.Enabled = true;
            typeof(FlatButton).GetMethod("OnMouseLeave", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(primary, [EventArgs.Empty]);
            typeof(FlatButton).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(primary, [new KeyEventArgs(Keys.Space)]);
            using var pressed = new Bitmap(size.Width, size.Height);
            primary.DrawToBitmap(pressed, new Rectangle(Point.Empty, size));
            Assert(pressed.GetPixel(size.Width / 2, 3).ToArgb() != enabledPrimary.GetPixel(size.Width / 2, 3).ToArgb(),
                "primary keyboard press has visible feedback distinct from its resting face");
            typeof(FlatButton).GetMethod("OnKeyUp", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(primary, [new KeyEventArgs(Keys.Space)]);
        }
    }
}
