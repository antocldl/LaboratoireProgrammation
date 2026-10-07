using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using LaboratoireProgrammation.Project.Helpers;
using LaboratoireProgrammation.Project.Models.Menu;
using LaboratoireProgrammation.Project.ModernOverseerWars.Views;
using LaboratoireProgrammation.Project.Services;
using LaboratoireProgrammation.Project.ViewModels.Menu;

namespace LaboratoireProgrammation.Project.ViewModels.Laboratoires.OverseerWars;

public class OverseerWarInit {
    public static async void Introduce(MainWindow win) {
        try {
            win.IsAnimating = true;
            win.OutputBox.Visibility = Visibility.Collapsed;
            win.TopText.Visibility = Visibility.Collapsed;
            win.BabyMode.Visibility = Visibility.Collapsed;
            win.BabyModeB3.Visibility = Visibility.Collapsed;
            win.VisualModeB2.Visibility = Visibility.Collapsed;
            win.VisualModeB3.Visibility = Visibility.Collapsed;
            win.MainMenuButton.Visibility = Visibility.Collapsed;
            win.WinBtnClose.Visibility = Visibility.Collapsed;
            win.WinBtnMinimize.Visibility = Visibility.Collapsed;
            win.WinBtnMaximize.Visibility = Visibility.Collapsed;
            win.InputBox.Visibility = Visibility.Collapsed;
            win.InputBoxIndicator.Visibility = Visibility.Collapsed;
            win.VaultShader.VignetteStrength += 0.45;

            var wasSl = false;

            if (ConsoleBehavior.SpeedLoad) {
                ConsoleBehavior.SpeedLoad = false;
                wasSl = true;
            }

            var width = win.ActualWidth;
            var fontMult = width >= 1600 ? 1.0 : width >= 1200 ? 0.8 : 0.6;

            var inspirationBlock = TerminalDisplay.CreateTitleBlock("Overseer Wars", 180 * fontMult,
                new SolidColorBrush(ColorHelper.HexToColor("#f7bb52")), 300);
            var inspirationBlockOff = TerminalDisplay.CreateTitleBlock("Overseer Wars", 182 * fontMult,
                new SolidColorBrush(ColorHelper.HexToColor("#1Af7bb52")), 300);
            var inspirationBlockGlitched = TerminalDisplay.CreateTitleBlock("ꊿ꒦ꑀꌅꈜꑀꑀꌅ ꅐꁲꌅꈜ", 184 * fontMult,
                new SolidColorBrush(Color.FromArgb(100, 25, 30, 30)), 280);
            var inspirationBlockGlitched2 = TerminalDisplay.CreateTitleBlock("ꊿ ꒦ ꑀꌅ ꈜ ꑀꌅ ꅐ ꁲꌅꈜ", 184 * fontMult,
                new SolidColorBrush(Color.FromArgb(200, 25, 30, 30)), 280);
            var inspirationBlockGlitched3 = TerminalDisplay.CreateTitleBlock("ꊿ ꒦ ꑀꌅ ꈜ ꑀꌅ ꅐ ꁲꌅꈜ", 184 * fontMult,
                new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)), 280);
            var inspirationBlockGlitched4 = TerminalDisplay.CreateTitleBlock("𝕺𝖛𝖊𝖗𝖘𝖊𝖊𝖗 𝖂𝖆𝖗𝖘", 190 * fontMult,
                new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)), 280);
            var inspirationBlockGlitched5 = TerminalDisplay.CreateTitleBlock("Ꝋᕓ𐌄𐌄𐌓𐌔𐌄𐌄𐌓 Ꮤ𐌀𐌓𐌔", 150 * fontMult,
                new SolidColorBrush(Color.FromArgb(200, 50, 20, 0)), 280);

            await Task.Delay(1000);
            win.BethesdaLogo.Visibility = Visibility.Visible;
            var logoScale = new ScaleTransform(1.0, 1.0);
            win.BethesdaLogo.RenderTransformOrigin = new Point(0.5, 0.5);
            win.BethesdaLogo.RenderTransform = logoScale;

            win.BethesdaLogo.Visibility = Visibility.Visible;

            var zoomAnim = new DoubleAnimation {
                To = 1.15,
                Duration = TimeSpan.FromSeconds(3),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };

            logoScale.BeginAnimation(ScaleTransform.ScaleXProperty, zoomAnim);
            logoScale.BeginAnimation(ScaleTransform.ScaleYProperty, zoomAnim);

            await Task.Delay(1500);
            win.BethesdaLogo.Opacity = 0.005;

            await Task.Delay(200);
            win.TerminalOutputPanel.Children.Add(inspirationBlock);
            await Task.Delay(600);
            win.VaultShader.Brightness -= 0.2;
            win.VaultShader.BloomStrength -= 0.2;
            win.TerminalOutputPanel.Children.Clear();
            win.TerminalOutputPanel.Children.Add(inspirationBlockOff);
            await Task.Delay(200);
            win.VaultShader.Brightness += 0.2;
            win.VaultShader.BloomStrength += 0.2;
            win.TerminalOutputPanel.Children.Clear();
            win.TerminalOutputPanel.Children.Add(inspirationBlock);
            await Task.Delay(1000);

            win.BethesdaLogo.Visibility = Visibility.Collapsed;
            var screenSize = win.VaultShader.ScreenResolution;

            win.VaultShader.ChromaticAberration += 1.5;
            win.VaultShader.FlickerIntensity += 1.5;
            win.VaultShader.Contrast += 0.05;
            win.VaultShader.PixelGridIntensity += 0.20;
            win.VaultShader.ScreenResolution = new Size(100, 100);

            win.TerminalOutputPanel.Children.Clear();
            win.TerminalOutputPanel.Children.Add(inspirationBlockGlitched);
            await Task.Delay(30);
            win.TerminalOutputPanel.Children.Clear();
            win.TerminalOutputPanel.Children.Add(inspirationBlockGlitched2);
            await Task.Delay(30);
            win.TerminalOutputPanel.Children.Clear();
            win.TerminalOutputPanel.Children.Add(inspirationBlockGlitched);
            await Task.Delay(30);
            win.TerminalOutputPanel.Children.Clear();
            win.TerminalOutputPanel.Children.Add(inspirationBlockGlitched3);
            await Task.Delay(20);
            win.TerminalOutputPanel.Children.Clear();
            win.TerminalOutputPanel.Children.Add(inspirationBlockGlitched);
            await Task.Delay(30);
            win.TerminalOutputPanel.Children.Clear();
            win.TerminalOutputPanel.Children.Add(inspirationBlockGlitched2);
            await Task.Delay(30);
            win.TerminalOutputPanel.Children.Clear();
            win.TerminalOutputPanel.Children.Add(inspirationBlockGlitched4);
            await Task.Delay(30);
            win.TerminalOutputPanel.Children.Clear();
            win.TerminalOutputPanel.Children.Add(inspirationBlockGlitched5);
            await Task.Delay(50);

            win.VaultShader.ChromaticAberration -= 1.5;
            win.VaultShader.FlickerIntensity -= 1.5;
            win.VaultShader.Contrast -= 0.05;
            win.VaultShader.PixelGridIntensity -= 0.20;
            win.VaultShader.ScreenResolution = screenSize;

            win.TerminalOutputPanel.Children.Clear();
            win.TerminalOutputPanel.Children.Add(inspirationBlock);
            await Task.Delay(400);

            win.TerminalOutputPanel.Children.Clear();
            await Task.Delay(100);

            var authorBlock1 = TerminalDisplay.CreateTitleBlock("</-/> Developped by </-/>", 100 * fontMult,
                new SolidColorBrush(ColorHelper.HexToColor("#fcc23a")), 100);
            var authorBlock1Glitched = TerminalDisplay.CreateTitleBlock("<ù-$> ꀸꍟꃴꍟ꒒ꂦꉣꉣꍟꀸ ꌃꌩ <%-:>", 100 * fontMult,
                new SolidColorBrush(ColorHelper.HexToColor("#1Afcc23a")), 100);
            var authorBlock2 = TerminalDisplay.CreateTitleBlock("anto.cldl", 80 * fontMult,
                new SolidColorBrush(Color.FromArgb(255, 255, 255, 250)), 0);
            var authorBlock2Glitched = TerminalDisplay.CreateTitleBlock("ꍏꈤ꓄ꂦ.ꉓ꒒ꀸ꒒", 80 * fontMult,
                new SolidColorBrush(Color.FromArgb(20, 255, 0, 0)), 0);
            win.TerminalOutputPanel.Children.Add(authorBlock1);
            win.TerminalOutputPanel.Children.Add(authorBlock2);
            await Task.Delay(1250);

            win.TerminalOutputPanel.Children.Clear();
            win.VaultShader.ChromaticAberration += 1.5;
            win.VaultShader.ScreenResolution = new Size(100, 100);
            win.TerminalOutputPanel.Children.Add(authorBlock1Glitched);
            win.TerminalOutputPanel.Children.Add(authorBlock2Glitched);
            await Task.Delay(80);

            win.TerminalOutputPanel.Children.Clear();
            win.VaultShader.ChromaticAberration -= 1.5;
            win.VaultShader.PixelGridIntensity -= 0.20;
            win.VaultShader.ScreenResolution = screenSize;
            win.TerminalOutputPanel.Children.Clear();
            var authorBlock3 = TerminalDisplay.CreateTitleBlock("< ! > Assets by < ! >", 100 * fontMult,
                new SolidColorBrush(ColorHelper.HexToColor("#fcc23a")), 100);
            var authorBlock3Glitched = TerminalDisplay.CreateTitleBlock("< ꀘ > ꍏꌗꌗꍟ꓄ꌗ ꌃꌩ < ꂵ >", 100 * fontMult,
                new SolidColorBrush(ColorHelper.HexToColor("#1Afcc23a")), 100);
            var authorBlock4 = TerminalDisplay.CreateTitleBlock("ines_qessouri", 80 * fontMult,
                new SolidColorBrush(Color.FromArgb(255, 255, 240, 240)), 0);
            var authorBlock4Glitched = TerminalDisplay.CreateTitleBlock("꒒ꈤꍟꌗ_ꋪꍟꌗꌗꍏꈤꋪ꒒", 80 * fontMult,
                new SolidColorBrush(Color.FromArgb(20, 255, 0, 0)), 0);
            win.TerminalOutputPanel.Children.Add(authorBlock3);
            win.TerminalOutputPanel.Children.Add(authorBlock4);
            await Task.Delay(1250);

            win.TerminalOutputPanel.Children.Clear();
            win.VaultShader.ChromaticAberration += 1.5;
            win.VaultShader.ScreenResolution = new Size(100, 100);
            win.TerminalOutputPanel.Children.Add(authorBlock3Glitched);
            win.TerminalOutputPanel.Children.Add(authorBlock4Glitched);
            await Task.Delay(30);

            win.VaultShader.ChromaticAberration -= 1.5;
            win.VaultShader.ScreenResolution = screenSize;

            win.TerminalOutputPanel.Children.Clear();
            await Task.Delay(40);

            if (wasSl) ConsoleBehavior.SpeedLoad = true;

            win.VaultShader.VignetteStrength -= 0.45;
            win.BethesdaLogo.Opacity = 1;
            win.TerminalOutputPanel.Children.Clear();

            ToggleVisibility(win);
        } catch (Exception ex) {
            System.Windows.MessageBox.Show(ex.ToString(), "Introduce Error");
        }
    }

    public static void ToggleVisibility(MainWindow win) {
        try {
            var menu = new StartMenuWindow();
            if (menu.ShowDialog() == true) {
                if (menu.LoadedState != null) {
                    var game = new OverseerWarsWindow(menu.LoadedState);
                    game.Show();
                } else {
                    var game = new OverseerWarsWindow(menu.Profile1, menu.Profile2);
                    game.Show();
                }
                win.Close();
            }
        } catch (Exception ex) {
            System.Windows.MessageBox.Show(ex.ToString(), "Launch Error");
        }
    }
}