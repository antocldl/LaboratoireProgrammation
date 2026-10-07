using System.ComponentModel;
using System.IO.Ports;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace LaboratoireProgrammation.Project.ViewModels.Laboratoires.Laboratoire___1;

public partial class LaboElectroniqueESP8266 : UserControl {
    private readonly SolidColorBrush _greenLedBrush = new(Colors.LimeGreen);
    private readonly SolidColorBrush _logRxBrush = new((Color)ColorConverter.ConvertFromString("#ffffff"));
    private readonly SolidColorBrush _logTimeBrush = new((Color)ColorConverter.ConvertFromString("#6b57ff"));
    private readonly SolidColorBrush _logTxBrush = new((Color)ColorConverter.ConvertFromString("#00ffaa"));
    private readonly SolidColorBrush _redLedBrush = new(Colors.Red);

    private string _buffer = "";
    private bool _isConnected;
    private SerialPort _serialPort;

    private int currentDisplay;
    private DispatcherTimer dateTimer;

    private SerialPort myArduino;

    public LaboElectroniqueESP8266() {
        InitializeComponent();

        if (DesignerProperties.GetIsInDesignMode(this)) return;

        ListerPortsCom();
        AjouterLog("Système", "Console initialisée. En attente de connexion...", _logRxBrush);
    }

    private async void ListerPortsCom() {
        try {
            CbxPorts.Items.Clear();
            string[] ports = await Task.Run(() => SerialPort.GetPortNames());

            foreach (var port in ports) CbxPorts.Items.Add(port);

            if (CbxPorts.Items.Count > 0) {
                CbxPorts.SelectedIndex = 0;
            }
            else {
                CbxPorts.Items.Add("Aucun port");
                CbxPorts.SelectedIndex = 0;
            }
        }
        catch (Exception ex) {
            CbxPorts.Items.Add("Erreur");
            CbxPorts.SelectedIndex = 0;
            CbxPorts.IsEnabled = false;
        }
    }

