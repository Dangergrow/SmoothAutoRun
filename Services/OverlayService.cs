using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using SmoothAutoRun.Models;

namespace SmoothAutoRun.Services
{
    public class OverlayService
    {
        private Window? _overlayWindow;
        private TextBlock? _overlayText;
        private Border? _overlayBorder;
        private DispatcherTimer? _updateTimer;
        private Process? _presentMonProcess;
        private PerformanceCounter? _cpuCounter;
        private PerformanceCounter? _gpuCounter;
        private bool _isRunning = false;
        private double _currentFps = 0;
        private readonly object _lock = new();

        public bool IsRunning => _isRunning;

        public void Start()
        {
            if (_isRunning) return;
            var s = ConfigService.Settings.Overlay;

            // Инициализируем счетчики заранее
            InitCounters();

            if (s.ShowFps) StartPresentMon();

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _overlayWindow = new Window
                {
                    Topmost = true, ShowInTaskbar = false,
                    WindowStyle = WindowStyle.None, AllowsTransparency = true,
                    Background = Brushes.Transparent,
                    Width = 220, Height = 100,
                    IsHitTestVisible = false, Focusable = false
                };

                _overlayBorder = new Border
                {
                    Background = ParseColor(s.BgColor),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8)
                };

                _overlayText = new TextBlock
                {
                    Foreground = ParseColor(s.TextColor),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = s.FontSize,
                    Text = "Загрузка..."
                };

                _overlayBorder.Child = _overlayText;
                _overlayWindow.Content = _overlayBorder;
                PositionWindow();

