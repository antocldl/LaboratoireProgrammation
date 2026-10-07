using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

public static class GpuEnforcer
{
    private const string GpuPreferenceRegistryKey = @"Software\Microsoft\DirectX\UserGpuPreferences";
    private const string HighPerformanceGpuValue = "GpuPreference=2;";

    [UnmanagedCallersOnly(EntryPoint = "NvOptimusEnablement")]
    public static uint NvOptimusEnablement() => 0x00000001;

    [UnmanagedCallersOnly(EntryPoint = "AmdPowerXpressRequestHighPerformance")]
    public static int AmdPowerXpressRequestHighPerformance() => 1;

    public static bool Initialize(bool restartIfPreferenceChanged = false)
    {
        ForceProcessHardwareRendering();
        
        bool registryConfigured = RegisterWindowsGpuPreference();

        if (registryConfigured && restartIfPreferenceChanged)
        {
            RestartApplication();
            return false;
        }

        return IsHardwareAccelerated();
    }

    public static void ForceProcessHardwareRendering()
    {
        RenderOptions.ProcessRenderMode = RenderMode.Default;
    }

    public static int GetRenderingTier()
    {
        return RenderCapability.Tier >> 16;
    }

    public static bool IsHardwareAccelerated()
    {
        return GetRenderingTier() >= 2;
    }

    public static bool RegisterWindowsGpuPreference()
    {
        try
        {
            string? processPath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(processPath) || !File.Exists(processPath))
            {
                return false;
            }

            using RegistryKey? key = Registry.CurrentUser.CreateSubKey(GpuPreferenceRegistryKey);
            if (key == null)
            {
                return false;
            }

            object? existingValue = key.GetValue(processPath);
            if (existingValue is string str && str.Contains("GpuPreference=2"))
            {
                return false;
            }

            key.SetValue(processPath, HighPerformanceGpuValue, RegistryValueKind.String);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void RestartApplication()
    {
        string? currentPath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(currentPath))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = currentPath,
            UseShellExecute = true
        });

        Application.Current.Shutdown();
    }
}