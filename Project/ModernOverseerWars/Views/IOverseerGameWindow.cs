using System.Windows;
using System.Windows.Media;
using LaboratoireProgrammation.Project.ModernOverseerWars.Models;
using LaboratoireProgrammation.Project.ModernOverseerWars.Models.Entities;
using LaboratoireProgrammation.Project.ModernOverseerWars.Models.Interfaces;

namespace LaboratoireProgrammation.Project.ModernOverseerWars.Views;

public interface IOverseerGameWindow {
    GameState State { get; }
    Vault ActiveVault { get; }
    bool CanInteract { get; }
    Color GetThemeColor();
    Color GetThemeBgDark();
    void RefreshAll();
    void OpenDwellerSheet(Dweller dw);
    void OpenEquipmentSelection(Dweller dw, bool isW);
    void StartDraggingCard(ControlCard card, Point position);
    void MoveDraggingCard(ControlCard card, Point currentMousePos);
    void StopDraggingCard(ControlCard card, Point dropPoint);
    void OnAssignmentSlotDropDirect(ControlCard card);
    void RequestEquip(Dweller dw, ICard item, bool isWeapon);
    void RequestUnequip(Dweller dw, bool isWeapon);
}
