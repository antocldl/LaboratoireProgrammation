using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LaboratoireProgrammation.Project.ModernOverseerWars.Helpers;
using LaboratoireProgrammation.Project.ModernOverseerWars.Models;

namespace LaboratoireProgrammation.Project.ModernOverseerWars.Views;

public partial class StartMenuWindow : Window {
    public PlayerProfile Profile1 { get; } = new() { Index = 0 };
    public PlayerProfile Profile2 { get; } = new() { Index = 1 };

    private int _p1ThemeIdx = 0;
    private int _p2ThemeIdx = 1;

    public StartMenuWindow() {
        InitializeComponent();
        InitializeThemes();
        HookEvents();
        UpdatePreviews();
    }

    private void InitializeThemes() {
        var viewModels = PlayerThemes.All.Select(ThemeViewModel.From).ToList();
        P1Themes.ItemsSource = viewModels;
        P2Themes.ItemsSource = viewModels;
    }

    private void HookEvents() {
        P1Name.TextChanged += (_, _) => UpdatePreviews();
        P2Name.TextChanged += (_, _) => UpdatePreviews();
    }

    private void OnP1ThemeClick(object sender, MouseButtonEventArgs e) {
        HandleThemeSelection(sender, ref _p1ThemeIdx, _p2ThemeIdx);
    }

    private void OnP2ThemeClick(object sender, MouseButtonEventArgs e) {
        HandleThemeSelection(sender, ref _p2ThemeIdx, _p1ThemeIdx);
    }

    private void HandleThemeSelection(object sender, ref int targetIndex, int otherIndex) {
        if (sender is not FrameworkElement { Tag: ThemeViewModel vm }) return;
        
        int newIdx = PlayerThemes.All.ToList().IndexOf(vm.Theme);
        
        if (newIdx == otherIndex) return;
        
        targetIndex = newIdx;
        UpdatePreviews();
    }

    private void UpdatePreviews() {
        ApplyThemeToPreview(P1Preview, P1PreviewText, P1Name.Text, PlayerThemes.All[_p1ThemeIdx]);
        ApplyThemeToPreview(P2Preview, P2PreviewText, P2Name.Text, PlayerThemes.All[_p2ThemeIdx]);

        RefreshDotSelection(P1Themes, _p1ThemeIdx, _p2ThemeIdx);
        RefreshDotSelection(P2Themes, _p2ThemeIdx, _p1ThemeIdx);
    }

    private void ApplyThemeToPreview(Border previewBox, TextBlock previewText, string playerName, PlayerTheme theme) {
        previewBox.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.BgColor));
        previewText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.Color));
        previewBox.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.Color));
        previewText.Text = playerName;
    }

    private void RefreshDotSelection(ItemsControl list, int selectedIdx, int otherIdx) {
        list.UpdateLayout();
        
        for (int i = 0; i < list.Items.Count; i++) {
            var container = list.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
            if (container == null) continue;
            
            var border = UIHelper.FindVisualChild<Border>(container);
            var check = UIHelper.FindVisualChild<TextBlock>(container);
            
            if (border == null || check == null) continue;
            
            bool isSelected = i == selectedIdx;
            bool isTaken = i == otherIdx;
            
            check.Visibility = isSelected ? Visibility.Visible : Visibility.Collapsed;
            border.BorderThickness = isSelected ? new Thickness(3) : new Thickness(1);
            border.BorderBrush = new SolidColorBrush(isSelected ? Colors.White : Colors.Transparent);
            container.Opacity = isTaken ? 0.15 : 1.0;
        }
    }

    public GameState? LoadedState { get; private set; }

    private void OnStart(object sender, RoutedEventArgs e) {
        Profile1.Pseudo = string.IsNullOrWhiteSpace(P1Name.Text) ? "Wanderer" : P1Name.Text;
        Profile1.Theme = PlayerThemes.All[_p1ThemeIdx];
        
        Profile2.Pseudo = string.IsNullOrWhiteSpace(P2Name.Text) ? "Courier" : P2Name.Text;
        Profile2.Theme = PlayerThemes.All[_p2ThemeIdx];
        
        DialogResult = true;
    }

    private void OnLoadGame(object sender, RoutedEventArgs e) {
        var loadWin = new LoadGameWindow();
        loadWin.Owner = this;
        if (loadWin.ShowDialog() == true && loadWin.LoadedState != null) {
            LoadedState = loadWin.LoadedState;
            DialogResult = true;
        }
    }

    private void OnMultiplayer(object sender, RoutedEventArgs e) {
        var mpMenu = new LaboratoireProgrammation.Project.ModernOverseerWarsMP.Views.OverseerWarsMpMenuWindow();
        mpMenu.Owner = this;
        Hide();
        mpMenu.ShowDialog();
        Show();
    }
}

public class ThemeViewModel {
    public PlayerTheme Theme { get; init; } = null!;
    public Color DisplayColor { get; init; }

    public static ThemeViewModel From(PlayerTheme t) {
        return new ThemeViewModel {
            Theme = t,
            DisplayColor = (Color)ColorConverter.ConvertFromString(t.Color)
        };
    }
}

public static class UIHelper {
    public static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) {
            var child = VisualTreeHelper.GetChild(parent, i);
            
            if (child is T typedChild) {
                return typedChild;
            }
            
            var result = FindVisualChild<T>(child);
            if (result != null) {
                return result;
            }
        }
        
        return null;
    }
}