using System.Diagnostics;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using LaboratoireProgrammation.Project.Helpers;
using LaboratoireProgrammation.Project.Services;
using LaboratoireProgrammation.Project.ViewModels.Menu;

namespace LaboratoireProgrammation.Project.Models.Exercices;

public class Exo4 {
    private static bool isExitMoved;
    private static int touchCount;

    private static double BurnIn,
        NoiseScale,
        NoiseIntensity,
        Brightness,
        Contrast,
        PhosphorDecay,
        BloomStrength,
        InterferenceSpeed,
        DistortionStrength;

    private static bool locked;
    private static int panicCount;

    private static readonly string ghostText = "ACCESS DENIED. INITIATING FORMAT...";
    private static int ghostIndex;
    private static bool ghostTypistActive;

    [DllImport("User32.dll")]
    private static extern bool SetCursorPos(int X, int Y);

    public static void InitializeVirus(MainWindow win) {
        RestartAsAdmin();

        BurnIn = win.VaultShader.BurnInIntensity;
        NoiseScale = win.VaultShader.NoiseScale;
        NoiseIntensity = win.VaultShader.NoiseIntensity;
        Brightness = win.VaultShader.Brightness;
        Contrast = win.VaultShader.Contrast;
        PhosphorDecay = win.VaultShader.PhosphorDecay;
        BloomStrength = win.VaultShader.BloomStrength;
        InterferenceSpeed = win.VaultShader.InterferenceSpeed;
        DistortionStrength = win.VaultShader.DistortionStrength;

        win.InputBox.PreviewTextInput += (s, e) => {
            if (ghostTypistActive) {
                e.Handled = true;
                if (ghostIndex < ghostText.Length) {
                    win.InputBox.Text += ghostText[ghostIndex];
                    win.InputBox.CaretIndex = win.InputBox.Text.Length;
                    ghostIndex++;
                }
            }
        };

        win.InputBox.PreviewKeyDown += (s, e) => {
            if (ghostTypistActive && (e.Key == Key.Back || e.Key == Key.Delete)) e.Handled = true;
        };

        win.WinBtnClose.MouseEnter += (sender, e) => {
            if (sender is Button button) {
                button.SetValue(Panel.ZIndexProperty, 101);
                button.IsCancel = true;

                button.Margin = !isExitMoved
                    ? new Thickness(button.Margin.Left, button.Margin.Top, button.Margin.Right + 60,
                        button.Margin.Bottom)
                    : new Thickness(button.Margin.Left, button.Margin.Top, button.Margin.Right - 60,
                        button.Margin.Bottom);

                isExitMoved = !isExitMoved;

                win.VaultShader.BurnInIntensity += 0.1;
                win.VaultShader.NoiseScale += 0.02;
                win.VaultShader.NoiseIntensity += 0.02;
                win.VaultShader.Brightness -= 0.05;
                win.VaultShader.Contrast += 0.05;
                win.VaultShader.PhosphorDecay += 0.1;
                win.VaultShader.BloomStrength += 0.1;
                win.VaultShader.InterferenceSpeed += 0.1;
                win.VaultShader.DistortionStrength += 0.02;

                touchCount++;

                switch (touchCount) {
                    case 1:
                        FirstTouch(win);
                        break;
                    case 2:
                        ghostTypistActive = true;
                        break;
                    case 3:
                        CrazyWindow(win);
                        break;
                    case 4:
                        TriggerGravity(win);
                        break;
                    case 5:
                        SpawnAnnoyingWindow(win);
                        break;
                    case 6:
                        Panic(win);
                        break;
                }
            }
        };

        win.WinBtnMinimize.MouseEnter += (sender, e) => {
            if (sender is Button button) button.Visibility = Visibility.Hidden;
        };

        win.WinBtnMaximize.MouseEnter += (sender, e) => {
            if (sender is Button button) button.Visibility = Visibility.Hidden;
        };

        win.Closing += (sender, e) => {
            if (Keyboard.IsKeyDown(Key.LeftAlt) && Keyboard.IsKeyDown(Key.F4)) {
                e.Cancel = true;

                if (locked) {
                    var width = win.ActualWidth;
                    var fontMult = width >= 1600 ? 1.0 : width >= 1200 ? 0.8 : 0.6;

                    panicCount++;

                    win.TerminalOutputPanel.Children.Clear();
                    var inspirationBlock = TerminalDisplay.CreateTitleBlock("Tentative de fuite : " + panicCount,
                        100 * fontMult, new SolidColorBrush(ColorHelper.HexToColor("#ffffff")), 200);
                    win.TerminalOutputPanel.Children.Add(inspirationBlock);

                    if (panicCount == 9) {
                        win.TerminalOutputPanel.Children.Clear();
                        var WARN = TerminalDisplay.CreateTitleBlock("JE FERAI PAS ÇA SI J'ÉTAIS TOI...", 70 * fontMult,
                            new SolidColorBrush(ColorHelper.HexToColor("#ffffff")), 200);
                        win.TerminalOutputPanel.Children.Add(WARN);
                    }

                    if (panicCount >= 10) TriggerBSoD(win);
                }
            }
        };

        Task.Run(async () => {
            var screenWidth = (int)SystemParameters.PrimaryScreenWidth;
            var screenHeight = (int)SystemParameters.PrimaryScreenHeight;
            var rnd = new Random();

            while (true) {
                await Task.Delay(100);

                if (locked)
                    Application.Current.Dispatcher.Invoke(() => {
                        win.WindowState = WindowState.Maximized;
                        SetCursorPos(rnd.Next(0, screenWidth), rnd.Next(0, screenHeight));
                    });
            }
        });

        Task.Run(async () => {
            while (true)
                if (panicCount > 0 || touchCount >= 4) {
                    SystemSounds.Beep.Play();
                    var delay = Math.Max(50, 800 - panicCount * 50 - touchCount * 20);
                    await Task.Delay(delay);
                }
                else {
                    await Task.Delay(1000);
                }
        });
    }

