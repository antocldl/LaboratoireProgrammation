using System.Windows;
using System.Windows.Controls;
using LaboratoireProgrammation.Project.Services;
using LaboratoireProgrammation.Project.ViewModels.Menu;

namespace LaboratoireProgrammation.Project.ViewModels.Laboratoires.OverseerWars;

public partial class OverseerWarsUi : UserControl {
    public OverseerWarsUi() {
        InitializeComponent();
    }

    private MainWindow? Win => Window.GetWindow(this) as MainWindow;

    public static void DisablePreviousUi(MainWindow win) {
        //TODO: unify b2.b3 hide/show to make it easily to maintain
        win.VisualModeB2.Visibility = Visibility.Collapsed;
        win.VisualModeB3.Visibility = Visibility.Collapsed;
        win.InputBox.Visibility = Visibility.Collapsed;
        win.InputBoxIndicator.Visibility = Visibility.Collapsed;
        win.MainMenuButton.Visibility = Visibility.Collapsed;
        win.WinBtnClose.Visibility = Visibility.Collapsed;
        win.WinBtnMaximize.Visibility = Visibility.Collapsed;
        win.WinBtnMinimize.Visibility = Visibility.Collapsed;
        win.TerminalScroller.Visibility = Visibility.Collapsed;
        win.TerminalOutputPanel.Visibility = Visibility.Collapsed;
        win.TopText.Visibility = Visibility.Collapsed;
        win.BabyMode.Visibility = Visibility.Collapsed;
        win.BabyModeB3.Visibility = Visibility.Collapsed;
        if (TerminalDisplay._outputBox != null) TerminalDisplay._outputBox.Visibility = Visibility.Collapsed;
    }
}