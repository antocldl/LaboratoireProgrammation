using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LaboratoireProgrammation.Project.ModernOverseerWars.Models;
using LaboratoireProgrammation.Project.ModernOverseerWarsMP.Networking;

namespace LaboratoireProgrammation.Project.ModernOverseerWarsMP.Views;

public partial class OverseerWarsMpMenuWindow : Window {
    private readonly LanServerScanner _scanner;
    private MpServer? _server;
    private MpClient? _client;

    public string LocalPlayerName { get; private set; } = "Wanderer";
    public int LocalThemeIndex { get; private set; } = 0;

    private bool _isHost = false;
    private bool _clientIsReady = false;

    public OverseerWarsMpMenuWindow() {
        InitializeComponent();

        InitializeThemes();
        UpdateProfilePreview();

        _scanner = new LanServerScanner();
        _scanner.ServersUpdated += OnServersUpdatedFromScanner;
        _scanner.Start();

        SelectTab(TabBrowserBtn, TabBrowser);
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void OnCloseClick(object sender, RoutedEventArgs e) {
        CleanupNetworking();
        Close();
    }

    private void CleanupNetworking() {
        _scanner.Dispose();
        _server?.Dispose();
        _server = null;
        _client?.Dispose();
        _client = null;
    }

    protected override void OnClosed(EventArgs e) {
        base.OnClosed(e);
        CleanupNetworking();
    }

    #region Tab Navigation
    private void OnTabBrowserClick(object sender, RoutedEventArgs e) => SelectTab(TabBrowserBtn, TabBrowser);
    private void OnTabHostClick(object sender, RoutedEventArgs e) => SelectTab(TabHostBtn, TabHost);
    private void OnTabProfileClick(object sender, RoutedEventArgs e) => SelectTab(TabProfileBtn, TabProfile);

    private void SelectTab(Button activeBtn, Grid activeGrid) {
        TabBrowser.Visibility = Visibility.Collapsed;
        TabHost.Visibility = Visibility.Collapsed;
        TabProfile.Visibility = Visibility.Collapsed;

        TabBrowserBtn.Opacity = 0.6;
        TabHostBtn.Opacity = 0.6;
        TabProfileBtn.Opacity = 0.6;

        activeGrid.Visibility = Visibility.Visible;
        activeBtn.Opacity = 1.0;
    }
    #endregion

    #region Profile & Themes
    private void InitializeThemes() {
        var items = new List<ThemeItemViewModel>();
        for (int i = 0; i < PlayerThemes.All.Length; i++) {
            var t = PlayerThemes.All[i];
            items.Add(new ThemeItemViewModel {
                Index = i,
                DisplayColor = (Color)ColorConverter.ConvertFromString(t.Color),
                IsSelected = i == LocalThemeIndex
            });
        }
        ThemeItemsList.ItemsSource = items;
    }

    private void OnProfileNameChanged(object sender, TextChangedEventArgs e) {
        LocalPlayerName = string.IsNullOrWhiteSpace(ProfileNameBox.Text) ? "Wanderer" : ProfileNameBox.Text.Trim();
        UpdateProfilePreview();
    }

    private void OnThemeDotClicked(object sender, MouseButtonEventArgs e) {
        if (sender is FrameworkElement { Tag: ThemeItemViewModel vm }) {
            LocalThemeIndex = vm.Index;
            var list = ThemeItemsList.ItemsSource as List<ThemeItemViewModel>;
            if (list != null) {
                foreach (var item in list) item.IsSelected = item.Index == LocalThemeIndex;
                ThemeItemsList.Items.Refresh();
            }
            UpdateProfilePreview();

            if (_client != null && !_isHost) {
                _ = _client.ChangeThemeAsync(LocalThemeIndex);
            }
        }
    }

    private void UpdateProfilePreview() {
        if (ProfilePreviewBorder == null || ProfilePreviewText == null) return;
        var theme = PlayerThemes.All[LocalThemeIndex];
        ProfilePreviewBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.BgColor));
        ProfilePreviewBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.Color));
        ProfilePreviewText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.Color));
        ProfilePreviewText.Text = LocalPlayerName;
    }
    #endregion

    #region Server Browser
    private void OnServersUpdatedFromScanner(List<DiscoveredServer> servers) {
        Dispatcher.InvokeAsync(() => {
            ServerListBox.ItemsSource = servers;
            EmptyScanText.Visibility = servers.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        });
    }

    private void OnRefreshScanClick(object sender, RoutedEventArgs e) {
        var servers = _scanner.GetServers();
        ServerListBox.ItemsSource = null;
        ServerListBox.ItemsSource = servers;
        EmptyScanText.Visibility = servers.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        GlobalStatusText.Text = "> RE-SCANNED LOCAL QUANTUM FREQUENCIES.";
    }

    private void OnServerSelectedChanged(object sender, SelectionChangedEventArgs e) {
        ConnectSelectedBtn.IsEnabled = ServerListBox.SelectedItem is DiscoveredServer;
    }

    private async void OnConnectSelectedClick(object sender, RoutedEventArgs e) {
        if (ServerListBox.SelectedItem is not DiscoveredServer srv) return;
        await ConnectToServerAsync(srv.IpAddress, srv.Port);
    }

    private async void OnDirectConnectClick(object sender, RoutedEventArgs e) {
        string ip = DirectIpBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(ip)) ip = "127.0.0.1";
        if (!int.TryParse(DirectPortBox.Text.Trim(), out int port)) port = 7777;

        await ConnectToServerAsync(ip, port);
    }

    private async Task ConnectToServerAsync(string ip, int port) {
        GlobalStatusText.Text = $"> ATTEMPTING COMM LINK TO {ip}:{port}...";

        _client = new MpClient(LocalPlayerName, LocalThemeIndex);
        _client.LobbyUpdated += OnClientLobbyUpdated;
        _client.ChatReceived += OnChatReceived;
        _client.Disconnected += OnClientDisconnected;
        _client.GameStarted += OnClientGameStarted;

        bool success = await _client.ConnectAsync(ip, port);
        if (!success) {
            GlobalStatusText.Text = $"> [ERROR] COULD NOT ESTABLISH LINK TO {ip}:{port}. CHECK PORT OR HOST STATUS.";
            _client.Dispose();
            _client = null;
            return;
        }

        _isHost = false;
        SetupLobbyView(isHost: false, serverName: $"Remote Vault ({ip})", address: $"{ip}:{port}");
    }
    #endregion

    #region Create Server
    private void OnCreateServerClick(object sender, RoutedEventArgs e) {
        string srvName = ServerNameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(srvName)) srvName = "Vault 101 Command";

        if (!int.TryParse(HostPortBox.Text.Trim(), out int port)) port = 7777;
        if (!int.TryParse(TurnDurationBox.Text.Trim(), out int turnDur)) turnDur = 60;
        bool broadcast = BroadcastLanCheck.IsChecked == true;

        try {
            _server = new MpServer(srvName, LocalPlayerName, LocalThemeIndex, port, turnDur, broadcast);
            _server.LobbyUpdated += OnServerLobbyUpdated;
            _server.ChatReceived += OnChatReceived;
            _server.ClientDisconnected += OnServerClientDisconnected;
            _server.GameStartedLocally += OnServerGameStartedLocally;

            _isHost = true;
            SetupLobbyView(isHost: true, serverName: srvName, address: $"0.0.0.0:{port}");
            GlobalStatusText.Text = $"> SERVER LAUNCHED ON PORT {port}. BROADCASTING ON FREQUENCY.";
        } catch (Exception ex) {
            GlobalStatusText.Text = $"> [ERROR] FAILED TO INITIALIZE SERVER: {ex.Message}";
        }
    }
    #endregion

    #region Lobby
    private void SetupLobbyView(bool isHost, string serverName, string address) {
        MenuContainer.Visibility = Visibility.Collapsed;
        LobbyContainer.Visibility = Visibility.Visible;

        LobbyServerNameText.Text = serverName;
        LobbyAddressText.Text = $" ({address})";
        LobbyStatusRoleText.Text = isHost ? "[ ROLE: HOSTING OVERSEER ]" : "[ ROLE: CHALLENGER OVERSEER ]";

        HostStartGameBtn.Visibility = isHost ? Visibility.Visible : Visibility.Collapsed;
        HostStartGameBtn.IsEnabled = false;

        ClientReadyBtn.Visibility = isHost ? Visibility.Collapsed : Visibility.Visible;
        _clientIsReady = false;
        ClientReadyBtn.Content = "> READY FOR COMBAT [OFF]";

        ChatLogText.Text = $"[SYSTEM] FREQUENCY LINK ESTABLISHED AT {DateTime.Now:HH:mm:ss}.\n";

        if (isHost) {
            UpdatePlayerCard(P1CardBorder, P1NameText, LocalPlayerName, LocalThemeIndex);
            P1StatusText.Text = "HOST - READY";

            P2NameText.Text = "AWAITING LINK...";
            P2StatusText.Text = "SCANNING...";
            P2CardBorder.BorderBrush = (SolidColorBrush)FindResource("TerminalDim");
            P2CardBorder.Background = new SolidColorBrush(Color.FromRgb(0x15, 0x15, 0x15));
            P2NameText.Foreground = (SolidColorBrush)FindResource("TerminalDim");
        }
    }

    private void UpdatePlayerCard(Border cardBorder, TextBlock nameText, string name, int themeIdx) {
        var theme = PlayerThemes.All[themeIdx];
        cardBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.BgColor));
        cardBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.Color));
        nameText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.Color));
        nameText.Text = name;
    }

    private void OnServerLobbyUpdated(LobbyUpdatePayload lobby) {
        Dispatcher.InvokeAsync(() => {
            UpdateLobbyUi(lobby);
            HostStartGameBtn.IsEnabled = lobby.HasClient && lobby.ClientReady;
        });
    }

    private void OnClientLobbyUpdated(LobbyUpdatePayload lobby) {
        Dispatcher.InvokeAsync(() => {
            UpdateLobbyUi(lobby);
        });
    }

    private void UpdateLobbyUi(LobbyUpdatePayload lobby) {
        LobbyServerNameText.Text = lobby.ServerName;

        // P1 (Host)
        UpdatePlayerCard(P1CardBorder, P1NameText, lobby.HostName, lobby.HostThemeIndex);
        P1StatusText.Text = "HOST - READY";

        // P2 (Client)
        if (lobby.HasClient) {
            UpdatePlayerCard(P2CardBorder, P2NameText, lobby.ClientName, lobby.ClientThemeIndex);
            P2StatusText.Text = lobby.ClientReady ? "READY FOR COMBAT" : "PREPARING...";
        } else {
            P2NameText.Text = "AWAITING LINK...";
            P2StatusText.Text = "SCANNING...";
            P2CardBorder.BorderBrush = (SolidColorBrush)FindResource("TerminalDim");
            P2CardBorder.Background = new SolidColorBrush(Color.FromRgb(0x15, 0x15, 0x15));
            P2NameText.Foreground = (SolidColorBrush)FindResource("TerminalDim");
        }
    }

    private void OnChatReceived(ChatMessagePayload chat) {
        Dispatcher.InvokeAsync(() => {
            ChatLogText.Text += $"[{chat.TimeString}] {chat.Sender}: {chat.Text}\n";
            ChatScrollViewer.ScrollToEnd();
        });
    }

    private void OnSendChatClick(object sender, RoutedEventArgs e) => SendChatMessage();
    private void OnChatInputKeyDown(object sender, KeyEventArgs e) {
        if (e.Key == Key.Enter) SendChatMessage();
    }

    private void SendChatMessage() {
        string text = ChatInputBox.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;
        ChatInputBox.Text = "";

        if (_isHost) {
            _server?.SendChatMessage(LocalPlayerName, text);
        } else {
            _ = _client?.SendChatAsync(text);
        }
    }

    private void OnClientReadyToggleClick(object sender, RoutedEventArgs e) {
        if (_client == null) return;
        _clientIsReady = !_clientIsReady;
        ClientReadyBtn.Content = _clientIsReady ? "> READY FOR COMBAT [ON]" : "> READY FOR COMBAT [OFF]";
        ClientReadyBtn.Foreground = _clientIsReady ? Brushes.LightGreen : (SolidColorBrush)FindResource("TerminalGreen");
        _ = _client.ToggleReadyAsync(_clientIsReady);
    }

    private void OnHostStartGameClick(object sender, RoutedEventArgs e) {
        if (_server == null) return;
        _server.StartGame();
    }

    private void OnServerGameStartedLocally(GameState state) {
        Dispatcher.InvokeAsync(() => {
            var srv = _server;
            _server = null; // Transfer ownership to game window so Close() does not dispose it
            var mpWindow = new OverseerWarsMpWindow(srv!, null, isPlayer1: true);
            mpWindow.Show();
            Close();
        });
    }

    private void OnClientGameStarted(GameState state) {
        Dispatcher.InvokeAsync(() => {
            var cli = _client;
            _client = null; // Transfer ownership to game window so Close() does not dispose it
            var mpWindow = new OverseerWarsMpWindow(null, cli!, isPlayer1: false);
            mpWindow.Show();
            Close();
        });
    }

    private void OnServerClientDisconnected(string clientName) {
        Dispatcher.InvokeAsync(() => {
            ChatLogText.Text += $"[SYSTEM] {clientName} disconnected from transmission channel.\n";
            HostStartGameBtn.IsEnabled = false;
        });
    }

    private void OnClientDisconnected(string reason) {
        Dispatcher.InvokeAsync(() => {
            GlobalStatusText.Text = $"> [DISCONNECTED] {reason}";
            OnAbortLobbyClick(this, new RoutedEventArgs());
        });
    }

    private void OnAbortLobbyClick(object sender, RoutedEventArgs e) {
        _server?.Dispose();
        _server = null;
        _client?.Dispose();
        _client = null;

        LobbyContainer.Visibility = Visibility.Collapsed;
        MenuContainer.Visibility = Visibility.Visible;
        GlobalStatusText.Text = "> DISCONNECTED FROM FREQUENCY.";
    }
    #endregion
}

public class ThemeItemViewModel {
    public int Index { get; set; }
    public Color DisplayColor { get; set; }
    public bool IsSelected { get; set; }

    public Visibility IsSelectedVisibility => IsSelected ? Visibility.Visible : Visibility.Collapsed;
}
