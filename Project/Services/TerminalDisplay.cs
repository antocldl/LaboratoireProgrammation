using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using LaboratoireProgrammation.Project.Helpers;
using LaboratoireProgrammation.Project.ViewModels.Menu;

namespace LaboratoireProgrammation.Project.Services;

public class TerminalDisplay {
    public static RichTextBox? _outputBox;
    private static TextBox? _inputBox;
    private static bool _initialized;

    private static readonly Color TerminalGreen = Color.FromArgb(255, 51, 255, 51);

    private static readonly FontFamily TerminalFont =
        new(new Uri("pack://application:,,,/LaboratoireProgrammation;component/"), "/Assets/fonts/#overseer");

    public static void Init(RichTextBox outp, TextBox inp) {
        if (_outputBox != null && _inputBox != null) return;
        _initialized = true;
        _outputBox = outp;
        _inputBox = inp;
    }

    private const int MaxBlocks = 150;
    private static readonly Typeface _terminalTypeface = new(TerminalFont, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

    private static void TrimExcessBlocks() {
        if (_outputBox == null) return;
        while (_outputBox.Document.Blocks.Count > MaxBlocks) {
            _outputBox.Document.Blocks.Remove(_outputBox.Document.Blocks.FirstBlock);
        }
    }

    public static void AppendOutput(string text, Color? hexColor = null) {
        if (!_initialized) return;
        var color = hexColor ?? TerminalGreen;

        var run = new Run($"{DateTime.Now:HH:mm:ss} | {text}") {
            Foreground = new SolidColorBrush(color)
        };

        var para = new Paragraph(run);
        TrimExcessBlocks();
        _outputBox!.Document.Blocks.Add(para);
        _outputBox.ScrollToEnd();
    }

    public static void AppendOutputOnSameLine(string text, Color? hexColor = null) {
        if (!_initialized) return;
        var color = hexColor ?? TerminalGreen;

        var run = new Run(text) { Foreground = new SolidColorBrush(color) };

        var para = new Paragraph();
        para.Inlines.Add(run);
        if (_outputBox!.Document.Blocks.Count > 0) {
            var lastPara = _outputBox.Document.Blocks.LastBlock as Paragraph;
            if (lastPara != null) lastPara.Inlines.Add(run);
        }
        else {
            TrimExcessBlocks();
            _outputBox.Document.Blocks.Add(para);
        }

        _outputBox.ScrollToEnd();
    }

    public static void SeparationLine() {
        if (!_initialized || _outputBox == null) return;

        if (!_outputBox.CheckAccess()) {
            _outputBox.Dispatcher.Invoke(SeparationLine);
            return;
        }

        var width = _outputBox.ActualWidth;
        if (width <= 0) {
            RoutedEventHandler? loadedHandler = null;
            loadedHandler = (s, e) => {
                _outputBox.Loaded -= loadedHandler;
                SeparationLine();
            };
            _outputBox.Loaded += loadedHandler;
            return;
        }

        var availableWidth =
            width - (_outputBox.Document.PagePadding.Left + _outputBox.Document.PagePadding.Right + 30);

        var formattedText = new FormattedText(
            "- ",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            _terminalTypeface,
            _outputBox.FontSize,
            Brushes.Black,
            VisualTreeHelper.GetDpi(_outputBox).PixelsPerDip
        );

        var charCount = (int)(availableWidth / formattedText.Width);
        if (charCount <= 0) charCount = 50;

        var line = string.Concat(Enumerable.Repeat("- ", Math.Max(1, charCount / 2)));
        var run = new Run(line) {
            FontFamily = TerminalFont,
            FontSize = _outputBox.FontSize,
            Foreground = ColorHelper.FancyTextBrush
        };

        var para = new Paragraph(run) {
            Margin = new Thickness(0),
            Padding = new Thickness(0)
        };

        TrimExcessBlocks();
        _outputBox.Document.Blocks.Add(para);
        _outputBox.ScrollToEnd();
    }

    public static void BlankSpace() {
        if (!_initialized) return;
        var run = new Run("");
        var para = new Paragraph(run);
        _outputBox!.Document.Blocks.Add(para);
        _outputBox!.ScrollToEnd();
    }

    public static void ModifyFontSize(int delta) {
        if (!_initialized) return;
        var newSize = Math.Max(_outputBox.FontSize + delta, 8);
        _outputBox.FontSize = newSize;
        _inputBox!.FontSize = newSize;
    }

    public static TextBlock CreateTitleBlock(string text, double size, Brush color) {
        if (!_initialized) throw new NullReferenceException("TerminalDisplay not initialized");

        return new TextBlock {
            Text = text,
            FontSize = size * 1.5,
            FontWeight = FontWeights.Regular,
            FontFamily = TerminalFont,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = color,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    public static TextBlock CreateTitleBlock(string text, double size, Brush color, double yOffset) {
        if (!_initialized) throw new NullReferenceException("TerminalDisplay not initialized");

        return new TextBlock {
            Text = text,
            FontSize = size * 1.5,
            FontWeight = FontWeights.Regular,
            FontFamily = TerminalFont,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = color,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, yOffset, 0, 0)
        };
    }

    public static void HideDisplay(MainWindow win) {
        _outputBox!.Visibility = Visibility.Collapsed;
        _inputBox!.Visibility = Visibility.Collapsed;

        win.TerminalOutputPanel.Visibility = Visibility.Collapsed;
    }

    public static void ShowDisplay() {
        _outputBox!.Visibility = Visibility.Visible;
        _inputBox!.Visibility = Visibility.Visible;
    }
}