using LaboratoireProgrammation.Project.ModernOverseerWars.Models;
using LaboratoireProgrammation.Project.ModernOverseerWars.Models.Entities;
using LaboratoireProgrammation.Project.ModernOverseerWars.Models.Enums;
using LaboratoireProgrammation.Project.ModernOverseerWars.Models.Interfaces;
using LaboratoireProgrammation.Project.ModernOverseerWars.Models.Map;
using LaboratoireProgrammation.Project.ModernOverseerWars.Models.Data;
using LaboratoireProgrammation.Project.ModernOverseerWars.Models.Data.Json;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace LaboratoireProgrammation.Project.ModernOverseerWars.Views;

public partial class ControlCard : UserControl {

    public static readonly DependencyProperty CardImageProperty =
        DependencyProperty.Register(nameof(CardImage), typeof(string), typeof(ControlCard), new PropertyMetadata("Card"));
    public static readonly DependencyProperty PowerProperty =
        DependencyProperty.Register(nameof(Power), typeof(int), typeof(ControlCard), new PropertyMetadata(0));

    public string CardImage {
        get => (string)GetValue(CardImageProperty);
        set => SetValue(CardImageProperty, value);
    }
    public int Power {
        get => (int)GetValue(PowerProperty);
        set => SetValue(PowerProperty, value);
    }

    public Dweller?  BoundDweller { get; private set; }
    public IWeapon?  BoundWeapon  { get; private set; }
    public IOutfit?  BoundOutfit  { get; private set; }
    public Dweller?  OwnerDweller { get; set; }

    private ControlCard? _weaponCard;
    private ControlCard? _outfitCard;

    private static readonly string[] RarityNames = { "common","uncommon","rare","epic","legendary" };
    private Point _dragStartPoint;

    public static ControlCard? DraggedCard { get; private set; }
    private Point _dragStartMousePos;
    private TranslateTransform _translateTransform = new();
    private bool _isDragging;

    public static double GlowSubtleness {
        get => GameConfigRepository.Config.CardGlowSubtleness;
        set { }
    }
    public static double ShakeIntensity {
        get => GameConfigRepository.Config.CardShakeIntensity;
        set { }
    }
    public static readonly HashSet<Dweller> DwellersToShakeOnLoad = new();

    private bool _isSelected;
    public bool IsSelected {
        get => _isSelected;
        set {
            if (_isSelected != value) {
                _isSelected = value;
                UpdateGlowEffect();
            }
        }
    }

    public ControlCard() {
        InitializeComponent();
        Unloaded += (s, e) => {
            if (BoundDweller != null) {
                BoundDweller.Damaged -= OnDwellerDamaged;
            }
        };
    }

    public void BindDweller(Dweller d) {
        if (BoundDweller != null) {
            BoundDweller.Damaged -= OnDwellerDamaged;
        }
        BoundDweller = d;
        d.Damaged += OnDwellerDamaged;

        CardImage    = d.Name;
        SupervisorBadge.Visibility = d.IsSupervisor ? Visibility.Visible : Visibility.Collapsed;
        StatsLine.Text = $"S:{d.Special_S} P:{d.Special_P} E:{d.Special_E} C:{d.Special_C}";
        HpBar.Maximum  = d.MaxHp;
        RefreshHp();
        UpdateGlowEffect();
        ApplyRarity(d.Rarity);
        ImgDweller.Source = MakeImageSource($"../../Assets/images/overseerWars/dwellers/{d.Texture}") ?? MakeImageSource("../../Assets/images/overseerWars/dwellers/dweller.png");

        if (DwellersToShakeOnLoad.Contains(d)) {
            DwellersToShakeOnLoad.Remove(d);
            Dispatcher.InvokeAsync(() => TriggerShake());
        }


    }

