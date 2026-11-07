using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using LaboratoireProgrammation.Project.ModernOverseerWarsMP.Models;
using WpfAnimatedGif;

namespace LaboratoireProgrammation.Project.ViewModels.Exercices {
    
    public partial class Exo3B3 : UserControl {
        private ImageAnimationController? _controller;

        public Exo3B3() {
            InitializeComponent();

            if (DataContext is INotifyPropertyChanged initialVm) {
                initialVm.PropertyChanged += ViewModel_PropertyChanged;
            }

            DataContextChanged += OnDataContextChanged;

            ImageBehavior.AddAnimationLoadedHandler(VaultBoy, (s, e) => {
                _controller = ImageBehavior.GetAnimationController(VaultBoy);
                UpdateAnimation();
            });
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
            if (e.OldValue is INotifyPropertyChanged oldVm) {
                oldVm.PropertyChanged -= ViewModel_PropertyChanged;
            }
            if (e.NewValue is INotifyPropertyChanged newVm) {
                newVm.PropertyChanged += ViewModel_PropertyChanged;
            }
            UpdateAnimation();
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(Exo3B3ViewModel.IsRunning)) {
                UpdateAnimation();
            }
        }

        private void UpdateAnimation() {
            _controller ??= ImageBehavior.GetAnimationController(VaultBoy);
            if (_controller == null) return;

            if (DataContext is Exo3B3ViewModel { IsRunning: true }) {
                _controller.Play();
            } else {
                _controller.Pause();
            }
        }
    }
}