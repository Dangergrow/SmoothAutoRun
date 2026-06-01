using System;
using System.IO;
using Newtonsoft.Json;
using SmoothAutoRun.Models;

namespace SmoothAutoRun.Services
{
    public static class ConfigService
    {
        private static string ConfigPath = "";
        public static AppSettings Settings { get; private set; } = new();

        public static void Initialize()
        {
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SmoothAutoRun");

            if (HasWriteAccess(exeDir))
            {
                Directory.CreateDirectory(exeDir);
                ConfigPath = Path.Combine(exeDir, "config.json");
            }
            else
            {
                Directory.CreateDirectory(appData);
                ConfigPath = Path.Combine(appData, "config.json");
            }

            Load();
        }

        public static void Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    Settings = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
                }
                else
                {
                    Settings = new AppSettings();
                    Save();
                }
            }
            catch
            {
                Settings = new AppSettings();
                Save();
            }
        }

        public static void Save()
        {
            try
            {
                string json = JsonConvert.SerializeObject(Settings, Formatting.Indented);
                File.WriteAllText(ConfigPath, json);
                // Скрываем файл конфига
                try { File.SetAttributes(ConfigPath, FileAttributes.Hidden); } catch { }
            }
            catch (Exception ex)
            {
                Logger.Log("ConfigService", $"Failed to save config: {ex.Message}");
            }
        }

        public static void ResetToDefaults()
        {
            Settings = new AppSettings();
            Save();
        }

        private static bool HasWriteAccess(string folder)
        {
            try
            {
                string testFile = Path.Combine(folder, ".write_test");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}