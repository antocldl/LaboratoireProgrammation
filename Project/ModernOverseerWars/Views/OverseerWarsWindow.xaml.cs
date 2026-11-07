using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using LaboratoireProgrammation.Project.ModernOverseerWars.Models;
using LaboratoireProgrammation.Project.ModernOverseerWars.Models.Entities;
using LaboratoireProgrammation.Project.ModernOverseerWars.Models.Enums;
using LaboratoireProgrammation.Project.ModernOverseerWars.Models.Interfaces;
using LaboratoireProgrammation.Project.ModernOverseerWars.Models.Map;
using LaboratoireProgrammation.Project.ModernOverseerWars.Controllers;
using LaboratoireProgrammation.Project.ModernOverseerWars.Helpers;
using LaboratoireProgrammation.Project.ModernOverseerWars.Models.Data;
using LaboratoireProgrammation.Project.ModernOverseerWars.Models.Data.Json;
using LaboratoireProgrammation.Project.ModernOverseerWars.Views.Controls;

namespace LaboratoireProgrammation.Project.ModernOverseerWars.Views;

public partial class OverseerWarsWindow : Window, IOverseerGameWindow {

    private static ControlCard? _heldCard;
    public static ControlCard? heldCard {
        get => _heldCard;
        set {
            if (_heldCard != value) {
                if (_heldCard != null) {
                    _heldCard.IsSelected = false;
                }
                _heldCard = value;
                if (_heldCard != null) {
                    _heldCard.IsSelected = true;
                }
            }
        }
    }

    private GameController _game = null!;
    private GameState       _state   => _game.State;
    public GameState State => _state;
    public Vault ActiveVault => _state.ActiveVault;
    public bool CanInteract => true;

    public void RequestEquip(Dweller dw, ICard item, bool isWeapon) {
        if (isWeapon && item is IWeapon w) {
            dw.EquippedWeapon = w;
            State.AddLog($"{dw.Name} equipped {w.Name}");
        } else if (!isWeapon && item is IOutfit o) {
            dw.EquippedOutfit = o;
            State.AddLog($"{dw.Name} equipped {o.Name}");
        }
        RefreshAll();
    }

    public void RequestUnequip(Dweller dw, bool isWeapon) {
        if (isWeapon && dw.EquippedWeapon != null) {
            State.AddLog($"{dw.Name} unequipped {dw.EquippedWeapon.Name}");
            dw.EquippedWeapon = null;
        } else if (!isWeapon && dw.EquippedOutfit != null) {
            State.AddLog($"{dw.Name} unequipped {dw.EquippedOutfit.Name}");
            dw.EquippedOutfit = null;
        }
        RefreshAll();
    }
    private TurnService     _turns   => _game.Turns;
    private ResourceService _res     => _game.Resources;
    private CombatService   _combat  => _game.Combat;
    private LootService     _loot    => _game.Loot;
    private MovementService _movement => _game.Movement;
    private EquipmentService _equipment => _game.Equipment;
    private HexTileControl? _selectedTile;
    private bool _showingVault = false;

    private Color _p1Color;
    private Color _p2Color;
    private Color _p1Bg;
    private Color _p2Bg;

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

    public OverseerWarsWindow(PlayerProfile p1, PlayerProfile p2) {
        InitializeComponent();
        _p1Color = (Color)ColorConverter.ConvertFromString(p1.Theme.Color);
        _p2Color = (Color)ColorConverter.ConvertFromString(p2.Theme.Color);
        _p1Bg    = (Color)ColorConverter.ConvertFromString(p1.Theme.BgColor);
        _p2Bg    = (Color)ColorConverter.ConvertFromString(p2.Theme.BgColor);

        Loaded += (s, e) => StartNewGame(p1, p2);
    }

    public OverseerWarsWindow(GameState state) {
        InitializeComponent();
        _p1Color = (Color)ColorConverter.ConvertFromString(state.Player1.Theme.Color);
        _p2Color = (Color)ColorConverter.ConvertFromString(state.Player2.Theme.Color);
        _p1Bg    = (Color)ColorConverter.ConvertFromString(state.Player1.Theme.BgColor);
        _p2Bg    = (Color)ColorConverter.ConvertFromString(state.Player2.Theme.BgColor);

        Loaded += (s, e) => StartLoadedGame(state);
    }

    private void StartNewGame(PlayerProfile p1, PlayerProfile p2) {
        GameController.LoadConfig();
        var state = new GameState { Player1 = p1, Player2 = p2 };
        state.InitVaults();
        _game = new GameController(state);

        _turns.TurnTick    += () => Dispatcher.InvokeAsync(OnTick);
        _turns.TurnExpired += () => Dispatcher.InvokeAsync(OnEndTurn_Internal);
        _combat.CombatEvent += msg => Dispatcher.InvokeAsync(() => AppendLog(msg));
        _combat.DamagePopup += (text, tile, isP1Dweller) =>
            Dispatcher.InvokeAsync(() => SpawnDamagePopup(text, tile, isP1Dweller));

        _game.SetupPlayers();
        BuildMapUI();
        BuildVaultUI();
        
        DeckPickerControl.LoadDeck(GetIdleDwellers(), _state.ActiveVault.UnusedWeapons, _state.ActiveVault.UnusedOutfits, _state.ActiveVault.Scraps);
        DeckPickerControl.CardSelected += card => { 
            heldCard = card; 
            UpdateInfoPanel(); 
            if (MapView.Visibility == Visibility.Visible && card.BoundDweller != null) {
                var startTile = _state.Map.Tiles.Cast<HexTile>().FirstOrDefault(t => t.Player1Dwellers.Contains(card.BoundDweller) || t.Player2Dwellers.Contains(card.BoundDweller));
                if (startTile == null) {
                    startTile = _state.IsPlayer1Turn ? _state.Map.Get(0, _state.Map.Rows / 2) : _state.Map.Get(_state.Map.Cols - 1, _state.Map.Rows / 2);
                }
                var vaultCtrl = MapCanvas.Children.OfType<HexTileControl>().FirstOrDefault(c => c.BoundTile == startTile);
                if (vaultCtrl != null) {
                    if (_selectedTile != null && _selectedTile != vaultCtrl) _selectedTile.SetSelected(false);
                    _selectedTile = vaultCtrl;
                    vaultCtrl.SetSelected(true);
                }
            }
        };
        GameOverOverlay.Visibility = Visibility.Collapsed;
        _turns.StartTurn(isPlayer1: true);
        ApplyTheme(isP1: true);
        RefreshAll();
        StartAutosaveTimer();
        double cW = GameConfigRepository.Config.MapCols * 100;
        double cH = GameConfigRepository.Config.MapRows * 100;
        MapView.ScrollToVerticalOffset(Math.Max(0, (cH - 480) / 2));
        MapView.ScrollToHorizontalOffset(Math.Max(0, (cW - 850) / 2));
    }

