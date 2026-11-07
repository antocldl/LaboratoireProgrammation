using System.Windows;
using System.Windows.Controls;
using LaboratoireProgrammation.Project.Models.Menu;

namespace LaboratoireProgrammation.Project.ViewModels.Menu;

public partial class BabyModeB3 : UserControl {
    public BabyModeB3() {
        InitializeComponent();
    }

    private MainWindow? ParentWindow => Window.GetWindow(this) as MainWindow;

    private void Exo1B3(object sender, RoutedEventArgs e) {
        if (ParentWindow is { } window) {
            Visibility = Visibility.Collapsed;
            RunSubprograms.RunSub(RunSubprograms.Subprograms.Exercice1B3, window);
        }
    }
    
    private void Exo2B3(object sender, RoutedEventArgs e) {
        if (ParentWindow is { } window) {
            Visibility = Visibility.Collapsed;
            RunSubprograms.RunSub(RunSubprograms.Subprograms.Exercice2B3, window);
        }
    }

    private void Exo3B3(object sender, RoutedEventArgs e) {
        if (ParentWindow is { } window) {
            Visibility = Visibility.Collapsed;
            RunSubprograms.RunSub(RunSubprograms.Subprograms.Exercice3B3, window);
        }
    }
}