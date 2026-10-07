using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using LaboratoireProgrammation.Project.ViewModels.Laboratoires.OverseerWars.DwellerComponent;
using LaboratoireProgrammation.Project.ViewModels.Laboratoires.OverseerWars.Registry;
using ColorHelper = LaboratoireProgrammation.Project.Helpers.ColorHelper;

namespace LaboratoireProgrammation.Project.ViewModels.Laboratoires.OverseerWars;

public partial class Wastelands : UserControl {
    // = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = =
    // [ - Constants - ]

    private const int WorldWidth = 250;
    private const int WorldHeight = 250;
    private const double BaseTileSize = 80.0;
    private const double MaxZoom = 24.0;
    private const double MinZoom = 0.1;
    private const double CardAspectRatio = 400.0 / 600.0;
    private const double CardHeightRatio = 0.40;
    private const int HoverDelayMs = 0;
    private const int MaxDwellersPerTile = 12;

    // = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = =
    // [ - Fields - ]

    private static Team? CurrentTeamView;
    private readonly List<Tile> _currentPath = new();

    private readonly List<Dweller> _deployedDwellers = new();
    private readonly Brush _discoveredBrush;

    private readonly Dictionary<Dweller, UIElement> _dwellerVisuals = new();
    private readonly Brush _enemyDwellerBrush;
    private readonly DispatcherTimer _gameLoopTimer = new();

    private readonly Pen _gridPen;
    private readonly DispatcherTimer _hoverTimer = new();
    private readonly Brush _movePathBrush;
    private readonly PlayerMapView _playerView;
    private readonly DwellerRegistry _registry;
    private readonly Brush _selectedBrush;
    private readonly Pen _selectedPen;
    private readonly Brush _selfDwellerBrush;
    private readonly Typeface _typeface = new("Segoe UI");
    private readonly Brush _undiscoveredBrush;

    private readonly WorldMap _worldMap;
    private UIElement? _activeHoverCard;

    private Vector _cameraPos;
    private Tile? _finalTile;
    private Dweller? _hoveredDweller;
    private Tile? _hoveredTile;
    private Point _hoverScreenPos;
    private bool _isDragging;
    private Point _lastMousePos;

    private Dweller? _selectedDweller;
    private Tile? _selectedTile;
    private double _zoom = 1.0;

    // = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = =
    // [ - Constructor - ]

    public Wastelands() {
        InitializeComponent();

        if (Visibility == Visibility.Hidden) return;

        _worldMap = new WorldMap(WorldWidth, WorldHeight);
        _playerView = new PlayerMapView(_worldMap);
        _registry = new DwellerRegistry();
        _registry.Initialize();

        _gridPen = MakePen(Brushes.Black, 1.0);
        _selectedPen = MakePen(Brushes.Yellow, 1.0);
        _discoveredBrush = MakeBrush(150, 150, 150);
        _undiscoveredBrush = MakeBrush(30, 30, 30);
        _selfDwellerBrush = MakeBrush(86, 166, 224);
        _enemyDwellerBrush = MakeBrush(168, 46, 53);
        _selectedBrush = MakeBrush(199, 162, 50);
        _movePathBrush = MakeBrush(100, 200, 0, 200);

        RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
        ClipToBounds = true;
        Focusable = true;

        _hoverTimer.Interval = TimeSpan.FromMilliseconds(HoverDelayMs);
        _hoverTimer.Tick += OnHoverTimerTick!;

        _gameLoopTimer.Interval = TimeSpan.FromMilliseconds(32);
        _gameLoopTimer.Tick += OnGameLoopTick!;

        IsVisibleChanged += (s, e) => {
            if (IsVisible) {
                if (!_gameLoopTimer.IsEnabled) _gameLoopTimer.Start();
            } else {
                _gameLoopTimer.Stop();
            }
        };

        Unloaded += (s, e) => {
            _gameLoopTimer.Stop();
        };

        InitTeamsAndDwellers();

        Loaded += (s, e) => {
            _playerView.OriginTile = _worldMap.World[0];
            EnforceConstraints();
            InvalidateVisual();
            RefreshDwellerVisuals();
            Focus();
            if (IsVisible && !_gameLoopTimer.IsEnabled) _gameLoopTimer.Start();
        };

        SizeChanged += (s, e) => {
            EnforceConstraints();
            InvalidateVisual();
            RefreshDwellerVisuals();
        };
    }

