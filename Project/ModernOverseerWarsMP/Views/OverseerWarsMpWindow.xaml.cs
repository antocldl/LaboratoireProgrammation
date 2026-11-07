using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using LaboratoireProgrammation.Project.ModernOverseerWars.Controllers;
using LaboratoireProgrammation.Project.ModernOverseerWars.Helpers;
using LaboratoireProgrammation.Project.ModernOverseerWars.Models;
using LaboratoireProgrammation.Project.ModernOverseerWars.Models.Entities;
using LaboratoireProgrammation.Project.ModernOverseerWars.Models.Enums;
using LaboratoireProgrammation.Project.ModernOverseerWars.Models.Interfaces;
using LaboratoireProgrammation.Project.ModernOverseerWars.Models.Map;
using LaboratoireProgrammation.Project.ModernOverseerWars.Views;
using LaboratoireProgrammation.Project.ModernOverseerWars.Views.Controls;
using LaboratoireProgrammation.Project.ModernOverseerWarsMP.Networking;

namespace LaboratoireProgrammation.Project.ModernOverseerWarsMP.Views;

public partial class OverseerWarsMpWindow : Window, IOverseerGameWindow {
    private readonly MpServer? _server;
    private readonly MpClient? _client;
    public bool IsPlayer1 { get; }

    private GameState _state = null!;
    public GameState State => _state;

    public Vault ActiveVault => IsPlayer1 ? _state.Vault1 : _state.Vault2;
    public Vault EnemyVault => IsPlayer1 ? _state.Vault2 : _state.Vault1;
    public PlayerProfile MyProfile => IsPlayer1 ? _state.Player1 : _state.Player2;
    public PlayerProfile EnemyProfile => IsPlayer1 ? _state.Player2 : _state.Player1;

    public bool IsMyTurn => IsPlayer1 ? _state.IsPlayer1Turn : !_state.IsPlayer1Turn;
    public bool CanInteract => IsMyTurn && _state.Phase != GamePhase.GameOver;

    private Color _myColor;
    private Color _myBg;
    private Color _enemyColor;

    private HexTileControl? _selectedTile;
    private bool _showingVault = false;

    private Room? _targetRoomForAssignment;
    private HexTile? _targetTileForDeployment;
    private Dweller? _selectedDwellerForAction;
    private Border? _selectedDwellerRowBorder;

    private Dweller? _equippingDweller;
    private bool _isEquippingWeapon;
    private ICard? _selectedEquipment;
    private Border? _selectedEquipmentRowBorder;

    private Dweller? _dwellerInAssignmentSlot;
    private Dweller? _sheetDweller;

    private Panel? _originalCardParent;
    private Decorator? _originalCardDecorator;
    private int _originalCardIndex;
    private UIElement? _dragPlaceholder;

    private int _turnDuration = 60;

    public OverseerWarsMpWindow(MpServer? server, MpClient? client, bool isPlayer1) {
        InitializeComponent();

        _server = server;
        _client = client;
        IsPlayer1 = isPlayer1;

        if (_server != null) {
            _state = _server.State!;
            _turnDuration = _server.TurnDuration;
            HookServerEvents();
        } else if (_client != null) {
            _state = _client.CurrentState!;
            _turnDuration = _client.TurnDuration;
            HookClientEvents();
        }

        InitThemeColors();
        ApplyTheme();

        Loaded += (s, e) => {
            BuildMapUI();
            BuildVaultUI();
            DeckPickerControl.LoadDeck(GetIdleDwellers(), ActiveVault.UnusedWeapons, ActiveVault.UnusedOutfits, ActiveVault.Scraps);
            HookDeckPicker();
            RefreshAll();

            double cW = _state.Map.Cols * 100;
            double cH = _state.Map.Rows * 100;
            MapView.ScrollToVerticalOffset(Math.Max(0, (cH - 480) / 2));
            MapView.ScrollToHorizontalOffset(Math.Max(0, (cW - 850) / 2));
        };
    }

    private void InitThemeColors() {
        var myTheme = MyProfile.Theme;
        var enemyTheme = EnemyProfile.Theme;

        _myColor = (Color)ColorConverter.ConvertFromString(myTheme.Color);
        _myBg = (Color)ColorConverter.ConvertFromString(myTheme.BgColor);
        _enemyColor = (Color)ColorConverter.ConvertFromString(enemyTheme.Color);

        NetStatusBadge.Text = IsPlayer1 
            ? $"[ LINK: ONLINE | P1 HOST vs {EnemyProfile.Pseudo} ]"
            : $"[ LINK: ONLINE | P2 GUEST vs {EnemyProfile.Pseudo} ]";
    }

    private void HookServerEvents() {
        if (_server == null) return;
        _server.GameStateUpdatedLocally += state => Dispatcher.InvokeAsync(() => ApplyNewState(state));
        _server.CombatEventOccurred += msg => Dispatcher.InvokeAsync(() => AppendLog(msg));
        _server.DamagePopupOccurred += (txt, tile, isP1) => Dispatcher.InvokeAsync(() => SpawnDamagePopup(txt, tile, isP1));
        _server.ClientDisconnected += name => Dispatcher.InvokeAsync(() => HandleOpponentDisconnected(name));
        _server.TurnTick += sec => Dispatcher.InvokeAsync(() => UpdateTimerBar(sec));
        _server.ChatReceived += chat => Dispatcher.InvokeAsync(() => AppendChatMessage(chat));
    }

