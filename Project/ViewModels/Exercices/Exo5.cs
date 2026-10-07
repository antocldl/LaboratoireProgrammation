using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using LaboratoireProgrammation.Project.Helpers;
using LaboratoireProgrammation.Project.Views.Exercices;
using Microsoft.Win32;

namespace LaboratoireProgrammation.Project.ViewModels.Exercices;

public partial class Exo5 : UserControl {
    private string _currentFilePath = string.Empty;
    private Color _currentFontColor;
    private Color _currentHighlightColor = Colors.Yellow;
    private double _currentLineHeight = double.NaN;
    private bool _hasUnsavedChanges;
    private bool _isLightMode;
    private bool _isUpdatingToolbar;

    public Exo5() {
        InitializeComponent();
        _currentFontColor = ColorHelper.FancyText;
        Loaded += (s, e) => {
            InitFonts();
            InitSizes();
            Editor.Focus();
        };
    }

    private async void InitFonts() {
        var fonts = new List<string>
            { "Ubuntu Mono", "Consolas", "Courier New", "Arial", "Calibri", "Times New Roman" };
        FontFamilyCombo.ItemsSource = fonts;
        FontFamilyCombo.SelectedItem = "Consolas";

        var systemFonts = await Task.Run(() => Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(f => f).ToList());
        foreach (var sf in systemFonts)
            if (!fonts.Contains(sf))
                fonts.Add(sf);
        FontFamilyCombo.ItemsSource = null;
        FontFamilyCombo.ItemsSource = fonts;
        FontFamilyCombo.SelectedItem = "Consolas";
    }

    private void InitSizes() {
        var sizes = new List<double> { 8, 9, 10, 11, 12, 14, 16, 18, 20, 22, 24, 28, 36, 48, 72 };
        FontSizeCombo.ItemsSource = sizes;
        FontSizeCombo.SelectedItem = 16.0;
    }

    private void NewCmdExecuted(object sender, ExecutedRoutedEventArgs e) {
        NewFileClick(null, null);
    }

    private void OpenCmdExecuted(object sender, ExecutedRoutedEventArgs e) {
        OpenFileClick(null, null);
    }

    private void SaveCmdExecuted(object sender, ExecutedRoutedEventArgs e) {
        SaveFileClick(null, null);
    }

    private void NewFileClick(object sender, RoutedEventArgs? e) {
        if (!EnsureSaved()) return;
        Editor.Document.Blocks.Clear();
        _currentFilePath = string.Empty;
        _hasUnsavedChanges = false;
        UpdateStatus("[ NEW ] : Nouveau document");
    }

    private void OpenFileClick(object sender, RoutedEventArgs? e) {
        if (!EnsureSaved()) return;
        var ofd = new OpenFileDialog {
            Filter = "Rich Text Format (*.rtf)|*.rtf|Texte brut (*.txt)|*.txt|Tous les fichiers (*.*)|*.*",
            DefaultExt = ".rtf"
        };
        if (ofd.ShowDialog() != true) return;
        var range = new TextRange(Editor.Document.ContentStart, Editor.Document.ContentEnd);
        using var fs = new FileStream(ofd.FileName, FileMode.Open);
        var fmt = ofd.FileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
            ? DataFormats.Text
            : DataFormats.Rtf;
        range.Load(fs, fmt);
        _currentFilePath = ofd.FileName;
        _hasUnsavedChanges = false;
        UpdateStatus($"[ OPEN ] : {Path.GetFileName(_currentFilePath)}");
    }

    private void SaveFileClick(object sender, RoutedEventArgs? e) {
        if (string.IsNullOrEmpty(_currentFilePath)) {
            SaveAsFileClick(sender, e);
            return;
        }

        CommitSave(_currentFilePath);
    }

    private void SaveAsFileClick(object sender, RoutedEventArgs? e) {
        var sfd = new SaveFileDialog {
            Filter = "Rich Text Format (*.rtf)|*.rtf|Texte brut (*.txt)|*.txt",
            DefaultExt = ".rtf"
        };
        if (sfd.ShowDialog() != true) return;
        _currentFilePath = sfd.FileName;
        CommitSave(_currentFilePath);
    }

    private void CommitSave(string path) {
        var range = new TextRange(Editor.Document.ContentStart, Editor.Document.ContentEnd);
        using var fs = new FileStream(path, FileMode.Create);
        var fmt = path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ? DataFormats.Text : DataFormats.Rtf;
        range.Save(fs, fmt);
        _hasUnsavedChanges = false;
        UpdateStatus($"[ SAVED ] : {Path.GetFileName(path)}");
    }

