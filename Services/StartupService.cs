using System;
using Microsoft.Win32;

namespace SmoothAutoRun.Services
{
    public static class StartupService
    {
        private const string APP_NAME = "SmoothAutoRun";
        private const string RUN_KEY = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static void SetAutoStart(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RUN_KEY, true);

                if (enable)
                {
                    string exePath = Environment.ProcessPath ?? AppContext.BaseDirectory;
                    key?.SetValue(APP_NAME, $"\"{exePath}\" --minimized");
                    Logger.Log("Startup", "Auto-start enabled");
                }
                else
                {
                    key?.DeleteValue(APP_NAME, false);
                    Logger.Log("Startup", "Auto-start disabled");
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Startup", $"Error setting auto-start: {ex.Message}");
            }
        }

        public static bool IsAutoStartEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RUN_KEY);
                return key?.GetValue(APP_NAME) != null;
            }
            catch
            {
                return false;
            }
        }
    }
}