    private void HookClientEvents() {
        if (_client == null) return;
        _client.GameStateUpdated += (state, sec) => Dispatcher.InvokeAsync(() => {
            ApplyNewState(state);
            UpdateTimerBar(sec);
        });
        _client.CombatEventReceived += msg => Dispatcher.InvokeAsync(() => AppendLog(msg));
        _client.DamagePopupReceived += (txt, c, r, isP1) => Dispatcher.InvokeAsync(() => SpawnDamagePopup(txt, c, r, isP1));
        _client.GameOverReceived += (winner, reason) => Dispatcher.InvokeAsync(() => ShowGameOver(winner, reason));
        _client.Disconnected += reason => Dispatcher.InvokeAsync(() => HandleOpponentDisconnected("Host"));
        _client.ChatReceived += chat => Dispatcher.InvokeAsync(() => AppendChatMessage(chat));
    }

    private void ApplyNewState(GameState newState) {
        _state = newState;
        ApplyTheme();
        RefreshAll();

        if (_state.Phase == GamePhase.GameOver) {
            ShowGameOver(_state.WinnerName ?? "Overseer", $"{_state.WinnerName} secured tactical dominance over the sector!");
        }
    }

    private void UpdateTimerBar(int secondsRemaining) {
        double pct = Math.Max(0.0, Math.Min(1.0, (double)secondsRemaining / _turnDuration));
        double fullW = ActualWidth > 0 ? ActualWidth : 1440;
        TimerBar.Width = fullW * pct;

        if (secondsRemaining <= 10) {
            TimerBar.Fill = new SolidColorBrush(Color.FromRgb(220, 50, 50));
        } else {
            TimerBar.Fill = (Brush)Resources["PlayerAccentBrush"];
        }
    }

    private void ApplyTheme() {
        Color accent = IsMyTurn ? _myColor : Color.FromRgb(0x88, 0x77, 0x44);
        Color bg = _myBg;

        Resources["PlayerAccentBrush"] = new SolidColorBrush(accent);
        Resources["PlayerAccentDimBrush"] = new SolidColorBrush(Color.FromArgb(80, accent.R, accent.G, accent.B));
        Resources["PlayerBgBrush"] = new SolidColorBrush(bg);

        var glow = new DropShadowEffect {
            Color = accent,
            BlurRadius = 12,
            ShadowDepth = 0,
            Opacity = 0.5
        };
        Resources["PlayerAccentGlow"] = glow;

        ActivePlayerLabel.Text = IsMyTurn
            ? $"[ SYSTEM AUTHORIZED: YOUR TURN ({MyProfile.Pseudo}) ]"
            : $"[ STANDBY: ENEMY TRANSMITTING ORDERS ({EnemyProfile.Pseudo}) ]";

        HexTileControl.ActivePlayerColor = _myColor;

        Color bgDark = Color.FromRgb((byte)(bg.R * 0.7), (byte)(bg.G * 0.7), (byte)(bg.B * 0.7));
        DeckPickerControl.ApplyTheme(bgDark, accent);

        foreach (VaultCardHolder h in RoomsPanel.Children.OfType<VaultCardHolder>()) {
            h.ApplyTheme(bgDark, accent);
        }

        BgUsernameText.Text = MyProfile.Pseudo;
        BgUsernameText.Fill = new SolidColorBrush(accent);
        BgUsernameText.Opacity = 0.03;

        EndTurnButton.IsEnabled = CanInteract;
        BuildRoomButton.IsEnabled = CanInteract;
    }

    public Color GetThemeColor() => _myColor;
    public Color GetThemeBgDark() => Color.FromRgb((byte)(_myBg.R * 0.7), (byte)(_myBg.G * 0.7), (byte)(_myBg.B * 0.7));

    #region Map & Vault UI
    private void BuildMapUI() {
        MapCanvas.Children.Clear();
        double tileW = 103, tileH = 103, offsetX = 0, offsetY = 0;
        for (int c = 0; c < _state.Map.Cols; c++) {
            for (int r = 0; r < _state.Map.Rows; r++) {
                var ctrl = new HexTileControl();
                ctrl.BindTile(_state.Map.Get(c, r));
                ctrl.Refresh(isP1: IsPlayer1);
                ctrl.CardDropped += OnCardDroppedOnTile;
                double x = offsetX + c * tileW;
                double y = offsetY + r * tileH;
                Canvas.SetLeft(ctrl, x);
                Canvas.SetTop(ctrl, y);
                MapCanvas.Children.Add(ctrl);
            }
        }
        MapCanvas.Width = offsetX + _state.Map.Cols * tileW;
        MapCanvas.Height = offsetY + _state.Map.Rows * tileH;
    }

    private void RefreshMapUI() {
        foreach (HexTileControl ctrl in MapCanvas.Children.OfType<HexTileControl>()) {
            ctrl.Refresh(isP1: IsPlayer1);
        }
    }