    private static void TriggerGravity(MainWindow win) {
        var rnd = new Random();
        foreach (UIElement child in win.TerminalOutputPanel.Children) {
            var transform = new TranslateTransform();
            child.RenderTransform = transform;

            var anim = new DoubleAnimation {
                To = win.ActualHeight,
                Duration = TimeSpan.FromSeconds(rnd.NextDouble() * 1.5 + 0.5),
                EasingFunction = new BounceEase { Bounces = 4, Bounciness = 1.5 }
            };

            transform.BeginAnimation(TranslateTransform.YProperty, anim);
        }
    }

    private static async void TriggerBSoD(MainWindow win) {
        win.Background = new SolidColorBrush(Color.FromRgb(0, 0, 170));
        win.TerminalOutputPanel.Children.Clear();
        win.TopText.Visibility = Visibility.Collapsed;
        win.InputBox.Visibility = Visibility.Collapsed;
        win.InputBoxIndicator.Visibility = Visibility.Collapsed;

        win.OutputBox.Visibility = Visibility.Visible;
        win.OutputBox.Foreground = Brushes.White;
        win.OutputBox.Document.Blocks.Clear();

        var rnd = new Random();
        for (var i = 0; i < 200; i++) {
            var hexLine =
                $"0x{rnd.Next(0x10000000, 0x7FFFFFFF):X8} 0x{rnd.Next(0x10000, 0x7FFFF):X8} FATAL_SYSTEM_CORRUPTION";
            TerminalDisplay.AppendOutput(hexLine);
            await Task.Delay(10);
        }

        PrintAllSystem32Files(win);
    }

    private static void SpawnAnnoyingWindow(MainWindow parentWin) {
        var annoying = new Window {
            Width = 350,
            Height = 150,
            Title = "RobCo OS - Fatal Error",
            WindowStyle = WindowStyle.ToolWindow,
            Topmost = true,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Background = Brushes.Black,
            Foreground = Brushes.LimeGreen,
            FontFamily = new FontFamily("Consolas")
        };

        var sp = new StackPanel { Margin = new Thickness(20), HorizontalAlignment = HorizontalAlignment.Center };
        sp.Children.Add(new TextBlock
            { Text = "Êtes-vous sûr de vouloir quitter ?", Margin = new Thickness(0, 0, 0, 20), FontSize = 14 });

        var btnYes = new Button
            { Content = "OUI", Width = 100, Background = Brushes.DarkRed, Foreground = Brushes.White };
        btnYes.Click += (s, e) => {
            SpawnAnnoyingWindow(parentWin);
            SpawnAnnoyingWindow(parentWin);
            annoying.Close();
        };

        sp.Children.Add(btnYes);
        annoying.Content = sp;
        annoying.Show();
    }