    public void BindWeapon(IWeapon w) {
        BoundWeapon  = w;
        CardImage    = w.Name;
        StatsLine.Text = $"DMG:{w.Damage} ({w.WeaponType[..Math.Min(3, w.WeaponType.Length)]})";
        HpBar.Visibility = Visibility.Collapsed;
        HpText.Visibility = Visibility.Collapsed;
        ApplyRarity(w.Rarity);
        string tex = w is Weapon wp ? wp.Texture : "placeholder.png";
        ImgDweller.Source = MakeImageSource($"../../Assets/images/overseerWars/weapons/{tex}") ?? MakeImageSource("../../Assets/images/overseerWars/weapons/placeholder.png");
    }

    public void BindOutfit(IOutfit o) {
        BoundOutfit  = o;
        CardImage    = o.Name;
        StatsLine.Text = $"ARM:{o.ArmorValue}";
        HpBar.Visibility  = Visibility.Collapsed;
        HpText.Visibility = Visibility.Collapsed;
        ApplyRarity(o.Rarity);
        string tex = o is Outfit op ? op.Texture : "placeholder.png";
        ImgDweller.Source = MakeImageSource($"../../Assets/images/overseerWars/outfits/{tex}") ?? MakeImageSource("../../Assets/images/overseerWars/outfits/placeholder.png");
    }

    public Scrap? BoundScrap { get; private set; }
    public void BindScrap(Scrap s) {
        BoundScrap = s;
        CardImage = s.Name;
        StatsLine.Text = "Junk item";
        HpBar.Visibility = Visibility.Collapsed;
        HpText.Visibility = Visibility.Collapsed;
        ApplyRarity(s.Rarity);
        ImgDweller.Source = MakeImageSource($"../../Assets/images/overseerWars/scraps/{s.Texture}") ?? MakeImageSource("../../Assets/images/overseerWars/weapons/placeholder.png");
    }

