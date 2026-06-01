using System;
using System.IO;

namespace SmoothAutoRun.Services
{
    public static class Logger
    {
        private static string LogPath = "";
        private static readonly object LockObj = new();

        public static void Initialize()
        {
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string logDir = Path.Combine(exeDir, "logs");

            if (!HasWriteAccess(exeDir))
            {
                string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SmoothAutoRun");
                logDir = Path.Combine(appData, "logs");
            }

            Directory.CreateDirectory(logDir);
            
            // Скрываем папку logs
            try { File.SetAttributes(logDir, FileAttributes.Hidden); } catch { }
            
            LogPath = Path.Combine(logDir, "app.log");
            
            // Скрываем файл лога
            try { File.SetAttributes(LogPath, FileAttributes.Hidden); } catch { }
        }

        public static void Log(string component, string message)
        {
            try
            {
                lock (LockObj)
                {
                    string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{component}] {message}";
                    File.AppendAllText(LogPath, line + Environment.NewLine);
                    System.Diagnostics.Debug.WriteLine(line);
                }
            }
            catch { }
        }

        private static bool HasWriteAccess(string folder)
        {
            try
            {
                if (!Directory.Exists(folder)) return false;
                string testFile = Path.Combine(folder, ".write_test");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                return true;
            }
            catch { return false; }
        }
    }
}