    private void InitTeamsAndDwellers() {
        var team = new Team(0, "Player");
        var team2 = new Team(0, "Enemy");
        CurrentTeamView = team;

        var all = _registry.GetAllDwellers();
        var garvey = all.FirstOrDefault(d => d.FirstName.Equals("Preston", StringComparison.OrdinalIgnoreCase));
        var butch = all.FirstOrDefault(d => d.FirstName.Equals("Butch", StringComparison.OrdinalIgnoreCase));
        var ghoul = all.FirstOrDefault(d => d.FirstName.Equals("Three", StringComparison.OrdinalIgnoreCase));

        garvey.CurrentState = DwellerState.Ally;
        butch.CurrentState = DwellerState.Ally;
        ghoul.CurrentState = DwellerState.Enemy;

        team.AddDweller(garvey);
        team.AddDweller(butch);
        team2.AddDweller(ghoul);

        var startTile = _worldMap.GetTile(0, 0);
        if (!_playerView.DiscoveredTiles.Contains(startTile)) _playerView.DiscoveredTiles.Add(startTile);

        MoveDwellerTo(butch, 0, 0);
        MoveDwellerTo(garvey, 0, 0);
        MoveDwellerTo(ghoul, 0, 2);

        _deployedDwellers.Add(garvey);
        _deployedDwellers.Add(butch);
        _deployedDwellers.Add(ghoul);
    }

    // = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = =
    // [ - Utils - ]

    private static Pen MakePen(Brush brush, double thickness) {
        var pen = new Pen(brush, thickness);
        pen.Freeze();
        return pen;
    }

    private static Brush MakeBrush(byte r, byte g, byte b, byte a = 255) {
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }

    // = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = =
    // [ - Public Methods - ]

    public void MoveDwellerTo(Dweller dweller, int x, int y) {
        dweller.Move(x, y);
        DiscoverTile(x, y);
        if (dweller.P > 3) DiscoverNeighbouringTiles(x, y, 1);
        if (dweller.P > 6) DiscoverNeighbouringTiles(x, y, 2);
        if (dweller.P > 8) DiscoverNeighbouringTiles(x, y, 3);
        if (dweller.P > 11) DiscoverNeighbouringTiles(x, y, 4);
        RefreshDwellerVisuals();
        InvalidateVisual();
    }

    public void DiscoverTile(int x, int y) {
        var tile = _worldMap.GetTile(x, y);
        if (!_playerView.DiscoveredTiles.Contains(tile)) _playerView.DiscoveredTiles.Add(tile);
        RefreshDwellerVisuals();
        InvalidateVisual();
    }

    public void DiscoverNeighbouringTiles(int x, int y, int radius) {
        for (var i = -radius; i <= radius; i++)
        for (var j = -radius; j <= radius; j++)
            if (_worldMap.IsTileInBounds(x + i, y + j))
                DiscoverTile(x + i, y + j);
    }

    public void ShowMovePath(Dweller dweller) {
        _currentPath.Clear();

        if (_hoveredTile == null) return;

        int cx = dweller.X, cy = dweller.Y;
        int tx = _hoveredTile.X, ty = _hoveredTile.Y;

        if (cx == tx && cy == ty) return;

        var pa = dweller.IndividuelActionPoints;

        while (pa-- > 0) {
            if (cx == tx && cy == ty) break;

            if (cx < tx) cx++;
            else if (cx > tx) cx--;
            else if (cy < ty) cy++;
            else cy--;

            if (!_worldMap.IsTileInBounds(cx, cy)) continue;
            var next = _worldMap.GetTile(cx, cy);
            _finalTile = next;
            _currentPath.Add(next);
        }
    }

    // = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = =
    // [ - Visuals - ]