                _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) };
                _updateTimer.Tick += UpdateOverlay;
                _updateTimer.Start();
                _overlayWindow.Show();
            });

            _isRunning = true;
            Logger.Log("Overlay", "Started");
        }

        private void InitCounters()
        {
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _cpuCounter.NextValue(); // Первый вызов всегда 0
                System.Threading.Thread.Sleep(100);
                _cpuCounter.NextValue(); // Второй вызов дает реальное значение
            }
            catch (Exception ex)
            {
                Logger.Log("Overlay", $"CPU counter error: {ex.Message}");
                _cpuCounter = null;
            }

            try
            {
                var cat = new PerformanceCounterCategory("GPU Engine");
                var instances = cat.GetInstanceNames();
                string? gpuInstance = null;

                // Ищем 3D engine
                foreach (var inst in instances)
                {
                    if (inst.Contains("engtype_3D") || inst.Contains("3D") || inst.Contains("gpu"))
                    {
                        gpuInstance = inst;
                        break;
                    }
                }

                // Если не нашли — берем первую
                if (gpuInstance == null && instances.Length > 0)
                    gpuInstance = instances[0];

                if (gpuInstance != null)
                {
                    _gpuCounter = new PerformanceCounter("GPU Engine", "Utilization Percentage", gpuInstance);
                    _gpuCounter.NextValue();
                    System.Threading.Thread.Sleep(100);
                    _gpuCounter.NextValue();
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Overlay", $"GPU counter error: {ex.Message}");
                _gpuCounter = null;
            }
        }

        public void Stop()
        {
            if (!_isRunning) return;
            StopPresentMon();

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _updateTimer?.Stop(); _updateTimer = null;
                _overlayWindow?.Close(); _overlayWindow = null;
                _overlayText = null; _overlayBorder = null;
            });

            try { _cpuCounter?.Dispose(); } catch { }
            try { _gpuCounter?.Dispose(); } catch { }
            _cpuCounter = null;
            _gpuCounter = null;
            _isRunning = false;
            Logger.Log("Overlay", "Stopped");
        }

        public void UpdateSettings()
        {
            if (!_isRunning) return;
            var s = ConfigService.Settings.Overlay;

            if (s.ShowFps && _presentMonProcess == null) StartPresentMon();
            if (!s.ShowFps && _presentMonProcess != null) StopPresentMon();

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (_overlayText == null || _overlayBorder == null || _overlayWindow == null) return;
                _overlayText.Foreground = ParseColor(s.TextColor);
                _overlayText.FontSize = s.FontSize; // Применяем размер шрифта
                _overlayBorder.Background = ParseColor(s.BgColor);
                PositionWindow();
            });
        }

        private void UpdateOverlay(object? sender, EventArgs e)
        {
            if (_overlayText == null) return;
            var s = ConfigService.Settings.Overlay;
            var lines = new List<string>();

            // FPS
            if (s.ShowFps)
            {
                double fps;
                lock (_lock) { fps = _currentFps; }
                lines.Add($"FPS: {fps:F0}");
            }

            // CPU
            if (s.ShowCpu && _cpuCounter != null)
            {
                try
                {
                    float cpu = _cpuCounter.NextValue();
                    if (cpu >= 0 && cpu <= 100)
                        lines.Add($"CPU: {cpu:F0}%");
                    else
                        lines.Add("CPU: --");
                }
                catch
                {
                    lines.Add("CPU: --");
                }
            }

            // GPU
            if (s.ShowGpu && _gpuCounter != null)
            {
                try
                {
                    float gpu = _gpuCounter.NextValue();
                    if (gpu >= 0 && gpu <= 100)
                        lines.Add($"GPU: {gpu:F0}%");
                    else
                        lines.Add("GPU: --");
                }
                catch
                {
                    lines.Add("GPU: --");
                }
            }

            _overlayText.Text = string.Join("\n", lines);
        }

        private void StartPresentMon()
        {
            try
            {
                // Ищем PresentMon.exe в нескольких местах
                string[] paths = {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PresentMon.exe"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "PresentMon.exe"),
                    @"C:\Program Files\PresentMon\PresentMon.exe",
                    @"C:\Program Files (x86)\PresentMon\PresentMon.exe"
                };

                string? exePath = null;
                foreach (var p in paths)
                {
                    if (File.Exists(p)) { exePath = p; break; }
                }

                if (exePath == null)
                {
                    Logger.Log("Overlay", "PresentMon.exe not found");
                    return;
                }

                Logger.Log("Overlay", $"PresentMon found at: {exePath}");

                _presentMonProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = "--output_stdout --no_csv --scroll_indicator",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    },
                    EnableRaisingEvents = true
                };

                _presentMonProcess.OutputDataReceived += (s, e) =>
                {
                    if (string.IsNullOrEmpty(e.Data)) return;
                    // Ищем Display fps
                    if (e.Data.Contains("Display=") && e.Data.Contains("fps"))
                    {
                        int di = e.Data.IndexOf("Display=");
                        int ps = e.Data.IndexOf("(", di);
                        int pe = e.Data.IndexOf(" fps)", ps);
                        if (ps > 0 && pe > 0)
                        {
                            string fpsStr = e.Data.Substring(ps + 1, pe - ps - 1).Trim();
                            if (double.TryParse(fpsStr, System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out double fps) && fps > 0)
                            {
                                lock (_lock) { _currentFps = fps; }
                            }
                        }
                    }
                };

                _presentMonProcess.Start();
                _presentMonProcess.BeginOutputReadLine();
                Logger.Log("Overlay", "PresentMon started successfully");
            }
            catch (Exception ex)
            {
                Logger.Log("Overlay", $"PresentMon start error: {ex.Message}");
                _presentMonProcess = null;
            }
        }

        private void StopPresentMon()
        {
            try
            {
                if (_presentMonProcess != null && !_presentMonProcess.HasExited)
                {
                    _presentMonProcess.Kill();
                    _presentMonProcess.Dispose();
                }
            }
            catch { }
            _presentMonProcess = null;
            _currentFps = 0;
        }

        private void PositionWindow()
        {
            if (_overlayWindow == null) return;
            double sw = SystemParameters.PrimaryScreenWidth;
            double sh = SystemParameters.PrimaryScreenHeight;
            double w = _overlayWindow.Width;
            double h = _overlayWindow.Height;

            switch (ConfigService.Settings.Overlay.Position)
            {
                case "Верхний левый": _overlayWindow.Left = 10; _overlayWindow.Top = 10; break;
                case "Верхний правый": _overlayWindow.Left = sw - w - 10; _overlayWindow.Top = 10; break;
                case "Нижний левый": _overlayWindow.Left = 10; _overlayWindow.Top = sh - h - 10; break;
                case "Нижний правый": _overlayWindow.Left = sw - w - 10; _overlayWindow.Top = sh - h - 10; break;
            }
        }

        private SolidColorBrush ParseColor(string hex)
        {
            try
            {
                if (hex == "#00000000") return Brushes.Transparent;
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            }
            catch { return Brushes.Lime; }
        }
    }
}