    private void StartLoadedGame(GameState state) {
        GameController.LoadConfig();
        _game = new GameController(state);

        _turns.TurnTick    += () => Dispatcher.InvokeAsync(OnTick);
        _turns.TurnExpired += () => Dispatcher.InvokeAsync(OnEndTurn_Internal);
        _combat.CombatEvent += msg => Dispatcher.InvokeAsync(() => AppendLog(msg));
        _combat.DamagePopup += (text, tile, isP1Dweller) =>
            Dispatcher.InvokeAsync(() => SpawnDamagePopup(text, tile, isP1Dweller));

        BuildMapUI();
        BuildVaultUI();
        
        DeckPickerControl.LoadDeck(GetIdleDwellers(), _state.ActiveVault.UnusedWeapons, _state.ActiveVault.UnusedOutfits, _state.ActiveVault.Scraps);
        DeckPickerControl.CardSelected += card => { 
            heldCard = card; 
            UpdateInfoPanel(); 
            if (MapView.Visibility == Visibility.Visible && card.BoundDweller != null) {
                var startTile = _state.Map.Tiles.Cast<HexTile>().FirstOrDefault(t => t.Player1Dwellers.Contains(card.BoundDweller) || t.Player2Dwellers.Contains(card.BoundDweller));
                if (startTile == null) {
                    startTile = _state.IsPlayer1Turn ? _state.Map.Get(0, _state.Map.Rows / 2) : _state.Map.Get(_state.Map.Cols - 1, _state.Map.Rows / 2);
                }
                var vaultCtrl = MapCanvas.Children.OfType<HexTileControl>().FirstOrDefault(c => c.BoundTile == startTile);
                if (vaultCtrl != null) {
                    if (_selectedTile != null && _selectedTile != vaultCtrl) _selectedTile.SetSelected(false);
                    _selectedTile = vaultCtrl;
                    vaultCtrl.SetSelected(true);
                }
            }
        };
        GameOverOverlay.Visibility = Visibility.Collapsed;
        _turns.ResumeTurn();
        ApplyTheme(_state.IsPlayer1Turn);
        RefreshAll();
        StartAutosaveTimer();
        double cW = GameConfigRepository.Config.MapCols * 100;
        double cH = GameConfigRepository.Config.MapRows * 100;
        MapView.ScrollToVerticalOffset(Math.Max(0, (cH - 480) / 2));
        MapView.ScrollToHorizontalOffset(Math.Max(0, (cW - 850) / 2));
    }

    private System.Windows.Threading.DispatcherTimer? _autosaveTimer;

    private void StartAutosaveTimer() {
        _autosaveTimer = new System.Windows.Threading.DispatcherTimer {
            Interval = TimeSpan.FromMinutes(10)
        };
        _autosaveTimer.Tick += (s, e) => SaveGame();
        _autosaveTimer.Start();
    }