    private static async void PrintAllSystem32Files(MainWindow win) {
        var path = @"C:\Windows\System32";
        win.OutputBox.Foreground = Brushes.White;
        win.OutputBox.FontWeight = FontWeights.SemiBold;
        win.OutputBox.Visibility = Visibility.Visible;
        win.TerminalOutputPanel.Visibility = Visibility.Hidden;

        try {
            var files = Directory.GetFiles(path, "*.dll").Take(600);
            var fileCount = 0;

            foreach (var file in files) {
                TerminalDisplay.AppendOutput("Suppression : " + file + " ...");
                fileCount++;

                if (fileCount % 45 == 0) win.OutputBox.Document.Blocks.Clear();
                await Task.Delay(5);
            }
        }
        catch (Exception ex) {
            TerminalDisplay.AppendOutput($"ERREUR D'ACCÈS: {ex.Message}");
        }

        win.OutputBox.Visibility = Visibility.Collapsed;
        await Task.Delay(1000);
    }

    private static async void FirstTouch(MainWindow win) {
        win.WinBtnClose.Visibility = Visibility.Hidden;

        var image = new Image();
        image.Source =
            new BitmapImage(new Uri(
                "pack://application:,,,/LaboratoireProgrammation;component/Assets/Images/fallout4.png",
                UriKind.Absolute));
        image.Width = 500;
        image.HorizontalAlignment = HorizontalAlignment.Center;
        image.VerticalAlignment = VerticalAlignment.Center;

        for (var i = 0; i < 25; i++) {
            if (i == 5) win.BethesdaLogo.Visibility = Visibility.Visible;
            if (i == 7) win.BethesdaLogo.Visibility = Visibility.Hidden;
            if (i == 10) win.Exo1.Visibility = Visibility.Visible;
            if (i == 12) win.Exo1.Visibility = Visibility.Hidden;

            if (i == 20)
                win.TerminalOutputPanel.Children.Add(image);
            else if (i == 24) win.TerminalOutputPanel.Children.Remove(image);

            win.Background = new SolidColorBrush(ColorHelper.HexToColor("#291b1b"));
            await Task.Delay(10);
            win.Background = new SolidColorBrush(ColorHelper.HexToColor("#732727"));
            await Task.Delay(10);
            win.Background = new SolidColorBrush(ColorHelper.HexToColor("#422441"));
            await Task.Delay(10);
            win.Background = new SolidColorBrush(ColorHelper.HexToColor("#5c0200"));
            await Task.Delay(10);
            win.Background = new SolidColorBrush(ColorHelper.HexToColor("#121212"));
            await Task.Delay(10);
            win.Background = new SolidColorBrush(ColorHelper.HexToColor("#291b1b"));
        }

        win.WinBtnClose.Visibility = Visibility.Visible;
    }

    private static async void CrazyWindow(MainWindow win) {
        win.WinBtnClose.Visibility = Visibility.Hidden;
        var width = win.ActualWidth;
        var fontMult = width >= 1600 ? 1.0 : width >= 1200 ? 0.8 : 0.6;

        var inspirationBlock = TerminalDisplay.CreateTitleBlock("TU T'EN VAS ?", 150 * fontMult,
            new SolidColorBrush(ColorHelper.HexToColor("#ffffff")), 200);
        win.TerminalOutputPanel.Children.Add(inspirationBlock);

        await Task.Delay(2000);
        win.WindowState = WindowState.Normal;
        var rnd = new Random();

        for (var i = 0; i < 10; i++) {
            await Task.Delay(20);
            var random = rnd.Next(-80, 60);
            win.Left += 10 + random;
            win.Top -= random;
        }

        for (var i = 0; i < 10; i++) {
            await Task.Delay(20);
            var random = rnd.Next(-200, 200);
            win.Top -= 10 + random;
            win.Left += random;
        }

        await Task.Delay(200);
        win.WindowState = WindowState.Maximized;
        await Task.Delay(200);

        win.OutputBox.Document.Blocks.Clear();
        win.OutputBox.Visibility = Visibility.Collapsed;
        win.TopText.Visibility = Visibility.Collapsed;
        win.InputBox.Visibility = Visibility.Collapsed;

        var glitchBlock1 = TerminalDisplay.CreateTitleBlock("ne pars pas !", 80 * fontMult,
            new SolidColorBrush(ColorHelper.HexToColor("#fcfe4d")), 0);
        var glitchBlock2 = TerminalDisplay.CreateTitleBlock("reste un peu avec nous !", 80 * fontMult,
            new SolidColorBrush(ColorHelper.HexToColor("#6190ff")), 0);
        var glitchBlock3 = TerminalDisplay.CreateTitleBlock("⦍𝚃⦐⦍𝚄⦐ ⦍𝚃⦐⦍'⦐⦍𝙴⦐⦍𝙽⦐ ⦍𝚅⦐⦍𝙰⦐⦍𝚂⦐ ⦍?⦐", 80 * fontMult,
            new SolidColorBrush(ColorHelper.HexToColor("#ffffff")), 0);
        var glitchBlock4 = TerminalDisplay.CreateTitleBlock("😱😱😱😱😱😱😱😱", 80 * fontMult,
            new SolidColorBrush(ColorHelper.HexToColor("#ff61ea")), 0);
        var glitchBlock5 = TerminalDisplay.CreateTitleBlock("KILL-9", 80 * fontMult,
            new SolidColorBrush(ColorHelper.HexToColor("#ffffff")), 0);

        win.TerminalOutputPanel.Children.Add(glitchBlock1);
        await Task.Delay(150);
        win.TerminalOutputPanel.Children.Add(glitchBlock2);
        await Task.Delay(150);
        win.TerminalOutputPanel.Children.Add(glitchBlock3);
        await Task.Delay(150);
        win.TerminalOutputPanel.Children.Add(glitchBlock4);
        await Task.Delay(150);
        win.TerminalOutputPanel.Children.Add(glitchBlock5);
        await Task.Delay(150);

        win.WindowState = WindowState.Normal;
        await Task.Delay(100);
        for (var i = 0; i < 10; i++) {
            await Task.Delay(20);
            if (i % 3 == 0)
                win.Top += 100;
            else
                win.Left += 100;
        }

        win.WindowState = WindowState.Maximized;
        win.WinBtnClose.Visibility = Visibility.Visible;
    }

