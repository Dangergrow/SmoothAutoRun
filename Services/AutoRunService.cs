using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using Newtonsoft.Json;
using SmoothAutoRun.Models;

namespace SmoothAutoRun.Services
{
    public class AutoRunService
    {
        private string Display(string source) => source switch
        {
            "HKCU\\Run" => "Реестр (Пользователь)",
            "HKLM\\Run" => "Реестр (Система)",
            "HKLM\\WOW6432Node\\Run" => "Реестр (Система x32)",
            "User Startup" => "Автозагрузка (Пользователь)",
            "Common Startup" => "Автозагрузка (Общая)",
            "Task Scheduler" => "Планировщик задач",
            "Service" => "Службы Windows",
            _ => source
        };

        public List<AutoRunEntry> GetAllEntries()
        {
            var entries = new List<AutoRunEntry>();
            entries.AddRange(GetRegistryEntries(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", "HKCU\\Run"));
            entries.AddRange(GetRegistryEntries(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", "HKLM\\Run"));
            entries.AddRange(GetRegistryEntries(Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "HKLM\\WOW6432Node\\Run"));
            entries.AddRange(GetStartupFolderEntries(Environment.SpecialFolder.Startup, "User Startup"));
            entries.AddRange(GetStartupFolderEntries(Environment.SpecialFolder.CommonStartup, "Common Startup"));
            return entries;
        }

        public List<AutoRunEntry> GetAllEntriesFull()
        {
            var entries = GetAllEntries();
            entries.AddRange(GetScheduledTasks());
            entries.AddRange(GetServices());
            return entries;
        }

        private List<AutoRunEntry> GetRegistryEntries(RegistryKey hive, string path, string source)
        {
            var entries = new List<AutoRunEntry>();
            try
            {
                using var key = hive.OpenSubKey(path);
                if (key != null)
                {
                    foreach (var valueName in key.GetValueNames())
                    {
                        try
                        {
                            var value = key.GetValue(valueName)?.ToString() ?? "";
                            entries.Add(new AutoRunEntry { Name = valueName, Source = source, SourceDisplay = Display(source), Path = value, Enabled = true });
                        }
                        catch { }
                    }
                }
            }
            catch { }
            return entries;
        }

        private List<AutoRunEntry> GetStartupFolderEntries(Environment.SpecialFolder folder, string source)
        {
            var entries = new List<AutoRunEntry>();
            try
            {
                string folderPath = Environment.GetFolderPath(folder);
                if (Directory.Exists(folderPath))
                {
                    foreach (var file in Directory.GetFiles(folderPath, "*.lnk"))
                    {
                        try { entries.Add(new AutoRunEntry { Name = Path.GetFileNameWithoutExtension(file), Source = source, SourceDisplay = Display(source), Path = file, Enabled = true }); }
                        catch { }
                    }
                    string disabledPath = Path.Combine(folderPath, "Disabled");
                    if (Directory.Exists(disabledPath))
                    {
                        foreach (var file in Directory.GetFiles(disabledPath, "*.lnk"))
                        {
                            try { entries.Add(new AutoRunEntry { Name = Path.GetFileNameWithoutExtension(file), Source = source, SourceDisplay = Display(source), Path = file, Enabled = false }); }
                            catch { }
                        }
                    }
                }
            }
            catch { }
            return entries;
        }

        private List<AutoRunEntry> GetScheduledTasks()
        {
            var entries = new List<AutoRunEntry>();
            try
            {
                using var ts = new TaskService();
                foreach (var task in ts.RootFolder.GetTasks(new System.Text.RegularExpressions.Regex(".*")))
                {
                    try
                    {
                        if (task.Definition.Triggers.Any(t => t is BootTrigger || t is LogonTrigger))
                            entries.Add(new AutoRunEntry { Name = task.Name, Source = "Task Scheduler", SourceDisplay = Display("Task Scheduler"), Path = task.Definition.Actions.FirstOrDefault()?.ToString() ?? task.Name, Enabled = task.Enabled });
                    }
                    catch { }
                }
            }
            catch { }
            return entries;
        }

        private List<AutoRunEntry> GetServices()
        {
            var entries = new List<AutoRunEntry>();
            try
            {
                foreach (var service in ServiceController.GetServices())
                {
                    try
                    {
                        if (service.StartType == ServiceStartMode.Automatic)
                            entries.Add(new AutoRunEntry { Name = service.ServiceName, Source = "Service", SourceDisplay = Display("Service"), Path = service.DisplayName, Enabled = service.Status == ServiceControllerStatus.Running });
                    }
                    catch { }
                }
            }
            catch { }
            return entries;
        }

        public void DisableEntry(AutoRunEntry entry)
        {
            try
            {
                switch (entry.Source)
                {
                    case "HKCU\\Run": DeleteRegistryEntry(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", entry.Name); break;
                    case "HKLM\\Run": DeleteRegistryEntry(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", entry.Name); break;
                    case "HKLM\\WOW6432Node\\Run": DeleteRegistryEntry(Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", entry.Name); break;
                    case "User Startup": case "Common Startup": MoveStartupEntry(entry, true); break;
                    case "Task Scheduler": DisableScheduledTask(entry.Name); break;
                    case "Service": SetServiceStartMode(entry.Name, "Disabled"); break;
                }
                entry.Enabled = false;
            }
            catch { }
        }

        public void EnableEntry(AutoRunEntry entry)
        {
            try
            {
                switch (entry.Source)
                {
                    case "HKCU\\Run": SetRegistryEntry(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", entry.Name, entry.Path); break;
                    case "HKLM\\Run": SetRegistryEntry(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", entry.Name, entry.Path); break;
                    case "HKLM\\WOW6432Node\\Run": SetRegistryEntry(Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", entry.Name, entry.Path); break;
                    case "User Startup": case "Common Startup": MoveStartupEntry(entry, false); break;
                    case "Task Scheduler": EnableScheduledTask(entry.Name); break;
                    case "Service": SetServiceStartMode(entry.Name, "Automatic"); break;
                }
                entry.Enabled = true;
            }
            catch { }
        }

        public void DeleteEntry(AutoRunEntry entry)
        {
            try
            {
                switch (entry.Source)
                {
                    case "HKCU\\Run": DeleteRegistryEntry(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", entry.Name); break;
                    case "HKLM\\Run": DeleteRegistryEntry(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", entry.Name); break;
                    case "HKLM\\WOW6432Node\\Run": DeleteRegistryEntry(Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", entry.Name); break;
                    case "User Startup": case "Common Startup": DeleteStartupFile(entry); break;
                    case "Task Scheduler": DeleteScheduledTask(entry.Name); break;
                }
            }
            catch { }
        }

        public void AddEntry(AutoRunEntry entry, string targetSource)
        {
            try
            {
                switch (targetSource)
                {
                    case "HKCU\\Run": SetRegistryEntry(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", entry.Name, entry.Path); break;
                    case "HKLM\\Run": SetRegistryEntry(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", entry.Name, entry.Path); break;
                    case "User Startup": CreateStartupShortcut(entry, Environment.SpecialFolder.Startup); break;
                    case "Common Startup": CreateStartupShortcut(entry, Environment.SpecialFolder.CommonStartup); break;
                }
            }
            catch { }
        }

        public void ExportEntry(AutoRunEntry entry, string filePath) => File.WriteAllText(filePath, JsonConvert.SerializeObject(entry, Formatting.Indented));
        public AutoRunEntry? ImportEntry(string filePath) => JsonConvert.DeserializeObject<AutoRunEntry>(File.ReadAllText(filePath));

        private void SetRegistryEntry(RegistryKey hive, string path, string name, string? value) { using var key = hive.OpenSubKey(path, true) ?? hive.CreateSubKey(path); if (value == null) key.DeleteValue(name, false); else key.SetValue(name, value); }
        private void DeleteRegistryEntry(RegistryKey hive, string path, string name) { using var key = hive.OpenSubKey(path, true); key?.DeleteValue(name, false); }

        private void MoveStartupEntry(AutoRunEntry entry, bool toDisabled)
        {
            string folder = entry.Source == "User Startup" ? Environment.GetFolderPath(Environment.SpecialFolder.Startup) : Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
            string disabledFolder = Path.Combine(folder, "Disabled");
            string src = toDisabled ? Path.Combine(folder, entry.Name + ".lnk") : Path.Combine(disabledFolder, entry.Name + ".lnk");
            string dst = toDisabled ? Path.Combine(disabledFolder, entry.Name + ".lnk") : Path.Combine(folder, entry.Name + ".lnk");
            if (!toDisabled) Directory.CreateDirectory(disabledFolder);
            if (File.Exists(src)) File.Move(src, dst);
        }

        private void DeleteStartupFile(AutoRunEntry entry)
        {
            string folder = entry.Source == "User Startup" ? Environment.GetFolderPath(Environment.SpecialFolder.Startup) : Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
            foreach (var f in new[] { Path.Combine(folder, entry.Name + ".lnk"), Path.Combine(folder, "Disabled", entry.Name + ".lnk") })
                if (File.Exists(f)) File.Delete(f);
        }

        private void CreateStartupShortcut(AutoRunEntry entry, Environment.SpecialFolder folder)
        {
            string shortcutPath = Path.Combine(Environment.GetFolderPath(folder), entry.Name + ".lnk");
            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
            var shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = entry.Path;
            shortcut.Arguments = entry.Arguments;
            shortcut.Save();
        }

        private void DisableScheduledTask(string name) { using var ts = new TaskService(); var t = ts.FindTask(name); if (t != null) t.Enabled = false; }
        private void EnableScheduledTask(string name) { using var ts = new TaskService(); var t = ts.FindTask(name); if (t != null) t.Enabled = true; }
        private void DeleteScheduledTask(string name) { using var ts = new TaskService(); ts.RootFolder.DeleteTask(name, false); }
        private void SetServiceStartMode(string name, string mode) { try { using var wmi = new System.Management.ManagementObject($"Win32_Service.Name='{name}'"); wmi.InvokeMethod("ChangeStartMode", new object[] { mode }); } catch { } }
    }
}