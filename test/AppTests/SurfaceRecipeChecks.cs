using LumenDeck.Material;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

internal static partial class Program
{
    private static void CheckSurfaceRecipes()
    {
        static Color Parse(string value) => (Color)ColorConverter.ConvertFromString(value);
        static Color Over(Color top, Color bottom)
        {
            double a = top.A / 255.0;
            return Color.FromRgb((byte)Math.Round(top.R * a + bottom.R * (1 - a)),
                (byte)Math.Round(top.G * a + bottom.G * (1 - a)), (byte)Math.Round(top.B * a + bottom.B * (1 - a)));
        }
        static double Luminance(Color c)
        {
            static double Linear(byte b) { double n = b / 255.0; return n <= .04045 ? n / 12.92 : Math.Pow((n + .055) / 1.055, 2.4); }
            return .2126 * Linear(c.R) + .7152 * Linear(c.G) + .0722 * Linear(c.B);
        }
        static double Ratio(Color a, Color b) => (Math.Max(Luminance(a), Luminance(b)) + .05) / (Math.Min(Luminance(a), Luminance(b)) + .05);
        foreach (bool dark in new[] { false, true })
        {
            var glass = SurfaceRecipe.For(dark, false, true, true);
            var foundation = Parse(glass.Foundation);
            double retained = (1 - foundation.A / 255.0) * (1 - Parse(glass.Top).A / 255.0);
            Assert(retained > (dark ? .18 : .13), "glass leaves a measured encoded-alpha budget through the content plane");
            foreach (var background in new[] { Colors.Black, Colors.White, Colors.Red, Colors.Lime, Colors.Blue, Colors.Cyan, Colors.Magenta, Colors.Yellow })
            {
                var basis = Over(foundation, background);
                foreach (string ink in dark ? new[] { "#F0F1F3", "#D4DCE7", "#FFD49C" } : new[] { "#252A30", "#3F4A56", "#684516" })
                    Assert(Ratio(Parse(ink), basis) >= 4.5, "glass foundation text survives extreme compositor colors in encoded-alpha preflight");
                foreach (var stop in new[] { glass.Top, glass.Bottom })
                {
                    var card = Over(Parse(stop), basis);
                    Assert(Ratio(Parse(dark ? "#D4DCE7" : "#3F4A56"), card) >= 4.5,
                        "glass secondary card text survives extreme compositor colors in encoded-alpha preflight");
                    Assert(Ratio(Parse(dark ? "#959FAE" : "#65727F"), card) >= 3,
                        "glass slider track has non-text contrast in encoded-alpha preflight");
                }
            }
            foreach (bool ceramic in new[] { false, true })
            {
                var solid = SurfaceRecipe.For(dark, ceramic, false, true);
                Assert(new[] { solid.Foundation, solid.Top, solid.Bottom }.All(s => Parse(s).A == 255),
                    "fallback and ceramic surfaces remain opaque without a compositor");
            }
        }
    }
}
