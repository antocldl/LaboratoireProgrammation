using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace LaboratoireProgrammation.Project.ViewModels.Laboratoires.MicroController;

public partial class FlashMaster : UserControl {
    public FlashMaster() {
        InitializeComponent();
        RefreshPorts();
    }

    private async void RefreshPorts() {
        CbxPorts.Items.Clear();
        try {
            var ports = await Task.Run(() => SerialPort.GetPortNames());
            foreach (var port in ports) CbxPorts.Items.Add(port);
            if (CbxPorts.Items.Count > 0) CbxPorts.SelectedIndex = 0;
        }
        catch { }
    }


    private void Browse_Click(object sender, RoutedEventArgs e) {
        var openFileDialog = new OpenFileDialog {
            Filter = "Hex files (*.hex)|*.hex|All files (*.*)|*.*"
        };
        if (openFileDialog.ShowDialog() == true) TxtHexPath.Text = openFileDialog.FileName;
    }

    private void Upload_Click(object sender, RoutedEventArgs e) {
        if (string.IsNullOrEmpty(TxtHexPath.Text) || CbxPorts.SelectedItem == null) {
            if (CbxProgram.Text.Contains("1"))
                TxtHexPath.Text =
                    "C:\\Users\\antoi\\Documents\\Atmel Studio\\7.0\\ArchitectureDesCPU\\ArchitectureDesCPU\\Debug\\Labo - 1.hex";

            else if (CbxProgram.Text.Contains("2"))
                TxtHexPath.Text =
                    "C:\\Users\\antoi\\Documents\\Atmel Studio\\7.0\\ArchitectureDesCPU\\ArchitectureDesCPU\\Debug\\Labo - 2.hex";

            else if (CbxProgram.Text.Contains("3"))
                TxtHexPath.Text =
                    "C:\\Users\\antoi\\Documents\\Atmel Studio\\7.0\\ArchitectureDesCPU\\ArchitectureDesCPU\\Debug\\Labo - 3.hex";

            else if (CbxProgram.Text.Contains("4"))
                TxtHexPath.Text =
                    "C:\\Users\\antoi\\Documents\\Atmel Studio\\7.0\\ArchitectureDesCPU\\ArchitectureDesCPU\\Debug\\Labo - 4.hex";

            else if (CbxProgram.Text.Contains("Tester"))
                TxtHexPath.Text =
                    "C:\\Users\\antoi\\Documents\\Arduino\\Code_ESP8266\\build\\arduino.avr.uno\\Code_ESP8266.ino.hex";

            else
                Log("[CRITICAL SYSTEM ERROR]: Invalid program selected : " + CbxProgram.Text);

            return;
        }

        ExecuteAvrdude(
            CbxPorts.SelectedItem.ToString(),
            ((ComboBoxItem)CbxBaud.SelectedItem).Content.ToString(),
            TxtHexPath.Text
        );

        TxtHexPath.Text = "";
    }


    private void ExecuteAvrdude(string port, string baud, string hexFile) {
        try {
            var avrPath = @"D:\avrdude.exe";
            var confPath = @"D:\avrdude.conf";


            if (!File.Exists(avrPath)) {
                Log("ERROR: avrdude.exe not found at D:\\");
                return;
            }

            if (!File.Exists(confPath)) {
                Log("ERROR: avrdude.conf not found at D:\\");
                return;
            }


            var args = $"-C \"{confPath}\" -p m328p -c avrisp -P {port} -b {baud} -U flash:w:\"{hexFile}\":i";

            var psi = new ProcessStartInfo {
                FileName = avrPath,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = new Process { StartInfo = psi }) {
                Log($">>> [INITIATING: avrdude]: {args}");

                process.Start();


                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();

                process.WaitForExit();


                if (!string.IsNullOrEmpty(output)) Log(output);
                if (!string.IsNullOrEmpty(error)) Log(error);

                if (process.ExitCode == 0) {
                    Log(">>> [SUCCESS: UPLOAD COMPLETE]");
                }
                else {
                    Log($">>> [FAILURE: EXIT CODE {process.ExitCode}]");
                    Log("CHECK: CAPACITOR (10µF), WIRING (MOSI/MISO), AND RESET PIN.");
                }
            }
        }
        catch (Exception ex) {
            Log($"[CRITICAL SYSTEM ERROR]: {ex.Message}");
        }
    }

    private void Log(string message) {
        TxtLogs.Text += $"\n[{DateTime.Now:HH:mm:ss}] {message}";
        ScrollLogs.ScrollToBottom();
    }
}