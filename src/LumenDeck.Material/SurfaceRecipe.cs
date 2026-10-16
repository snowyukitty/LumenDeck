using System.Windows.Media;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("LumenDeck.AppTests")]

namespace LumenDeck.Material;

/// <summary>Authored overlays, separate from the Windows compositor's blur/tint policy.</summary>
internal sealed record SurfaceRecipe(string Foundation, string Top, string Bottom)
{
    internal static SurfaceRecipe For(bool dark, bool ceramic, bool native, bool glass) =>
        ceramic ? new("#F1EEE7", "#FFFCFAF6", "#FFF8F5EF") :
        !native ? dark ? new("#20262F", "#FF2D323B", "#FF24282F") : new("#E2E9EF", "#FFFFFFFF", "#FFEFF3F5") :
        glass ? dark ? new("#B3141A23", "#592D323B", "#8524282F") : new("#CCEDF2F8", "#4DFFFFFF", "#8CEFF3F5") :
        dark ? new("#30141A23", "#C22D323B", "#D624282F") : new("#38E9EEF3", "#D9FFFFFF", "#E6EFF3F5");

    internal LinearGradientBrush Surface => new(
        (Color)ColorConverter.ConvertFromString(Top), (Color)ColorConverter.ConvertFromString(Bottom), 90);
}