    private void BuildVaultUI() {
        RoomsPanel.Children.Clear();
        var vault = ActiveVault;
        foreach (var room in vault.Rooms) {
            var holder = new VaultCardHolder();
            holder.BindRoom(room, vault);
            holder.CardAssigned += OnCardAssignedToRoom;
            holder.AssignClicked += h => {
                if (!CanInteract) { ShowWarning("Cannot assign during enemy turn!"); return; }
                _targetRoomForAssignment = h.BoundRoom;
                _targetTileForDeployment = null;
                OpenDwellerSelection();
            };
            holder.TechBonusRequested += OnTechBonus;
            RoomsPanel.Children.Add(holder);
        }

        Color bgDark = GetThemeBgDark();
        foreach (VaultCardHolder h in RoomsPanel.Children.OfType<VaultCardHolder>()) {
            h.ApplyTheme(bgDark, _myColor);
        }
    }

    private void RefreshVaultUI() {
        foreach (VaultCardHolder h in RoomsPanel.Children.OfType<VaultCardHolder>()) {
            h.Refresh();
        }
    }

    private void HookDeckPicker() {
        DeckPickerControl.CardSelected += card => {
            if (!CanInteract) return;
            OverseerWarsWindow.heldCard = card;
            UpdateInfoPanel();
            if (MapView.Visibility == Visibility.Visible && card.BoundDweller != null) {
                var startTile = _state.Map.Tiles.Cast<HexTile>().FirstOrDefault(t => 
                    (IsPlayer1 ? t.Player1Dwellers : t.Player2Dwellers).Contains(card.BoundDweller));
                if (startTile == null) {
                    startTile = IsPlayer1 
                        ? _state.Map.Get(0, _state.Map.Rows / 2) 
                        : _state.Map.Get(_state.Map.Cols - 1, _state.Map.Rows / 2);
                }
                var vaultCtrl = MapCanvas.Children.OfType<HexTileControl>().FirstOrDefault(c => c.BoundTile == startTile);
                if (vaultCtrl != null) {
                    if (_selectedTile != null && _selectedTile != vaultCtrl) _selectedTile.SetSelected(false);
                    _selectedTile = vaultCtrl;
                    vaultCtrl.SetSelected(true);
                }
            }
        };
    }
    #endregion

    #region Tile Interaction & Deployment
    private void OnTileClicked(HexTileControl ctrl) {
        if (!CanInteract) {
            ShowWarning("Wait for your turn to command dwellers.");
            return;
        }

        if (_selectedTile != null && _selectedTile != ctrl) {
            var myDwellers = IsPlayer1 ? _selectedTile.BoundTile.Player1Dwellers : _selectedTile.BoundTile.Player2Dwellers;
            var dw = OverseerWarsWindow.heldCard?.BoundDweller ?? myDwellers.LastOrDefault(d => d.IsAlive);
            if (dw != null && (myDwellers.Contains(dw) || OverseerWarsWindow.heldCard?.BoundDweller == dw)) {
                bool hasEnemy = IsPlayer1 
                    ? ctrl.BoundTile.Player2Dwellers.Any(d => d.IsAlive) 
                    : ctrl.BoundTile.Player1Dwellers.Any(d => d.IsAlive);
                if (hasEnemy) {
                    ShowWarning("Cannot move onto enemy occupied tile!");
                    return;
                }
                int cost = GetDeploymentCost(dw, ctrl.BoundTile);
                if (cost <= ActiveVault.ActionPoints) {
                    ExecuteMoveOrDeploy(dw, ctrl.BoundTile, cost);
                    _selectedTile.SetSelected(false);
                    _selectedTile = null;
                    OverseerWarsWindow.heldCard = null;
                    return;
                } else {
                    ShowWarning("Not enough PA to move there!");
                }
            }
        }

        if (_selectedTile == ctrl) {
            _selectedTile.SetSelected(false);
            _selectedTile = null;
            UpdateInfoPanel();
            return;
        }

        if (_selectedTile != null && _selectedTile != ctrl) _selectedTile.SetSelected(false);
        _selectedTile = ctrl;
        ctrl.SetSelected(true);
        UpdateInfoPanel();
    }

    private void OnCardDroppedOnTile(HexTileControl ctrl, ControlCard card) {
        if (!CanInteract) {
            ShowWarning("Cannot deploy during opponent's turn!");
            return;
        }
        if (_selectedTile != null && _selectedTile != ctrl) _selectedTile.SetSelected(false);
        _selectedTile = ctrl;
        DeployCard(card);
    }

    private bool DeployCard(ControlCard? card) {
        if (card?.BoundDweller == null || _selectedTile?.BoundTile == null) {
            _selectedTile = null;
            return false;
        }
        var tile = _selectedTile.BoundTile;
        if (!tile.IsNavigable) {
            ShowWarning("Tile is unnavigable!");
            _selectedTile = null;
            return false;
        }
        bool hasEnemy = IsPlayer1 
            ? tile.Player2Dwellers.Any(d => d.IsAlive) 
            : tile.Player1Dwellers.Any(d => d.IsAlive);
        if (hasEnemy) {
            ShowWarning("Cannot deploy to enemy occupied tile!");
            _selectedTile = null;
            return false;
        }

        int cost = GetDeploymentCost(card.BoundDweller, tile);
        if (cost > ActiveVault.ActionPoints) {
            ShowWarning("Tile too far away, you need more PA");
            _selectedTile = null;
            return false;
        }

        ExecuteMoveOrDeploy(card.BoundDweller, tile, cost);

        OverseerWarsWindow.heldCard = null;
        if (_selectedTile != null) _selectedTile.SetSelected(false);
        _selectedTile = null;
        return true;
    }