    private void PrintClick(object sender, RoutedEventArgs e) {
        var pd = new PrintDialog();
        if (pd.ShowDialog() != true) return;
        var docPaginator = ((IDocumentPaginatorSource)Editor.Document).DocumentPaginator;
        pd.PrintDocument(docPaginator, "Exo5 - Document");
    }

    private bool EnsureSaved() {
        if (!_hasUnsavedChanges) return true;
        var result = MessageBox.Show(
            "Des modifications non sauvegardées existent. Voulez-vous sauvegarder ?",
            "Attention",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);
        if (result == MessageBoxResult.Cancel) return false;
        if (result == MessageBoxResult.Yes) SaveFileClick(null, null);
        return true;
    }

    private void Editor_TextChanged(object sender, TextChangedEventArgs e) {
        _hasUnsavedChanges = true;
        UpdateWordCount();
    }

    private void UpdateWordCount() {
        var text = new TextRange(Editor.Document.ContentStart, Editor.Document.ContentEnd).Text;
        var words = text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
        var chars = text.Replace("\r", "").Replace("\n", "").Length;
        if (WordCountText != null)
            WordCountText.Text = $"Mots: {words}  |  Caractères: {chars}";
    }

    private void UpdateStatus(string msg) {
        if (StatusText != null) StatusText.Text = msg;
    }

    private void Editor_SelectionChanged(object sender, RoutedEventArgs e) {
        if (_isUpdatingToolbar) return;
        _isUpdatingToolbar = true;

        var font = Editor.Selection.GetPropertyValue(TextElement.FontFamilyProperty);
        if (font != DependencyProperty.UnsetValue && font is FontFamily ff)
            FontFamilyCombo.SelectedItem = ff.Source;

        var size = Editor.Selection.GetPropertyValue(TextElement.FontSizeProperty);
        if (size != DependencyProperty.UnsetValue)
            FontSizeCombo.Text = size.ToString();

        BtnBold.IsChecked = Editor.Selection.GetPropertyValue(TextElement.FontWeightProperty) is FontWeight fw &&
                            fw == FontWeights.Bold;
        BtnItalic.IsChecked = Editor.Selection.GetPropertyValue(TextElement.FontStyleProperty) is FontStyle fs &&
                              fs == FontStyles.Italic;

        var decorations = Editor.Selection.GetPropertyValue(Inline.TextDecorationsProperty) as TextDecorationCollection;
        BtnUnderline.IsChecked =
            decorations != null && decorations.Any(d => d.Location == TextDecorationLocation.Underline);
        BtnStrike.IsChecked = decorations != null &&
                              decorations.Any(d => d.Location == TextDecorationLocation.Strikethrough);

        var alignment = Editor.Selection.GetPropertyValue(Block.TextAlignmentProperty);
        BtnAlignLeft.IsChecked = alignment is TextAlignment taL && taL == TextAlignment.Left;
        BtnAlignCenter.IsChecked = alignment is TextAlignment taC && taC == TextAlignment.Center;
        BtnAlignRight.IsChecked = alignment is TextAlignment taR && taR == TextAlignment.Right;
        BtnAlignJustify.IsChecked = alignment is TextAlignment taJ && taJ == TextAlignment.Justify;

        _isUpdatingToolbar = false;
    }

