using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LaboratoireProgrammation.Project.ViewModels.Menu;
using LaboratoireProgrammation.Project.Views.Exercices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LaboratoireProgrammation.Project.ViewModels.Exercices;

public partial class Exo3 : UserControl {
    // = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = =
    // [ - Fields - ]

    public static readonly string TargetDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Exercice-3");
    public static string SelectedFilePath = string.Empty;
    public static int SelectedHumanIndex = -1;
    public static string SortBy = "Name";
    private readonly List<Button> HumansButtons = new();

    // = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = =
    // [ - Initializers - ]

    public Exo3() {
        InitializeComponent();
        Loaded += async (s, e) => {
            await Task.Delay(10);
            InitUi(Window.GetWindow(this));
            InitFolder();
            await Refresh();
        };
    }

    private void InitUi(Window? window) {
        if (window is not MainWindow win) return;
    }

    private void InitFolder() {
        if (!Directory.Exists(TargetDir)) Directory.CreateDirectory(TargetDir);
    }

    // = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = =
    // [ - Click Events - ]

    private void SortFilesClick(object sender, RoutedEventArgs e) {
        switch (SortBy) {
            case "Name":
                SortBy = "NameI";
                B1.Content = "📁 Trier par : Nom (A-Z)";
                break;
            case "NameI":
                SortBy = "Date";
                B1.Content = "📁 Trier par : Nom (Z-A)";
                break;
            case "Date":
                SortBy = "DateI";
                B1.Content = "📁 Trier par : Date (Ancien)";
                break;
            case "DateI":
                SortBy = "Name";
                B1.Content = "📁 Trier par : Date (Nouveau)";
                break;
        }

        _ = UpdateFiles();
    }

    private void NewFileClick(object sender, RoutedEventArgs e) {
        var fileName = $"Robco_{DateTime.Now:HH-mm-ss}.txt";
        var newFilePath = Path.Combine(TargetDir, fileName);

        var iteration = 1;
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var newFileName = "";

        while (File.Exists(newFilePath)) {
            newFileName = $"{baseName} ({iteration}){extension}";
            newFilePath = Path.Combine(TargetDir, newFileName);
            iteration++;
        }

        if (!File.Exists(newFilePath)) File.Create(newFilePath);

        SelectedFilePath = newFilePath;

        FileContentScrollViewer_Names.Visibility = Visibility.Visible;
        FileContentScrollViewer_Names2.Visibility = Visibility.Visible;
        FileContentScrollViewer_Qualities.Visibility = Visibility.Visible;
        FileContentScrollViewer_Salary.Visibility = Visibility.Visible;

        NamesLabel.Visibility = Visibility.Visible;
        Names2Label.Visibility = Visibility.Visible;
        QualityLabel.Visibility = Visibility.Visible;
        SalaryLabel.Visibility = Visibility.Visible;

        _ = UpdateFiles();
    }

    private void NewHumanClick(object sender, RoutedEventArgs e) {
        var win = Application.Current.MainWindow as MainWindow;
        if (win == null) return;

        win.Exo3B.Visibility = Visibility.Visible;
        Opacity = 0.1;
        Panel.SetZIndex(this, -1);
    }

    private void SelectHumanClick(object sender, RoutedEventArgs e, int index) {
        SelectedHumanIndex = index;

        foreach (var button in HumansButtons)
            if (button.Tag?.ToString() == index.ToString())
                button.Style = (Style)FindResource("RobcoBtnTriggered");
            else
                button.Style = (Style)FindResource("RobcoBtn");
    }

    private void DeleteFileClick(object sender, RoutedEventArgs e) {
        if (File.Exists(SelectedFilePath)) {
            for (var retry = 0; retry < 10; retry++) {
                try {
                    File.Delete(SelectedFilePath);
                    break;
                }
                catch (IOException) {
                    Thread.Sleep(50);
                }
            }

            SelectedFilePath = string.Empty;
            SelectedHumanIndex = -1;
            HumansButtons.Clear();

            FileContentScrollViewer_Names.Visibility = Visibility.Collapsed;
            FileContentScrollViewer_Names2.Visibility = Visibility.Collapsed;
            FileContentScrollViewer_Qualities.Visibility = Visibility.Collapsed;
            FileContentScrollViewer_Salary.Visibility = Visibility.Collapsed;

            NamesLabel.Visibility = Visibility.Collapsed;
            Names2Label.Visibility = Visibility.Collapsed;
            QualityLabel.Visibility = Visibility.Collapsed;
            SalaryLabel.Visibility = Visibility.Collapsed;

            _ = UpdateFiles();
        }
    }

    private void DeleteHumanClick(object sender, RoutedEventArgs e) {
        if (SelectedHumanIndex == -1) return;

        if (File.Exists(SelectedFilePath)) {
            var jsonObjects = GetAllJsonObjects(SelectedFilePath);
            jsonObjects.RemoveAt(SelectedHumanIndex);
            File.WriteAllText(SelectedFilePath, string.Join(Environment.NewLine, jsonObjects));
            PrintHumanData(jsonObjects);
        }
    }

    // = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = =
    // [ - Tasks - ]