    private void ExecuteMoveOrDeploy(Dweller dw, HexTile tile, int cost) {
        if (_server != null) {
            // Local Host Execution
            ActiveVault.ActionPoints -= cost;
            RemoveDwellerFromMap(dw);
            bool isHomeVault = (IsPlayer1 && tile.Type == TileType.Player1Vault) || (!IsPlayer1 && tile.Type == TileType.Player2Vault);
            if (!isHomeVault) {
                if (IsPlayer1) tile.Player1Dwellers.Add(dw);
                else tile.Player2Dwellers.Add(dw);
            }
            if (IsPlayer1) tile.IsRevealedByP1 = true;
            else tile.IsRevealedByP2 = true;

            _state.AddLog($"{dw.Name} → ({tile.Col},{tile.Row}) [{cost} PA]");
            if (tile.HasConflict) {
                _server.Game?.Combat.ResolveCombatsOnMap();
                CheckLocalVictory();
            }
            _server.BroadcastGameState();
        } else if (_client != null) {
            // Client Network Request
            var act = new ActionRequestPayload {
                ActionType = MpActionType.DeployDweller,
                DwellerId = dw.Id,
                TargetCol = tile.Col,
                TargetRow = tile.Row
            };
            _ = _client.SendActionAsync(act);
        }
    }

    private int GetDeploymentCost(Dweller d, HexTile target) {
        var startTile = _state.Map.Tiles.Cast<HexTile>().FirstOrDefault(t => 
            (IsPlayer1 ? t.Player1Dwellers : t.Player2Dwellers).Contains(d));
        if (startTile == null) {
            startTile = IsPlayer1 
                ? _state.Map.Get(0, _state.Map.Rows / 2) 
                : _state.Map.Get(_state.Map.Cols - 1, _state.Map.Rows / 2);
        }
        int dist = GridMath.GetDistance(startTile, target);
        return Math.Max(1, dist * 2 - d.Special_A);
    }

    private void RemoveDwellerFromMap(Dweller d) {
        for (int c = 0; c < _state.Map.Cols; c++)
            for (int r = 0; r < _state.Map.Rows; r++) {
                _state.Map.Get(c, r).Player1Dwellers.Remove(d);
                _state.Map.Get(c, r).Player2Dwellers.Remove(d);
            }
    }
    #endregion

    #region Room Assignments & Construction
    private void OnCardAssignedToRoom(VaultCardHolder holder, ControlCard card) {
        if (!CanInteract) { ShowWarning("Cannot assign during opponent's turn!"); return; }
        if (card.BoundDweller == null || holder.BoundRoom == null) return;
        if (holder.BoundRoom.AssignedDwellers.Contains(card.BoundDweller)) return;
        if (ActiveVault.ActionPoints <= 0) { ShowWarning("No action points left!"); return; }

        if (_server != null) {
            _server.Game?.Resources.TryAssignDweller(ActiveVault, card.BoundDweller, holder.BoundRoom);
            ActiveVault.ActionPoints--;
            RemoveDwellerFromMap(card.BoundDweller);
            _server.BroadcastGameState();
        } else if (_client != null) {
            var act = new ActionRequestPayload {
                ActionType = MpActionType.AssignRoom,
                DwellerId = card.BoundDweller.Id,
                RoomType = holder.BoundRoom.Type
            };
            _ = _client.SendActionAsync(act);
        }
    }