    private void FontFamilyCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) {
        if (_isUpdatingToolbar || FontFamilyCombo.SelectedItem == null) return;
        Editor.Selection.ApplyPropertyValue(TextElement.FontFamilyProperty,
            new FontFamily(FontFamilyCombo.SelectedItem.ToString()));
        Editor.Focus();
    }

    private void FontSizeCombo_TextChanged(object sender, TextChangedEventArgs e) {
        if (_isUpdatingToolbar) return;
        if (double.TryParse((string?)FontSizeCombo.Text, out var size) && size > 0)
            Editor.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, size);
    }

    private void FontSizeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) {
        if (_isUpdatingToolbar || FontSizeCombo.SelectedItem == null) return;
        Editor.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, FontSizeCombo.SelectedItem);
        Editor.Focus();
    }

    private void IncreaseFontSizeClick(object sender, RoutedEventArgs e) {
        if (double.TryParse((string?)FontSizeCombo.Text, out var size)) {
            FontSizeCombo.Text = (size + 2).ToString();
            Editor.Focus();
        }
    }

    private void DecreaseFontSizeClick(object sender, RoutedEventArgs e) {
        if (double.TryParse((string?)FontSizeCombo.Text, out var size) && size > 2) {
            FontSizeCombo.Text = (size - 2).ToString();
            Editor.Focus();
        }
    }

    private void BoldClick(object sender, RoutedEventArgs e) {
        var current = Editor.Selection.GetPropertyValue(TextElement.FontWeightProperty);
        var newWeight = current is FontWeight fw && fw == FontWeights.Bold ? FontWeights.Normal : FontWeights.Bold;
        Editor.Selection.ApplyPropertyValue(TextElement.FontWeightProperty, newWeight);
        Editor.Focus();
    }

    private void ItalicClick(object sender, RoutedEventArgs e) {
        var current = Editor.Selection.GetPropertyValue(TextElement.FontStyleProperty);
        var newStyle = current is FontStyle fs && fs == FontStyles.Italic ? FontStyles.Normal : FontStyles.Italic;
        Editor.Selection.ApplyPropertyValue(TextElement.FontStyleProperty, newStyle);
        Editor.Focus();
    }

    private void UnderlineClick(object sender, RoutedEventArgs e) {
        var current = Editor.Selection.GetPropertyValue(Inline.TextDecorationsProperty) as TextDecorationCollection;
        var hasUnderline = current != null && current.Any(d => d.Location == TextDecorationLocation.Underline);
        Editor.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty,
            hasUnderline ? null : TextDecorations.Underline);
        Editor.Focus();
    }

    private void StrikeClick(object sender, RoutedEventArgs e) {
        var current = Editor.Selection.GetPropertyValue(Inline.TextDecorationsProperty) as TextDecorationCollection;
        var hasStrike = current != null && current.Any(d => d.Location == TextDecorationLocation.Strikethrough);
        Editor.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty,
            hasStrike ? null : TextDecorations.Strikethrough);
        Editor.Focus();
    }

    private void SuperscriptClick(object sender, RoutedEventArgs e) {
        var current = Editor.Selection.GetPropertyValue(Inline.BaselineAlignmentProperty);
        var newAlign = current is BaselineAlignment ba && ba == BaselineAlignment.Superscript
            ? BaselineAlignment.Baseline
            : BaselineAlignment.Superscript;
        Editor.Selection.ApplyPropertyValue(Inline.BaselineAlignmentProperty, newAlign);
        Editor.Focus();
    }

    private void SubscriptClick(object sender, RoutedEventArgs e) {
        var current = Editor.Selection.GetPropertyValue(Inline.BaselineAlignmentProperty);
        var newAlign = current is BaselineAlignment ba && ba == BaselineAlignment.Subscript
            ? BaselineAlignment.Baseline
            : BaselineAlignment.Subscript;
        Editor.Selection.ApplyPropertyValue(Inline.BaselineAlignmentProperty, newAlign);
        Editor.Focus();
    }

    private void AlignLeftClick(object sender, RoutedEventArgs e) {
        Editor.Selection.ApplyPropertyValue(Block.TextAlignmentProperty, TextAlignment.Left);
        Editor.Focus();
    }

    private void AlignCenterClick(object sender, RoutedEventArgs e) {
        Editor.Selection.ApplyPropertyValue(Block.TextAlignmentProperty, TextAlignment.Center);
        Editor.Focus();
    }

    private void AlignRightClick(object sender, RoutedEventArgs e) {
        Editor.Selection.ApplyPropertyValue(Block.TextAlignmentProperty, TextAlignment.Right);
        Editor.Focus();
    }

    private void AlignJustifyClick(object sender, RoutedEventArgs e) {
        Editor.Selection.ApplyPropertyValue(Block.TextAlignmentProperty, TextAlignment.Justify);
        Editor.Focus();
    }

    private void IncreaseIndentClick(object sender, RoutedEventArgs e) {
        EditingCommands.IncreaseIndentation.Execute(null, Editor);
        Editor.Focus();
    }

    private void DecreaseIndentClick(object sender, RoutedEventArgs e) {
        EditingCommands.DecreaseIndentation.Execute(null, Editor);
        Editor.Focus();
    }

    private void BulletListClick(object sender, RoutedEventArgs e) {
        EditingCommands.ToggleBullets.Execute(null, Editor);
        Editor.Focus();
    }

    private void NumberListClick(object sender, RoutedEventArgs e) {
        EditingCommands.ToggleNumbering.Execute(null, Editor);
        Editor.Focus();
    }

    private void FontColorClick(object sender, RoutedEventArgs e) {
        var colors = new[] {
            Colors.White, ColorHelper.FancyText, Colors.Orange, Colors.LimeGreen,
            Colors.Cyan, Colors.Red, Colors.Magenta, Colors.Gray, Colors.Black
        };
        var picker = BuildColorPicker(colors, picked => {
            _currentFontColor = picked;
            FontColorIndicator.Background = new SolidColorBrush(picked);
            Editor.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(picked));
            Editor.Focus();
        });
        picker.PlacementTarget = BtnFontColor;
        picker.IsOpen = true;
    }

    private void HighlightClick(object sender, RoutedEventArgs e) {
        var colors = new[] { Colors.Yellow, Colors.Lime, Colors.Cyan, Colors.Magenta, Colors.Transparent };
        var picker = BuildColorPicker(colors, picked => {
            _currentHighlightColor = picked;
            HighlightIndicator.Background = new SolidColorBrush(picked);
            var brush = picked == Colors.Transparent ? null : (Brush)new SolidColorBrush(picked);
            Editor.Selection.ApplyPropertyValue(TextElement.BackgroundProperty, brush);
            Editor.Focus();
        });
        picker.PlacementTarget = BtnHighlight;
        picker.IsOpen = true;
    }

    private Popup BuildColorPicker(Color[] colors, Action<Color> onPick) {
        var panel = new WrapPanel { Width = 130, Background = new SolidColorBrush(Color.FromRgb(17, 17, 19)) };
        var popup = new Popup {
            Child = new Border {
                Background = new SolidColorBrush(Color.FromRgb(17, 17, 19)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(42, 42, 46)),
                BorderThickness = new Thickness(1),
                Child = panel
            },
            AllowsTransparency = true,
            Placement = PlacementMode.Bottom,
            StaysOpen = false
        };
        foreach (var c in colors) {
            var col = c;
            var btn = new Button {
                Width = 22, Height = 22, Margin = new Thickness(3),
                Background = col == Colors.Transparent ? Brushes.Transparent : new SolidColorBrush(col),
                BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                ToolTip = col == Colors.Transparent ? "Aucun" : col.ToString()
            };
            btn.Click += (_, _) => {
                popup.IsOpen = false;
                onPick(col);
            };
            panel.Children.Add(btn);
        }

        return popup;
    }

    private void UndoClick(object sender, RoutedEventArgs e) {
        Editor.Undo();
        Editor.Focus();
    }

    private void RedoClick(object sender, RoutedEventArgs e) {
        Editor.Redo();
        Editor.Focus();
    }

    private void InsertSeparatorClick(object sender, RoutedEventArgs e) {
        var sep = new Border {
            BorderBrush = _isLightMode ? Brushes.Black : new SolidColorBrush(_currentFontColor),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Margin = new Thickness(0, 10, 0, 10)
        };
        var buc = new BlockUIContainer(sep);
        var target = Editor.CaretPosition.Paragraph ?? Editor.Document.Blocks.LastBlock;
        Editor.Document.Blocks.InsertAfter(target, buc);
        Editor.CaretPosition = buc.ElementEnd;
        Editor.Focus();
    }

    private void ToggleLineSpacingClick(object sender, RoutedEventArgs e) {
        _currentLineHeight = double.IsNaN(_currentLineHeight) ? 30.0 : double.NaN;
        foreach (var block in Editor.Document.Blocks)
            block.LineHeight = _currentLineHeight;
        Editor.Focus();
    }

    private void ToggleThemeClick(object sender, RoutedEventArgs e) {
        _isLightMode = !_isLightMode;
        if (_isLightMode) {
            Editor.Background = Brushes.White;
            Editor.Foreground = Brushes.Black;
            Editor.CaretBrush = Brushes.Black;
        }
        else {
            Editor.Background = Brushes.Transparent;
            Editor.Foreground = ColorHelper.FancyTextBrush;
            Editor.CaretBrush = ColorHelper.FancyTextBrush;
        }
    }

    private void FindReplaceClick(object sender, RoutedEventArgs e) {
        var win = new FindReplaceDialog(Editor);
        win.Owner = Window.GetWindow(this);
        win.Show();
    }
}