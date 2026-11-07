using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using LaboratoireProgrammation.Project.Helpers;
using LaboratoireProgrammation.Project.ModernHospital;
using LaboratoireProgrammation.Project.ViewModels.Laboratoires.OverseerWars;
using LaboratoireProgrammation.Project.ViewModels.Menu;

namespace LaboratoireProgrammation.Project.Models.Menu;

public class MenuLoaders {
    public enum IntroStyle {
        TypewriterGlitch,
        FadeOnly
    }

    private const string _author = "@ anto.cldl";
    private const string _author2 = "MADE.BY :// @ anto.cldl";

    private const string _title_exo1 = "Exercice [ 1 ]";
    private const string _sub_exo1 = ": // DATE : FEB . 2026  //  BOUTONS ET IMAGES";

    private const string _title_exo1b3 = "Exercice [ 1 - B3 ]";
    private const string _sub_exo1b3 = ": // DATE : OCT . 2026  //  COURRIEL / SMTP DISPATCH";
    
    private const string _title_exo2b3 = "Exercice [ 2 - B3 ]";
    private const string _sub_exo2b3 = ": // DATE : OCT . 2026  //  T.ODOO";
    
    private const string _title_exo3b3 = "Exercice [ 3 - B3 ]";
    private const string _sub_exo3b3 = ": // DATE : OCT . 2026  // CHRONOS";

    private const string _title_exo2 = "Exercice [ 2 ]";
    private const string _sub_exo2 = ": // DATE : FEB . 2026  //  TRANSFERT DE FICHIERS";

    private const string _title_exo3 = "Exercice [ 3 ]";
    private const string _sub_exo3 = ": // DATE : FEB . 2026  //  GESTIONNAIRE D'EMPLOYÉS";

    private const string _title_exo4 = "Exercice [ 4 ]";
    private const string _sub_exo4 = ": // DATE : FEB . 2026  //  SIMULATION.VIRUS";

    private const string _title_exo5 = "Exercice [ 5 ]";
    private const string _sub_exo5 = ": // DATE : FEB . 2026  //  ÉDITEUR DE TEXTE";

    private const string _title_exo6 = "Exercice [ 6 ]";
    private const string _sub_exo6 = ": // DATE : MAR . 2026  //  SPIROGRAPHIE";

    private const string _title_exo7 = "Exercice [ 7 ]";
    private const string _sub_exo7 = ": // DATE : MAR . 2026  //  KEY.LOGGER";

    private const string _title_exo8 = "Exercice [ 8 ]";
    private const string _sub_exo8 = ": // DATE : MAR . 2026  //  EXPLORATEUR ROB.CO";

    private const string _title_exo9 = "Exercice [ 9 ]";
    private const string _sub_exo9 = ": // DATE : APR . 2026  //  RÉACTEUR ABRI 101";

    private const string _title_exo10 = "Exercice [ 10 ]";
    private const string _sub_exo10 = ": // DATE : MAY . 2026  //  HISTO.EN.FOLIES";

    private const string _title_lab1 = "Laboratoire [ 1 ]";
    private const string _sub_lab1 = ": // DATE : FEB . 2026  //  MISE EN PRATIQUE";

    private const string _title_esp8266 = "Capteurs [ ESP.8266 ]";
    private const string _sub_esp8266 = ": // DATE : APR . 2026  //  MISE EN PRATIQUE";

    private const string _title_flashZ = "FlashMaster [ Zeta ]";
    private const string _sub_flashZ = ": // DATE : APR . 2026  //  ⟊⟊☌⎅⎎⊑⍾ ⎅⟊⌇⎎⊑⌇ ⊑⎅⌇⟊☍⊑";
    
    private const string _title_uplink = "Up.Link [ LCD ] ";
    private const string _sub_uplink = ": // DATE : MAY . 2026  //  ⟊⟊☌⎅⎎⊑⍾ ⎅⟊⌇⎎⊑⌇ ⊑⎅⌇⟊☍⊑";