    private async Task UpdateFiles() {
        var container = new StackPanel();

        if (!Directory.Exists(TargetDir)) return;
        var files = new List<FileInfo>();
        switch (SortBy) {
            case "Name":
                files = Directory.GetFiles(TargetDir)
                    .Select(f => new FileInfo(f))
                    .OrderBy(f => f.Name)
                    .ToList();
                break;
            case "NameI":
                files = Directory.GetFiles(TargetDir)
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.Name)
                    .ToList();
                break;
            case "Date":
                files = Directory.GetFiles(TargetDir)
                    .Select(f => new FileInfo(f))
                    .OrderBy(f => f.CreationTime)
                    .ToList();
                break;
            case "DateI":
                files = Directory.GetFiles(TargetDir)
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTime)
                    .ToList();
                break;
        }

        foreach (var fileInfo in files) {
            var button = new Button {
                Content = "- " + fileInfo.Name,
                Tag = fileInfo.FullName,
                Margin = new Thickness(2),
                Style = (Style)FindResource(SelectedFilePath == fileInfo.FullName ? "RobcoBtnTriggered" : "RobcoBtn")
            };

            button.Click += (sender, e) => {
                if (sender is Button btn && btn.Tag is string path)
                    SelectFile(path);
            };

            container.Children.Add(button);
        }

        FilesScrollViewer.Content = container;
        await Task.Yield();
    }

    public async Task Refresh() {
        await UpdateFiles();
        if (!string.IsNullOrEmpty(SelectedFilePath)) PrintHumanData(GetAllJsonObjects(SelectedFilePath));
    }

    private void SelectFile(string filePath) {
        SelectedFilePath = filePath;

        if (FilesScrollViewer.Content is StackPanel panel)
            foreach (Control child in panel.Children)
                if (child is Button btn)
                    btn.Style = btn.Tag?.ToString() == filePath
                        ? (Style)FindResource("RobcoBtnTriggered")
                        : (Style)FindResource("RobcoBtn");

        FileContentScrollViewer_Names.Visibility = Visibility.Visible;
        FileContentScrollViewer_Names2.Visibility = Visibility.Visible;
        FileContentScrollViewer_Qualities.Visibility = Visibility.Visible;
        FileContentScrollViewer_Salary.Visibility = Visibility.Visible;
        PrintHumanData(GetAllJsonObjects(SelectedFilePath));
    }

    // = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = =
    // [ - Utils - ]

    private List<string> GetAllJsonObjects(string filePath) {
        var jsonObjects = new List<string>();

        if (!File.Exists(filePath) || new FileInfo(filePath).Length == 0)
            return jsonObjects;

        try {
            using (var streamReader = File.OpenText(filePath))
            using (var jsonReader = new JsonTextReader(streamReader)) {
                jsonReader.SupportMultipleContent = true;
                while (jsonReader.Read())
                    if (jsonReader.TokenType == JsonToken.StartObject) {
                        var obj = JObject.Load(jsonReader);
                        jsonObjects.Add(obj.ToString(Formatting.None));
                    }
            }
        }
        catch (Exception ex) {
            Debug.WriteLine("Error reading JSON: " + ex.Message);
        }

        return jsonObjects;
    }

    private void PrintHumanData(List<string> objects) {
        var names = new StackPanel();
        foreach (var jsonObject in objects) {
            var human = Human.Deserialize(jsonObject);
            if (human == null) return;
            var button = new Button {
                Content = human.Name,
                Tag = objects.IndexOf(jsonObject),
                Margin = new Thickness(2),
                Style = (Style)FindResource("RobcoBtn")
            };

            button.Click += (sender, e) => { SelectHumanClick(sender, e, objects.IndexOf(jsonObject)); };
            names.Children.Add(button);
            HumansButtons.Add(button);
        }

        var names2 = new StackPanel();
        foreach (var jsonObject in objects) {
            var human = Human.Deserialize(jsonObject);
            if (human == null) return;
            var button = new Button {
                Content = human.Name2,
                Tag = objects.IndexOf(jsonObject),
                Margin = new Thickness(2),
                Style = (Style)FindResource("RobcoBtn")
            };

            button.Click += (sender, e) => { SelectHumanClick(sender, e, objects.IndexOf(jsonObject)); };
            names2.Children.Add(button);
            HumansButtons.Add(button);
        }

        var qualities = new StackPanel();
        foreach (var jsonObject in objects) {
            var human = Human.Deserialize(jsonObject);
            if (human == null) return;
            var button = new Button {
                Content = human.Quality,
                Tag = objects.IndexOf(jsonObject),
                Margin = new Thickness(2),
                Style = (Style)FindResource("RobcoBtn")
            };

            button.Click += (sender, e) => { SelectHumanClick(sender, e, objects.IndexOf(jsonObject)); };
            qualities.Children.Add(button);
            HumansButtons.Add(button);
        }

        var salary = new StackPanel();
        foreach (var jsonObject in objects) {
            var human = Human.Deserialize(jsonObject);
            if (human == null) return;
            var button = new Button {
                Content = human.Salary + " €",
                Tag = objects.IndexOf(jsonObject),
                Margin = new Thickness(2),
                Style = (Style)FindResource("RobcoBtn")
            };

            button.Click += (sender, e) => { SelectHumanClick(sender, e, objects.IndexOf(jsonObject)); };
            salary.Children.Add(button);
            HumansButtons.Add(button);
        }

        FileContentScrollViewer_Names.Content = names;
        FileContentScrollViewer_Qualities.Content = qualities;
        FileContentScrollViewer_Names2.Content = names2;
        FileContentScrollViewer_Salary.Content = salary;
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject? depObj) where T : DependencyObject {
        if (depObj != null)
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++) {
                var child = VisualTreeHelper.GetChild(depObj, i);

                if (child is T) yield return (T)child;
                foreach (var childOfChild in FindVisualChildren<T>(child)) yield return childOfChild;
            }
    }

    // = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = =
    // [ - Other Events - ]

    private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e) {
        if (!(sender is ScrollViewer sv)) return;
        foreach (var otherSv in FindVisualChildren<ScrollViewer>(sv.Parent as UIElement)) {
            if (otherSv == sv) continue;
            otherSv.ScrollToVerticalOffset(sv.VerticalOffset);
        }
    }
}