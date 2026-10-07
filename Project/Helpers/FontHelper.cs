using System.Windows.Media;

namespace LaboratoireProgrammation.Project.Helpers;

public class FontHelper {
    private static readonly Lazy<FontFamily[]> _cachedFonts = new(() => Fonts.SystemFontFamilies.ToArray());
    private static readonly Random _random = new();

    public static FontFamily getRandomFont() {
        var installedFonts = _cachedFonts.Value;
        if (installedFonts.Length == 0) return new FontFamily("Arial");
        var randomIndex = _random.Next(installedFonts.Length);
        return installedFonts[randomIndex];
    }
}