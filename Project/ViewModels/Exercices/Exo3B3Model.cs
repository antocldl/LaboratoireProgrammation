using System.Windows.Threading;
using LaboratoireProgrammation.Project.ModernOverseerWarsMP.Helpers;
using LaboratoireProgrammation.Project.ViewModels.Exercices;

namespace LaboratoireProgrammation.Project.ModernOverseerWarsMP.Models {
    public class Exo3B3ViewModel : ViewModelBase {
        private readonly AnalogStopwatchModel _model;
        private readonly DispatcherTimer _timer;
        public bool IsRunning => _model.IsRunning;
        public RelayCommand StartCommand { get; }
        public RelayCommand StopCommand { get; }
        public RelayCommand ResetCommand { get; }

        public Exo3B3ViewModel() {
            _model = new AnalogStopwatchModel();

            _timer = new DispatcherTimer {
                Interval = TimeSpan.FromSeconds(1)
            }; _timer.Tick += Timer_Tick;

            StartCommand = new RelayCommand(
                _ => Start(),
                _ => !_model.IsRunning
            );

            StopCommand = new RelayCommand(
                _ => Stop(),
                _ => _model.IsRunning
            );

            ResetCommand = new RelayCommand(
                _ => Reset(),
                _ => _model.ElapsedSeconds > 0
            );
        }

        public double SecondAngle => (_model.ElapsedSeconds % 60) * 6.0;
        public double MinuteAngle => (_model.ElapsedSeconds / 60.0 % 60.0) * 6.0;

        public string DigitalDisplay {
            get {
                TimeSpan t = TimeSpan.FromSeconds(_model.ElapsedSeconds);
                return t.ToString(@"mm\:ss");
            }
        }

        private void Timer_Tick(object sender, EventArgs e) {
            _model.Increment();
            NotifyTimerProperties();
        }

        private void Start() {
            _model.IsRunning = true;
            _timer.Start();
            OnPropertyChanged(nameof(IsRunning));
            UpdateCommands();
        }

        private void Stop() {
            _model.IsRunning = false;
            _timer.Stop();
            OnPropertyChanged(nameof(IsRunning));
            UpdateCommands();
        }

        private void Reset() {
            _timer.Stop();
            _model.Reset();
            NotifyTimerProperties();
            OnPropertyChanged(nameof(IsRunning));
            UpdateCommands();
        }

        private void NotifyTimerProperties() {
            OnPropertyChanged(nameof(SecondAngle));
            OnPropertyChanged(nameof(MinuteAngle));
            OnPropertyChanged(nameof(DigitalDisplay));
        }

        private void UpdateCommands() {
            StartCommand.RaiseCanExecuteChanged();
            StopCommand.RaiseCanExecuteChanged();
            ResetCommand.RaiseCanExecuteChanged();
        }
    }
}