    private void RefreshDwellerVisuals() {
        var toRemove = _dwellerVisuals.Keys.Where(d => !_deployedDwellers.Contains(d)).ToList();
        foreach (var d in toRemove) {
            RootGrid.Children.Remove(_dwellerVisuals[d]);
            _dwellerVisuals.Remove(d);
        }

        var byTile = new Dictionary<string, List<Dweller>>();
        foreach (var d in _deployedDwellers) {
            var key = $"{d.X},{d.Y}";
            if (!byTile.ContainsKey(key)) byTile[key] = new List<Dweller>();
            byTile[key].Add(d);
        }

        foreach (var group in byTile.Values) {
            var countOnTile = Math.Min(group.Count, MaxDwellersPerTile);
            var cols = countOnTile > 3 ? 3 : 2;

            for (var i = 0; i < group.Count; i++) {
                var dweller = group[i];
                var tile = _worldMap.GetTile(dweller.X, dweller.Y);

                var discovered = _playerView.DiscoveredTiles.Contains(tile);
                if (!discovered) {
                    if (_dwellerVisuals.ContainsKey(dweller)) {
                        RootGrid.Children.Remove(_dwellerVisuals[dweller]);
                        _dwellerVisuals.Remove(dweller);
                    }

                    continue;
                }

                if (!_dwellerVisuals.ContainsKey(dweller)) RegisterDwellerVisual(dweller);

                var visual = _dwellerVisuals[dweller];
                var tileSize = BaseTileSize * _zoom;
                var bx = (dweller.X * BaseTileSize - _cameraPos.X) * _zoom;
                var by = (dweller.Y * BaseTileSize - _cameraPos.Y) * _zoom;

                double fx = bx, fy = by, fs = tileSize;

                if (countOnTile > 1 && i < MaxDwellersPerTile) {
                    var cell = tileSize / cols;
                    fx = bx + i % cols * cell;
                    fy = by + i / cols * cell;
                    fs = cell;
                }

                var fw = (FrameworkElement)visual;
                fw.Width = fs - 4;
                fw.Height = fs - 4;

                visual.Visibility = i < MaxDwellersPerTile ? Visibility.Visible : Visibility.Collapsed;
                visual.RenderTransform = new TranslateTransform(fx, fy);
            }
        }
    }

    private void RegisterDwellerVisual(Dweller dweller) {
        var visual = dweller.CreateCharacterVisual();
        var fw = (FrameworkElement)visual;
        fw.HorizontalAlignment = HorizontalAlignment.Left;
        fw.VerticalAlignment = VerticalAlignment.Top;
        Panel.SetZIndex(visual, 120);

        var captured = dweller;

        visual.MouseEnter += (s, e) => {
            _hoveredDweller = captured;
            _hoverScreenPos = e.GetPosition(RootGrid);
            _hoverTimer.Stop();
            _hoverTimer.Start();
        };
        visual.MouseLeave += (s, e) => {
            _hoverTimer.Stop();
            _hoveredDweller = null;
            RemoveHoverCard();
        };
        visual.MouseMove += (s, e) => { _hoverScreenPos = e.GetPosition(RootGrid); };
        visual.MouseDown += (s, e) => {
            if (dweller.Team != CurrentTeamView) return;

            // Reset the old selection's state and visual
            if (_selectedDweller != null && _selectedDweller != dweller) {
                _selectedDweller.CurrentState =
                    _selectedDweller.Team == CurrentTeamView ? DwellerState.Ally : DwellerState.Enemy;
                if (_dwellerVisuals.TryGetValue(_selectedDweller, out var oldVisual)) {
                    RootGrid.Children.Remove(oldVisual);
                    _dwellerVisuals.Remove(_selectedDweller);
                }
            }

            // Update the new selected dweller
            dweller.CurrentState = DwellerState.Selected;
            _selectedDweller = dweller;
            _selectedTile = _worldMap.GetTile(dweller.X, dweller.Y);

            // Remove its current visual so RefreshDwellerVisuals recreates it with White outline
            RootGrid.Children.Remove(visual);
            _dwellerVisuals.Remove(dweller);

            RefreshDwellerVisuals();
            InvalidateVisual();
            e.Handled = true;
        };

        RootGrid.Children.Add(visual);
        _dwellerVisuals.Add(dweller, visual);
    }