    private const string _title_main = "TERMINAL ROB:CO";
    private const string _sub_main = "//: WELCOME -  USER ://";

    private static bool _isAnimating;
    private static bool _skipIntro;
    private static readonly Random _rng = new();

    private static readonly SolidColorBrush _exo1_title = Brush("FFC000");
    private static readonly SolidColorBrush _exo1_sub = Brush("FFE170");
    private static readonly SolidColorBrush _exo1_author = Brush("FFFFFF");

    private static readonly SolidColorBrush _exo1b3_title = Brush("3DD68C");
    private static readonly SolidColorBrush _exo1b3_sub = Brush("75FFB4");
    
    private static readonly SolidColorBrush _exo2b3_title = Brush("fcfe4d");
    private static readonly SolidColorBrush _exo2b3_sub = Brush("fcf4ed");
    
    private static readonly SolidColorBrush _exo3b3_title = Brush("d61c2e");
    private static readonly SolidColorBrush _exo3b3_sub = Brush("e66f3c");

    private static readonly SolidColorBrush _exo2_title = Brush("FF4242");
    private static readonly SolidColorBrush _exo2_sub = Brush("FF9191");

    private static readonly SolidColorBrush _exo3_title = Brush("4AFF4D");
    private static readonly SolidColorBrush _exo3_sub = Brush("75FF77");

    private static readonly SolidColorBrush _exo4_title = Brush("294DFF");
    private static readonly SolidColorBrush _exo4_sub = Brush("788DFF");

    private static readonly SolidColorBrush _exo5_title = Brush("FD47FF");
    private static readonly SolidColorBrush _exo5_sub = Brush("FD91FF");

    private static readonly SolidColorBrush _exo6_title = Brush("51AD64");
    private static readonly SolidColorBrush _exo6_sub = Brush("77B582");

    private static readonly SolidColorBrush _exo7_title = Brush("8A51AD");
    private static readonly SolidColorBrush _exo7_sub = Brush("A077B5");

    private static readonly SolidColorBrush _exo8_title = Brush("FFF04D");
    private static readonly SolidColorBrush _exo8_sub = Brush("E3D776");

    private static readonly SolidColorBrush _exo9_title = Brush("54FFED");
    private static readonly SolidColorBrush _exo9_sub = Brush("82E0D5");

    private static readonly SolidColorBrush _exo10_title = Brush("294DFF");
    private static readonly SolidColorBrush _exo10_sub = Brush("788DFF");

    private static readonly SolidColorBrush _lab1_title = Brush("FD47FF");
    private static readonly SolidColorBrush _lab1_sub = Brush("FD91FF");

    private static readonly SolidColorBrush _esp8266_title = Brush("91ff47");
    private static readonly SolidColorBrush _esp8266_sub = Brush("adff91");

    private static readonly SolidColorBrush _main_title = Brush("872020");
    private static readonly SolidColorBrush _main_sub = Brush("A63C3C");
    private static readonly SolidColorBrush _main_author = Brush("D3DEDD");

    private static readonly FontFamily _fontTitle = new(new Uri("pack://application:,,,/"), "./Assets/fonts/#bebas neue");
    private static readonly FontFamily _fontSub = new(new Uri("pack://application:,,,/"), "./Assets/fonts/#bebas neue");

    private static SolidColorBrush Brush(string hex) {
        var b = new SolidColorBrush(ColorHelper.HexToColor(hex));
        b.Freeze();
        return b;
    }

    private static async Task RunIntro(MainWindow win, string title, string sub, string author, SolidColorBrush tBrush,
        SolidColorBrush sBrush, SolidColorBrush aBrush, IntroStyle style = IntroStyle.TypewriterGlitch) {
        if (_isAnimating) return;

        try {
            PrepareState(win);

            if (style == IntroStyle.TypewriterGlitch && !_skipIntro) {
                await ShowNoiseBlast(win, tBrush);
                await ShowBootLines(win, sBrush);
                win.TerminalOutputPanel.Children.Clear();
            }

            await PlayHeaderSequence(win, title, sub, tBrush, sBrush, style);
            await PlayFooterSequence(win, author, tBrush, aBrush);

            if (!ConsoleBehavior.SpeedLoad && !_skipIntro) await CRTPowerOff(win);

            FinalizeUI(win);
        }
        finally {
            CleanupState(win);
        }
    }

