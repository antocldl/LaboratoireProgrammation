using System.Windows.Media;

namespace LaboratoireProgrammation.Project.Helpers;

public class ColorHelper {
    public static Color FancyText = HexToColor("#fcfe4d");
    public static Color HoverFancyText = HexToColor("e3b946");
    public static Color HomeText = HexToColor("#ff9838");
    public static Color VisualText = HexToColor("#6fff59");
    public static Color Text = HexToColor("#ededed");
    public static Color UserInput = HexToColor("#6ee9ff");
    public static Color Information = HexToColor("#fdff6e");
    public static Color Warning = HexToColor("#ff8f4a");
    public static Color Critical = HexToColor("#c7312c");
    public static Color Success = HexToColor("#29e62c");
    public static Color Hospi = HexToColor("#00d4aa");

    public static Color FadeGreen = HexToColor("#4FF743");
    public static Color FadeRed = HexToColor("#F74343");
    public static Color Snow = HexToColor("#fcfeff");

    private static SolidColorBrush MakeFrozen(Color color) {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static SolidColorBrush _fancyTextBrush = MakeFrozen(FancyText);
    private static SolidColorBrush _hoverFancyTextBrush = MakeFrozen(HoverFancyText);
    private static SolidColorBrush _homeBrush = MakeFrozen(HomeText);
    private static SolidColorBrush _visualBrush = MakeFrozen(VisualText);
    private static SolidColorBrush _textBrush = MakeFrozen(Text);
    private static SolidColorBrush _userInputBrush = MakeFrozen(UserInput);
    private static SolidColorBrush _informationBrush = MakeFrozen(Information);
    private static SolidColorBrush _warningBrush = MakeFrozen(Warning);
    private static SolidColorBrush _criticalBrush = MakeFrozen(Critical);
    private static SolidColorBrush _successBrush = MakeFrozen(Success);
    private static SolidColorBrush _fadeGreenBrush = MakeFrozen(FadeGreen);
    private static SolidColorBrush _fadeRedBrush = MakeFrozen(FadeRed);
    private static SolidColorBrush _snowBrush = MakeFrozen(Snow);
    private static SolidColorBrush _hospiBrush = MakeFrozen(Hospi);

    public static SolidColorBrush FancyTextBrush {
        get => _fancyTextBrush;
        set {
            FancyText = value.Color;
            _fancyTextBrush = MakeFrozen(FancyText);
        }
    }

    public static SolidColorBrush HoverFancyTextBrush {
        get => _hoverFancyTextBrush;
        set {
            HoverFancyText = value.Color;
            _hoverFancyTextBrush = MakeFrozen(HoverFancyText);
        }
    }

    public static SolidColorBrush HomeBrush {
        get => _homeBrush;
        set {
            HomeText = value.Color;
            _homeBrush = MakeFrozen(HomeText);
        }
    }

    public SolidColorBrush VisualBrush {
        get => _visualBrush;
        set {
            VisualText = value.Color;
            _visualBrush = MakeFrozen(VisualText);
        }
    }

    public static SolidColorBrush TextBrush {
        get => _textBrush;
        set {
            Text = value.Color;
            _textBrush = MakeFrozen(Text);
        }
    }

    public static SolidColorBrush UserInputBrush {
        get => _userInputBrush;
        set {
            UserInput = value.Color;
            _userInputBrush = MakeFrozen(UserInput);
        }
    }

    public static SolidColorBrush InformationBrush {
        get => _informationBrush;
        set {
            Information = value.Color;
            _informationBrush = MakeFrozen(Information);
        }
    }

    public static SolidColorBrush WarningBrush {
        get => _warningBrush;
        set {
            Warning = value.Color;
            _warningBrush = MakeFrozen(Warning);
        }
    }

    public static SolidColorBrush CriticalBrush {
        get => _criticalBrush;
        set {
            Critical = value.Color;
            _criticalBrush = MakeFrozen(Critical);
        }
    }

    public static SolidColorBrush SuccessBrush {
        get => _successBrush;
        set {
            Success = value.Color;
            _successBrush = MakeFrozen(Success);
        }
    }

    public static SolidColorBrush FadeGreenBrush {
        get => _fadeGreenBrush;
        set {
            FadeGreen = value.Color;
            _fadeGreenBrush = MakeFrozen(FadeGreen);
        }
    }

    public static SolidColorBrush FadeRedBrush {
        get => _fadeRedBrush;
        set {
            FadeRed = value.Color;
            _fadeRedBrush = MakeFrozen(FadeRed);
        }
    }

    public static SolidColorBrush SnowBrush {
        get => _snowBrush;
        set {
            Snow = value.Color;
            _snowBrush = MakeFrozen(Snow);
        }
    }
    
    public static SolidColorBrush HospiBrush {
        get => _hospiBrush;
        set {
            Hospi = value.Color;
            _hospiBrush = MakeFrozen(Hospi);
        }
    }


    public static Color HexToColor(string hex) {
        hex = hex.Replace("#", "");

        if (hex.Length == 6)
            hex = "FF" + hex;

        return Color.FromArgb(
            Convert.ToByte(hex.Substring(0, 2), 16), // A
            Convert.ToByte(hex.Substring(2, 2), 16), // R
            Convert.ToByte(hex.Substring(4, 2), 16), // G
            Convert.ToByte(hex.Substring(6, 2), 16) // B
        );
    }

    public static System.Drawing.Color HexToColorSystem(string hex) {
        hex = hex.Replace("#", "");

        if (hex.Length == 6)
            hex = "FF" + hex;

        return System.Drawing.Color.FromArgb(
            Convert.ToInt32(hex.Substring(0, 2), 16), // A
            Convert.ToInt32(hex.Substring(2, 2), 16), // R
            Convert.ToInt32(hex.Substring(4, 2), 16), // G
            Convert.ToInt32(hex.Substring(6, 2), 16) // B
        );
    }

    public static SolidColorBrush HexToColorBrush(string hex) {
        return new SolidColorBrush(HexToColor(hex));
    }

    public static SolidColorBrush generateRandomColor() {
        var random = new Random();
        return new SolidColorBrush(Color.FromArgb((byte)random.Next(0, 256), (byte)random.Next(0, 256),
            (byte)random.Next(0, 256), 0xFF));
    }

    public static SolidColorBrush GetLighter(Brush? brush, int amount) {
        if (brush is SolidColorBrush scb) {
            var color = scb.Color;
            var a = (byte)(color.A - (byte)(255 * (amount / 100.0)));
            var r = (byte)(color.R + (byte)(255 * (amount / 100.0)));
            var g = (byte)(color.G + (byte)(255 * (amount / 100.0)));
            var b = (byte)(color.B + (byte)(255 * (amount / 100.0)));
            return new SolidColorBrush(Color.FromArgb(a, Math.Min(a, r), Math.Min(a, g), Math.Min(a, b)));
        }

        return new SolidColorBrush(Colors.Transparent);
    }
}