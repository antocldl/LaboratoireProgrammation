using LaboratoireProgrammation.Project.ModernOverseerWars.Models;
using LaboratoireProgrammation.Project.ModernOverseerWars.Models.Entities;
using LaboratoireProgrammation.Project.ModernOverseerWars.Models.Enums;
using LaboratoireProgrammation.Project.ModernOverseerWars.Models.Interfaces;
using LaboratoireProgrammation.Project.ModernOverseerWars.Models.Map;
using LaboratoireProgrammation.Project.ModernOverseerWars.Models.Data;
using LaboratoireProgrammation.Project.ModernOverseerWars.Models.Data.Json;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace LaboratoireProgrammation.Project.ModernOverseerWars.Views;

public partial class HexTileControl : UserControl {

    public HexTile? BoundTile  { get; private set; }
    public bool     IsSelected { get; private set; }
    public bool     IsHovered  { get; private set; }

    public static Color ActivePlayerColor { get; set; } = Color.FromRgb(0x41, 0x52, 0x1F);

    private static readonly Color FogColor      = Color.FromRgb(5, 4, 5);
    private static readonly Color ExploredColor = Color.FromRgb(17, 17, 17);

    public event Action<HexTileControl, ControlCard>? CardDropped;

    private bool _lastIsP1 = true;
    private ControlCard? _wastelandHoverCard;

    public ControlCard WastelandHoverCard {
        get {
            if (_wastelandHoverCard == null) {
                _wastelandHoverCard = new ControlCard {
                    IsHitTestVisible = false,
                    Margin = new Thickness(10, 0, 0, 0)
                };
                WastelandCardPopup.Child = _wastelandHoverCard;
            }
            return _wastelandHoverCard;
        }
    }

    public HexTileControl() { InitializeComponent(); }

    public void BindTile(HexTile tile) { BoundTile = tile; Refresh(isP1: true); }

    public void Refresh(bool isP1) {
        _lastIsP1 = isP1;
        if (BoundTile == null) return;

        if (!BoundTile.IsNavigable) {
            TileIcon.Text     = "";
            TileLabel.Text    = "";
            DwellersPanel.Children.Clear();
            ApplyColors(FogColor);
            HexShape.Opacity = 0.15;
            return;
        }
        HexShape.Opacity = 1.0;

        bool revealed = BoundTile.IsRevealedFor(isP1);

        if (!revealed) {
            TileIcon.Text     = "";
            TileLabel.Text    = "";
            DwellersPanel.Children.Clear();
            ApplyColors(FogColor);
            return;
        }

        (TileIcon.Text, TileLabel.Text) = BoundTile.Type switch {
            TileType.Player1Vault => ("[V]", "Vault P1"),
            TileType.Player2Vault => ("[V]", "Vault P2"),
            TileType.Location     => ("[L]", BoundTile.LocationName),
            _                     => ("",  " "),
        };

        int p1 = BoundTile.Player1Dwellers.Count(d => d.IsAlive);
        int p2 = BoundTile.Player2Dwellers.Count(d => d.IsAlive);
        
        DwellersPanel.Children.Clear();
        var p1Alive = BoundTile.Player1Dwellers.Where(dw => dw.IsAlive).ToList();
        for (int i = 0; i < p1Alive.Count; i++) {
            var card = new ControlCard(); card.BindDweller(p1Alive[i]); card.IsHitTestVisible = _lastIsP1;
            var tg = new TransformGroup();
            tg.Children.Add(new RotateTransform(Random.Shared.Next(-5, 6)));
            tg.Children.Add(new TranslateTransform(i * 3, i * 3));
            card.RenderTransform = tg;
            card.RenderTransformOrigin = new Point(0.5, 0.5);
            DwellersPanel.Children.Add(new Viewbox { Width = 90, Height = 90, Child = card, Margin = new Thickness(0) });
        }
        var p2Alive = BoundTile.Player2Dwellers.Where(dw => dw.IsAlive).ToList();
        for (int i = 0; i < p2Alive.Count; i++) {
            var card = new ControlCard(); card.BindDweller(p2Alive[i]); card.IsHitTestVisible = !_lastIsP1;
            var tg = new TransformGroup();
            tg.Children.Add(new RotateTransform(Random.Shared.Next(-5, 6)));
            tg.Children.Add(new TranslateTransform(i * 3, i * 3));
            card.RenderTransform = tg;
            card.RenderTransformOrigin = new Point(0.5, 0.5);
            DwellersPanel.Children.Add(new Viewbox { Width = 90, Height = 90, Child = card, Margin = new Thickness(0) });
        }

        TileIcon.Foreground = new SolidColorBrush(Colors.White);
        TileLabel.Foreground = new SolidColorBrush(Colors.White);
        TileIcon.FontWeight = FontWeights.Bold;
        TileLabel.FontWeight = FontWeights.Bold;
        
        UpdateVisual();
        if (BoundTile.HasConflict) {
            HexShape.Stroke = new SolidColorBrush(Color.FromRgb(220, 60, 60));
        }
    }