    private void OnHoverTimerTick(object sender, EventArgs e) {
        _hoverTimer.Stop();
        RemoveHoverCard();

        if (_hoveredDweller == null) return;

        var card = _hoveredDweller.CreateCardVisual();
        var h = ActualHeight * CardHeightRatio;
        var w = h * CardAspectRatio;

        var fw = (FrameworkElement)card;
        fw.HorizontalAlignment = HorizontalAlignment.Left;
        fw.VerticalAlignment = VerticalAlignment.Top;
        fw.Width = w;
        fw.Height = h;

        card.RenderTransform = new TranslateTransform(_hoverScreenPos.X + 20, _hoverScreenPos.Y + 20);
        Panel.SetZIndex(card, 300);

        _activeHoverCard = card;
        RootGrid.Children.Add(card);
    }

    private void RemoveHoverCard() {
        RootGrid.Children.Remove(_activeHoverCard);
        _activeHoverCard = null;
    }

    // = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = =
    // [ - Camera - ]

    private void EnforceConstraints() {
        if (ActualWidth == 0 || ActualHeight == 0) return;

        var mapW = WorldWidth * BaseTileSize;
        var mapH = WorldHeight * BaseTileSize;

        var minZoom = Math.Max(ActualWidth / mapW, ActualHeight / mapH);
        _zoom = Math.Clamp(_zoom, Math.Max(minZoom, MinZoom), MaxZoom);

        var viewW = ActualWidth / _zoom;
        var viewH = ActualHeight / _zoom;

        _cameraPos.X = Math.Clamp(_cameraPos.X, 0, Math.Max(0, mapW - viewW));
        _cameraPos.Y = Math.Clamp(_cameraPos.Y, 0, Math.Max(0, mapH - viewH));
    }

    // = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = =
    // [ - Input - ]

    protected override void OnMouseDown(MouseButtonEventArgs e) {
        Focus();

        if (e.ChangedButton == MouseButton.Middle || e.ChangedButton == MouseButton.Right) {
            _isDragging = true;
            _lastMousePos = e.GetPosition(this);
            CaptureMouse();
        }
        else if (e.ChangedButton == MouseButton.Left) {
            if (_selectedDweller != null) {
                if (_finalTile == _hoveredTile && _finalTile != null)
                    MoveDwellerTo(_selectedDweller, _finalTile.X, _finalTile.Y);

                // Force visual reset when clicking anywhere on the map
                _selectedDweller.CurrentState =
                    _selectedDweller.Team == CurrentTeamView ? DwellerState.Ally : DwellerState.Enemy;
                if (_dwellerVisuals.TryGetValue(_selectedDweller, out var oldVisual)) {
                    RootGrid.Children.Remove(oldVisual);
                    _dwellerVisuals.Remove(_selectedDweller);
                }

                _selectedDweller = null;
                _selectedTile = null;
                _currentPath.Clear();
            }

            HandleClick(e.GetPosition(this));
        }

        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e) {
        var pos = e.GetPosition(this);

        if (_isDragging) {
            var delta = _lastMousePos - pos;
            _cameraPos.X += delta.X / _zoom;
            _cameraPos.Y += delta.Y / _zoom;
            _lastMousePos = pos;
            EnforceConstraints();
            InvalidateVisual();
            RefreshDwellerVisuals();
        }

        var tx = (int)Math.Floor((_cameraPos.X + pos.X / _zoom) / BaseTileSize);
        var ty = (int)Math.Floor((_cameraPos.Y + pos.Y / _zoom) / BaseTileSize);
        _hoveredTile = _worldMap.GetTile(tx, ty);

        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseButtonEventArgs e) {
        if (_isDragging) {
            _isDragging = false;
            ReleaseMouseCapture();
        }

        base.OnMouseUp(e);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e) {
        var mouse = e.GetPosition(this);
        var factor = e.Delta > 0 ? 1.2 : 0.8;
        var oldZoom = _zoom;

        _zoom = Math.Clamp(_zoom * factor, MinZoom, MaxZoom);

        if (Math.Abs(_zoom - oldZoom) > 0.0001) {
            var worldMouse = (Vector)mouse / oldZoom + _cameraPos;
            _cameraPos = worldMouse - (Vector)mouse / _zoom;
            EnforceConstraints();
            InvalidateVisual();
            RefreshDwellerVisuals();
        }
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e) {
        var step = BaseTileSize / _zoom;
        var handled = true;

        if (e.Key == Key.W || e.Key == Key.Up) {
            _cameraPos.Y -= step;
        }
        else if (e.Key == Key.S || e.Key == Key.Down) {
            _cameraPos.Y += step;
        }
        else if (e.Key == Key.A || e.Key == Key.Left) {
            _cameraPos.X -= step;
        }
        else if (e.Key == Key.D || e.Key == Key.Right) {
            _cameraPos.X += step;
        }
        else if (e.Key == Key.Escape) {
            if (_selectedDweller != null) {
                _selectedDweller.CurrentState =
                    _selectedDweller.Team == CurrentTeamView ? DwellerState.Ally : DwellerState.Enemy;
                if (_dwellerVisuals.TryGetValue(_selectedDweller, out var v)) {
                    RootGrid.Children.Remove(v);
                    _dwellerVisuals.Remove(_selectedDweller);
                }
            }

            _selectedDweller = null;
            _selectedTile = null;
            _currentPath.Clear();
        }
        else {
            handled = false;
        }

        if (handled) {
            EnforceConstraints();
            InvalidateVisual();
            RefreshDwellerVisuals();
            e.Handled = true;
        }

        base.OnPreviewKeyDown(e);
    }