    private static async void Panic(MainWindow win) {
        locked = true;

        win.WindowState = WindowState.Minimized;
        win.Background = new SolidColorBrush(ColorHelper.HexToColor("#0000AA"));

        win.VaultShader.BurnInIntensity = BurnIn;
        win.VaultShader.NoiseScale = NoiseScale;
        win.VaultShader.NoiseIntensity = NoiseIntensity;
        win.VaultShader.Brightness = Brightness;
        win.VaultShader.Contrast = Contrast;
        win.VaultShader.PhosphorDecay = PhosphorDecay;
        win.VaultShader.BloomStrength = BloomStrength;
        win.VaultShader.InterferenceSpeed = InterferenceSpeed;
        win.VaultShader.DistortionStrength = DistortionStrength;

        win.WinBtnMaximize.Visibility = Visibility.Collapsed;
        win.WinBtnMinimize.Visibility = Visibility.Collapsed;
        win.WinBtnClose.Visibility = Visibility.Collapsed;
        win.TopText.Visibility = Visibility.Collapsed;
        win.InputBox.Visibility = Visibility.Collapsed;
        win.OutputBox.Visibility = Visibility.Collapsed;
        win.Exo1.Visibility = Visibility.Collapsed;
        win.Exo2.Visibility = Visibility.Collapsed;
        win.Exo3.Visibility = Visibility.Collapsed;
        win.Exo3B.Visibility = Visibility.Collapsed;
        win.Exo1B.Visibility = Visibility.Collapsed;
        win.VisualModeB2.Visibility = Visibility.Collapsed;
        win.VisualModeB3.Visibility = Visibility.Collapsed;
        win.MainMenuButton.Visibility = Visibility.Collapsed;
        win.InputBoxIndicator.Visibility = Visibility.Collapsed;

        win.TerminalOutputPanel.Children.Clear();
        await Task.Delay(100);

        try {
            var cursorImage = new Image();
            cursorImage.LayoutTransform = new ScaleTransform(6, 6);
            cursorImage.Source =
                new BitmapImage(new Uri(
                    "pack://application:,,,/LaboratoireProgrammation;component/Assets/images/empty.png",
                    UriKind.Absolute));
            win.Cursor = CursorHelper.CreateCursor(cursorImage, 0, 0);
        }
        catch {
            win.Cursor = Cursors.None;
        }

        win.WindowStyle = WindowStyle.None;
        win.ResizeMode = ResizeMode.NoResize;
        win.WindowState = WindowState.Maximized;
    }

    private static bool IsRunAsAdmin() {
        var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static void RestartAsAdmin() {
        if (!IsRunAsAdmin()) {
            var proc = new ProcessStartInfo {
                UseShellExecute = true,
                WorkingDirectory = Environment.CurrentDirectory,
                FileName = Process.GetCurrentProcess().MainModule?.FileName,
                Verb = "runas"
            };

            try {
                Process.Start(proc);
                Environment.Exit(0);
            }
            catch (Exception) { }
        }
    }
}