    public void SetReachableHighlight(bool reachable) {
        if (reachable) {
            ReachableOverlay.Fill = new SolidColorBrush(Lighten(ActivePlayerColor, 0.20));
            ReachableOverlay.Visibility = Visibility.Visible;
        } else {
            ReachableOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private DateTime _lastScrollTime = DateTime.MinValue;

    private void OnMouseWheel(object s, MouseWheelEventArgs e) {
        if (BoundTile == null) return;
        if ((DateTime.Now - _lastScrollTime).TotalSeconds < 0.5) {
            e.Handled = true;
            return;
        }
        _lastScrollTime = DateTime.Now;

        var list = _lastIsP1 ? BoundTile.Player1Dwellers : BoundTile.Player2Dwellers;
        var alive = list.Where(d => d.IsAlive).ToList();
        if (alive.Count > 1) {
            if (e.Delta > 0) {
                var first = alive[0];
                list.Remove(first);
                list.Add(first);
            } else {
                var last = alive.Last();
                list.Remove(last);
                list.Insert(0, last);
            }
            Refresh(_lastIsP1);

            var topDweller = list.LastOrDefault(d => d.IsAlive);
            if (topDweller != null && IsHovered) {
                WastelandHoverCard.BindDweller(topDweller);
            }
        }
        e.Handled = true;
    }

    public void SetSelected(bool selected) {
        if (BoundTile?.IsNavigable == false) return;
        IsSelected = selected;
        UpdateVisual();
    }

    public void SetHovered(bool hovered) {
        if (BoundTile?.IsNavigable == false) return;
        IsHovered = hovered;
        UpdateVisual();
    }

    private void OnHoverEnter(object s, MouseEventArgs e) {
        if (BoundTile?.IsNavigable == false) return;
        IsHovered = true;
        UpdateVisual();
        var list = _lastIsP1 ? BoundTile.Player1Dwellers : BoundTile.Player2Dwellers;
        var topDweller = list.LastOrDefault(d => d.IsAlive);
        if (topDweller != null) {
            WastelandHoverCard.BindDweller(topDweller);
            WastelandCardPopup.IsOpen = true;
        }
    }
    private void OnHoverLeave(object s, MouseEventArgs e) {
        if (BoundTile?.IsNavigable == false) return;
        IsHovered = false;
        UpdateVisual();
        WastelandCardPopup.IsOpen = false;
    }

    private void OnRightClick(object s, MouseButtonEventArgs e) {
        if (WastelandCardPopup.IsOpen && WastelandHoverCard.BoundDweller != null) {
            e.Handled = true;
            WastelandHoverCard.OpenContextMenu();
        }
    }

    public bool IsPath { get; private set; }
    public void SetPath(bool isPath) { IsPath = isPath; UpdateVisual(); }

    private void UpdateVisual() {
        if (BoundTile == null || !BoundTile.IsNavigable) {
            return;
        }

        bool revealed = BoundTile.IsRevealedFor(_lastIsP1);
        Color baseColor = revealed ? ExploredColor : FogColor;
        Color fill = baseColor;

        bool hasSelf = _lastIsP1 ? BoundTile.Player1Dwellers.Any(d => d.IsAlive) : BoundTile.Player2Dwellers.Any(d => d.IsAlive);
        bool hasEnemy = _lastIsP1 ? BoundTile.Player2Dwellers.Any(d => d.IsAlive) : BoundTile.Player1Dwellers.Any(d => d.IsAlive);

        if (IsSelected) {
            fill = Blend(fill, ActivePlayerColor, 0.55);
        }

        if (hasSelf && hasEnemy) {
            fill = Blend(fill, Color.FromRgb(200, 100, 50), 0.3);
        } else if (hasSelf) {
            fill = Blend(fill, Color.FromRgb(50, 200, 50), 0.3);
        } else if (hasEnemy) {
            fill = Blend(fill, Color.FromRgb(200, 50, 50), 0.3);
        }

        if (IsPath) {
            Color pathColor = Color.FromArgb(165, 255, 215, 0);
            try {
                pathColor = (Color)ColorConverter.ConvertFromString(GameConfigRepository.Config.PathColor);
            } catch {
            }
            fill = Blend(fill, pathColor, 0.55);
        }

        if (IsHovered) {
            fill = Lighten(fill, 0.10);
        }

        ApplyColors(fill);

        if (PathOverlay != null) {
            PathOverlay.Visibility = Visibility.Collapsed;
        }

        if (SelectedOverlay != null) {
            SelectedOverlay.Visibility = IsSelected ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void ApplyColors(Color fill) {
        HexShape.Fill   = new SolidColorBrush(fill);
        HexShape.Stroke ??= new SolidColorBrush(Color.FromArgb(80, 0xFF, 0xFF, 0xFF));
    }

    private static Color Blend(Color a, Color b, double t) => Color.FromRgb(
        (byte)(a.R + (b.R - a.R) * t),
        (byte)(a.G + (b.G - a.G) * t),
        (byte)(a.B + (b.B - a.B) * t));

    private static Color Lighten(Color c, double amount) => Color.FromRgb(
        (byte)Math.Min(255, c.R + 255 * amount),
        (byte)Math.Min(255, c.G + 255 * amount),
        (byte)Math.Min(255, c.B + 255 * amount));

    private void OnDrop(object s, DragEventArgs e) {
    }
    private void OnDragEnterTile(object s, DragEventArgs e) { }
    private void OnDragLeaveTile(object s, DragEventArgs e) { }
    private void OnDragOverTile(object s, DragEventArgs e) { }
}