    private void HandleClick(Point screenPos) {
        var x = (int)Math.Floor((_cameraPos.X + screenPos.X / _zoom) / BaseTileSize);
        var y = (int)Math.Floor((_cameraPos.Y + screenPos.Y / _zoom) / BaseTileSize);

        if (x < 0 || x >= WorldWidth || y < 0 || y >= WorldHeight) return;

        _selectedTile = _worldMap.GetTile(x, y);
        InvalidateVisual();
        RefreshDwellerVisuals();
    }

    // = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = =
    // [ - Render - ]

    protected override void OnRender(DrawingContext dc) {
        dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, ActualWidth, ActualHeight));

        if (ActualWidth == 0 || ActualHeight == 0) return;

        var tileSize = BaseTileSize * _zoom;
        var startX = (int)Math.Floor(_cameraPos.X / BaseTileSize);
        var startY = (int)Math.Floor(_cameraPos.Y / BaseTileSize);
        var cols = (int)Math.Ceiling(ActualWidth / tileSize) + 1;
        var rows = (int)Math.Ceiling(ActualHeight / tileSize) + 1;
        var offX = _cameraPos.X % BaseTileSize * _zoom;
        var offY = _cameraPos.Y % BaseTileSize * _zoom;

        for (var y = 0; y <= rows; y++)
        for (var x = 0; x <= cols; x++) {
            int tx = startX + x, ty = startY + y;
            if (tx < 0 || tx >= WorldWidth || ty < 0 || ty >= WorldHeight) continue;

            var dx = Math.Floor(x * tileSize - offX);
            var dy = Math.Floor(y * tileSize - offY);
            var rect = new Rect(dx, dy, Math.Ceiling(tileSize), Math.Ceiling(tileSize));
            var tile = _worldMap.GetTile(tx, ty);

            var discovered = _playerView.DiscoveredTiles.Contains(tile);
            var isSelected = tile == _selectedTile;
            var isPath = _currentPath.Contains(tile);

            var brush = discovered ? _discoveredBrush : _undiscoveredBrush;
            if (isSelected) brush = _selectedBrush;
            if (isPath) brush = _movePathBrush;
            if (_hoveredTile == tile) brush = ColorHelper.GetLighter(brush, 10);

            dc.DrawRectangle(brush, isSelected ? _selectedPen : _gridPen, rect);

            if (tileSize > 25 && discovered) {
                var text = new FormattedText(
                    $"{tx},{ty}",
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    _typeface,
                    tileSize * 0.1,
                    Brushes.White,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.DrawText(text, new Point(dx + 2, dy + 2));
            }
        }
    }

    // = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = =
    // [ - Game Loop - ]

    private void OnGameLoopTick(object sender, EventArgs e) {
        if (_selectedDweller != null && _hoveredTile != null) ShowMovePath(_selectedDweller);
        InvalidateVisual();
        RefreshDwellerVisuals();
    }
}