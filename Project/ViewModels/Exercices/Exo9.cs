using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using LaboratoireProgrammation.Project.Helpers;

namespace LaboratoireProgrammation.Project.Views.Exercices;

public partial class Exo9 : UserControl {
    private readonly ReactorCoreHelper _reactor;
    private Process _externalNotepadProcess;
    private Stopwatch _notepadStopwatch;
    private DispatcherTimer _uiTimer;

    public Exo9() {
        InitializeComponent();
        _reactor = new ReactorCoreHelper();
        Loaded += Exo9_Loaded;
    }

    private void Exo9_Loaded(object sender, RoutedEventArgs e) {
        if (!VaultProcessHelper.CheckSingleInstance(Process.GetCurrentProcess().ProcessName)) {
            TxtSecurityStatus.Text = "🔒 ERREUR FATALE : ACCÈS SIMULTANÉ DÉTECTÉ. (ERR.X145Y3)";
            TxtSecurityStatus.Foreground = Brushes.Red;
            InputGrade.IsEnabled = false;
            InputNom.IsEnabled = false;
        }
        else {
            TxtSecurityStatus.Text = "✅ Système en état. Instance unique validée.";
        }
    }

    private async void BtnAuthenticate_Click(object sender, RoutedEventArgs e) {
        var grade = InputGrade.Text;
        var nom = InputNom.Text;

        if (string.IsNullOrWhiteSpace(grade) || string.IsNullOrWhiteSpace(nom)) return;

        var path = @"ProgrammeSecondaire.exe";

        try {
            var result = await Task.Run(() => VaultProcessHelper.AuthenticateUser(path, grade, nom));

            PanelLogin.Visibility = Visibility.Hidden;
            PanelReactor.Visibility = Visibility.Visible;

            ShowInternalNotepad();
            LaunchExternalNotepad();
        }
        catch (Exception ex) {
            TxtSecurityStatus.Text = "Erreur de communication avec le programme secondaire.";
        }
    }

    private void ShowInternalNotepad() {
        PopupNotepad.Visibility = Visibility.Visible;
        _notepadStopwatch = Stopwatch.StartNew();

        _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _uiTimer.Tick += (s, ev) => { TxtTimer.Text = _notepadStopwatch.Elapsed.ToString(@"mm\:ss"); };
        _uiTimer.Start();
    }

    private void LaunchExternalNotepad() {
        var tempPath = Path.Combine(Path.GetTempPath(), "VaultTec_Guide.txt");
        _externalNotepadProcess = VaultProcessHelper.OpenSurvivalGuide(tempPath);
    }

    private void BtnCloseNotepad_Click(object sender, RoutedEventArgs e) {
        _notepadStopwatch.Stop();
        _uiTimer.Stop();
        PopupNotepad.Visibility = Visibility.Hidden;

        if (_externalNotepadProcess != null && !_externalNotepadProcess.HasExited) _externalNotepadProcess.Kill();
    }

    private async void BtnStartPumps_Click(object sender, RoutedEventArgs e) {
        TxtPumpLog.Text = "";
        var btn = sender as Button;
        btn.IsEnabled = false;

        var threadP = new Thread(() => RunPump('P'));
        var threadS = new Thread(() => RunPump('S'));

        threadP.Start();
        threadS.Start();

        await Task.Run(() => {
            threadP.Join();
            threadS.Join();
        });

        Dispatcher.Invoke(() => {
            TxtPumpLog.Text += "\n\n[ CYCLES TERMINÉS ]";
            btn.IsEnabled = true;
        });
    }

    private void RunPump(char id) {
        var rnd = new Random();
        var str = id.ToString();
        for (var i = 0; i < 250; i++) {
            Thread.Sleep(rnd.Next(1, 5));
            Dispatcher.BeginInvoke(() => { TxtPumpLog.Text += str; });
        }
    }

    private async void BtnRegulate_Click(object sender, RoutedEventArgs e) {
        _reactor.ResetPressure();
        UpdatePressureUI();

        var btn = sender as Button;
        btn.IsEnabled = false;

        var pressureAction = DetermineSecurityMethod();

        var threadA = new Thread(() => ApplyPressure(pressureAction));
        var threadB = new Thread(() => ApplyPressure(pressureAction));

        threadA.Start();
        threadB.Start();

        await Task.Run(() => {
            threadA.Join();
            threadB.Join();
        });

        UpdatePressureUI();
        btn.IsEnabled = true;
    }

    private Action DetermineSecurityMethod() {
        if (RbLock.IsChecked == true) return _reactor.IncreasePressureLock;
        if (RbRWLock.IsChecked == true) return _reactor.IncreasePressureRWLock;
        if (RbMutex.IsChecked == true) return _reactor.IncreasePressureMutex;

        return _reactor.IncreasePressureUnsafe;
    }

    private void ApplyPressure(Action action) {
        for (var i = 0; i < 100; i++) {
            action();
            Dispatcher.Invoke(UpdatePressureUI);
        }
    }

    private void UpdatePressureUI() {
        TxtPressure.Text = _reactor.Pressure.ToString("D3");
        TxtPressure.Foreground = _reactor.Pressure > 200
            ? Brushes.Red
            : Brushes.LightGreen;
    }
}