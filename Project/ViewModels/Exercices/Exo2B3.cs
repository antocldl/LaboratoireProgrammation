using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace LaboratoireProgrammation.Project.ViewModels.Exercices
{
    public class TaskModel : INotifyPropertyChanged
    {
        private string _titre;
        private bool _isDone;
        private string _priority;

        public string Titre 
        { 
            get => _titre; 
            set { _titre = value; OnPropertyChanged(nameof(Titre)); } 
        }

        public bool IsDone 
        { 
            get => _isDone; 
            set { _isDone = value; OnPropertyChanged(nameof(IsDone)); } 
        }

        public string Priority
        {
            get => _priority;
            set { _priority = value; OnPropertyChanged(nameof(Priority)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class TaskStateColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isDone && isDone)
                return new SolidColorBrush(Colors.Gray);

            return Application.Current.Resources["TerminalYellowBrush"] ?? new SolidColorBrush(Colors.White);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    public partial class Exo2B3 : UserControl
    {
        private ObservableCollection<TaskModel> _allTasks;
        private readonly string _saveFilePath;

        public Exo2B3()
        {
            InitializeComponent();
            _saveFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "todolist.json");
            LoadTasks();
            LstTasks.ItemsSource = _allTasks;
            UpdateProgress();
        }

        private void LoadTasks()
        {
            if (File.Exists(_saveFilePath))
            {
                try
                {
                    var json = File.ReadAllText(_saveFilePath);
                    var tasks = JsonSerializer.Deserialize<ObservableCollection<TaskModel>>(json);
                    _allTasks = tasks ?? new ObservableCollection<TaskModel>();
                }
                catch
                {
                    _allTasks = new ObservableCollection<TaskModel>();
                }
            }
            else
            {
                _allTasks = new ObservableCollection<TaskModel>();
            }
        }

        private void SaveTasks()
        {
            try
            {
                var json = JsonSerializer.Serialize(_allTasks);
                File.WriteAllText(_saveFilePath, json);
                MessageBox.Show("To-Do list sauvegardée avec succès.", "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors de la sauvegarde : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtNewTask.Text)) return;

            string priorityStr = "Normale";
            if (CmbPriority.SelectedItem is ComboBoxItem cbi)
            {
                priorityStr = cbi.Content.ToString();
            }

            var newTask = new TaskModel
            {
                Titre = TxtNewTask.Text,
                IsDone = false,
                Priority = priorityStr
            };

            _allTasks.Add(newTask);
            TxtNewTask.Clear();
            UpdateProgress();
            ApplyFilter();
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (LstTasks.SelectedItem is TaskModel selectedTask)
            {
                _allTasks.Remove(selectedTask);
                UpdateProgress();
                ApplyFilter();
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            SaveTasks();
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            var filter = TxtSearch.Text.ToLower();
            if (string.IsNullOrWhiteSpace(filter))
            {
                LstTasks.ItemsSource = _allTasks;
            }
            else
            {
                LstTasks.ItemsSource = new ObservableCollection<TaskModel>(
                    _allTasks.Where(t => t.Titre != null && t.Titre.ToLower().Contains(filter))
                );
            }
        }

        private void Task_StateChanged(object sender, RoutedEventArgs e)
        {
            UpdateProgress();
        }

        private void UpdateProgress()
        {
            if (_allTasks == null || !_allTasks.Any())
            {
                PrgTasks.Value = 0;
                LblProgress.Text = "Progression : 0%";
                return;
            }

            int completed = _allTasks.Count(t => t.IsDone);
            int total = _allTasks.Count;
            double percentage = (double)completed / total * 100;

            PrgTasks.Value = percentage;
            LblProgress.Text = $"Progression : {Math.Round(percentage)}%";
        }
    }
}