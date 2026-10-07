using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LaboratoireProgrammation.Project.Helpers;
using LaboratoireProgrammation.Project.Services;
using LaboratoireProgrammation.Project.ViewModels.Menu;
using Microsoft.Data.SqlClient;
using MySqlConnector;

namespace LaboratoireProgrammation.Project.ViewModels.Laboratoires.Laboratoire___1;

public partial class ConnectControl : UserControl {
    private static readonly string server = "localhost";
    private static readonly string database = "LAB_HOPITAL";
    private static readonly string user = "root";
    private static readonly string password = "";

    public static string SuccessfulConnectionString =
        $"Server={server};Database={database};User Id={user};Password={password};";

    private static Func<IDbConnection> connectionFactory;
    private static IDbConnection connectionInterface;
    private static MySqlConnection mySqlConnection;
    private bool isTrying;
    private MySqlDataReader myReader;
    private int TrialsLeft = 4;
    private static Cursor? _hourglassCursor;

    private static Cursor GetHourglassCursor() {
        if (_hourglassCursor != null) return _hourglassCursor;
        var cursorImage = new Image {
            LayoutTransform = new ScaleTransform(6, 6),
            Source = new BitmapImage(new Uri(
                "pack://application:,,,/LaboratoireProgrammation;component/Assets/images/hourglass.png",
                UriKind.Absolute))
        };
        _hourglassCursor = CursorHelper.CreateCursor(cursorImage, 40, 40);
        return _hourglassCursor;
    }

    public ConnectControl() {
        InitializeComponent();
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e) {
        if (isTrying) return;
        isTrying = true;

        pbStatus.Visibility = Visibility.Visible;

        var server = ServerInput.Text;
        var database = DatabaseInput.Text;
        var user = UserInput.Text;
        var password = PasswordInput.Password;

        var parentWindow = WindowHelper.getParentWindow(this);

        if (parentWindow != null) {
            parentWindow.Cursor = GetHourglassCursor();
        }

        if (DbTypeInput.SelectedIndex == 1) {
            var msSqlConnString =
                $"Server={server};Database={database};User Id={user};Password={password};TrustServerCertificate=True;";
            connectionFactory = () => new SqlConnection(msSqlConnString);
        }
        else {
            var mySqlConnString = $"Server={server};Database={database};User={user};Password={password};";
            connectionFactory = () => new MySqlConnection(mySqlConnString);
            mySqlConnection = new MySqlConnection(mySqlConnString);
        }

        var db = new SqlUtils(connectionFactory);

        try {
            await Task.Run(() => db.AssertStatus());
        }
        catch (Exception ex) {
            pbStatus.Visibility = Visibility.Hidden;
            Label.Content = "[ERROR] > Oh non! La connexion à la base de données a échoué";
            Label.Visibility = Visibility.Visible;
            RemainingTrials.Visibility = Visibility.Visible;
            RemainingTrialsBar.Visibility = Visibility.Visible;
            TrialsLeft--;
            RemainingTrials.Content = "Essais restants (" + TrialsLeft + ")";
            RemainingTrialsBar.Value = TrialsLeft * 25;

            ServerInput.Text = "";
            DatabaseInput.Text = "";
            UserInput.Text = "";
            PasswordInput.Password = "";

            if (TrialsLeft == 0) LockTrials();

            isTrying = false;
            if (parentWindow != null) parentWindow.Cursor = null;

            return;
        }

        isTrying = false;
        if (parentWindow != null) parentWindow.Cursor = null;
        Label.Content = "[SUCCESS] > Connexion à la base de données effectuée avec succès !";
        Label.Visibility = Visibility.Visible;
        pbStatus.Visibility = Visibility.Hidden;

        if (DbTypeInput.SelectedIndex == 1)
            SuccessfulConnectionString =
                $"Server={server};Database={database};User Id={user};Password={password};TrustServerCertificate=True;";
        else
            SuccessfulConnectionString = $"Server={server};Database={database};User={user};Password={password};";

        ConfirmConnection();
    }

    private void LockTrials() {
        Label.Visibility = Visibility.Visible;
        Label.Content = "[ERROR] <!> Trop de tentatives ! Vous avez été bloqué(e) ! <!>";
        pbStatus.Visibility = Visibility.Hidden;
        RemainingTrials.Visibility = Visibility.Hidden;
        RemainingTrialsBar.Visibility = Visibility.Hidden;
        ConnectButton.Visibility = Visibility.Hidden;

        ConnectButton.Foreground = ColorHelper.CriticalBrush;
        Label.Foreground = ColorHelper.CriticalBrush;
        Label.BorderBrush = ColorHelper.CriticalBrush;
        ServerLabel.Foreground = ColorHelper.CriticalBrush;
        DatabaseLabel.Foreground = ColorHelper.CriticalBrush;
        UserLabel.Foreground = ColorHelper.CriticalBrush;
        PasswordLabel.Foreground = ColorHelper.CriticalBrush;
        ServerInput.BorderBrush = ColorHelper.CriticalBrush;
        DatabaseInput.BorderBrush = ColorHelper.CriticalBrush;
        UserInput.BorderBrush = ColorHelper.CriticalBrush;
        PasswordInput.BorderBrush = ColorHelper.CriticalBrush;
        TitleLabel.Foreground = ColorHelper.CriticalBrush;
        DbTypeLabel.Foreground = ColorHelper.CriticalBrush;
        DbTypeInput.BorderBrush = ColorHelper.CriticalBrush;
    }

    private async void ConfirmConnection() {
        pbStatus.Visibility = Visibility.Hidden;
        RemainingTrials.Visibility = Visibility.Hidden;
        RemainingTrialsBar.Visibility = Visibility.Hidden;
        ConnectButton.Visibility = Visibility.Hidden;

        ConnectButton.Foreground = ColorHelper.SuccessBrush;
        Label.Foreground = ColorHelper.SuccessBrush;
        Label.BorderBrush = ColorHelper.SuccessBrush;
        ServerLabel.Foreground = ColorHelper.SuccessBrush;
        DatabaseLabel.Foreground = ColorHelper.SuccessBrush;
        UserLabel.Foreground = ColorHelper.SuccessBrush;
        PasswordLabel.Foreground = ColorHelper.SuccessBrush;
        ServerInput.BorderBrush = ColorHelper.SuccessBrush;
        DatabaseInput.BorderBrush = ColorHelper.SuccessBrush;
        UserInput.BorderBrush = ColorHelper.SuccessBrush;
        PasswordInput.BorderBrush = ColorHelper.SuccessBrush;
        TitleLabel.Foreground = ColorHelper.SuccessBrush;
        DbTypeLabel.Foreground = ColorHelper.SuccessBrush;
        DbTypeInput.BorderBrush = ColorHelper.SuccessBrush;

        await Task.Delay(3000);

        var cmd = new MySqlCommand("SELECT * FROM To_Hire", mySqlConnection);

        try {
            mySqlConnection.Open();
            myReader = cmd.ExecuteReader();

            while (myReader.Read()) {
                if (Label.Content.ToString().Length > 0)
                    Label.Content += Environment.NewLine;

                for (var i = 0; i < myReader.FieldCount; i++)
                    Label.Content += myReader[i] + "    ";
            }
        }
        catch (Exception ex) {
            MessageBox.Show(ex.Message);
        }

        mySqlConnection.Close();

        await Task.Delay(5000);


        var parentWindow = WindowHelper.getParentWindow(this);
        var mainWindow = (MainWindow)parentWindow;
        mainWindow.BackToMenu(null, null);
    }
}