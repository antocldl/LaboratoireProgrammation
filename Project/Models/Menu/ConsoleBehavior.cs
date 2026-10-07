using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media;
using LaboratoireProgrammation.Project.Services;
using LaboratoireProgrammation.Project.ViewModels.Menu;

namespace LaboratoireProgrammation.Project.Models.Menu;

public class ConsoleBehavior {
    private readonly List<string> _commandHistory = new();
    private int _historyIndex = -1;

    public Action? OnClearInputRequest;
    public Action<string, Color>? OnOutputRequest;
    public Action<string, Color>? OnOutputSameLineRequest;

    public static bool SpeedLoad { get; set; } = true;
    public static int Babymode { get; set; }
    public static bool MemfyMode { get; set; }
    public static bool IsAdjustingSize { get; set; }

    public void SetBabyMode(bool enabled) {
        Babymode = enabled ? 1 : 0;
    }

    public bool GetBabyMode() {
        return Babymode == 1;
    }

    public void ToggleBabyMode() {
        Babymode = Babymode == 1 ? 0 : 1;
    }

    public void RequestOutput(string text, Color color) {
        OnOutputRequest?.Invoke(text, color);
    }

    public void RequestOutputSameLine(string text, Color color) {
        OnOutputSameLineRequest?.Invoke(text, color);
    }

    public async Task ProcessInput(string cmd, MainWindow context) {
        if (string.IsNullOrWhiteSpace(cmd)) return;

        _commandHistory.Add(cmd);
        _historyIndex = _commandHistory.Count;

        CommandsProcessor.ProcessCommand(cmd.ToLower(), context);
        OnClearInputRequest?.Invoke();
    }

    public string? GetHistory(int direction) {
        if (_commandHistory.Count == 0) return null;

        _historyIndex += direction;
        _historyIndex = Math.Clamp(_historyIndex, 0, _commandHistory.Count);

        if (_historyIndex < _commandHistory.Count) return _commandHistory[_historyIndex];

        return string.Empty;
    }

    public bool GetIsAdjustingSize() {
        return IsAdjustingSize;
    }
}