    private void ApplyRarity(CardRarity rarity) {
        string r = RarityNames[(int)rarity];
        string base_ = "../../Assets/images/overseerWars/cards/";
        ImgBorder.Source = MakeImageSource($"{base_}card_border_{r}.png");
        ImgColor.Source  = MakeImageSource($"{base_}card_color_{r}.png");
        ImgTop.Source    = MakeImageSource($"{base_}card_top_{r}.png");

        if (HolofoilOverlay == null) return;

        if (rarity == CardRarity.Epic || rarity == CardRarity.Legendary) {
            HolofoilOverlay.Visibility = Visibility.Visible;
            
            var brush = new LinearGradientBrush {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                MappingMode = BrushMappingMode.RelativeToBoundingBox
            };
            
            var transform = new TranslateTransform(-1.5, -1.5);
            brush.Transform = transform;
            
            if (rarity == CardRarity.Epic) {
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 0));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 0.35));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(0x37, 0x88, 0xC0, 0xFF), 0.45));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(0x5C, 0xE0, 0xE0, 0xFF), 0.5));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(0x37, 0x88, 0x80, 0xFF), 0.55));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 0.65));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1.0));
            } else {
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 0));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 0.3));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(0x2A, 0xFF, 0x80, 0x80), 0.4));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(0x3C, 0xFF, 0xFF, 0x80), 0.45));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(0x4C, 0xFF, 0x80, 0xFF), 0.5));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(0x3C, 0xFF, 0xFF, 0x80), 0.55));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(0x2A, 0xFF, 0x80, 0x80), 0.6));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 0.7));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1.0));
            }
            
            HolofoilOverlay.Background = brush;
            
            var sweepAnim = new DoubleAnimation {
                From = -1.5,
                To = 1.5,
                Duration = TimeSpan.FromSeconds(GameConfigRepository.Config.CardHoloSweepSeconds),
                RepeatBehavior = RepeatBehavior.Forever
            };
            
            transform.BeginAnimation(TranslateTransform.XProperty, sweepAnim);
            transform.BeginAnimation(TranslateTransform.YProperty, sweepAnim);
        } else {
            HolofoilOverlay.Visibility = Visibility.Collapsed;
            if (HolofoilOverlay.Background is LinearGradientBrush lgb && lgb.Transform is TranslateTransform tt) {
                tt.BeginAnimation(TranslateTransform.XProperty, null);
                tt.BeginAnimation(TranslateTransform.YProperty, null);
            }
        }
    }
    
    private ImageSource MakeImageSource(string relativePath) {
        try {
            string cleaned = relativePath.Replace("../", "").Replace("..\\", "");
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string absPath = "";
            while (!string.IsNullOrEmpty(baseDir)) {
                string test = Path.Combine(baseDir, cleaned);
                if (File.Exists(test)) { absPath = test; break; }
                baseDir = Path.GetDirectoryName(baseDir)!;
            }
            if (string.IsNullOrEmpty(absPath)) {
                absPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, cleaned);
            }
            if (File.Exists(absPath)) {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(absPath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                return bitmap;
            }
            return null!;
        }
        catch {
            return null!;
        }
    }

    private bool _isDeckMode;
    public void SetDeckMode(bool deckMode) {
        _isDeckMode = deckMode;
        if (deckMode) {
            LayoutTransform = new ScaleTransform(0.9, 0.9);
            RenderTransformOrigin = new Point(0.5, 0.5);


        }
    }

    private void OnMouseEnter(object s, MouseEventArgs e) {
        Panel.SetZIndex(this, 1000);
        RenderTransformOrigin = new Point(0.5, 0.5);
        var scale = new ScaleTransform(1.06, 1.06);
        RenderTransform = scale;
    }

    private void OnMouseLeave(object s, MouseEventArgs e) {
        Panel.SetZIndex(this, 0);
        var scale = new ScaleTransform(1.0, 1.0);
        RenderTransform = scale;

        if (TiltSkew != null && TiltRotate != null) {
            var animX = new DoubleAnimation(0, TimeSpan.FromSeconds(0.5)) {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            var animY = new DoubleAnimation(0, TimeSpan.FromSeconds(0.5)) {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            var animRot = new DoubleAnimation(0, TimeSpan.FromSeconds(0.5)) {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            TiltSkew.BeginAnimation(SkewTransform.AngleXProperty, animX);
            TiltSkew.BeginAnimation(SkewTransform.AngleYProperty, animY);
            TiltRotate.BeginAnimation(RotateTransform.AngleProperty, animRot);
        }
    }

    private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
        if (Name == "SheetDwellerCard") return;
        _dragStartPoint = e.GetPosition(null);
        OverseerWarsWindow.heldCard = this;
    }

    private void OnPreviewMouseMove(object sender, MouseEventArgs e) {
        if (e.LeftButton == MouseButtonState.Pressed && !_isDragging && OverseerWarsWindow.heldCard == this) {
            Point pos = e.GetPosition(null);
            if (Math.Abs(pos.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(pos.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance) {
                var win = Window.GetWindow(this) as IOverseerGameWindow;
                if (win != null && !win.CanInteract) return;
                _isDragging = true;
                DraggedCard = this;
                if (win != null && win is Window w) {
                    Point winPos = e.GetPosition(w);
                    win.StartDraggingCard(this, winPos);
                }
                CaptureMouse();
            }
        }

        if (_isDragging && IsMouseCaptured) {
            var win = Window.GetWindow(this) as IOverseerGameWindow;
            if (win != null && win is Window w) {
                Point winPos = e.GetPosition(w);
                win.MoveDraggingCard(this, winPos);
            }
        }
    }

    private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
        if (_isDragging) {
            _isDragging = false;
            DraggedCard = null;
            ReleaseMouseCapture();
            var win = Window.GetWindow(this) as IOverseerGameWindow;
            if (win != null && win is Window w) {
                Point winPos = e.GetPosition(w);
                win.StopDraggingCard(this, winPos);
            }
        }
    }

    private void OnRightClick(object sender, MouseButtonEventArgs e) {
        if (BoundDweller == null) return;
        e.Handled = true;
        OpenContextMenu();
    }

    public void RefreshHp() {
        if (BoundDweller == null) return;
        HpBar.Value = BoundDweller.CurrentHp;
        HpText.Text = $"{BoundDweller.CurrentHp}/{BoundDweller.MaxHp}";
        
        double ratio = (double)BoundDweller.CurrentHp / BoundDweller.MaxHp;
        
        // Use RobCo Terminal Colors for Health
        HpBar.Foreground = ratio > 0.5
            ? new SolidColorBrush(Color.FromRgb(27, 253, 2))  // Terminal Green
            : new SolidColorBrush(Color.FromRgb(255, 51, 51)); // Terminal Red
    }

    public void OpenContextMenu() {
        if (BoundDweller == null) return;
        var win = Window.GetWindow(this) as IOverseerGameWindow ?? System.Windows.Application.Current.Windows.OfType<IOverseerGameWindow>().FirstOrDefault();
        if (win == null || !win.CanInteract) return;

        Color fgColor = win.GetThemeColor();
        Color bgColor = win.GetThemeBgDark();
        var fgBrush = new SolidColorBrush(fgColor);
        var bgBrush = new SolidColorBrush(bgColor);

        var cm = new ContextMenu {
            Style = (Style)FindResource("ThemedContextMenu")
        };
        cm.Resources["ThemeFgBrush"] = fgBrush;
        cm.Resources["ThemeBgBrush"] = bgBrush;

        var miSheet = new MenuItem {
            Header = "> VIEW PERSONNEL DATA",
            Style = (Style)FindResource("ThemedMenuItem")
        };
        miSheet.Click += (s, ev) => win.OpenDwellerSheet(BoundDweller);
        cm.Items.Add(miSheet);
        cm.Items.Add(new Separator());

        var vault = win.ActiveVault;
        var rc = new[] { "#969696", "#64C864", "#6496FA", "#B450DC", "#FFB400" };

        var miWeapon = new MenuItem {
            Header = "> ASSIGN WEAPON",
            Style = (Style)FindResource("ThemedMenuItem")
        };

        var equippedWeapon = BoundDweller.EquippedWeapon;
        var unusedWeapons = vault.UnusedWeapons.ToList();

        if (equippedWeapon != null) {
            var nameStack = new StackPanel { Orientation = Orientation.Horizontal };
            nameStack.Children.Add(new Border {
                Background = (Brush)new BrushConverter().ConvertFromString(rc[(int)equippedWeapon.Rarity])!,
                Width = 8, Height = 8, Margin = new Thickness(0, 0, 8, 0) // Squared off
            });
            nameStack.Children.Add(new TextBlock { Text = $"{equippedWeapon.Name} (EQUIPPED)", FontWeight = FontWeights.Bold });
            nameStack.Children.Add(new TextBlock { Text = $"[DMG:{equippedWeapon.Damage}]", Opacity = 0.7, FontSize = 10, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });

            var miEquipped = new MenuItem {
                Header = nameStack,
                IsChecked = true,
                Style = (Style)FindResource("ThemedMenuItem")
            };
            miEquipped.Click += (s, ev) => {
                win.RequestUnequip(BoundDweller, true);
            };
            miWeapon.Items.Add(miEquipped);
            miWeapon.Items.Add(new Separator());
        }

        if (unusedWeapons.Count == 0 && equippedWeapon == null) {
            var miNoWeapons = new MenuItem {
                Header = "NO INVENTORY AVAILABLE",
                IsEnabled = false,
                Style = (Style)FindResource("ThemedMenuItem")
            };
            miWeapon.Items.Add(miNoWeapons);
        } else {
            foreach (var weapon in unusedWeapons) {
                var nameStack = new StackPanel { Orientation = Orientation.Horizontal };
                nameStack.Children.Add(new Border {
                    Background = (Brush)new BrushConverter().ConvertFromString(rc[(int)weapon.Rarity])!,
                    Width = 8, Height = 8, Margin = new Thickness(0, 0, 8, 0) // Squared off
                });
                nameStack.Children.Add(new TextBlock { Text = weapon.Name.ToUpper(), FontWeight = FontWeights.Bold });
                nameStack.Children.Add(new TextBlock { Text = $"[DMG:{weapon.Damage}]", Opacity=0.7, FontSize = 10, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });

                var miW = new MenuItem {
                    Header = nameStack,
                    Style = (Style)FindResource("ThemedMenuItem")
                };
                miW.Click += (s, ev) => {
                    win.RequestEquip(BoundDweller, weapon, true);
                };
                miWeapon.Items.Add(miW);
            }
        }

        var miOutfit = new MenuItem {
            Header = "> ASSIGN OUTFIT",
            Style = (Style)FindResource("ThemedMenuItem")
        };

        var equippedOutfit = BoundDweller.EquippedOutfit;
        var unusedOutfits = vault.UnusedOutfits.ToList();

        if (equippedOutfit != null) {
            var nameStack = new StackPanel { Orientation = Orientation.Horizontal };
            nameStack.Children.Add(new Border {
                Background = (Brush)new BrushConverter().ConvertFromString(rc[(int)equippedOutfit.Rarity])!,
                Width = 8, Height = 8, Margin = new Thickness(0, 0, 8, 0)
            });
            nameStack.Children.Add(new TextBlock { Text = $"{equippedOutfit.Name} (EQUIPPED)", FontWeight = FontWeights.Bold });
            
            string statsStr = $"[ARM:{equippedOutfit.ArmorValue}]";
            if (equippedOutfit is Outfit op) statsStr += $" (S:{op.S} P:{op.P} E:{op.E} L:{op.L})";
            
            nameStack.Children.Add(new TextBlock { Text = statsStr, Opacity = 0.7, FontSize = 10, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });

            var miEquipped = new MenuItem {
                Header = nameStack,
                IsChecked = true,
                Style = (Style)FindResource("ThemedMenuItem")
            };
            miEquipped.Click += (s, ev) => {
                win.RequestUnequip(BoundDweller, false);
            };
            miOutfit.Items.Add(miEquipped);
            miOutfit.Items.Add(new Separator());
        }

        if (unusedOutfits.Count == 0 && equippedOutfit == null) {
            var miNoOutfits = new MenuItem {
                Header = "NO INVENTORY AVAILABLE",
                IsEnabled = false,
                Style = (Style)FindResource("ThemedMenuItem")
            };
            miOutfit.Items.Add(miNoOutfits);
        } else {
            foreach (var outfit in unusedOutfits) {
                var nameStack = new StackPanel { Orientation = Orientation.Horizontal };
                nameStack.Children.Add(new Border {
                    Background = (Brush)new BrushConverter().ConvertFromString(rc[(int)outfit.Rarity])!,
                    Width = 8, Height = 8, Margin = new Thickness(0, 0, 8, 0)
                });
                nameStack.Children.Add(new TextBlock { Text = outfit.Name.ToUpper(), FontWeight = FontWeights.Bold });
                
                string statsStr = $"[ARM:{outfit.ArmorValue}]";
                if (outfit is Outfit op) statsStr += $" (S:{op.S} P:{op.P} E:{op.E} L:{op.L})";
                
                nameStack.Children.Add(new TextBlock { Text = statsStr, Opacity = 0.7, FontSize = 10, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });

                var miO = new MenuItem {
                    Header = nameStack,
                    Style = (Style)FindResource("ThemedMenuItem")
                };
                miO.Click += (s, ev) => {
                    win.RequestEquip(BoundDweller, outfit, false);
                };
                miOutfit.Items.Add(miO);
            }
        }

        cm.Items.Add(miWeapon);
        cm.Items.Add(miOutfit);
        cm.IsOpen = true;
    }

    private void OnDwellerDamaged(int damageAmount) {
        Dispatcher.InvokeAsync(() => {
            RefreshHp();
            UpdateGlowEffect();
            TriggerShake();
        });
    }

    public void UpdateGlowEffect() {
        if (CardGlow == null) return;

        Color targetColor = Colors.Transparent;
        bool shouldGlow = false;

        if (IsSelected) {
            targetColor = Color.FromRgb(0x33, 0xFF, 0x33); // Green for selected
            shouldGlow = true;
        } else if (BoundDweller != null) {
            if (BoundDweller.IsSupervisor) {
                targetColor = Color.FromRgb(0xFF, 0xD7, 0x00); // Golden for supervisor
                shouldGlow = true;
            } else if (BoundDweller.CurrentHp < BoundDweller.MaxHp * 0.5) {
                targetColor = Color.FromRgb(0xFF, 0x33, 0x33); // Red for low health/injured
                shouldGlow = true;
            }
        }

        if (shouldGlow) {
            CardGlow.Color = targetColor;
            CardGlow.BlurRadius = 12;
            
            var opacityAnim = new DoubleAnimation {
                From = 0.3 * GlowSubtleness,
                To = 0.9 * GlowSubtleness,
                Duration = TimeSpan.FromSeconds(1.2),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };

            CardGlow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty, opacityAnim);
        } else {
            CardGlow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty, null);
            CardGlow.BlurRadius = 0;
            CardGlow.Opacity = 0;
            CardGlow.Color = Colors.Transparent;
        }
    }

    public void TriggerShake() {
        if (TremorTransform == null) return;
        
        double offset = 8.0 * ShakeIntensity;
        
        var shakeAnim = new DoubleAnimationUsingKeyFrames {
            Duration = TimeSpan.FromMilliseconds(400)
        };
        
        shakeAnim.KeyFrames.Add(new LinearDoubleKeyFrame(-offset, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(50))));
        shakeAnim.KeyFrames.Add(new LinearDoubleKeyFrame(offset, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(100))));
        shakeAnim.KeyFrames.Add(new LinearDoubleKeyFrame(-offset * 0.7, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150))));
        shakeAnim.KeyFrames.Add(new LinearDoubleKeyFrame(offset * 0.7, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(200))));
        shakeAnim.KeyFrames.Add(new LinearDoubleKeyFrame(-offset * 0.4, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(250))));
        shakeAnim.KeyFrames.Add(new LinearDoubleKeyFrame(offset * 0.4, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(300))));
        shakeAnim.KeyFrames.Add(new LinearDoubleKeyFrame(-offset * 0.2, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(350))));
        shakeAnim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(400))));
        
        TremorTransform.BeginAnimation(TranslateTransform.XProperty, shakeAnim);
    }

    private void OnMouseMove(object sender, MouseEventArgs e) {
        if (_isDragging) return;
        if (TiltSkew == null || TiltRotate == null) return;

        Point localPos = e.GetPosition(MainCardGrid);
        double w = MainCardGrid.ActualWidth > 0 ? MainCardGrid.ActualWidth : 140;
        double h = MainCardGrid.ActualHeight > 0 ? MainCardGrid.ActualHeight : 200;
        double nx = (localPos.X - w / 2) / (w / 2);
        double ny = (localPos.Y - h / 2) / (h / 2);

        nx = Math.Max(-1, Math.Min(1, nx));
        ny = Math.Max(-1, Math.Min(1, ny));

        double targetAngleX = -ny * GameConfigRepository.Config.CardTiltAngleX;
        double targetAngleY = nx * GameConfigRepository.Config.CardTiltAngleY;
        double targetAngle = nx * ny * GameConfigRepository.Config.CardTiltAngleRot;

        var animX = new DoubleAnimation(targetAngleX, TimeSpan.FromMilliseconds(80));
        var animY = new DoubleAnimation(targetAngleY, TimeSpan.FromMilliseconds(80));
        var animRot = new DoubleAnimation(targetAngle, TimeSpan.FromMilliseconds(80));

        TiltSkew.BeginAnimation(SkewTransform.AngleXProperty, animX);
        TiltSkew.BeginAnimation(SkewTransform.AngleYProperty, animY);
        TiltRotate.BeginAnimation(RotateTransform.AngleProperty, animRot);
    }
}
