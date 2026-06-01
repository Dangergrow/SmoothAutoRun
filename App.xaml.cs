using System;
using System.Windows;
using SmoothAutoRun.Services;
using SmoothAutoRun.Views;
using Hardcodet.Wpf.TaskbarNotification;

namespace SmoothAutoRun
{
    public partial class App : System.Windows.Application
    {
        private TaskbarIcon? _trayIcon;
        private MainWindow? _mainWindow;
        public AnticheatDetector? AnticheatDetector { get; private set; }
        public static App Instance => (App)System.Windows.Application.Current;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Logger.Initialize();
            Logger.Log("App", "Starting...");
            ConfigService.Initialize();

            string? startMinEnv = Environment.GetEnvironmentVariable("SMOOTHAUTORUN_START_MINIMIZED");
            bool startMinimized = startMinEnv == "true";

            ApplyTheme();

            AnticheatDetector = new AnticheatDetector(ConfigService.Settings.AnticheatProcesses);
            AnticheatDetector.GameDetectedChanged += OnGameDetectedChanged;
            AnticheatDetector.Start();

            _mainWindow = new MainWindow();

            if (startMinimized)
                _mainWindow.Hide();
            else
                _mainWindow.Show();

            InitializeTray(_mainWindow);
            Logger.Log("App", "Started successfully");
        }

        private void InitializeTray(MainWindow mainWindow)
        {
            _trayIcon = new TaskbarIcon
            {
                ToolTipText = "SmoothAutoRun",
                Visibility = Visibility.Visible
            };

            // Пробуем загрузить иконку
            try
            {
                string iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "app.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    _trayIcon.Icon = new System.Drawing.Icon(iconPath);
                }
            }
            catch { }

            var contextMenu = new System.Windows.Controls.ContextMenu();

            var showItem = new System.Windows.Controls.MenuItem { Header = "Показать/Скрыть" };
            showItem.Click += (s, args) =>
            {
                if (mainWindow.IsVisible)
                    mainWindow.Hide();
                else
                {
                    mainWindow.Show();
                    mainWindow.Activate();
                }
            };

            var exitItem = new System.Windows.Controls.MenuItem { Header = "Выход" };
            exitItem.Click += (s, args) =>
            {
                ForceShutdown();
            };

            contextMenu.Items.Add(showItem);
            contextMenu.Items.Add(new System.Windows.Controls.Separator());
            contextMenu.Items.Add(exitItem);

            _trayIcon.ContextMenu = contextMenu;
            _trayIcon.TrayLeftMouseDown += (s, args) =>
            {
                if (mainWindow.IsVisible)
                    mainWindow.Hide();
                else
                {
                    mainWindow.Show();
                    mainWindow.Activate();
                }
            };

            _trayIcon.TrayMouseDoubleClick += (s, args) =>
            {
                mainWindow.Show();
                mainWindow.Activate();
            };
        }

        private void ForceShutdown()
        {
            try
            {
                _mainWindow?.ForceClose();
                ConfigService.Save();
                AnticheatDetector?.Stop();
                if (_trayIcon != null)
                {
                    _trayIcon.Visibility = Visibility.Collapsed;
                    _trayIcon.Dispose();
                }
            }
            catch { }

            Environment.Exit(0);
        }

        private void ApplyTheme()
        {
            bool dark = ConfigService.Settings.DarkMode;
            ApplyThemeResources(dark);
        }

        public void ApplyThemeNow(bool dark)
        {
            Dispatcher.Invoke(() =>
            {
                ApplyThemeResources(dark);
                ConfigService.Settings.DarkMode = dark;
                ConfigService.Save();

                if (_mainWindow != null)
                {
                    _mainWindow.Background = dark
                        ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x1E, 0x2E))
                        : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF0, 0xF0, 0xF5));

                    _mainWindow.UpdateTheme(dark);
                }
            });
        }

        private void ApplyThemeResources(bool dark)
        {
            var bg = dark ? (System.Windows.Media.Brush)Resources["BgDarkBrush"] : Resources["BgLightBrush"];
            var sidebar = dark ? (System.Windows.Media.Brush)Resources["SidebarDarkBrush"] : Resources["SidebarLightBrush"];
            var text = dark ? (System.Windows.Media.Brush)Resources["TextDarkBrush"] : Resources["TextLightBrush"];

            Resources["AppBackground"] = bg;
            Resources["AppSidebar"] = sidebar;
            Resources["AppText"] = text;
        }

        private void OnGameDetectedChanged(bool gameRunning)
        {
            Dispatcher.Invoke(() =>
            {
                Logger.Log("App", $"Game detected: {gameRunning}");
                if (_trayIcon != null)
                    _trayIcon.ToolTipText = gameRunning ? "SmoothAutoRun - Режим игры" : "SmoothAutoRun";
            });
        }

        protected override void OnExit(ExitEventArgs e)
        {
            ForceShutdown();
            base.OnExit(e);
        }
    }
}