    private void SaveGame() {
        try {
            SaveLoadManager.Save(State);
        } catch {
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e) {
        base.OnClosing(e);
        SaveGame();
    }



    private static Color LightenColor(Color c, double amount) {
        if (amount > 0) {
            return Color.FromRgb(
                (byte)Math.Max(0, Math.Min(255, c.R + (255 - c.R) * amount)),
                (byte)Math.Max(0, Math.Min(255, c.G + (255 - c.G) * amount)),
                (byte)Math.Max(0, Math.Min(255, c.B + (255 - c.B) * amount)));
        } else {
            double f = 1.0 + amount;
            return Color.FromRgb(
                (byte)Math.Max(0, Math.Min(255, c.R * f)),
                (byte)Math.Max(0, Math.Min(255, c.G * f)),
                (byte)Math.Max(0, Math.Min(255, c.B * f)));
        }
    }

    private void ApplyTheme(bool isP1) {
        Color accent = isP1 ? _p1Color : _p2Color;
        Color bg     = isP1 ? _p1Bg   : _p2Bg;
        
        this.Resources["PlayerAccentBrush"] = new SolidColorBrush(accent);
        this.Resources["PlayerAccentDimBrush"] = new SolidColorBrush(Color.FromArgb(80, accent.R, accent.G, accent.B));
        this.Resources["PlayerBgBrush"] = new SolidColorBrush(bg);
        
        var glow = new DropShadowEffect {
            Color = accent,
            BlurRadius = 15,
            ShadowDepth = 0,
            Opacity = 0.6
        };
        this.Resources["PlayerAccentGlow"] = glow;

        ActivePlayerLabel.Text = isP1
            ? $"[ SYSTEM AUTHORIZED: {_state.Player1.Pseudo} ]"
            : $"[ SYSTEM AUTHORIZED: {_state.Player2.Pseudo} ]";
            
        HexTileControl.ActivePlayerColor = accent;

        Color bgLight = LightenColor(bg, 0.10);
        Color bgDark = LightenColor(bg, -0.10);

        DeckPickerControl.ApplyTheme(bgDark, accent);
        
        foreach (VaultCardHolder h in RoomsPanel.Children.OfType<VaultCardHolder>()) h.ApplyTheme(bgDark, accent);

        string pseudo = isP1 ? _state.Player1.Pseudo : _state.Player2.Pseudo;
        BgUsernameText.Text = pseudo;
        BgUsernameText.Fill = new SolidColorBrush(accent);
        BgUsernameText.Opacity = 0.03;

        _dwellerInAssignmentSlot = null;
        UpdateAssignmentSlotUI();
    }
    public Color GetThemeColor() {
        return _state.IsPlayer1Turn ? _p1Color : _p2Color;
    }

    public Color GetThemeBgDark() {
        Color bg = _state.IsPlayer1Turn ? _p1Bg : _p2Bg;
        return LightenColor(bg, -0.10);
    }

    private void BuildMapUI() {
        MapCanvas.Children.Clear();
        double tileW = 103, tileH = 103, offsetX = 0, offsetY = 0;
        for (int c = 0; c < _state.Map.Cols; c++)
            for (int r = 0; r < _state.Map.Rows; r++) {
                var ctrl = new HexTileControl();
                ctrl.BindTile(_state.Map.Get(c, r));
                ctrl.Refresh(isP1: _state.IsPlayer1Turn);
                ctrl.CardDropped  += OnCardDroppedOnTile;
                double x = offsetX + c * tileW;
                double y = offsetY + r * tileH;
                Canvas.SetLeft(ctrl, x);
                Canvas.SetTop(ctrl, y);
                MapCanvas.Children.Add(ctrl);
            }
        MapCanvas.Width  = offsetX + _state.Map.Cols * tileW;
        MapCanvas.Height = offsetY + _state.Map.Rows * tileH;
    }

    private void RefreshMapUI() {
        bool isP1 = _state.IsPlayer1Turn;
        foreach (HexTileControl ctrl in MapCanvas.Children.OfType<HexTileControl>())
            ctrl.Refresh(isP1);
    }

    private void BuildVaultUI() {
        RoomsPanel.Children.Clear();
        var vault = _state.ActiveVault;
        foreach (var room in vault.Rooms) {
            var holder = new VaultCardHolder();
            holder.BindRoom(room, vault);
            holder.CardAssigned += OnCardAssignedToRoom;
            holder.AssignClicked += h => {
                _targetRoomForAssignment = h.BoundRoom;
                _targetTileForDeployment = null;
                OpenDwellerSelection();
            };
            holder.TechBonusRequested += OnTechBonus;
            RoomsPanel.Children.Add(holder);
        }
        Color accent = _state.IsPlayer1Turn ? _p1Color : _p2Color;
        Color bg = _state.IsPlayer1Turn ? _p1Bg : _p2Bg;
        Color bgDark = Color.FromRgb((byte)(bg.R * 0.7), (byte)(bg.G * 0.7), (byte)(bg.B * 0.7));
        foreach (VaultCardHolder h in RoomsPanel.Children.OfType<VaultCardHolder>())
            h.ApplyTheme(bgDark, accent);
    }

    private void RefreshVaultUI() {
        foreach (VaultCardHolder h in RoomsPanel.Children.OfType<VaultCardHolder>())
            h.Refresh();
    }

    private void OnTileClicked(HexTileControl ctrl) {
        if (_selectedTile != null && _selectedTile != ctrl) {
            var activeDwellers = _state.IsPlayer1Turn ? _selectedTile.BoundTile.Player1Dwellers : _selectedTile.BoundTile.Player2Dwellers;
            var dw = heldCard?.BoundDweller ?? activeDwellers.LastOrDefault(d => d.IsAlive);
            if (dw != null && (activeDwellers.Contains(dw) || heldCard?.BoundDweller == dw)) {
                bool hasEnemy = _state.IsPlayer1Turn 
                    ? ctrl.BoundTile.Player2Dwellers.Any(d => d.IsAlive) 
                    : ctrl.BoundTile.Player1Dwellers.Any(d => d.IsAlive);
                if (hasEnemy) {
                    ShowWarning("Cannot move to enemy occupied tile!");
                    return;
                }
                int cost = GetDeploymentCost(dw, ctrl.BoundTile);
                if (cost <= _state.ActiveVault.ActionPoints) {
                    _state.ActiveVault.ActionPoints -= cost;
                    RemoveDwellerFromMap(dw);
                    bool isHomeVault = (_state.IsPlayer1Turn && ctrl.BoundTile.Type == TileType.Player1Vault) || (!_state.IsPlayer1Turn && ctrl.BoundTile.Type == TileType.Player2Vault);
                    if (!isHomeVault) {
                        if (_state.IsPlayer1Turn) ctrl.BoundTile.Player1Dwellers.Add(dw);
                        else ctrl.BoundTile.Player2Dwellers.Add(dw);
                    }
                    ctrl.BoundTile.RevealFor(_state.IsPlayer1Turn);
                    _state.AddLog($"{dw.Name} moved to ({ctrl.BoundTile.Col},{ctrl.BoundTile.Row}) [{cost} PA]");
                    if (ctrl.BoundTile.HasConflict) { _combat.ResolveCombatsOnMap(); CheckGameOver(); }
                    _selectedTile.SetSelected(false);
                    _selectedTile = null;
                    heldCard = null;
                    RefreshAll();
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

        if (_selectedTile != null && _selectedTile != ctrl)
            _selectedTile.SetSelected(false);
        _selectedTile = ctrl;
        ctrl.SetSelected(true);
        UpdateInfoPanel();
    }

    private void OnCardDroppedOnTile(HexTileControl ctrl, ControlCard card) {
        if (_selectedTile != null && _selectedTile != ctrl) _selectedTile.SetSelected(false);
        _selectedTile = ctrl;
        DeployCard(card);
    }

    private void OnToggleInfoPanel(object sender, RoutedEventArgs e) {
        if (InfoContentPanel.Visibility == Visibility.Visible) {
            InfoContentPanel.Visibility = Visibility.Collapsed;
            CollapseInfoButton.Content = " + ";
        } else {
            InfoContentPanel.Visibility = Visibility.Visible;
            CollapseInfoButton.Content = " - ";
        }
    }

    private void UpdateInfoPanel() {
        DeployButton.Visibility = Visibility.Collapsed;
        if (_selectedTile?.BoundTile == null) {
            InfoTitle.Text = "Select a tile or dweller";
            InfoBody.Text = "";
            return;
        }
        var tile = _selectedTile.BoundTile;
        bool isP1 = _state.IsPlayer1Turn;
        bool revealed = tile.IsRevealedFor(isP1);
        InfoTitle.Text = revealed ? (tile.LocationName != "" ? tile.LocationName
            : tile.Type == TileType.Player1Vault ? "P1 Vault"
            : tile.Type == TileType.Player2Vault ? "P2 Vault"
            : "Wasteland") : "Fog of War";

        string body = "";
        if (revealed) {
            body += $"P1 dwellers: {tile.Player1Dwellers.Count(d => d.IsAlive)}\n";
            body += $"P2 dwellers: {tile.Player2Dwellers.Count(d => d.IsAlive)}\n\n";

            var enemyDwellers = isP1 ? tile.Player2Dwellers : tile.Player1Dwellers;
            foreach (var enemy in enemyDwellers.Where(d => d.IsAlive)) {
                body += $"⚔ {enemy.Name} (HP: {enemy.CurrentHp}/{enemy.MaxHp})\n";
                body += $"   S:{enemy.Special_S} P:{enemy.Special_P} E:{enemy.Special_E} C:{enemy.Special_C}\n";
                body += $"   I:{enemy.Special_I} A:{enemy.Special_A} L:{enemy.Special_L}\n";
                if (enemy.EquippedWeapon != null) body += $"   W: {enemy.EquippedWeapon.Name}\n";
                if (enemy.EquippedOutfit != null) body += $"   O: {enemy.EquippedOutfit.Name}\n";
            }
        } else {
            body = "Unexplored";
        }
        InfoBody.Text = body.TrimEnd();

        var canAct = _state.ActiveVault.ActionPoints > 0;
        if (canAct && revealed && tile.IsNavigable) {
            DeployButton.Visibility = Visibility.Visible;
        }
    }

    private int GetDeploymentCost(Dweller d, HexTile target) {
        var current = _state.Map.Tiles.Cast<HexTile>().FirstOrDefault(t => t.Player1Dwellers.Contains(d) || t.Player2Dwellers.Contains(d));
        if (current == null) {
            current = _state.IsPlayer1Turn 
                ? _state.Map.Get(0, _state.Map.Rows / 2)
                : _state.Map.Get(_state.Map.Cols - 1, _state.Map.Rows / 2);
        }
        return Helpers.GridMath.GetDistance(current, target);
    }

    private HexTile? FindDwellerTile(Dweller d) {
        for (int c = 0; c < _state.Map.Cols; c++)
            for (int r = 0; r < _state.Map.Rows; r++) {
                var t = _state.Map.Get(c, r);
                if (t.Player1Dwellers.Contains(d) || t.Player2Dwellers.Contains(d)) return t;
            }
        return null;
    }

    private void OnDeployDweller(object s, RoutedEventArgs e) {
        if (_selectedTile?.BoundTile == null) return;
        _targetRoomForAssignment = null;
        _targetTileForDeployment = _selectedTile.BoundTile;
        OpenDwellerSelection();
    }

    private bool DeployCard(ControlCard? card) {
        if (card?.BoundDweller == null || _selectedTile?.BoundTile == null) {
            _selectedTile = null;
            return false;
        }
        if (!_selectedTile.BoundTile.IsNavigable) {
            ShowWarning("Tile is unnavigable!");
            _selectedTile = null;
            return false;
        }
        var vault = _state.ActiveVault;
        var tile = _selectedTile.BoundTile;
        bool isP1 = _state.IsPlayer1Turn;
        bool hasEnemy = isP1 
            ? tile.Player2Dwellers.Any(d => d.IsAlive) 
            : tile.Player1Dwellers.Any(d => d.IsAlive);
        if (hasEnemy) {
            ShowWarning("Cannot deploy to enemy occupied tile!");
            _selectedTile = null;
            return false;
        }
        int cost = GetDeploymentCost(card.BoundDweller, tile);
        if (cost > vault.ActionPoints) {
            ShowWarning("Tile too far away, you need more PA");
            _selectedTile = null;
            return false;
        }
        
        if (card.OwnerDweller != null) {
            if (card.BoundWeapon != null) {
                card.OwnerDweller.EquippedWeapon = null;
            }
            if (card.BoundOutfit != null) {
                card.OwnerDweller.EquippedOutfit = null;
            }
        }

        vault.ActionPoints -= cost;
        RemoveDwellerFromMap(card.BoundDweller);
        bool isHomeVault = (isP1 && tile.Type == TileType.Player1Vault) || (!isP1 && tile.Type == TileType.Player2Vault);
        if (!isHomeVault) {
            var dwellerList = isP1 ? tile.Player1Dwellers : tile.Player2Dwellers;
            dwellerList.Add(card.BoundDweller);
        }
        tile.IsRevealedByP1 = isP1 ? true : tile.IsRevealedByP1;
        tile.IsRevealedByP2 = isP1 ? tile.IsRevealedByP2 : true;

        if (tile.HasConflict) {
            _combat.ResolveCombatsOnMap();
            CheckGameOver();
        }
        _state.AddLog($"{card.BoundDweller.Name} → ({tile.Col},{tile.Row}) [{cost} PA]");
        heldCard = null;
        if (_selectedTile != null) {
            _selectedTile.SetSelected(false);
        }
        _selectedTile = null;
        heldCard = null;
        RefreshAll();
        return true;
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



    private void OnCardAssignedToRoom(VaultCardHolder holder, ControlCard card) {
        if (card.BoundDweller == null || holder.BoundRoom == null) return;
        if (holder.BoundRoom.AssignedDwellers.Contains(card.BoundDweller)) return; // Already in room, no PA cost
        if (_state.ActiveVault.ActionPoints <= 0) { ShowWarning("No action points left!"); return; }

        bool isHeavyTask = holder.BoundRoom.Type == RoomType.Generator || 
                           holder.BoundRoom.Type == RoomType.WeaponFactory || 
                           holder.BoundRoom.Type == RoomType.OutfitFactory || 
                           holder.BoundRoom.Type == RoomType.TrainingCenter;
        if (isHeavyTask) {
            ControlCard.DwellersToShakeOnLoad.Add(card.BoundDweller);
        }

        _res.TryAssignDweller(_state.ActiveVault, card.BoundDweller, holder.BoundRoom);
        _state.ActiveVault.ActionPoints--;
        RefreshAll();
    }

    private void OnTechBonus(string bonus) {
        var (ok, err) = _res.TryUnlockTechBonus(_state.ActiveVault, bonus);
        if (!ok) AppendLog(err);
        RefreshAll();
    }

    private void OnEndTurn(object s, RoutedEventArgs e) => OnEndTurn_Internal();
    private void OnEndTurn_Internal() {
        if (_state.Phase == GamePhase.GameOver) {
            return;
        }

        if (_selectedTile != null) {
            _selectedTile.SetSelected(false);
            _selectedTile = null;
        }

        bool isP1 = _state.IsPlayer1Turn;

        for (int c = 0; c < _state.Map.Cols; c++) {
            for (int r = 0; r < _state.Map.Rows; r++) {
                var t = _state.Map.Get(c, r);
                if (t.IsNavigable) {
                    var dweller = (isP1 ? t.Player1Dwellers : t.Player2Dwellers).FirstOrDefault(d => d.IsAlive);
                    if (dweller != null) {
                        var vault = isP1 ? _state.Vault1 : _state.Vault2;
                        var (card, msg) = _loot.SearchTile(t, dweller, isP1, vault);
                        AppendLog($"[Auto-Loot ({c},{r})] {msg}");
                        if (card is Dweller nd) {
                            vault.Dwellers.Add(nd);
                        } else if (card is IWeapon w) {
                            vault.Weapons.Add(w);
                        } else if (card is IOutfit o) {
                            vault.Outfits.Add(o);
                        }
                    }
                }
            }
        }

        _turns.EndTurn();
        _combat.ResolveCombatsOnMap();
        CheckGameOver();

        if (_state.Phase != GamePhase.GameOver) {
            ApplyTheme(isP1: _state.IsPlayer1Turn);
            if (_showingVault) {
                BuildVaultUI();
            }
        }
        
        RefreshAll();
    }

    private void OnToggleView(object s, RoutedEventArgs e) {
        _showingVault = !_showingVault;
        MapView.Visibility   = _showingVault ? Visibility.Collapsed : Visibility.Visible;
        MapBorder.Visibility = _showingVault ? Visibility.Collapsed : Visibility.Visible;
        VaultView.Visibility = _showingVault ? Visibility.Visible   : Visibility.Collapsed;
        PhaseIndicator.Content = _showingVault ? "🗺 MAP" : "🏠 VAULT";
        if (_showingVault) { BuildVaultUI(); RefreshVaultUI(); }
    }

    private void OnBuildRoom(object s, RoutedEventArgs e) {
        var vault    = _state.ActiveVault;
        var existing = vault.Rooms.Select(r => r.Type);
        var dlg      = new BuildRoomDialog(existing) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.ChosenRoom.HasValue) {
            var (ok, err) = _res.TryBuildRoom(vault, dlg.ChosenRoom.Value);
            if (!ok) AppendLog(err);
            else { BuildVaultUI(); RefreshAll(); }
        }
    }

    private void OnPlayAgain(object s, RoutedEventArgs e) {
        var menu = new StartMenuWindow { Owner = this };
        if (menu.ShowDialog() == true)
            StartNewGame(menu.Profile1, menu.Profile2);
    }

    private void OnTick() {
        double pct = (double)_turns.SecondsRemaining / _turns.TurnDurationSeconds;
        double fullW = ActualWidth > 0 ? ActualWidth : 1280;
        TimerBar.Width = fullW * pct;

        if (_turns.SecondsRemaining <= 10) {
            Color warn = Color.FromRgb(200, 60, 60);
            TimerBar.Fill = new SolidColorBrush(warn);
        }
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
            } catch {
            }
        }
        Color col = isP1Dweller ? _p1Color : _p2Color;
        DamagePopupHelper.Spawn(text, PopupCanvas, x, y, col);
    }

    private void RefreshHUD() {
        var vault = _state.ActiveVault;
        ElecText.Text  = $"⚡ {vault.Electricity}";
        WaterText.Text = $"💧 {vault.Water}";
        FoodText.Text  = $"🍴 {vault.Food}";
        ApText.Text    = $"PA: {vault.ActionPoints}/{vault.MaxActionPoints}";
        TurnText.Text  = $"Turn {_state.TurnNumber}";
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
        DeckPickerControl.LoadDeck(GetIdleDwellers(), _state.ActiveVault.UnusedWeapons, _state.ActiveVault.UnusedOutfits, _state.ActiveVault.Scraps);
    }

    private IEnumerable<Dweller> GetIdleDwellers() {
        return _state.ActiveVault.Dwellers.Where(d => d.IsAlive && GetDwellerStatus(d) == "Idle in Deck");
    }

    private void RemoveDwellerFromMap(Dweller d) {
        for (int c = 0; c < _state.Map.Cols; c++)
            for (int r = 0; r < _state.Map.Rows; r++) {
                _state.Map.Get(c, r).Player1Dwellers.Remove(d);
                _state.Map.Get(c, r).Player2Dwellers.Remove(d);
            }
    }

    private void AppendLog(string msg) {
        if (string.IsNullOrEmpty(msg)) return;
        LogText.Text += msg + "\n";
        LogScroller.ScrollToEnd();
    }

    private void CheckGameOver() {
        if (_state.Phase != GamePhase.GameOver) return;
        GameOverOverlay.Visibility = Visibility.Visible;
        bool p1Won = _state.WinnerName == _state.Player1.Pseudo;
        GameOverTitle.Text    = p1Won ? "🏆 VICTORY!" : "💀 DEFEAT";
        GameOverWinner.Text   = $"{_state.WinnerName} eliminated the enemy Overseer!";
        GameOverTitle.Foreground = new SolidColorBrush(Colors.White);
    }

    private void OnRoomsPreviewMouseWheel(object sender, MouseWheelEventArgs e) {
        var hit = VisualTreeHelper.HitTest(RoomsScrollViewer, e.GetPosition(RoomsScrollViewer));
        var holder = FindParent<VaultCardHolder>(hit?.VisualHit);
        if (holder != null && holder.BoundRoom?.AssignedDwellers.Count > 1) {
            return;
        }
        if (e.Delta > 0) RoomsScrollViewer.LineLeft();
        else RoomsScrollViewer.LineRight();
        e.Handled = true;
    }

    private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject {
        while (child != null) {
            if (child is T parent) return parent;
            child = VisualTreeHelper.GetParent(child);
        }
        return null;
    }

    private void OpenDwellerSelection() {
        DwellerSelectionList.Children.Clear();
        _selectedDwellerForAction = null;
        _selectedDwellerRowBorder = null;

        var vault = _state.ActiveVault;
        foreach (var d in vault.Dwellers) {
            if (_targetTileForDeployment != null) {
                int cost = GetDeploymentCost(d, _targetTileForDeployment);
                if (cost > vault.ActionPoints) continue;
            }
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

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.8, GridUnitType.Star) });

        var nameStack = new StackPanel();
        nameStack.Children.Add(new TextBlock { Text = d.Name, Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 12 });
        var rc = new[] { "#969696", "#64C864", "#6496FA", "#B450DC", "#FFB400" };
        nameStack.Children.Add(new TextBlock { Text = d.Rarity.ToString(), Foreground = (Brush)new BrushConverter().ConvertFromString(rc[(int)d.Rarity])!, FontSize = 9, FontWeight = FontWeights.Bold });
        Grid.SetColumn(nameStack, 0);
        grid.Children.Add(nameStack);

        grid.Children.Add(new TextBlock { Text = $"S:{d.Special_S} P:{d.Special_P} E:{d.Special_E} C:{d.Special_C}\nI:{d.Special_I} A:{d.Special_A} L:{d.Special_L}", Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xDD)), FontSize = 10, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(grid.Children[^1], 1);

        grid.Children.Add(new TextBlock { Text = $"W: {d.EquippedWeapon?.Name ?? "None"}\nO: {d.EquippedOutfit?.Name ?? "None"}", Foreground = new SolidColorBrush(Color.FromRgb(0x99, 0xAA, 0x99)), FontSize = 10, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(grid.Children[^1], 2);

        grid.Children.Add(new TextBlock { Text = $"HP: {d.CurrentHp}/{d.MaxHp}", Foreground = d.CurrentHp > d.MaxHp / 2 ? Brushes.LightGreen : Brushes.OrangeRed, FontSize = 10, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(grid.Children[^1], 3);

        grid.Children.Add(new TextBlock { Text = GetDwellerStatus(d), Foreground = new SolidColorBrush(Color.FromRgb(0xBB, 0xBB, 0xBB)), FontSize = 10, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(grid.Children[^1], 4);

        border.Child = grid;
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

    private string GetDwellerStatus(Dweller d) {
        foreach (var room in _state.ActiveVault.Rooms)
            if (room.AssignedDwellers.Contains(d)) return $"Working in {room.Name}";
        for (int c = 0; c < _state.Map.Cols; c++)
            for (int r = 0; r < _state.Map.Rows; r++) {
                var t = _state.Map.Get(c, r);
                if (t.Player1Dwellers.Contains(d) || t.Player2Dwellers.Contains(d))
                    return $"At ({c},{r}) {(t.LocationName != "" ? t.LocationName : " ")}";
            }
        return "Idle in Deck";
    }

    private void OnCancelDwellerSelection(object s, RoutedEventArgs e) => DwellerSelectionOverlay.Visibility = Visibility.Collapsed;
    private void OnConfirmDwellerSelection(object s, RoutedEventArgs e) {
        if (_selectedDwellerForAction == null) return;
        DwellerSelectionOverlay.Visibility = Visibility.Collapsed;
        var dw = _selectedDwellerForAction;

        if (_targetRoomForAssignment != null) {
            if (_state.ActiveVault.ActionPoints <= 0) { ShowWarning("No action points left!"); return; }
            bool isHeavyTask = _targetRoomForAssignment.Type == RoomType.Generator || 
                               _targetRoomForAssignment.Type == RoomType.WeaponFactory || 
                               _targetRoomForAssignment.Type == RoomType.OutfitFactory || 
                               _targetRoomForAssignment.Type == RoomType.TrainingCenter;
            if (isHeavyTask) {
                ControlCard.DwellersToShakeOnLoad.Add(dw);
            }
            _res.TryAssignDweller(_state.ActiveVault, dw, _targetRoomForAssignment);
            _state.ActiveVault.ActionPoints--;
            RemoveDwellerFromMap(dw);
            if (dw == _dwellerInAssignmentSlot) _dwellerInAssignmentSlot = null;
            RefreshAll();
        }
        else if (_targetTileForDeployment != null) {
            if (!_targetTileForDeployment.IsNavigable) { ShowWarning("Tile is unnavigable!"); return; }
            var vault = _state.ActiveVault;
            var tile = _targetTileForDeployment;
            int cost = GetDeploymentCost(dw, tile);
            if (cost > vault.ActionPoints) { ShowWarning("Tile too far away, you need more PA"); return; }
            vault.ActionPoints -= cost;
            RemoveDwellerFromMap(dw);
            if (dw == _dwellerInAssignmentSlot) _dwellerInAssignmentSlot = null;
            bool isP1 = _state.IsPlayer1Turn;
            bool isHomeVault = (isP1 && tile.Type == TileType.Player1Vault) || (!isP1 && tile.Type == TileType.Player2Vault);
            if (!isHomeVault) {
                var dwellerList = isP1 ? tile.Player1Dwellers : tile.Player2Dwellers;
                dwellerList.Add(dw);
            }
            tile.IsRevealedByP1 = isP1 ? true : tile.IsRevealedByP1;
            tile.IsRevealedByP2 = isP1 ? tile.IsRevealedByP2 : true;
            if (tile.HasConflict) { _combat.ResolveCombatsOnMap(); CheckGameOver(); }
            _state.AddLog($"{dw.Name} → ({tile.Col},{tile.Row}) [{cost} PA]");
            if (_selectedTile != null) _selectedTile.SetSelected(false);
            _selectedTile = null;
            RefreshAll();
        }
    }

    public void OpenEquipmentSelection(Dweller dw, bool isW) {
        _equippingDweller = dw;
        _isEquippingWeapon = isW;
        _selectedEquipment = null;
        _selectedEquipmentRowBorder = null;

        EquipmentSelectionTitle.Text = isW ? "SELECT WEAPON" : "SELECT OUTFIT";
        EquipmentSelectionList.Children.Clear();

        var vault = _state.ActiveVault;
        if (isW) {
            foreach (var weapon in vault.UnusedWeapons) {
                var row = CreateEquipmentRow(weapon);
                EquipmentSelectionList.Children.Add(row);
            }
        } else {
            foreach (var outfit in vault.UnusedOutfits) {
                var row = CreateEquipmentRow(outfit);
                EquipmentSelectionList.Children.Add(row);
            }
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

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });

        var nameStack = new StackPanel();
        nameStack.Children.Add(new TextBlock { Text = item.Name, Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 12 });
        var rc = new[] { "#969696", "#64C864", "#6496FA", "#B450DC", "#FFB400" };
        nameStack.Children.Add(new TextBlock { Text = item.Rarity.ToString(), Foreground = (Brush)new BrushConverter().ConvertFromString(rc[(int)item.Rarity])!, FontSize = 9, FontWeight = FontWeights.Bold });
        Grid.SetColumn(nameStack, 0);
        grid.Children.Add(nameStack);

        string statsStr = "";
        if (item is IWeapon w) {
            statsStr = $"Damage: {w.Damage} ({w.WeaponType})";
        } else if (item is IOutfit o) {
            statsStr = $"Armor: {o.ArmorValue}";
            if (o is Outfit op) {
                statsStr += $" (S:{op.S} P:{op.P} E:{op.E} L:{op.L})";
            }
        }
        grid.Children.Add(new TextBlock { Text = statsStr, Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xDD)), FontSize = 10, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(grid.Children[^1], 1);

        border.Child = grid;
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

    public void OpenDwellerSheet(Dweller dw) {
        _sheetDweller = dw;
        DwellerSheetOverlay.Visibility = Visibility.Visible;
        
        Color bg = GetThemeBgDark();
        DwellerSheetBorder.Background = new SolidColorBrush(Color.FromArgb(240, bg.R, bg.G, bg.B));
        
        Color accent = GetThemeColor();
        DwellerSheetBorder.BorderBrush = new SolidColorBrush(accent);
        SheetStatusText.Foreground = new SolidColorBrush(accent);

        RefreshDwellerSheet();
    }

    private void OnCloseDwellerSheet(object sender, RoutedEventArgs e) {
        DwellerSheetOverlay.Visibility = Visibility.Collapsed;
        _sheetDweller = null;
    }

    public void RefreshDwellerSheet() {
        if (_sheetDweller == null) return;

        SheetStatusText.Text = GetDwellerLocationString(_sheetDweller);
        SheetDwellerCard.BindDweller(_sheetDweller);

        if (_sheetDweller.EquippedWeapon != null) {
            SheetWeaponPlaceholder.Visibility = Visibility.Collapsed;
            SheetWeaponViewbox.Visibility = Visibility.Visible;
            
            SheetWeaponViewbox.Child = null;
            var wCard = new ControlCard();
            wCard.BindWeapon(_sheetDweller.EquippedWeapon);
            wCard.OwnerDweller = _sheetDweller;
            SheetWeaponViewbox.Child = wCard;
        } else {
            SheetWeaponPlaceholder.Visibility = Visibility.Visible;
            SheetWeaponViewbox.Visibility = Visibility.Collapsed;
            SheetWeaponViewbox.Child = null;
        }

        if (_sheetDweller.EquippedOutfit != null) {
            SheetOutfitPlaceholder.Visibility = Visibility.Collapsed;
            SheetOutfitViewbox.Visibility = Visibility.Visible;
            
            SheetOutfitViewbox.Child = null;
            var oCard = new ControlCard();
            oCard.BindOutfit(_sheetDweller.EquippedOutfit);
            oCard.OwnerDweller = _sheetDweller;
            SheetOutfitViewbox.Child = oCard;
        } else {
            SheetOutfitPlaceholder.Visibility = Visibility.Visible;
            SheetOutfitViewbox.Visibility = Visibility.Collapsed;
            SheetOutfitViewbox.Child = null;
        }
    }

    private string GetDwellerLocationString(Dweller dw) {
        if (_state.Vault1 != null) {
            foreach (var room in _state.Vault1.Rooms) {
                if (room.AssignedDwellers.Contains(dw)) {
                    string name = room.Name.Replace("🌿 ", "").Replace("🔫 ", "").Replace("👕 ", "").Replace("🏋️ ", "").Replace("🔬 ", "");
                    return $"{name}:Vault";
                }
            }
        }
        if (_state.Vault2 != null) {
            foreach (var room in _state.Vault2.Rooms) {
                if (room.AssignedDwellers.Contains(dw)) {
                    string name = room.Name.Replace("🌿 ", "").Replace("🔫 ", "").Replace("👕 ", "").Replace("🏋️ ", "").Replace("🔬 ", "");
                    return $"{name}:Vault";
                }
            }
        }
        for (int c = 0; c < _state.Map.Cols; c++) {
            for (int r = 0; r < _state.Map.Rows; r++) {
                var tile = _state.Map.Get(c, r);
                if (tile.Player1Dwellers.Contains(dw) || tile.Player2Dwellers.Contains(dw)) {
                    return $"{c}-{r}:Wasteland";
                }
            }
        }
        return "Idle:Vault";
    }

    private void OnCancelEquipmentSelection(object s, RoutedEventArgs e) => EquipmentSelectionOverlay.Visibility = Visibility.Collapsed;
    private void OnConfirmEquipmentSelection(object s, RoutedEventArgs e) {
        if (_equippingDweller == null || _selectedEquipment == null) return;
        EquipmentSelectionOverlay.Visibility = Visibility.Collapsed;

        if (_isEquippingWeapon && _selectedEquipment is IWeapon w) {
            _equippingDweller.EquippedWeapon = w;
            _state.AddLog($"{_equippingDweller.Name} equipped {w.Name}");
        } else if (!_isEquippingWeapon && _selectedEquipment is IOutfit o) {
            _equippingDweller.EquippedOutfit = o;
            _state.AddLog($"{_equippingDweller.Name} equipped {o.Name}");
        }
        RefreshAll();
    }

    private void OnAssignmentSlotDragOver(object sender, DragEventArgs e) {
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void OnAssignmentSlotDrop(object sender, DragEventArgs e) {
        var held = heldCard;
        if (held != null && held.BoundDweller != null) {
            _selectedDwellerForAction = held.BoundDweller;
            UpdateAssignmentSlotUI();
            heldCard = null;
        }
    }

    private void OnReturnCardDrop(object sender, DragEventArgs e) {
        var held = heldCard;
        if (held != null) {
            if (held.BoundWeapon != null && held.OwnerDweller != null) {
                held.OwnerDweller.EquippedWeapon = null;
                _state.AddLog($"{held.OwnerDweller.Name} unequipped {held.BoundWeapon.Name}");
                RefreshAll();
                UpdateAssignmentSlotUI();
            }
            else if (held.BoundOutfit != null && held.OwnerDweller != null) {
                held.OwnerDweller.EquippedOutfit = null;
                _state.AddLog($"{held.OwnerDweller.Name} unequipped {held.BoundOutfit.Name}");
                RefreshAll();
                UpdateAssignmentSlotUI();
            }
            heldCard = null;
        }
    }

    public void OnAssignmentSlotDropDirect(ControlCard card) {
        if (card.BoundDweller != null) {
            _dwellerInAssignmentSlot = card.BoundDweller;
            UpdateAssignmentSlotUI();
        }
        else if (_dwellerInAssignmentSlot != null) {
            if (card.BoundWeapon != null) {
                if (card.OwnerDweller != null) card.OwnerDweller.EquippedWeapon = null;
                _dwellerInAssignmentSlot.EquippedWeapon = card.BoundWeapon;
                _state.AddLog($"{_dwellerInAssignmentSlot.Name} equipped {card.BoundWeapon.Name}");
                RefreshAll();
                UpdateAssignmentSlotUI();
            }
            else if (card.BoundOutfit != null) {
                if (card.OwnerDweller != null) card.OwnerDweller.EquippedOutfit = null;
                _dwellerInAssignmentSlot.EquippedOutfit = card.BoundOutfit;
                _state.AddLog($"{_dwellerInAssignmentSlot.Name} equipped {card.BoundOutfit.Name}");
                RefreshAll();
                UpdateAssignmentSlotUI();
            }
        }
    }

    private void UpdateAssignmentSlotUI() {
        AssignmentSlotContainer.Children.Clear();
        if (_dwellerInAssignmentSlot == null) {
            var placeholder = new Border {
                BorderBrush = new SolidColorBrush(Color.FromRgb(10, 10, 10)),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(8),
                Width = 140, Height = 200,
                Background = new SolidColorBrush(Color.FromRgb(19, 19, 19))
            };
            placeholder.Child = new TextBlock {
                Text = "Drag dweller card here",
                Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                FontSize = 11, FontStyle = FontStyles.Italic,
                VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap
            };
            AssignmentSlotContainer.Children.Add(placeholder);
        } else {
            var card = new ControlCard();
            card.BindDweller(_dwellerInAssignmentSlot);
            AssignmentSlotContainer.Children.Add(card);
        }
    }

    private bool IsPointOverElement(FrameworkElement element, Point windowPoint) {
        if (element.Visibility != Visibility.Visible) return false;
        try {
            Point rel = this.TranslatePoint(windowPoint, element);
            return rel.X >= 0 && rel.X <= element.ActualWidth && rel.Y >= 0 && rel.Y <= element.ActualHeight;
        } catch {
            return false;
        }
    }


    public void StartDraggingCard(ControlCard card, Point position) {
        if (card.Parent is Panel p) {
            _originalCardParent = p;
            _originalCardDecorator = null;
            _originalCardIndex = p.Children.IndexOf(card);
            
            _dragPlaceholder = new Border {
                Width = card.ActualWidth,
                Height = card.ActualHeight,
                Background = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)),
                BorderThickness = new Thickness(1, 1, 1, 1),
                Margin = card.Margin
            };
            p.Children.Insert(_originalCardIndex, _dragPlaceholder);
            p.Children.Remove(card);
        } else if (card.Parent is Decorator d) {
            _originalCardParent = null;
            _originalCardDecorator = d;
            
            _dragPlaceholder = new Border {
                Width = card.ActualWidth,
                Height = card.ActualHeight,
                Background = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)),
                BorderThickness = new Thickness(1, 1, 1, 1),
                Margin = card.Margin
            };
            d.Child = _dragPlaceholder;
        }

        DragOverlayCanvas.Children.Add(card);
        Canvas.SetLeft(card, position.X - card.ActualWidth / 2);
        Canvas.SetTop(card, position.Y - card.ActualHeight / 2);
        
        heldCard = card;
        if (_selectedTile != null) {
            _selectedTile.SetSelected(false);
            _selectedTile = null;
        }

        if (card.BoundDweller != null) {
            foreach (HexTileControl tileCtrl in MapCanvas.Children.OfType<HexTileControl>()) {
                if (tileCtrl.BoundTile != null && tileCtrl.BoundTile.IsNavigable) {
                    if (GetDeploymentCost(card.BoundDweller, tileCtrl.BoundTile) <= _state.ActiveVault.ActionPoints) {
                        tileCtrl.SetReachableHighlight(true);
                    }
                }
            }
        }
    }

    private HexTileControl? GetPreciseHoveredTile(Point windowPoint) {
        HexTileControl? bestTile = null;
        double minDistance = double.MaxValue;
        foreach (HexTileControl tileCtrl in MapCanvas.Children.OfType<HexTileControl>()) {
            if (tileCtrl.Visibility != Visibility.Visible || tileCtrl.BoundTile == null) continue;
            try {
                double w = tileCtrl.ActualWidth > 0 ? tileCtrl.ActualWidth : 100;
                double h = tileCtrl.ActualHeight > 0 ? tileCtrl.ActualHeight : 100;
                Point center = tileCtrl.TranslatePoint(new Point(w / 2, h / 2), this);
                double dx = windowPoint.X - center.X;
                double dy = windowPoint.Y - center.Y;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist < minDistance) {
                    minDistance = dist;
                    bestTile = tileCtrl;
                }
            } catch {}
        }
        if (minDistance < 50) return bestTile;
        return null;
    }

    public void MoveDraggingCard(ControlCard card, Point currentMousePos) {
        double targetScale = 1.0;
        
        if (MapView.Visibility == Visibility.Visible && MapBorder.IsVisible) {
            Point mapRel = MapBorder.TranslatePoint(new Point(0, 0), this);
            double minX = mapRel.X;
            double minY = mapRel.Y;
            double maxX = mapRel.X + MapBorder.ActualWidth;
            double maxY = mapRel.Y + MapBorder.ActualHeight;

            double dx = 0;
            if (currentMousePos.X < minX) dx = minX - currentMousePos.X;
            else if (currentMousePos.X > maxX) dx = currentMousePos.X - maxX;

            double dy = 0;
            if (currentMousePos.Y < minY) dy = minY - currentMousePos.Y;
            else if (currentMousePos.Y > maxY) dy = currentMousePos.Y - maxY;

            double dist = Math.Max(0, Math.Sqrt(dx * dx + dy * dy));

            if (dist < 200) {
                double factor = dist / 200.0;
                double tileScale = 100.0 * (MapScale?.ScaleX ?? 1.0) / 140.0;
                targetScale = tileScale + factor * (1.0 - tileScale);
            }

            var hoveredTile = GetPreciseHoveredTile(currentMousePos);
            foreach (HexTileControl tileCtrl in MapCanvas.Children.OfType<HexTileControl>()) {
                tileCtrl.SetHovered(tileCtrl == hoveredTile);
            }
        }
        
        card.RenderTransform = new ScaleTransform(targetScale, targetScale);
        Canvas.SetLeft(card, currentMousePos.X - (140 * targetScale / 2));
        Canvas.SetTop(card, currentMousePos.Y - (200 * targetScale / 2));
    }

    public void StopDraggingCard(ControlCard card, Point dropPoint) {
        bool handled = false;
        foreach (HexTileControl tileCtrl in MapCanvas.Children.OfType<HexTileControl>()) {
            tileCtrl.SetReachableHighlight(false);
        }

        if (MapView.Visibility == Visibility.Visible) {
            var hoveredTile = GetPreciseHoveredTile(dropPoint);
            if (hoveredTile != null) {
                hoveredTile.SetHovered(false);
                if (_selectedTile != null && _selectedTile != hoveredTile) _selectedTile.SetSelected(false);
                _selectedTile = hoveredTile;
                handled = DeployCard(card);
            }
            foreach (HexTileControl tileCtrl in MapCanvas.Children.OfType<HexTileControl>()) {
                tileCtrl.SetHovered(false);
            }
        }
        
        if (!handled && VaultView.Visibility == Visibility.Visible) {
            foreach (VaultCardHolder holder in RoomsPanel.Children.OfType<VaultCardHolder>()) {
                if (IsPointOverElement(holder, dropPoint)) {
                    OnCardAssignedToRoom(holder, card);
                    handled = true;
                    break;
                }
            }
        }
        
        if (!handled && IsPointOverElement(AssignmentSlotContainer, dropPoint)) {
            OnAssignmentSlotDropDirect(card);
            handled = true;
        }

        if (!handled && IsPointOverElement(ReturnBorder, dropPoint)) {
            if (card.BoundWeapon != null && card.OwnerDweller != null) {
                card.OwnerDweller.EquippedWeapon = null;
                _state.AddLog($"{card.OwnerDweller.Name} unequipped {card.BoundWeapon.Name}");
                handled = true;
            }
            else if (card.BoundOutfit != null && card.OwnerDweller != null) {
                card.OwnerDweller.EquippedOutfit = null;
                _state.AddLog($"{card.OwnerDweller.Name} unequipped {card.BoundOutfit.Name}");
                handled = true;
            }
            RefreshAll();
            UpdateAssignmentSlotUI();
            if (DwellerSheetOverlay.Visibility == Visibility.Visible) {
                RefreshDwellerSheet();
            }
        }

        if (!handled && DwellerSheetOverlay.Visibility == Visibility.Visible && _sheetDweller != null) {
            if (IsPointOverElement(SheetWeaponGrid, dropPoint)) {
                if (card.BoundWeapon != null) {
                    if (card.OwnerDweller != null) card.OwnerDweller.EquippedWeapon = null;
                    _sheetDweller.EquippedWeapon = card.BoundWeapon;
                    _state.AddLog($"{_sheetDweller.Name} equipped {card.BoundWeapon.Name}");
                    handled = true;
                    RefreshAll();
                    RefreshDwellerSheet();
                }
            }
            else if (IsPointOverElement(SheetOutfitGrid, dropPoint)) {
                if (card.BoundOutfit != null) {
                    if (card.OwnerDweller != null) card.OwnerDweller.EquippedOutfit = null;
                    _sheetDweller.EquippedOutfit = card.BoundOutfit;
                    _state.AddLog($"{_sheetDweller.Name} equipped {card.BoundOutfit.Name}");
                    handled = true;
                    RefreshAll();
                    RefreshDwellerSheet();
                }
            }
        }
        
        if (!handled && IsPointOverElement(DeckPickerControl, dropPoint)) {
            if (card.BoundDweller != null) {
                foreach (var r in _state.ActiveVault.Rooms) {
                    if (r.AssignedDwellers.Contains(card.BoundDweller)) {
                        r.AssignedDwellers.Remove(card.BoundDweller);
                        break;
                    }
                }
                RemoveDwellerFromMap(card.BoundDweller);
                RefreshAll();
                handled = true;
            }
        }

        DragOverlayCanvas.Children.Remove(card);
        card.RenderTransform = new ScaleTransform(1, 1);
        card.BeginAnimation(Canvas.LeftProperty, null);
        card.BeginAnimation(Canvas.TopProperty, null);

        if (_originalCardParent != null) {
            if (_dragPlaceholder != null) _originalCardParent.Children.Remove(_dragPlaceholder);
            if (!handled) _originalCardParent.Children.Insert(_originalCardIndex, card);
        } else if (_originalCardDecorator != null) {
            if (!handled) _originalCardDecorator.Child = card;
            else _originalCardDecorator.Child = null;
        }

        _originalCardParent = null;
        _originalCardDecorator = null;
        _dragPlaceholder = null;
        heldCard = null;
        RefreshAll();
    }
    
    private Point _mapDragStart;
    private Point _mapScrollStart;
    private bool _isDraggingMap;

    private void MapCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
        if (e.OriginalSource is not FrameworkElement el || el.TemplatedParent is not HexTileControl) {
            _isDraggingMap = true;
            _mapDragStart = e.GetPosition(this);
            _mapScrollStart = new Point(MapView.HorizontalOffset, MapView.VerticalOffset);
            MapCanvas.CaptureMouse();
        }
    }
    
    private void MapCanvas_MouseMove(object sender, MouseEventArgs e) {
        if (_isDraggingMap) {
            Point current = e.GetPosition(this);
            double dx = current.X - _mapDragStart.X;
            double dy = current.Y - _mapDragStart.Y;
            MapView.ScrollToHorizontalOffset(_mapScrollStart.X - dx);
            MapView.ScrollToVerticalOffset(_mapScrollStart.Y - dy);
        } else {
            var hoveredTile = GetPreciseHoveredTile(e.GetPosition(this));
            bool isMoveMode = _selectedTile != null && (
                heldCard?.BoundDweller != null || 
                (_state.IsPlayer1Turn ? _selectedTile.BoundTile.Player1Dwellers : _selectedTile.BoundTile.Player2Dwellers).Any(d => d.IsAlive));
            
            foreach (HexTileControl tileCtrl in MapCanvas.Children.OfType<HexTileControl>()) {
                if (hoveredTile != null && isMoveMode && tileCtrl.BoundTile != null) {
                    var activeDwellers = _state.IsPlayer1Turn ? _selectedTile.BoundTile.Player1Dwellers : _selectedTile.BoundTile.Player2Dwellers;
                    var dw = heldCard?.BoundDweller ?? activeDwellers.LastOrDefault(d => d.IsAlive);
                    if (dw != null && GetDeploymentCost(dw, hoveredTile.BoundTile) <= _state.ActiveVault.ActionPoints && hoveredTile.BoundTile.IsNavigable) {
                        int distTotal = GetDeploymentCost(dw, hoveredTile.BoundTile);
                        var startTile = _state.Map.Tiles.Cast<HexTile>().FirstOrDefault(t => t.Player1Dwellers.Contains(dw) || t.Player2Dwellers.Contains(dw));
                        if (startTile == null) {
                            startTile = _state.IsPlayer1Turn ? _state.Map.Get(0, _state.Map.Rows / 2) : _state.Map.Get(_state.Map.Cols - 1, _state.Map.Rows / 2);
                        }

                        bool onPath = false;
                        if (startTile != null) onPath = Helpers.GridMath.IsOnLine(startTile, hoveredTile.BoundTile, tileCtrl.BoundTile);
                        
                        tileCtrl.SetPath(onPath);
                    } else {
                        tileCtrl.SetPath(false);
                    }
                } else {
                    tileCtrl.SetPath(false);
                }
                tileCtrl.SetHovered(tileCtrl == hoveredTile);
            }
        }
    }
    
    private void MapCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
        if (_isDraggingMap) {
            _isDraggingMap = false;
            MapCanvas.ReleaseMouseCapture();
            Point current = e.GetPosition(this);
            double dist = Math.Sqrt(Math.Pow(current.X - _mapDragStart.X, 2) + Math.Pow(current.Y - _mapDragStart.Y, 2));
            if (dist < 5) {
                var clickedTile = GetPreciseHoveredTile(current);
                if (clickedTile != null) OnTileClicked(clickedTile);
            }
        }
    }

    private void MapView_MouseWheel(object sender, MouseWheelEventArgs e) {
        double zoomDelta = e.Delta > 0 ? 0.1 : -0.1;
        double newScale = (MapScale?.ScaleX ?? 1.0) + zoomDelta;
        if (newScale >= 0.5 && newScale <= 2.0 && MapScale != null) {
            MapScale.ScaleX = newScale;
            MapScale.ScaleY = newScale;
        }
        e.Handled = true;
    }
}