    private static void PrepareState(MainWindow win) {
        _isAnimating = true;
        _skipIntro = false;
        win.PreviewKeyDown += HandleInputSkip;
        win.PreviewMouseDown += HandleInputSkip;

        win.OutputBox.Document.Blocks.Clear();
        HideEverything(win);
        win.TerminalOutputPanel.Children.Clear();
        win.TerminalOutputPanel.VerticalAlignment = VerticalAlignment.Center;
    }

    private static void CleanupState(MainWindow win) {
        win.PreviewKeyDown -= HandleInputSkip;
        win.PreviewMouseDown -= HandleInputSkip;
        RestoreEverything(win);
        _isAnimating = false;
        _skipIntro = false;
    }

    private static async Task PlayHeaderSequence(MainWindow win, string title, string sub, SolidColorBrush tBrush,
        SolidColorBrush sBrush, IntroStyle style) {
        var rule1 = BuildAccentRule(tBrush);
        win.TerminalOutputPanel.Children.Add(rule1);
        FadeIn(rule1);
        SlideUp(rule1);
        await Wait(ConsoleBehavior.SpeedLoad ? 0 : 120);

        var titleBlock = BuildCenteredLabel(title, 210, _fontTitle, tBrush, 8);
        win.TerminalOutputPanel.Children.Add(titleBlock);
        SlideUp(titleBlock, 350, 30);

        if (style == IntroStyle.TypewriterGlitch) await TypewriterGlitch(titleBlock, 22);
        else FadeIn(titleBlock, 350);

        await Wait(ConsoleBehavior.SpeedLoad ? 0 : 400);

        var subBlock = BuildCenteredLabel(sub, 56, _fontSub, sBrush, 10);
        win.TerminalOutputPanel.Children.Add(subBlock);
        SlideUp(subBlock, 300, 15);

        if (style == IntroStyle.TypewriterGlitch) await TypewriterGlitch(subBlock, 12);
        else FadeIn(subBlock);

        await Wait(ConsoleBehavior.SpeedLoad ? 0 : 600);
    }

    private static async Task PlayFooterSequence(MainWindow win, string author, SolidColorBrush tBrush,
        SolidColorBrush aBrush) {
        var rule2 = BuildAccentRule(tBrush);
        win.TerminalOutputPanel.Children.Add(rule2);
        FadeIn(rule2);
        await Wait(ConsoleBehavior.SpeedLoad ? 0 : 200);

        var cursor = BuildBlinkingCursor(tBrush);
        win.TerminalOutputPanel.Children.Add(cursor);
        await Wait(ConsoleBehavior.SpeedLoad ? 0 : 700);
        win.TerminalOutputPanel.Children.Remove(cursor);

        var authorBlock = BuildCenteredLabel(author, 56, _fontSub, aBrush, 14);
        win.TerminalOutputPanel.Children.Add(authorBlock);
        FadeIn(authorBlock, 400);
        SlideUp(authorBlock, 400, 10);
        await Wait(ConsoleBehavior.SpeedLoad ? 0 : 1200);
    }

    private static void FinalizeUI(MainWindow win) {
        win.TerminalOutputPanel.Children.Clear();
        win.TerminalOutputPanel.VerticalAlignment = VerticalAlignment.Top;
    }

    private static async Task Wait(int ms) {
        var elapsed = 0;
        while (elapsed < ms && !_skipIntro) {
            await Task.Delay(16);
            elapsed += 16;
        }
    }

    private static void HandleInputSkip(object sender, InputEventArgs e) {
        _skipIntro = true;
    }

