using System.Collections.Generic;

namespace SmoothAutoRun.Models
{
    public class AppSettings
    {
        public bool StartWithWindows { get; set; } = true;
        public bool StartMinimizedToTray { get; set; } = false;
        public bool DarkMode { get; set; } = true;
        public bool AutoDetectTheme { get; set; } = true;

        public List<AutoRunEntry> AutoRunEntries { get; set; } = new();
        public List<FirewallRule> FirewallRules { get; set; } = new();
        public OverlaySettings Overlay { get; set; } = new();

        public List<string> AnticheatProcesses { get; set; } = new()
        {
            "vgc.exe", "EasyAntiCheat.exe", "BEService.exe",
            "FACEIT.exe", "ESEA.exe", "mrac.exe", "PunkBuster.exe"
        };
    }

    public class AutoRunEntry
    {
        public string Name { get; set; } = "";
        public string Source { get; set; } = "";
        public string SourceDisplay { get; set; } = "";
        public string Path { get; set; } = "";
        public string Arguments { get; set; } = "";
        public bool Enabled { get; set; } = true;
    }

    public class FirewallRule
    {
        public string Name { get; set; } = "";
        public string ExePath { get; set; } = "";
        public bool Blocked { get; set; } = true;
    }

    public class OverlaySettings
    {
        public bool ShowFps { get; set; } = true;
        public bool ShowCpu { get; set; } = false;
        public bool ShowGpu { get; set; } = false;
        public string Position { get; set; } = "Верхний правый";
        public string TextColor { get; set; } = "#00FF00";
        public string BgColor { get; set; } = "#000000";
        public int FontSize { get; set; } = 14;
        public string Hotkey { get; set; } = "F9";
        public bool AutoDisableForVanguard { get; set; } = true;
    }

    public class DiskInfo
    {
        public string Model { get; set; } = "";
        public string Type { get; set; } = "";
        public string SerialNumber { get; set; } = "";
        public string Size { get; set; } = "";
        public string DriveLetter { get; set; } = "";
        public string HealthStatus { get; set; } = "";
        public string HealthColor { get; set; } = "#888888";
        public string Temperature { get; set; } = "N/A";
        public string PowerOnCount { get; set; } = "N/A";
        public string PowerOnHours { get; set; } = "N/A";
        public string InterfaceType { get; set; } = "";
        public string MediaType { get; set; } = "";
        public string Index { get; set; } = "0";
    }
}