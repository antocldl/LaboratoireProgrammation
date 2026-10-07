using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LaboratoireProgrammation.Project.Views.Exercices;

public class FileSystemEntity {
    public string EntityName { get; set; }
    public string EntitySize { get; set; }
    public string CreationDate { get; set; }
    public string ModificationDate { get; set; }
    public string FullPath { get; set; }
    public bool IsDirectory { get; set; }
}

public partial class Exo___8 : UserControl {
    public Exo___8() {
        InitializeComponent();
    }

    private void SetupExplorerData(object sender, RoutedEventArgs e) {
        InitializeStorageDrives();
    }

    private void InitializeStorageDrives() {
        var availableDrives = Environment.GetLogicalDrives();

        foreach (var drivePath in availableDrives) {
            var driveContainer = new TreeViewItem {
                Header = drivePath,
                Tag = drivePath
            };

            driveContainer.Items.Add(null);
            DirectoryTree.Items.Add(driveContainer);
        }
    }

    private void HandleDirectoryExpansion(object sender, RoutedEventArgs e) {
        var currentContainer = e.OriginalSource as TreeViewItem;

        if (currentContainer == null || currentContainer.Items.Count != 1 || currentContainer.Items[0] != null) return;

        currentContainer.Items.Clear();
        var containerPath = currentContainer.Tag.ToString();

        PopulateSubDirectories(currentContainer, containerPath);
    }

    private void PopulateSubDirectories(TreeViewItem parentContainer, string targetPath) {
        try {
            if (!Directory.Exists(targetPath)) return;

            var childDirectories = Directory.GetDirectories(targetPath);

            foreach (var directoryPath in childDirectories) {
                var dirInfo = new DirectoryInfo(directoryPath);
                var directoryContainer = new TreeViewItem {
                    Header = dirInfo.Name,
                    Tag = directoryPath
                };

                directoryContainer.Items.Add(null);
                parentContainer.Items.Add(directoryContainer);
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (Exception ex) {
            SystemStatusIndicator.Text = $"[ ERROR ] {ex.Message}";
        }
    }

    private void HandleDirectorySelection(object sender, RoutedPropertyChangedEventArgs<object> e) {
        var selectedContainer = DirectoryTree.SelectedItem as TreeViewItem;

        if (selectedContainer != null) {
            var targetPath = selectedContainer.Tag.ToString();
            DisplayFolderAssets(targetPath);
        }
    }

    private async void DisplayFolderAssets(string targetPath) {
        FileBrowser.Items.Clear();

        try {
            if (!Directory.Exists(targetPath)) return;

            var entities = await Task.Run(() => {
                var list = new List<FileSystemEntity>();
                var childDirectories = Directory.GetDirectories(targetPath);
                foreach (var dirPath in childDirectories) {
                    var dirInfo = new DirectoryInfo(dirPath);
                    list.Add(new FileSystemEntity {
                        EntityName = dirInfo.Name,
                        EntitySize = "<DIR>",
                        CreationDate = dirInfo.CreationTime.ToString("yyyy-MM-dd HH:mm"),
                        ModificationDate = dirInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm"),
                        FullPath = dirInfo.FullName,
                        IsDirectory = true
                    });
                }

                var targetFiles = Directory.GetFiles(targetPath);
                foreach (var filePath in targetFiles) {
                    var assetInfo = new FileInfo(filePath);
                    list.Add(new FileSystemEntity {
                        EntityName = assetInfo.Name,
                        EntitySize = FormatAssetSize(assetInfo.Length),
                        CreationDate = assetInfo.CreationTime.ToString("yyyy-MM-dd HH:mm"),
                        ModificationDate = assetInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm"),
                        FullPath = assetInfo.FullName,
                        IsDirectory = false
                    });
                }
                return list;
            });

            foreach (var entity in entities) {
                FileBrowser.Items.Add(entity);
            }

            SystemStatusIndicator.Text = $"[ SUCCESS ] Loaded {entities.Count} items from {targetPath}";
        }
        catch (UnauthorizedAccessException) {
            SystemStatusIndicator.Text = "[ DENIED ] Access restricted to selected directory";
        }
        catch (Exception ex) {
            SystemStatusIndicator.Text = $"[ ERROR ] {ex.Message}";
        }
    }

    private void HandleAssetDoubleClick(object sender, MouseButtonEventArgs e) {
        if (FileBrowser.SelectedItem is FileSystemEntity selectedEntity) {
            if (!selectedEntity.IsDirectory)
                try {
                    Process.Start(new ProcessStartInfo {
                        FileName = selectedEntity.FullPath,
                        UseShellExecute = true
                    });
                    SystemStatusIndicator.Text = $"[ LAUNCHED ] {selectedEntity.EntityName}";
                }
                catch (Exception ex) {
                    SystemStatusIndicator.Text = $"[ ERROR ] Could not open file: {ex.Message}";
                }
            else
                DisplayFolderAssets(selectedEntity.FullPath);
        }
    }

    private string FormatAssetSize(long bytes) {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double size = bytes;
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1) {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:0.##} {units[unitIndex]}";
    }

    private void HandleApplicationExit(object sender, RoutedEventArgs e) {
        var parentWindow = Window.GetWindow(this);
        parentWindow?.Close();
    }

    private void HandleViewModeChange(object sender, SelectionChangedEventArgs e) {
        if (FileBrowser == null || ViewModeSelector.SelectedItem == null) return;

        var selectedMode = ViewModeSelector.SelectedItem as ComboBoxItem;
        var modeName = selectedMode.Content.ToString();

        if (modeName == "Details")
            FileBrowser.View = FileDetailsView;
        else
            FileBrowser.View = null;

        SystemStatusIndicator.Text = $"[ VIEW ] Switched to {modeName}";
    }
}