    private static TextBlock BuildCenteredLabel(string text, double fontSize, FontFamily font, SolidColorBrush brush,
        double topMargin) {
        return new TextBlock {
            Text = text, FontSize = fontSize, FontFamily = font, Foreground = brush, Opacity = 0,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, topMargin, 0, 0),
            Effect = new DropShadowEffect { Color = brush.Color, ShadowDepth = 0, BlurRadius = 18, Opacity = 0.65 }
        };
    }

    private static TextBlock BuildAccentRule(SolidColorBrush brush) {
        return new TextBlock {
            Text = new string('─', 60), FontSize = 20, FontFamily = _fontSub,
            Foreground = brush, Opacity = 0, HorizontalAlignment = HorizontalAlignment.Center
        };
    }

    private static void FadeIn(UIElement el, int ms = 300) {
        var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(ms)) {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        el.BeginAnimation(UIElement.OpacityProperty, anim);
    }

    private static void SlideUp(UIElement el, int ms = 350, double fromY = 20) {
        el.RenderTransform = new TranslateTransform(0, fromY);
        var anim = new DoubleAnimation(fromY, 0, TimeSpan.FromMilliseconds(ms)) {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        ((TranslateTransform)el.RenderTransform).BeginAnimation(TranslateTransform.YProperty, anim);
    }

    private static async Task TypewriterGlitch(TextBlock tb, int msPerChar = 18) {
        var fullText = tb.Text;
        tb.Text = "";
        tb.Opacity = 1;
        const string glitchChars = "&*#@%?![]{}<>X01";

        for (var i = 0; i < fullText.Length; i++) {
            if (_skipIntro) {
                tb.Text = fullText;
                return;
            }

            if (fullText[i] == ' ') {
                tb.Text += ' ';
                continue;
            }

            for (var g = 0; g < 2; g++) {
                if (_skipIntro) break;
                tb.Text = fullText.Substring(0, i) + glitchChars[_rng.Next(glitchChars.Length)];
                await Task.Delay(10);
            }

            tb.Text = fullText.Substring(0, i + 1);
            await Task.Delay(_rng.Next(10) > 7 ? msPerChar * 2 : msPerChar);
        }
    }

    private static void HideEverything(MainWindow win) {
        win.OutputBox.Visibility = win.TopText.Visibility = win.InputBox.Visibility =
            win.InputBoxIndicator.Visibility = win.VisualModeB2.Visibility = win.VisualModeB3.Visibility =
                win.MainMenuButton.Visibility = win.WinBtnClose.Visibility =
                    win.WinBtnMaximize.Visibility = win.WinBtnMinimize.Visibility = Visibility.Collapsed;
    }

    private static void RestoreEverything(MainWindow win) {
        win.OutputBox.Visibility = win.TopText.Visibility = win.InputBox.Visibility =
            win.InputBoxIndicator.Visibility = win.VisualModeB2.Visibility = win.VisualModeB3.Visibility =
                win.MainMenuButton.Visibility = win.WinBtnClose.Visibility =
                    win.WinBtnMaximize.Visibility = win.WinBtnMinimize.Visibility = Visibility.Visible;
    }

    private static async Task ShowNoiseBlast(MainWindow win, SolidColorBrush accentBrush, int lines = 4) {
        var noiseBlocks = new TextBlock[lines];
        for (var i = 0; i < lines; i++) {
            if (_skipIntro) break;
            var unfrozen = new SolidColorBrush(accentBrush.Color) { Opacity = 0.2 + _rng.NextDouble() * 0.25 };
            var tb = new TextBlock {
                Text = GenerateHexNoise(52), FontSize = 20, FontFamily = _fontSub, Foreground = unfrozen,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            win.TerminalOutputPanel.Children.Add(tb);
            noiseBlocks[i] = tb;
            await Task.Delay(30);
        }

        await Wait(120);
        foreach (var tb in noiseBlocks)
            if (tb != null)
                win.TerminalOutputPanel.Children.Remove(tb);
    }

    private static string GenerateHexNoise(int length = 48) {
        const string chars = "0123456789ABCDEF .:";
        var sb = new StringBuilder(length);
        for (var i = 0; i < length; i++) sb.Append(chars[_rng.Next(chars.Length)]);
        return sb.ToString();
    }

    private static async Task ShowBootLines(MainWindow win, SolidColorBrush accentBrush) {
        string[] lines = {
            "> INIT SEQUENCE... [ OK ]", "> LOADING MODULE... [ OK ]",
            "> CHECKSUM VERIFY... [ " + GenerateHexNoise(8).Replace(" ", "") + " ]"
        };
        foreach (var line in lines) {
            if (_skipIntro) break;
            var tb = new TextBlock {
                Text = line, FontSize = 20, FontFamily = _fontSub, Foreground = accentBrush, Opacity = 0,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            win.TerminalOutputPanel.Children.Add(tb);
            FadeIn(tb, 150);
            await Wait(220);
        }
    }

    private static TextBlock BuildBlinkingCursor(SolidColorBrush brush) {
        var cursor = new TextBlock {
            Text = "█", FontSize = 22, FontFamily = _fontSub, Foreground = brush,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var blink = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever };
        blink.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        blink.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(500))));
        blink.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1000))));
        cursor.BeginAnimation(UIElement.OpacityProperty, blink);
        return cursor;
    }

    private static async Task CRTPowerOff(MainWindow win) {
        var flash = new Rectangle {
            Fill = Brushes.White, Opacity = 0.8, RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new ScaleTransform(1, 1)
        };
        win.TerminalOutputPanel.Children.Add(flash);
        var st = (ScaleTransform)flash.RenderTransform;

        var animY = new DoubleAnimation(1, 0.005, TimeSpan.FromMilliseconds(120));
        var animX = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(120))
            { BeginTime = TimeSpan.FromMilliseconds(120) };
        var animFade = new DoubleAnimation(0.8, 0, TimeSpan.FromMilliseconds(80))
            { BeginTime = TimeSpan.FromMilliseconds(240) };

        var tcs = new TaskCompletionSource<bool>();
        animFade.Completed += (_, _) => tcs.SetResult(true);

        st.BeginAnimation(ScaleTransform.ScaleYProperty, animY);
        st.BeginAnimation(ScaleTransform.ScaleXProperty, animX);
        flash.BeginAnimation(UIElement.OpacityProperty, animFade);
        await tcs.Task;
    }

    public static async Task Run_Exo1(MainWindow win) {
        await RunIntro(win, _title_exo1, _sub_exo1, _author, _exo1_title, _exo1_sub, _exo1_author);
        win.Exo1.Visibility = win.Exo1B.Visibility = Visibility.Visible;
    }
    public static async Task Run_Exo2(MainWindow win) {
        await RunIntro(win, _title_exo2, _sub_exo2, _author, _exo2_title, _exo2_sub, _exo1_author);
        win.Exo2.Visibility = Visibility.Visible;
    }

    public static async Task Run_Exo3(MainWindow win) {
        await RunIntro(win, _title_exo3, _sub_exo3, _author, _exo3_title, _exo3_sub, _exo1_author);
        win.Exo3.Visibility = Visibility.Visible;
    }

    public static async Task Run_Exo4(MainWindow win) {
        await RunIntro(win, _title_exo4, _sub_exo4, _author, _exo4_title, _exo4_sub, _exo1_author);
    }

    public static async Task Run_Exo5(MainWindow win) {
        await RunIntro(win, _title_exo5, _sub_exo5, _author, _exo5_title, _exo5_sub, _exo1_author);
        win.Exo5.Visibility = Visibility.Visible;
    }

    public static async Task Run_Exo6(MainWindow win) {
        await RunIntro(win, _title_exo6, _sub_exo6, _author, _exo6_title, _exo6_sub, _exo1_author);
        win.Exo6.Visibility = Visibility.Visible;
    }

    public static async Task Run_Exo7(MainWindow win) {
        await RunIntro(win, _title_exo7, _sub_exo7, _author, _exo7_title, _exo7_sub, _exo1_author);
        win.Exo7.Visibility = Visibility.Visible;
    }

    public static async Task Run_Exo8(MainWindow win) {
        await RunIntro(win, _title_exo8, _sub_exo8, _author, _exo8_title, _exo8_sub, _exo1_author);
        win.Exo8.Visibility = Visibility.Visible;
    }

    public static async Task Run_Exo9(MainWindow win) {
        await RunIntro(win, _title_exo9, _sub_exo9, _author, _exo9_title, _exo9_sub, _exo1_author);
        win.Exo9.Visibility = Visibility.Visible;
    }

    public static async Task Run_Exo10(MainWindow win) {
        await RunIntro(win, _title_exo10, _sub_exo10, _author, _exo10_title, _exo10_sub, _exo1_author);
        win.Exo10.Visibility = Visibility.Visible;
    }

    public static async Task Run_Lab1(MainWindow win) {
        await RunIntro(win, _title_lab1, _sub_lab1, _author, _lab1_title, _lab1_sub, _exo1_author);
        win.Labo1B.Visibility = Visibility.Visible;
    }

    public static async Task Run_ESP8266(MainWindow win) {
        await RunIntro(win, _title_esp8266, _sub_esp8266, _author, _esp8266_title, _esp8266_sub, _exo1_author);
        win.ESP8266.Visibility = Visibility.Visible;
    }

    public static async Task Run_Main(MainWindow win) {
        await RunIntro(win, _title_main, _sub_main, _author2, _main_title, _main_sub, _main_author,
            IntroStyle.FadeOnly);
        win.BabyMode.Visibility = ConsoleBehavior.Babymode == 1 ? Visibility.Visible : Visibility.Collapsed;
    }

    public static async Task RunHospital(MainWindow win) {
        Window hospital = new HospitalWindow();
        hospital.Show();
        win.Close();
    }

    public static async Task RunFlashMaster(MainWindow win) {
        await RunIntro(win, _title_flashZ, _title_flashZ, _author2, _lab1_title, _lab1_sub, _main_author,
            IntroStyle.FadeOnly);
        win.FlashMaster.Visibility = Visibility.Visible;
    }

    public static async Task RunUplink(MainWindow win) {
        await RunIntro(win, _title_uplink, _title_uplink, _author2, _lab1_title, _lab1_sub, _main_author,
            IntroStyle.FadeOnly);
        win.Uplink.Visibility = Visibility.Visible;
    }

    public static async Task RunOverseerWars(MainWindow win) {
        OverseerWarInit.Introduce(win);
    }

    public static async Task Run_Exo1B3(MainWindow win) {
        await RunIntro(win, _title_exo1b3, _sub_exo1b3, _author, _exo1b3_title, _exo1b3_sub, _exo1_author);
        win.OutputBox.Visibility = Visibility.Collapsed;
        win.InputBox.Visibility = Visibility.Collapsed;
        win.InputBoxIndicator.Visibility = Visibility.Collapsed;
        win.Exo1B3.Visibility = Visibility.Visible;
    }
    
    public static async Task Run_Exo2B3(MainWindow win) {
        await RunIntro(win, _title_exo2b3, _sub_exo2b3, _author, _exo2b3_title, _exo2b3_sub, _exo1_author);
        win.OutputBox.Visibility = Visibility.Collapsed;
        win.InputBox.Visibility = Visibility.Collapsed;
        win.InputBoxIndicator.Visibility = Visibility.Collapsed;
        win.Exo2B3.Visibility = Visibility.Visible;
    }
    
    public static async Task Run_Exo3B3(MainWindow win) {
        await RunIntro(win, _title_exo3b3, _sub_exo3b3, _author, _exo3b3_title, _exo3b3_sub, _exo1_author);
        win.OutputBox.Visibility = Visibility.Collapsed;
        win.InputBox.Visibility = Visibility.Collapsed;
        win.InputBoxIndicator.Visibility = Visibility.Collapsed;
        win.Exo3B3.Visibility = Visibility.Visible;
    }
}