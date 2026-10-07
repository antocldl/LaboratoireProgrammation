using System.Windows;
using System.Windows.Media;
using LaboratoireProgrammation.Project.Helpers;
using LaboratoireProgrammation.Project.Models.Menu;
using LaboratoireProgrammation.Project.Services;
using LaboratoireProgrammation.Project.ViewModels.Menu;

namespace LaboratoireProgrammation.Project.Models.Miscellaneous;

public class RunMemfy {
    private static void MemfyAgreementLaunch(MainWindow win) {
        win.OutputBox.Document.Blocks.Clear();

        win.OutputBox.Visibility = Visibility.Collapsed;
        win.TopText.Visibility = Visibility.Collapsed;
        win.InputBox.Visibility = Visibility.Collapsed;

        win.TerminalOutputPanel.Children.Clear();

        win.MemfyAgreement.Visibility = Visibility.Visible;
    }

    public static async void Run(MainWindow win) {
        MemfyAgreementLaunch(win);

        win.OutputBox.Document.Blocks.Clear();

        win.OutputBox.Visibility = Visibility.Collapsed;
        win.TopText.Visibility = Visibility.Collapsed;
        win.InputBox.Visibility = Visibility.Collapsed;

        var width = win.ActualWidth;
        var fontMult = width >= 1600 ? 1.0 : width >= 1200 ? 0.8 : 0.6;

        var inspirationBlock = TerminalDisplay.CreateTitleBlock("[ - MemfyAI - ]", 76 * fontMult,
            new SolidColorBrush(ColorHelper.HexToColor("#ff96fa")));
        win.TerminalOutputPanel.Children.Add(inspirationBlock);

        var descriptionBlock = TerminalDisplay.CreateTitleBlock("< Disclaimer > Use Cautiously !", 50 * fontMult,
            new SolidColorBrush(ColorHelper.HexToColor("#fcb3f9")));
        win.TerminalOutputPanel.Children.Add(descriptionBlock);

        if (ConsoleBehavior.SpeedLoad) await Task.Delay(50);
        else await Task.Delay(200);

        var authorBlock = TerminalDisplay.CreateTitleBlock("anto.cldl", 200 * fontMult,
            new SolidColorBrush(ColorHelper.HexToColor("#ffffff")));
        win.TerminalOutputPanel.Children.Add(authorBlock);

        if (ConsoleBehavior.SpeedLoad) await Task.Delay(50);
        else await Task.Delay(200);

        win.TerminalOutputPanel.Children.Clear();

        win.InputBox.Visibility = Visibility.Visible;
        win.TopText.Visibility = Visibility.Visible;
        win.OutputBox.Visibility = Visibility.Visible;
        win.InputBox.Focus();

        ConsoleBehavior.MemfyMode = true;
    }
}