    private void BtnConnect_Click(object sender, RoutedEventArgs e) {
        if (!_isConnected) {
            if (CbxPorts.SelectedItem == null || CbxPorts.SelectedItem.ToString() == "Aucun port" ||
                CbxPorts.SelectedItem.ToString() == "Erreur") return;

            var portName = CbxPorts.SelectedItem.ToString();

            try {
                _serialPort = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One);
                _serialPort.DataReceived += SerialPort_DataReceived;
                _serialPort.Open();

                _isConnected = true;
                BtnConnect.Content = "DISCONNECT";
                CbxPorts.IsEnabled = false;

                LedConnection.Fill = _greenLedBrush;
                LedConnection.Opacity = 1.0;

                AjouterLog("Système", $"Connecté au port {portName}", _logTxBrush);
            }
            catch (Exception ex) {
                MessageBox.Show($"Erreur de connexion : {ex.Message}", "Erreur Système", MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        else {
            DeconnecterPortSerie();
        }
    }

    private void DeconnecterPortSerie() {
        if (_serialPort != null && _serialPort.IsOpen) _serialPort.Close();

        _isConnected = false;
        BtnConnect.Content = "CONNECT";
        CbxPorts.IsEnabled = true;

        LedConnection.Fill = _redLedBrush;
        LedConnection.Opacity = 0.5;

        TxtTemperature.Text = "--.- °C";
        TxtHumidite.Text = "--.- %";
        TxtLuminosite.Text = "---- LUX";

        AjouterLog("Système", "Déconnecté.", _logRxBrush);
    }

    private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e) {
        try {
            var data = _serialPort.ReadExisting();

            foreach (var c in data)
                if (c == '\n') {
                    var trameComplete = _buffer.Trim();
                    _buffer = "";
                    Dispatcher.Invoke(() => TraiterTrame(trameComplete));
                }
                else if (c != '\r') {
                    _buffer += c;
                }
        }
        catch (Exception) { }
    }

    private void TraiterTrame(string trame) {
        if (string.IsNullOrEmpty(trame) || !trame.StartsWith("T:")) return;

        try {
            var sections = trame.Split(',');

            if (sections.Length == 3) {
                var tempStr = sections[0].Replace("T:", "");
                var humStr = sections[1].Replace("H:", "");
                var lumStr = sections[2].Replace("L:", "");

                TxtTemperature.Text = $"{tempStr} °C";
                TxtHumidite.Text = $"{humStr} %";
                TxtLuminosite.Text = $"{lumStr} LUX";

                _ = ClignoterLedRx();

                AjouterLog("RX", trame, _logRxBrush);
            }
        }
        catch { }
    }

    private async Task ClignoterLedRx() {
        LedRx.Opacity = 1.0;
        await Task.Delay(100);
        LedRx.Opacity = 0.2;
    }

    private void ToggleLogs_Click(object sender, RoutedEventArgs e) {
        var parentBorder = (Border)LogScrollViewer.Parent;
        parentBorder.Visibility =
            parentBorder.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
    }

    private void AjouterLog(string prefixe, string message, SolidColorBrush couleurMessage) {
        TxtLogs.Inlines.Add(new Run($"[{DateTime.Now:HH:mm:ss}] ") { Foreground = _logTimeBrush });
        TxtLogs.Inlines.Add(new Run($"[{prefixe}] {message}\n") { Foreground = couleurMessage });
        LogScrollViewer.ScrollToEnd();
    }

    private void EnvoyerCommande(string commande) {
        if (_isConnected && _serialPort != null && _serialPort.IsOpen) {
            _serialPort.Write(commande);
            AjouterLog("TX", $"Requête d'envoi commande [{commande}]", _logTxBrush);
        }
    }

    private void BtnLight_Click(object sender, RoutedEventArgs e) {
        var btn = sender as Button;
        if (btn == null || btn.Tag == null) return;

        var cmd = btn.Tag.ToString();
        EnvoyerCommande(cmd);

        BtnLightOn.Style = (Style)FindResource("RedBtnInactive");
        BtnLightAuto.Style = (Style)FindResource("RedBtnInactive");
        BtnLightOff.Style = (Style)FindResource("RedBtnInactive");
        btn.Style = (Style)FindResource("RedBtnActive");

        if (cmd == "a") IndicateurLumiere.Opacity = 1.0;
        else if (cmd == "c") IndicateurLumiere.Opacity = 0.2;
        else IndicateurLumiere.Opacity = 0.2;
    }

    private void BtnTemp_Click(object sender, RoutedEventArgs e) {
        var btn = sender as Button;
        if (btn == null || btn.Tag == null) return;

        var cmd = btn.Tag.ToString();
        EnvoyerCommande(cmd);

        BtnTempOn.Style = (Style)FindResource("BlueBtnInactive");
        BtnTempAuto.Style = (Style)FindResource("BlueBtnInactive");
        BtnTempOff.Style = (Style)FindResource("BlueBtnInactive");
        btn.Style = (Style)FindResource("BlueBtnActive");

        if (cmd == "b") IndicateurTemp.Opacity = 1.0;
        else if (cmd == "u") IndicateurTemp.Opacity = 0.2;
        else IndicateurTemp.Opacity = 0.2;
    }

    private void BtnDateToggle_Click(object sender, RoutedEventArgs e) {
        currentDisplay = currentDisplay == 0 ? 1 : 0;

        if (currentDisplay == 1) {
            DateBlock.Text = "[ AFFICHAGE DE DATE ] (ON)";

            if (myArduino == null) myArduino = new SerialPort("COM13", 9600);

            try {
                if (!myArduino.IsOpen) myArduino.Open();

                if (dateTimer == null) {
                    dateTimer = new DispatcherTimer();
                    dateTimer.Interval = TimeSpan.FromSeconds(0.1);
                    dateTimer.Tick += SendDateToArduino;
                }

                dateTimer.Start();
            }
            catch (Exception ex) {
                Console.WriteLine("Error: " + ex.Message);
            }
        }
        else {
            DateBlock.Text = "[ AFFICHAGE DE DATE ] (OFF)";

            if (dateTimer != null) dateTimer.Stop();

            try {
                if (myArduino != null && myArduino.IsOpen) {
                    myArduino.WriteLine("DATE:OFF");
                    myArduino.Close();
                }
            }
            catch (Exception ex) {
                Console.WriteLine("Error: " + ex.Message);
            }
        }
    }

    private void SendDateToArduino(object sender, EventArgs e) {
        try {
            if (myArduino != null && myArduino.IsOpen) {
                var currentDate = DateTime.Now.ToString("dd/MM/yyyy");
                var currentTime = DateTime.Now.ToString("HH:mm:ss");
                myArduino.WriteLine($"DATE:{currentDate}|{currentTime}");
            }
        }
        catch (Exception ex) {
            Console.WriteLine("Error: " + ex.Message);
        }
    }

    private void BtnSendPassword_Click(object sender, RoutedEventArgs e) {
        var pass = TxtPasswordInput.Text;
        if (string.IsNullOrWhiteSpace(pass)) return;

        try {
            if (myArduino == null) myArduino = new SerialPort("COM13", 9600);

            if (!myArduino.IsOpen) myArduino.Open();

            myArduino.WriteLine($"PASS:{pass}");
            TxtPasswordInput.Text = "";
        }
        catch (Exception ex) {
            Console.WriteLine("Error: " + ex.Message);
        }
    }
}