    private void OnBuildRoom(object s, RoutedEventArgs e) {
        if (!CanInteract) { ShowWarning("Cannot build during opponent's turn!"); return; }
        var vault = ActiveVault;
        var existing = vault.Rooms.Select(r => r.Type);
        var dlg = new BuildRoomDialog(existing) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.ChosenRoom.HasValue) {
            if (_server != null) {
                var (ok, err) = _server.Game!.Resources.TryBuildRoom(vault, dlg.ChosenRoom.Value);
                if (!ok) AppendLog(err);
                else {
                    BuildVaultUI();
                    _server.BroadcastGameState();
                }
            } else if (_client != null) {
                var act = new ActionRequestPayload {
                    ActionType = MpActionType.BuildRoom,
                    RoomType = dlg.ChosenRoom.Value
                };
                _ = _client.SendActionAsync(act);
            }
        }
    }

    private void OnTechBonus(string bonus) {
        if (!CanInteract) return;
        if (_server != null) {
            var (ok, err) = _server.Game!.Resources.TryUnlockTechBonus(ActiveVault, bonus);
            if (!ok) AppendLog(err);
            else _server.BroadcastGameState();
        } else if (_client != null) {
            var act = new ActionRequestPayload {
                ActionType = MpActionType.TechBonus,
                TechBonusName = bonus
            };
            _ = _client.SendActionAsync(act);
        }
    }
    #endregion

    #region Equip & Unequip (IOverseerGameWindow)
    public void RequestEquip(Dweller dw, ICard item, bool isWeapon) {
        if (!CanInteract) { ShowWarning("Cannot modify equipment during opponent's turn!"); return; }

        if (_server != null) {
            if (isWeapon && item is IWeapon w) {
                dw.EquippedWeapon = w;
                _state.AddLog($"{dw.Name} equipped {w.Name}");
            } else if (!isWeapon && item is IOutfit o) {
                dw.EquippedOutfit = o;
                _state.AddLog($"{dw.Name} equipped {o.Name}");
            }
            _server.BroadcastGameState();
        } else if (_client != null) {
            var act = new ActionRequestPayload {
                ActionType = MpActionType.EquipItem,
                DwellerId = dw.Id,
                ItemId = isWeapon ? ((IWeapon)item).Id : ((IOutfit)item).Id,
                IsWeapon = isWeapon
            };
            _ = _client.SendActionAsync(act);
        }
    }

    public void RequestUnequip(Dweller dw, bool isWeapon) {
        if (!CanInteract) { ShowWarning("Cannot modify equipment during opponent's turn!"); return; }

        if (_server != null) {
            if (isWeapon && dw.EquippedWeapon != null) {
                _state.AddLog($"{dw.Name} unequipped {dw.EquippedWeapon.Name}");
                dw.EquippedWeapon = null;
            } else if (!isWeapon && dw.EquippedOutfit != null) {
                _state.AddLog($"{dw.Name} unequipped {dw.EquippedOutfit.Name}");
                dw.EquippedOutfit = null;
            }
            _server.BroadcastGameState();
        } else if (_client != null) {
            var act = new ActionRequestPayload {
                ActionType = MpActionType.UnequipItem,
                DwellerId = dw.Id,
                IsWeapon = isWeapon
            };
            _ = _client.SendActionAsync(act);
        }
    }
    #endregion

    #region Turn Ending
    private void OnEndTurn(object s, RoutedEventArgs e) {
        if (!CanInteract) { ShowWarning("Wait for your turn!"); return; }

        if (_server != null) {
            _server.HostEndTurn();
        } else if (_client != null) {
            var act = new ActionRequestPayload { ActionType = MpActionType.EndTurn };
            _ = _client.SendActionAsync(act);
        }
    }
    #endregion

    #region UI Views & HUD
    private void OnToggleView(object s, RoutedEventArgs e) {
        _showingVault = !_showingVault;
        MapView.Visibility = _showingVault ? Visibility.Collapsed : Visibility.Visible;
        MapBorder.Visibility = _showingVault ? Visibility.Collapsed : Visibility.Visible;
        VaultView.Visibility = _showingVault ? Visibility.Visible : Visibility.Collapsed;
        PhaseIndicator.Content = _showingVault ? "🗺 MAP" : "🏠 VAULT";
        if (_showingVault) { BuildVaultUI(); RefreshVaultUI(); }
    }

    private void RefreshHUD() {
        var vault = ActiveVault;
        ElecText.Text = $"⚡ {vault.Electricity}";
        WaterText.Text = $"💧 {vault.Water}";
        FoodText.Text = $"🍴 {vault.Food}";
        ApText.Text = $"PA: {vault.ActionPoints}/{vault.MaxActionPoints}";
        TurnText.Text = $"Turn {_state.TurnNumber}";
    }

    public void RefreshAll() {
        RefreshHUD();
        RefreshMapUI();
        if (_showingVault) RefreshVaultUI();
        RefreshDeckUI();
        DeckPickerControl.RefreshAll();
        UpdateInfoPanel();
        if (DwellerSheetOverlay.Visibility == Visibility.Visible) {
            RefreshDwellerSheet();
        }
    }

    private void RefreshDeckUI() {
        DeckPickerControl.LoadDeck(GetIdleDwellers(), ActiveVault.UnusedWeapons, ActiveVault.UnusedOutfits, ActiveVault.Scraps);
    }

    private IEnumerable<Dweller> GetIdleDwellers() {
        return ActiveVault.Dwellers.Where(d => d.IsAlive && GetDwellerStatus(d) == "Idle in Deck");
    }

    private string GetDwellerStatus(Dweller d) {
        foreach (var room in ActiveVault.Rooms)
            if (room.AssignedDwellers.Contains(d)) return $"Working in {room.Name}";
        for (int c = 0; c < _state.Map.Cols; c++)
            for (int r = 0; r < _state.Map.Rows; r++) {
                var t = _state.Map.Get(c, r);
                if (t.Player1Dwellers.Contains(d) || t.Player2Dwellers.Contains(d))
                    return $"At ({c},{r}) {(t.LocationName != "" ? t.LocationName : " ")}";
            }
        return "Idle in Deck";
    }

    private void UpdateInfoPanel() {
        DeployButton.Visibility = Visibility.Collapsed;
        if (_selectedTile?.BoundTile == null) {
            InfoTitle.Text = "Select a tile or dweller";
            InfoBody.Text = "";
            return;
        }
        var tile = _selectedTile.BoundTile;
        bool revealed = tile.IsRevealedFor(IsPlayer1);
        InfoTitle.Text = revealed ? (tile.LocationName != "" ? tile.LocationName
            : tile.Type == TileType.Player1Vault ? "P1 Vault"
            : tile.Type == TileType.Player2Vault ? "P2 Vault"
            : "Wasteland") : "Fog of War";

        if (!revealed) {
            InfoBody.Text = "Unexplored wasteland territory. Deploy personnel to scout this sector.";
            return;
        }

        string body = $"Coordinates: ({tile.Col}, {tile.Row})\nType: {tile.Type}\n";
        var dwellers = IsPlayer1 ? tile.Player1Dwellers : tile.Player2Dwellers;
        var enemies = IsPlayer1 ? tile.Player2Dwellers : tile.Player1Dwellers;

        if (dwellers.Any()) body += $"\nYour Dwellers:\n" + string.Join("\n", dwellers.Select(d => $"• {d.Name} (HP:{d.CurrentHp})"));
        if (enemies.Any()) body += $"\nEnemy Hostiles:\n" + string.Join("\n", enemies.Select(d => $"• {d.Name} (HP:{d.CurrentHp})"));

        InfoBody.Text = body;
    }

    private void ShowWarning(string message) {
        WarningText.Text = message;
        WarningPanel.Visibility = Visibility.Visible;
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        timer.Tick += (s, e) => {
            WarningPanel.Visibility = Visibility.Collapsed;
            timer.Stop();
        };
        timer.Start();
    }

    private void AppendLog(string msg) {
        if (string.IsNullOrEmpty(msg)) return;
        LogText.Text += msg + "\n";
        LogScroller.ScrollToEnd();
    }

    private void SpawnDamagePopup(string text, HexTile tile, bool isP1Dweller) {
        var tileCtrl = MapCanvas.Children.OfType<HexTileControl>().FirstOrDefault(c => c.BoundTile == tile);
        double x = PopupCanvas.ActualWidth / 2;
        double y = PopupCanvas.ActualHeight / 2;
        if (tileCtrl != null && tileCtrl.IsVisible) {
            try {
                Point center = tileCtrl.TranslatePoint(new Point(tileCtrl.ActualWidth / 2, tileCtrl.ActualHeight / 2), PopupCanvas);
                x = center.X;
                y = center.Y;
            } catch { }
        }
        DamagePopupHelper.Spawn(text, PopupCanvas, x, y, isP1Dweller ? _myColor : _enemyColor);
    }

    private void SpawnDamagePopup(string text, int col, int row, bool isP1Dweller) {
        var tile = _state.Map.Get(col, row);
        if (tile != null) SpawnDamagePopup(text, tile, isP1Dweller);
    }
    #endregion

    #region In-Game Comms Terminal (Chat)
    private void OnToggleChatDrawer(object sender, RoutedEventArgs e) {
        InGameChatDrawer.Visibility = InGameChatDrawer.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnCloseChatDrawer(object sender, RoutedEventArgs e) => InGameChatDrawer.Visibility = Visibility.Collapsed;

    private void OnSendInGameChatClick(object sender, RoutedEventArgs e) => SendInGameChat();
    private void OnInGameChatKeyDown(object sender, KeyEventArgs e) {
        if (e.Key == Key.Enter) SendInGameChat();
    }

    private void SendInGameChat() {
        string text = InGameChatInput.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;
        InGameChatInput.Text = "";

        if (_server != null) _server.SendChatMessage(MyProfile.Pseudo, text);
        else if (_client != null) _ = _client.SendChatAsync(text);
    }

    private void AppendChatMessage(ChatMessagePayload chat) {
        InGameChatText.Text += $"[{chat.TimeString}] {chat.Sender}: {chat.Text}\n";
        InGameChatScroller.ScrollToEnd();
        if (InGameChatDrawer.Visibility != Visibility.Visible) {
            ChatToggleButton.Foreground = Brushes.LightGreen;
        }
    }
    #endregion

    #region Game Over & Disconnect
    private void CheckLocalVictory() {
        if (_state.Phase == GamePhase.GameOver) {
            ShowGameOver(_state.WinnerName ?? "Overseer", $"{_state.WinnerName} eliminated opposing forces!");
        }
    }

    private void ShowGameOver(string winnerName, string reason) {
        GameOverOverlay.Visibility = Visibility.Visible;
        bool iWon = winnerName == MyProfile.Pseudo;
        GameOverTitle.Text = iWon ? "🏆 VICTORY - SECTOR SECURED!" : "💀 DEFEAT - OVERSEER PURGED";
        GameOverWinner.Text = reason;
        GameOverTitle.Foreground = iWon ? Brushes.LightGreen : Brushes.OrangeRed;
    }

    private void HandleOpponentDisconnected(string opponentName) {
        ShowGameOver(MyProfile.Pseudo, $"{opponentName} lost quantum link connection. You secured victory by sector default!");
    }

    private void OnReturnToMenuClick(object sender, RoutedEventArgs e) {
        var menu = new OverseerWarsMpMenuWindow();
        menu.Show();
        Close();
    }

    protected override void OnClosed(EventArgs e) {
        base.OnClosed(e);
        _server?.Dispose();
        _client?.Dispose();
    }
    #endregion

    #region Dweller Sheet & Equipment Helpers
    public void OpenDwellerSheet(Dweller dw) {
        _sheetDweller = dw;
        DwellerSheetOverlay.Visibility = Visibility.Visible;
        RefreshDwellerSheet();
    }

    private void OnCloseDwellerSheet(object sender, RoutedEventArgs e) => DwellerSheetOverlay.Visibility = Visibility.Collapsed;

    public void RefreshDwellerSheet() {
        if (_sheetDweller == null) return;
        SheetStatusText.Text = $"STATUS: {GetDwellerStatus(_sheetDweller)}";
        SheetDwellerCard.BindDweller(_sheetDweller);

        if (_sheetDweller.EquippedWeapon != null) {
            SheetWeaponPlaceholder.Visibility = Visibility.Collapsed;
            SheetWeaponViewbox.Visibility = Visibility.Visible;
            var c = new ControlCard(); c.BindWeapon(_sheetDweller.EquippedWeapon);
            SheetWeaponViewbox.Child = c;
        } else {
            SheetWeaponPlaceholder.Visibility = Visibility.Visible;
            SheetWeaponViewbox.Visibility = Visibility.Collapsed;
        }

        if (_sheetDweller.EquippedOutfit != null) {
            SheetOutfitPlaceholder.Visibility = Visibility.Collapsed;
            SheetOutfitViewbox.Visibility = Visibility.Visible;
            var c = new ControlCard(); c.BindOutfit(_sheetDweller.EquippedOutfit);
            SheetOutfitViewbox.Child = c;
        } else {
            SheetOutfitPlaceholder.Visibility = Visibility.Visible;
            SheetOutfitViewbox.Visibility = Visibility.Collapsed;
        }
    }

    public void OpenEquipmentSelection(Dweller dw, bool isW) {
        _equippingDweller = dw;
        _isEquippingWeapon = isW;
        EquipmentSelectionTitle.Text = isW ? "[ SELECT WEAPON ]" : "[ SELECT OUTFIT ]";
        EquipmentSelectionList.Children.Clear();
        _selectedEquipment = null;
        _selectedEquipmentRowBorder = null;

        var vault = ActiveVault;
        var items = isW ? vault.UnusedWeapons.Cast<ICard>().ToList() : vault.UnusedOutfits.Cast<ICard>().ToList();
        foreach (var it in items) {
            var row = CreateEquipmentRow(it);
            EquipmentSelectionList.Children.Add(row);
        }
        EquipmentSelectionOverlay.Visibility = Visibility.Visible;
    }

    private UIElement CreateEquipmentRow(ICard item) {
        var border = new Border {
            Background = new SolidColorBrush(Color.FromRgb(0x15, 0x15, 0x30)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x45)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(0, 0, 0, 6),
            Padding = new Thickness(8),
            Cursor = Cursors.Hand
        };

        var stack = new StackPanel { Orientation = Orientation.Horizontal };
        stack.Children.Add(new TextBlock { Text = item.Name, Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 12 });
        border.Child = stack;

        border.MouseDown += (s, e) => {
            if (_selectedEquipmentRowBorder != null) {
                _selectedEquipmentRowBorder.Background = new SolidColorBrush(Color.FromRgb(0x15, 0x15, 0x30));
                _selectedEquipmentRowBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x45));
            }
            border.Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x3A, 0x60));
            border.BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x88, 0xFF));
            _selectedEquipmentRowBorder = border;
            _selectedEquipment = item;
        };

        return border;
    }

    private void OnCancelEquipmentSelection(object s, RoutedEventArgs e) => EquipmentSelectionOverlay.Visibility = Visibility.Collapsed;
    private void OnConfirmEquipmentSelection(object s, RoutedEventArgs e) {
        if (_equippingDweller == null || _selectedEquipment == null) return;
        EquipmentSelectionOverlay.Visibility = Visibility.Collapsed;
        RequestEquip(_equippingDweller, _selectedEquipment, _isEquippingWeapon);
    }

    private void OpenDwellerSelection() {
        DwellerSelectionList.Children.Clear();
        _selectedDwellerForAction = null;
        _selectedDwellerRowBorder = null;

        var vault = ActiveVault;
        foreach (var d in vault.Dwellers) {
            var row = CreateDwellerRow(d);
            DwellerSelectionList.Children.Add(row);
        }
        DwellerSelectionOverlay.Visibility = Visibility.Visible;
    }

    private UIElement CreateDwellerRow(Dweller d) {
        var border = new Border {
            Background = new SolidColorBrush(Color.FromRgb(0x15, 0x15, 0x30)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x45)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(0, 0, 0, 6),
            Padding = new Thickness(8),
            Cursor = Cursors.Hand
        };
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        sp.Children.Add(new TextBlock { Text = $"{d.Name} (HP:{d.CurrentHp}/{d.MaxHp})", Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 12 });
        sp.Children.Add(new TextBlock { Text = $" - {GetDwellerStatus(d)}", Foreground = Brushes.Gray, FontSize = 11, Margin = new Thickness(8, 0, 0, 0) });
        border.Child = sp;

        border.MouseDown += (s, e) => {
            if (_selectedDwellerRowBorder != null) {
                _selectedDwellerRowBorder.Background = new SolidColorBrush(Color.FromRgb(0x15, 0x15, 0x30));
                _selectedDwellerRowBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x45));
            }
            border.Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x3A, 0x60));
            border.BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x88, 0xFF));
            _selectedDwellerRowBorder = border;
            _selectedDwellerForAction = d;
        };

        return border;
    }

    private void OnCancelDwellerSelection(object s, RoutedEventArgs e) => DwellerSelectionOverlay.Visibility = Visibility.Collapsed;
    private void OnConfirmDwellerSelection(object s, RoutedEventArgs e) {
        if (_selectedDwellerForAction == null) return;
        DwellerSelectionOverlay.Visibility = Visibility.Collapsed;
        var dw = _selectedDwellerForAction;

        if (_targetRoomForAssignment != null) {
            if (_server != null) {
                _server.Game?.Resources.TryAssignDweller(ActiveVault, dw, _targetRoomForAssignment);
                ActiveVault.ActionPoints--;
                RemoveDwellerFromMap(dw);
                _server.BroadcastGameState();
            } else if (_client != null) {
                var act = new ActionRequestPayload {
                    ActionType = MpActionType.AssignRoom,
                    DwellerId = dw.Id,
                    RoomType = _targetRoomForAssignment.Type
                };
                _ = _client.SendActionAsync(act);
            }
        }
    }
    #endregion

    #region Drag & Drop Handlers
    public void StartDraggingCard(ControlCard card, Point position) {
        if (!CanInteract) return;
        DragOverlayCanvas.Children.Clear();
        var ghost = new ControlCard();
        if (card.BoundDweller != null) ghost.BindDweller(card.BoundDweller);
        else if (card.BoundWeapon != null) ghost.BindWeapon(card.BoundWeapon);
        else if (card.BoundOutfit != null) ghost.BindOutfit(card.BoundOutfit);
        ghost.Opacity = 0.8;
        Canvas.SetLeft(ghost, position.X - 50);
        Canvas.SetTop(ghost, position.Y - 70);
        DragOverlayCanvas.Children.Add(ghost);
    }

    public void MoveDraggingCard(ControlCard card, Point currentMousePos) {
        if (DragOverlayCanvas.Children.Count > 0) {
            var ghost = DragOverlayCanvas.Children[0];
            Canvas.SetLeft(ghost, currentMousePos.X - 50);
            Canvas.SetTop(ghost, currentMousePos.Y - 70);
        }
    }

    public void StopDraggingCard(ControlCard card, Point dropPoint) {
        DragOverlayCanvas.Children.Clear();
    }

    public void OnAssignmentSlotDropDirect(ControlCard card) {
        if (!CanInteract || card.BoundDweller == null) return;
        _dwellerInAssignmentSlot = card.BoundDweller;
        AssignmentSlotContainer.Children.Clear();
        var c = new ControlCard(); c.BindDweller(_dwellerInAssignmentSlot);
        AssignmentSlotContainer.Children.Add(c);
    }

    private void OnAssignmentSlotDrop(object sender, DragEventArgs e) {
        if (OverseerWarsWindow.heldCard != null) {
            OnAssignmentSlotDropDirect(OverseerWarsWindow.heldCard);
        }
    }

    private void OnReturnCardDrop(object sender, DragEventArgs e) {
        if (!CanInteract) return;
        if (OverseerWarsWindow.heldCard?.BoundWeapon != null && OverseerWarsWindow.heldCard.OwnerDweller != null) {
            RequestUnequip(OverseerWarsWindow.heldCard.OwnerDweller, true);
        } else if (OverseerWarsWindow.heldCard?.BoundOutfit != null && OverseerWarsWindow.heldCard.OwnerDweller != null) {
            RequestUnequip(OverseerWarsWindow.heldCard.OwnerDweller, false);
        }
    }

    private void OnAssignmentSlotDragOver(object sender, DragEventArgs e) {
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void OnDeployDweller(object s, RoutedEventArgs e) {
        if (_selectedTile != null && OverseerWarsWindow.heldCard != null) {
            DeployCard(OverseerWarsWindow.heldCard);
        }
    }

    private void OnToggleInfoPanel(object sender, RoutedEventArgs e) {
        InfoContentPanel.Visibility = InfoContentPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        CollapseInfoButton.Content = InfoContentPanel.Visibility == Visibility.Visible ? "X" : "+";
    }

    private void OnRoomsPreviewMouseWheel(object sender, MouseWheelEventArgs e) {
        if (e.Delta > 0) RoomsScrollViewer.LineLeft();
        else RoomsScrollViewer.LineRight();
        e.Handled = true;
    }

    private Point _lastPanPoint;
    private bool _isPanning = false;

    private void MapCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
        var hit = VisualTreeHelper.HitTest(MapCanvas, e.GetPosition(MapCanvas));
        var tile = FindParent<HexTileControl>(hit?.VisualHit);
        if (tile != null) {
            OnTileClicked(tile);
            return;
        }

        _isPanning = true;
        _lastPanPoint = e.GetPosition(this);
        MapCanvas.CaptureMouse();
    }

    private void MapCanvas_MouseMove(object sender, MouseEventArgs e) {
        if (_isPanning) {
            Point current = e.GetPosition(this);
            double deltaX = current.X - _lastPanPoint.X;
            double deltaY = current.Y - _lastPanPoint.Y;
            _lastPanPoint = current;

            MapView.ScrollToHorizontalOffset(MapView.HorizontalOffset - deltaX);
            MapView.ScrollToVerticalOffset(MapView.VerticalOffset - deltaY);
        }
    }

    private void MapCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
        if (_isPanning) {
            _isPanning = false;
            MapCanvas.ReleaseMouseCapture();
        }
    }

    private void MapView_MouseWheel(object sender, MouseWheelEventArgs e) {
        double zoom = e.Delta > 0 ? 1.1 : 0.9;
        double newX = MapScale.ScaleX * zoom;
        if (newX >= 0.5 && newX <= 2.0) {
            MapScale.ScaleX = newX;
            MapScale.ScaleY = newX;
        }
        e.Handled = true;
    }

    private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject {
        while (child != null) {
            if (child is T parent) return parent;
            child = VisualTreeHelper.GetParent(child);
        }
        return null;
    }
    #endregion
}
