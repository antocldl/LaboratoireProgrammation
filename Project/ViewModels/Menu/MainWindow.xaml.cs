using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using LaboratoireProgrammation.Project.Helpers;
using LaboratoireProgrammation.Project.Models.Menu;
using LaboratoireProgrammation.Project.ModernHospital;
using LaboratoireProgrammation.Project.ModernOverseerWars;
using LaboratoireProgrammation.Project.ModernOverseerWars.Views;
using LaboratoireProgrammation.Project.Services;

namespace LaboratoireProgrammation.Project.ViewModels.Menu;

public enum VisualMenuState {
    None,
    Bloc2,
    Bloc3
}

public partial class MainWindow : Window {
    private readonly ConsoleBehavior _behavior = new();
    private readonly Color _terminalGreen = Color.FromRgb(51, 255, 51);
    private VisualMenuState _currentVisualMenu = VisualMenuState.None;
    public bool IsAnimating;

    public MainWindow() {
        InitializeComponent();

        SetupModelEvents();
        TerminalDisplay.Init(OutputBox, InputBox);
        SizeHelper.setFullscreen(this);

        AnimateLoader();
    }

    private void SetupModelEvents() {
        _behavior.OnOutputRequest = (txt, clr) => TerminalDisplay.AppendOutput(txt, clr);
        _behavior.OnOutputSameLineRequest = (txt, clr) => TerminalDisplay.AppendOutputOnSameLine(txt, clr);
        _behavior.OnClearInputRequest = () => InputBox.Clear();
    }

    private IEnumerable<UIElement> GetAllExerciseViews() {
        yield return Exo1;
        yield return Exo1B;
        yield return Exo1B3;
        yield return Exo2;
        yield return Exo3;
        yield return Exo3B;
        yield return Exo5;
        yield return Exo6;
        yield return Exo7;
        yield return Exo8;
        yield return Exo9;
        yield return Exo10;
        yield return Labo1B;
        yield return ESP8266;
        yield return FlashMaster;
        yield return MemfyAgreement;
        yield return OverseerWarsMenu;
        yield return BackgroundRectangle;
        yield return BethesdaLogo;
        yield return Uplink;
    }

    private void HideAllExerciseViews() {
        foreach (var view in GetAllExerciseViews()) {
            if (view != null) view.Visibility = Visibility.Collapsed;
        }
    }

    public void MainInit() {
        HideAllExerciseViews();

        _currentVisualMenu = VisualMenuState.None;
        UpdateVisualMenusUi();

        OutputBox.Document.Blocks.Clear();
        TerminalOutputPanel.Children.Clear();
        TerminalDisplay.SeparationLine();
    }

    public void OnExerciseOpened() {
        _currentVisualMenu = VisualMenuState.None;
        if (VisualModeB2 != null) VisualModeB2.Content = "Menu Bloc.2 [ ❌ ]";
        if (VisualModeB3 != null) VisualModeB3.Content = "Menu Bloc.3 [ ❌ ]";
        if (BabyMode != null) BabyMode.Visibility = Visibility.Collapsed;
        if (BabyModeB3 != null) BabyModeB3.Visibility = Visibility.Collapsed;
    }

    public void SetVisualMenu(VisualMenuState targetState) {
        if (IsAnimating) return;

        _currentVisualMenu = (_currentVisualMenu == targetState) ? VisualMenuState.None : targetState;
        UpdateVisualMenusUi();
    }

    private void UpdateVisualMenusUi() {
        HideAllExerciseViews();

        VisualModeB2.Content = _currentVisualMenu == VisualMenuState.Bloc2 ? "Menu Bloc.2 [ ✔ ]" : "Menu Bloc.2 [ ❌ ]";
        VisualModeB3.Content = _currentVisualMenu == VisualMenuState.Bloc3 ? "Menu Bloc.3 [ ✔ ]" : "Menu Bloc.3 [ ❌ ]";

        switch (_currentVisualMenu) {
            case VisualMenuState.Bloc2:
                OutputBox.Visibility = Visibility.Collapsed;
                InputBox.Visibility = Visibility.Collapsed;
                InputBoxIndicator.Visibility = Visibility.Collapsed;
                BabyMode.Visibility = Visibility.Visible;
                BabyModeB3.Visibility = Visibility.Collapsed;
                break;

            case VisualMenuState.Bloc3:
                OutputBox.Visibility = Visibility.Collapsed;
                InputBox.Visibility = Visibility.Collapsed;
                InputBoxIndicator.Visibility = Visibility.Collapsed;
                BabyMode.Visibility = Visibility.Collapsed;
                BabyModeB3.Visibility = Visibility.Visible;
                break;

            case VisualMenuState.None:
            default:
                BabyMode.Visibility = Visibility.Collapsed;
                BabyModeB3.Visibility = Visibility.Collapsed;
                OutputBox.Visibility = Visibility.Visible;
                InputBox.Visibility = Visibility.Visible;
                InputBoxIndicator.Visibility = Visibility.Visible;
                TerminalDisplay.Init(OutputBox, InputBox);
                InputBox.Focus();
                break;
        }
    }

    private void B2MenuSwitch(object sender, RoutedEventArgs e) {
        SetVisualMenu(VisualMenuState.Bloc2);
    }

    private void B3MenuSwitch(object sender, RoutedEventArgs e) {
        SetVisualMenu(VisualMenuState.Bloc3);
    }

    public void BackToMenu(object sender, RoutedEventArgs e) {
        MainInit();
    }

    private void InputBox_PreviewKeyDown(object sender, KeyEventArgs e) {
        if (_behavior.GetIsAdjustingSize()) {
            HandleFontSizeMode(e);
            return;
        }

        HandleStandardMode(e);
    }

    private void HandleFontSizeMode(KeyEventArgs e) {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        switch (key) {
            case Key.Up:
                ModifyFontSize(1);
                e.Handled = true;
                break;
            case Key.Down:
                ModifyFontSize(-1);
                e.Handled = true;
                break;
            case Key.Enter:
                ConsoleBehavior.IsAdjustingSize = false;
                TerminalDisplay.AppendOutput($"[CONFIG] FONT SIZE SAVED: {OutputBox.FontSize}");
                InputBox.Clear();
                e.Handled = true;
                break;
        }
    }

    private async void HandleStandardMode(KeyEventArgs e) {
        switch (e.Key) {
            case Key.Enter:
                await _behavior.ProcessInput(InputBox.Text, this);
                e.Handled = true;
                break;
            case Key.Up:
                UpdateInputFromHistory(-1);
                e.Handled = true;
                break;
            case Key.Down:
                UpdateInputFromHistory(1);
                e.Handled = true;
                break;
        }
    }

    private void UpdateInputFromHistory(int direction) {
        var historyText = _behavior.GetHistory(direction);
        if (historyText != null) {
            InputBox.Text = historyText;
            InputBox.CaretIndex = InputBox.Text.Length;
        }
    }

    public void BlankSpace() {
        OutputBox.Document.Blocks.Add(new Paragraph(new Run("")));
        OutputBox.ScrollToEnd();
    }

    private void ModifyFontSize(int delta) {
        var newSize = Math.Max(OutputBox.FontSize + delta, 8);
        OutputBox.FontSize = newSize;
        InputBox.FontSize = newSize;
    }

    private void MinimizeProgram(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void MaximizeProgram(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void CloseProgram(object sender, RoutedEventArgs e) => Close();

    private async void AnimateLoader() {
        IsAnimating = true;
        await MenuLoaders.Run_Main(this);
        MainInit();
        BlankSpace();
        IsAnimating = false;
    }
}