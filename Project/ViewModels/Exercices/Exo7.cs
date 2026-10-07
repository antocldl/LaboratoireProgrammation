using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LaboratoireProgrammation.Project.Helpers;

namespace LaboratoireProgrammation.Project.Views.Exercices;

public partial class Exo7 : UserControl {
    private const string FontBase = "pack://application:,,,/LaboratoireProgrammation;component/Assets/fonts/";

    public Exo7() {
        InitializeComponent();

        IsVisibleChanged += (s, e) => {
            var parentWindow = Window.GetWindow(this);
            if (parentWindow == null) return;

            parentWindow.PreviewKeyDown -= ParentWindow_PreviewKeyDown;
            parentWindow.PreviewKeyUp -= ParentWindow_PreviewKeyUp;

            if (IsVisible) {
                parentWindow.PreviewKeyDown += ParentWindow_PreviewKeyDown;
                parentWindow.PreviewKeyUp += ParentWindow_PreviewKeyUp;
                UpdateToggleKeys();
            }
        };

        Unloaded += (s, e) => {
            var parentWindow = Window.GetWindow(this);
            if (parentWindow != null) {
                parentWindow.PreviewKeyDown -= ParentWindow_PreviewKeyDown;
                parentWindow.PreviewKeyUp -= ParentWindow_PreviewKeyUp;
            }
        };
    }

    private void ParentWindow_PreviewKeyDown(object sender, KeyEventArgs e) {
        var actualKey = e.Key == Key.System ? e.SystemKey : e.Key;
        var keyName = "Key_" + actualKey;
        AddKeyToHistory(e.Key);
        LightUpKey(keyName);
    }

    private void ParentWindow_PreviewKeyUp(object sender, KeyEventArgs e) {
        var actualKey = e.Key == Key.System ? e.SystemKey : e.Key;
        var keyName = "Key_" + actualKey;

        LightDownKey(keyName);

        UpdateToggleKeys();
    }

    private void LightUpKey(string keyName) {
        if (FindName(keyName) is Border keyVisual) {
            keyVisual.Background = ColorHelper.FancyTextBrush;
            keyVisual.BorderBrush = Brushes.White;

            if (keyVisual.Child is TextBlock textBlock) textBlock.Foreground = Brushes.Black;
        }
    }

    private void LightDownKey(string keyName) {
        if (FindName(keyName) is Border keyVisual) {
            keyVisual.Background = (SolidColorBrush)FindResource("KeyDefaultBg");
            keyVisual.BorderBrush = (SolidColorBrush)FindResource("KeyDefaultBorder");

            if (keyVisual.Child is TextBlock textBlock)
                textBlock.Foreground = (SolidColorBrush)FindResource("KeyTextDefault");
        }
    }

    private void UpdateToggleKeys() {
        if (Keyboard.IsKeyToggled(Key.CapsLock)) LightUpKey("Key_CapsLock");
        else LightDownKey("Key_CapsLock");

        if (Keyboard.IsKeyToggled(Key.NumLock)) LightUpKey("Key_NumLock");
        else LightDownKey("Key_NumLock");
    }

    private void AddKeyToHistory(Key key) {
        var date = DateTime.Now;
        var content = $"[{date.Hour}:{date.Minute}:{date.Second}] {key.ToString()}";

        var keyReport = new TextBlock {
            FontFamily = new FontFamily(new Uri(FontBase), "./#doto"),
            FontSize = 14,
            Foreground = ColorHelper.FancyTextBrush,
            Text = content,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 44, 0, 0)
        };

        History.Children.Insert(1, keyReport);

        if (History.Children.Count >= 21) History.Children.RemoveAt(20);
    }
}