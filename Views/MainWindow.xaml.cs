using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using SmoothAutoRun.Models;
using SmoothAutoRun.Services;

namespace SmoothAutoRun.Views
{
    public partial class MainWindow : Window
    {
        private AutoRunService? _autoRunService;
        private FirewallService? _firewallService;
        private OverlayService? _overlayService;
        private VolumeService? _volumeService;
        private DiskService? _diskService;
        private List<Button> _menuButtons = new();
        private List<AutoRunEntry> _allAutoRunEntries = new();
        private bool _loadingSettings = false;
        private bool _initialized = false;
        private IntPtr _windowHandle;
        private const int _hotkeyId = 9001;

        public MainWindow()
        {
            InitializeComponent();

            _menuButtons = new List<Button> { BtnAutoRun, BtnFirewall, BtnOverlay, BtnVolume, BtnDisks, BtnSettings };
            _autoRunService = new AutoRunService();
            _firewallService = new FirewallService();
            _overlayService = new OverlayService();
            _volumeService = new VolumeService();
            _diskService = new DiskService();

            LoadSettings();
            _initialized = true;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            foreach (var btn in _menuButtons) btn.Style = (Style)FindResource("MenuButton");
            BtnAutoRun.Style = (Style)FindResource("MenuButtonActive");

            var helper = new WindowInteropHelper(this);
            _windowHandle = helper.Handle;
            var source = HwndSource.FromHwnd(_windowHandle);
            source?.AddHook(WndProc);
            RegisterOverlayHotkey();

            if (ChkVolumeEnabled.IsChecked == true) _volumeService?.Start();
            await LoadAutoRunEntriesFast();
            _ = Task.Run(() => { try { Dispatcher.Invoke(() => RefreshFirewallList()); } catch { } });
        }

        public void UpdateTheme(bool dark)
        {
            var cardBg = dark ? "#2A2A3E" : "#E8E8F0";
            var sidebarBg = dark ? "#252536" : "#D0D0E0";
            var mainBg = dark ? "#1E1E2E" : "#F0F0F5";
            var textColor = dark ? "#CDD6F4" : "#1E1E2E";
            var inputBg = dark ? "#1A1A2E" : "#FFFFFF";
            var borderColor = dark ? "#3A3A50" : "#C0C0D0";
            var textSecondary = dark ? "#8B8BA0" : "#555555";
            try
            {
                Resources["CardBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(cardBg));
                Resources["SidebarBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(sidebarBg));
                Resources["MainBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(mainBg));
                Resources["TextPrimary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(textColor));
                Resources["InputBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(inputBg));
                Resources["BorderColor"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(borderColor));
                Resources["TextSecondary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(textSecondary));
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(mainBg));
                BuildAutoRunTree();
                RefreshFirewallList();
            }
            catch { }
        }

        private async Task LoadAutoRunEntriesFast()
        {
            try { _allAutoRunEntries = await Task.Run(() => _autoRunService?.GetAllEntries() ?? new List<AutoRunEntry>()); }
            catch { _allAutoRunEntries = new List<AutoRunEntry>(); }
            BuildAutoRunTree();
        }

        private void BuildAutoRunTree()
        {
            TreeAutoRun.Items.Clear();
            string filter = TxtAutoRunSearch?.Text?.ToLower() ?? "";
            var entries = string.IsNullOrEmpty(filter) ? _allAutoRunEntries
                : _allAutoRunEntries.Where(x => x.Name.ToLower().Contains(filter) || x.Path.ToLower().Contains(filter) || x.SourceDisplay.ToLower().Contains(filter)).ToList();
            var grouped = entries.GroupBy(e => e.SourceDisplay).OrderBy(g => g.Key);
            foreach (var group in grouped)
            {
                var cat = new TreeViewItem { Header = $"{group.Key} ({group.Count()})", FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Colors.Orange), IsExpanded = true };
                foreach (var entry in group)
                {
                    var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
                    var info = new StackPanel { Width = 380 };
                    info.Children.Add(new TextBlock { Text = entry.Name, FontWeight = FontWeights.Bold, FontSize = 12, Foreground = (Brush)FindResource("TextPrimary") });
                    info.Children.Add(new TextBlock { Text = entry.Path, FontSize = 10, Foreground = new SolidColorBrush(Colors.Gray), TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 360 });
                    var toggle = new CheckBox { IsChecked = entry.Enabled, Style = (Style)FindResource("ToggleSwitch"), Margin = new Thickness(8, 0, 8, 0), Tag = entry };
                    toggle.Checked += AutoRunToggle_Changed; toggle.Unchecked += AutoRunToggle_Changed;
                    var btnDel = new Button { Content = "✕", Width = 28, Height = 28, FontSize = 14, Background = new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C)), Foreground = Brushes.White, ToolTip = "Удалить", Margin = new Thickness(2), Tag = entry };
                    btnDel.Click += AutoRunDelete_Click;
                    var btnExp = new Button { Content = "↓", Width = 28, Height = 28, FontSize = 12, Background = (Brush)FindResource("AccentBrush"), Foreground = Brushes.White, ToolTip = "Экспорт", Tag = entry };
                    btnExp.Click += AutoRunExport_Click;
                    panel.Children.Add(info); panel.Children.Add(toggle); panel.Children.Add(btnDel); panel.Children.Add(btnExp);
                    cat.Items.Add(new TreeViewItem { Header = panel });
                }
                TreeAutoRun.Items.Add(cat);
            }
        }

        private void AutoRunToggle_Changed(object sender, RoutedEventArgs e) { if (_loadingSettings || !_initialized) return; if (sender is CheckBox cb && cb.Tag is AutoRunEntry entry) { if (cb.IsChecked == true) _autoRunService?.EnableEntry(entry); else _autoRunService?.DisableEntry(entry); } }

        private void LoadSettings()
        {
            _loadingSettings = true;
            var s = ConfigService.Settings;
            ChkShowFps.IsChecked = s.Overlay.ShowFps; ChkShowCpu.IsChecked = s.Overlay.ShowCpu; ChkShowGpu.IsChecked = s.Overlay.ShowGpu;
            ChkOverlayVanguard.IsChecked = s.Overlay.AutoDisableForVanguard;
            SelectComboBoxItem(CmbOverlayPosition, s.Overlay.Position); SelectComboBoxItem(CmbOverlayFontSize, s.Overlay.FontSize.ToString());
            TxtOverlayHotkey.Text = s.Overlay.Hotkey;
            try { BtnOverlayTextColor.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(s.Overlay.TextColor)); } catch { }
            try { BtnOverlayBgColor.Background = s.Overlay.BgColor == "#00000000" ? Brushes.Transparent : new SolidColorBrush((Color)ColorConverter.ConvertFromString(s.Overlay.BgColor)); } catch { }
            ChkStartWithWindows.IsChecked = s.StartWithWindows; ChkStartMinimized.IsChecked = s.StartMinimizedToTray;
            ChkDarkMode.IsChecked = s.DarkMode; ChkVolumeEnabled.IsChecked = false;
            TxtAnticheatList.Text = string.Join("\n", s.AnticheatProcesses);
            _loadingSettings = false;
        }

        private void SaveAllSettings()
        {
            if (!_initialized) return;
            var s = ConfigService.Settings;
            s.Overlay.ShowFps = ChkShowFps?.IsChecked ?? true; s.Overlay.ShowCpu = ChkShowCpu?.IsChecked ?? false; s.Overlay.ShowGpu = ChkShowGpu?.IsChecked ?? false;
            s.Overlay.AutoDisableForVanguard = ChkOverlayVanguard?.IsChecked ?? true;
            s.Overlay.Position = GetComboBoxValue(CmbOverlayPosition) ?? "Верхний правый";
            s.Overlay.FontSize = int.TryParse(GetComboBoxValue(CmbOverlayFontSize), out int fs) ? fs : 14;
            s.Overlay.Hotkey = TxtOverlayHotkey?.Text ?? "F9";
            s.StartWithWindows = ChkStartWithWindows?.IsChecked ?? true; s.StartMinimizedToTray = ChkStartMinimized?.IsChecked ?? false;
            s.DarkMode = ChkDarkMode?.IsChecked ?? true;
            s.AnticheatProcesses = TxtAnticheatList?.Text?.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList() ?? new List<string> { "vgc.exe" };
            ConfigService.Save(); StartupService.SetAutoStart(s.StartWithWindows);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) { if (msg == 0x0312 && wParam.ToInt32() == _hotkeyId) { ToggleOverlay(); handled = true; } return IntPtr.Zero; }
        private void RegisterOverlayHotkey() { UnregisterHotkey(); string hk = ConfigService.Settings.Overlay.Hotkey ?? "F9"; if (Enum.TryParse(hk, out System.Windows.Forms.Keys key)) NativeMethods.RegisterHotKey(_windowHandle, _hotkeyId, 0, (uint)key); }
        private void UnregisterHotkey() => NativeMethods.UnregisterHotKey(_windowHandle, _hotkeyId);
        private void ToggleOverlay() { if (_overlayService?.IsRunning == true) { _overlayService.Stop(); BtnOverlayStart.IsEnabled = true; BtnOverlayStop.IsEnabled = false; } else { _overlayService?.Start(); BtnOverlayStart.IsEnabled = false; BtnOverlayStop.IsEnabled = true; } }

        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button clicked) { foreach (var b in _menuButtons) b.Style = (Style)FindResource("MenuButton"); clicked.Style = (Style)FindResource("MenuButtonActive"); }
            TabAutoRun.Visibility = TabFirewall.Visibility = TabOverlay.Visibility = TabVolume.Visibility = TabDisks.Visibility = TabSettings.Visibility = Visibility.Collapsed;
            if (sender == BtnAutoRun) { TabAutoRun.Visibility = Visibility.Visible; TxtTabTitle.Text = "Автозагрузка"; }
            else if (sender == BtnFirewall) { TabFirewall.Visibility = Visibility.Visible; TxtTabTitle.Text = "Брандмауэр"; }
            else if (sender == BtnOverlay) { TabOverlay.Visibility = Visibility.Visible; TxtTabTitle.Text = "Оверлей"; }
            else if (sender == BtnVolume) { TabVolume.Visibility = Visibility.Visible; TxtTabTitle.Text = "Звук"; }
            else if (sender == BtnDisks) { TabDisks.Visibility = Visibility.Visible; TxtTabTitle.Text = "Диски"; }
            else if (sender == BtnSettings) { TabSettings.Visibility = Visibility.Visible; TxtTabTitle.Text = "Настройки"; }
        }

        private async void AutoRunRefresh_Click(object sender, RoutedEventArgs e) { await Task.Run(() => _allAutoRunEntries = _autoRunService?.GetAllEntriesFull() ?? new List<AutoRunEntry>()); BuildAutoRunTree(); }
        private void AutoRunSearch_Changed(object sender, TextChangedEventArgs e) { if (TxtSearchWatermark != null) TxtSearchWatermark.Visibility = string.IsNullOrEmpty(TxtAutoRunSearch?.Text) ? Visibility.Visible : Visibility.Collapsed; BuildAutoRunTree(); }
        private void AutoRunDelete_Click(object sender, RoutedEventArgs e) { if (sender is Button b && b.Tag is AutoRunEntry en && MessageBox.Show($"Удалить '{en.Name}'?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) { _autoRunService?.DeleteEntry(en); _allAutoRunEntries.Remove(en); BuildAutoRunTree(); } }
        private void AutoRunExport_Click(object sender, RoutedEventArgs e) { if (sender is Button b && b.Tag is AutoRunEntry en) { var d = new SaveFileDialog { FileName = $"{en.Name}.json", Filter = "JSON files (*.json)|*.json" }; if (d.ShowDialog() == true) _autoRunService?.ExportEntry(en, d.FileName); } }
        private void AutoRunAdd_Click(object sender, RoutedEventArgs e) { var dlg = new AddAutoRunDialog { Owner = this }; if (dlg.ShowDialog() == true && dlg.ResultEntry != null) { _autoRunService?.AddEntry(dlg.ResultEntry, dlg.ResultEntry.Source); _allAutoRunEntries.Add(dlg.ResultEntry); BuildAutoRunTree(); } }
        private void AutoRunImport_Click(object sender, RoutedEventArgs e) { var d = new OpenFileDialog { Filter = "JSON files (*.json)|*.json" }; if (d.ShowDialog() == true) { var en = _autoRunService?.ImportEntry(d.FileName); if (en != null) { _autoRunService?.AddEntry(en, en.Source); _allAutoRunEntries.Add(en); BuildAutoRunTree(); } } }

        private void RefreshFirewallList() { try { LstFirewallRules.ItemsSource = _firewallService?.GetBlockedApps() ?? new List<FirewallRule>(); } catch { } }
        private void InternetToggle_Click(object sender, RoutedEventArgs e) => _firewallService?.ToggleInternet(sender == BtnInternetOn);
        private void BrowseApp_Click(object sender, RoutedEventArgs e) { var d = new OpenFileDialog { Filter = "Исполняемые файлы (*.exe)|*.exe" }; if (d.ShowDialog() == true) TxtBlockAppPath.Text = d.FileName; }
        private void BlockApp_Click(object sender, RoutedEventArgs e) { if (!string.IsNullOrEmpty(TxtBlockAppPath?.Text)) { string n = System.IO.Path.GetFileNameWithoutExtension(TxtBlockAppPath.Text); _firewallService?.BlockApplication(TxtBlockAppPath.Text, n); RefreshFirewallList(); TxtBlockAppPath.Text = ""; MessageBox.Show($"Приложение {n} заблокировано!", "Брандмауэр", MessageBoxButton.OK, MessageBoxImage.Information); } }
        private void UnblockApp_Click(object sender, RoutedEventArgs e) { if (sender is Button b && b.Tag is FirewallRule r) { _firewallService?.UnblockApplication(r.Name); RefreshFirewallList(); } }

        private void OverlayStart_Click(object sender, RoutedEventArgs e) { _overlayService?.Start(); BtnOverlayStart.IsEnabled = false; BtnOverlayStop.IsEnabled = true; }
        private void OverlayStop_Click(object sender, RoutedEventArgs e) { _overlayService?.Stop(); BtnOverlayStart.IsEnabled = true; BtnOverlayStop.IsEnabled = false; }
        private void OverlaySetting_Changed(object sender, RoutedEventArgs e) { if (_loadingSettings || !_initialized) return; SaveAllSettings(); _overlayService?.UpdateSettings(); RegisterOverlayHotkey(); }
        private void ChooseOverlayTextColor_Click(object sender, RoutedEventArgs e) { var c = PickColor(ConfigService.Settings.Overlay.TextColor); BtnOverlayTextColor.Background = new SolidColorBrush(c); ConfigService.Settings.Overlay.TextColor = $"#{c.R:X2}{c.G:X2}{c.B:X2}"; ConfigService.Save(); _overlayService?.UpdateSettings(); }
        private void ChooseOverlayBgColor_Click(object sender, RoutedEventArgs e) { var c = PickColor(ConfigService.Settings.Overlay.BgColor); BtnOverlayBgColor.Background = new SolidColorBrush(c); ConfigService.Settings.Overlay.BgColor = $"#{c.R:X2}{c.G:X2}{c.B:X2}"; ConfigService.Save(); _overlayService?.UpdateSettings(); }
        private void ClearOverlayBg_Click(object sender, RoutedEventArgs e) { ConfigService.Settings.Overlay.BgColor = "#00000000"; ConfigService.Save(); BtnOverlayBgColor.Background = Brushes.Transparent; _overlayService?.UpdateSettings(); }
        private Color PickColor(string hex) { try { var d = new ColorPaletteDialog((Color)ColorConverter.ConvertFromString(hex)) { Owner = this }; if (d.ShowDialog() == true) return d.SelectedColor; } catch { } return Colors.White; }
        private void ResetOverlay_Click(object sender, RoutedEventArgs e) { _loadingSettings = true; ChkShowFps.IsChecked = true; ChkShowCpu.IsChecked = false; ChkShowGpu.IsChecked = false; CmbOverlayPosition.SelectedIndex = 1; CmbOverlayFontSize.SelectedIndex = 2; BtnOverlayTextColor.Background = new SolidColorBrush(Colors.Lime); BtnOverlayBgColor.Background = new SolidColorBrush(Colors.Black); TxtOverlayHotkey.Text = "F9"; ChkOverlayVanguard.IsChecked = true; _loadingSettings = false; SaveAllSettings(); RegisterOverlayHotkey(); _overlayService?.UpdateSettings(); }

        private void VolumeSetting_Changed(object sender, RoutedEventArgs e) { if (_loadingSettings || !_initialized) return; if (ChkVolumeEnabled?.IsChecked == true) _volumeService?.Start(); else _volumeService?.Stop(); }
        private async void ScanDisks_Click(object sender, RoutedEventArgs e) { BtnScanDisks.IsEnabled = false; BtnScanDisks.Content = "Сканирование..."; List<DiskInfo>? disks = null; await Task.Run(() => disks = _diskService?.ScanAllDisks()); LstDisks.ItemsSource = disks; BtnScanDisks.IsEnabled = true; BtnScanDisks.Content = "Сканировать диски"; }
        private void Settings_Changed(object sender, RoutedEventArgs e) { if (_loadingSettings || !_initialized) return; SaveAllSettings(); App.Instance.ApplyThemeNow(ChkDarkMode?.IsChecked ?? true); }
        private void SaveSettings_Click(object sender, RoutedEventArgs e) { SaveAllSettings(); MessageBox.Show("Настройки сохранены!", "SmoothAutoRun", MessageBoxButton.OK, MessageBoxImage.Information); }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left) DragMove(); }
        private void Minimize_Click(object sender, RoutedEventArgs e) => Hide();
        private void Close_Click(object sender, RoutedEventArgs e) { ForceClose(); Application.Current.Shutdown(); }
        private void Window_Closing(object sender, CancelEventArgs e) { UnregisterHotkey(); SaveAllSettings(); }
        public void ForceClose() { try { _overlayService?.Stop(); } catch { } try { _volumeService?.Stop(); } catch { } }

        private void SelectComboBoxItem(ComboBox cmb, string value) { foreach (ComboBoxItem item in cmb.Items) { if (item.Content?.ToString() == value) { cmb.SelectedItem = item; return; } } }
        private string? GetComboBoxValue(ComboBox? cmb) => (cmb?.SelectedItem as ComboBoxItem)?.Content?.ToString();

        private static class NativeMethods { [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h, int id, uint mod, uint vk); [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h, int id); }
    }

    public class AddAutoRunDialog : Window
    {
        public AutoRunEntry? ResultEntry;
        public AddAutoRunDialog()
        {
            Title = "Добавить в автозагрузку"; Width = 500; Height = 240; WindowStyle = WindowStyle.ToolWindow; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x2E)); Foreground = Brushes.White;
            var g = new Grid { Margin = new Thickness(16) };
            for (int i = 0; i < 4; i++) g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var txtN = new TextBox { Text = "Моя программа", Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x2E)), Foreground = Brushes.White, BorderBrush = new SolidColorBrush(Colors.Gray), Height = 28 };
            var txtP = new TextBox { Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x2E)), Foreground = Brushes.White, BorderBrush = new SolidColorBrush(Colors.Gray), Height = 28, Width = 340, Margin = new Thickness(0, 0, 8, 0) };
            var cmbS = new ComboBox { Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x2E)), Foreground = Brushes.White, BorderBrush = new SolidColorBrush(Colors.Gray), Height = 28 };
            cmbS.Items.Add(new ComboBoxItem { Content = "Реестр (Пользователь)", Tag = "HKCU\\Run", IsSelected = true });
            cmbS.Items.Add(new ComboBoxItem { Content = "Реестр (Система)", Tag = "HKLM\\Run" });
            cmbS.Items.Add(new ComboBoxItem { Content = "Автозагрузка (Пользователь)", Tag = "User Startup" });
            cmbS.Items.Add(new ComboBoxItem { Content = "Автозагрузка (Общая)", Tag = "Common Startup" });
            g.Children.Add(new TextBlock { Text = "Название:", Foreground = new SolidColorBrush(Colors.Gray), Margin = new Thickness(0, 0, 0, 2) });
            Grid.SetRow(txtN, 0); txtN.Margin = new Thickness(0, 18, 0, 8); g.Children.Add(txtN);
            g.Children.Add(new TextBlock { Text = "Путь:", Foreground = new SolidColorBrush(Colors.Gray), Margin = new Thickness(0, 0, 0, 2) });
            var pnl = new StackPanel { Orientation = Orientation.Horizontal };
            var btnBr = new Button { Content = "Обзор", Width = 70, Height = 28, Background = new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x35)), Foreground = Brushes.White, FontSize = 11 };
            btnBr.Click += (s, ev) => { var d = new OpenFileDialog { Filter = "*.exe|*.exe|*.*|*.*" }; if (d.ShowDialog() == true) txtP.Text = d.FileName; };
            pnl.Children.Add(txtP); pnl.Children.Add(btnBr);
            Grid.SetRow(pnl, 1); pnl.Margin = new Thickness(0, 18, 0, 8); g.Children.Add(pnl);
            g.Children.Add(new TextBlock { Text = "Источник:", Foreground = new SolidColorBrush(Colors.Gray), Margin = new Thickness(0, 0, 0, 2) });
            Grid.SetRow(cmbS, 2); cmbS.Margin = new Thickness(0, 18, 0, 8); g.Children.Add(cmbS);
            var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
            var ok = new Button { Content = "Добавить", Width = 90, Height = 30, Background = new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x35)), Foreground = Brushes.White, Margin = new Thickness(0, 0, 8, 0) };
            ok.Click += (s, ev) => { if (string.IsNullOrWhiteSpace(txtP.Text)) { MessageBox.Show("Укажите путь!"); return; } ResultEntry = new AutoRunEntry { Name = txtN.Text, Path = txtP.Text, Source = ((ComboBoxItem)cmbS.SelectedItem).Tag?.ToString() ?? "HKCU\\Run", SourceDisplay = ((ComboBoxItem)cmbS.SelectedItem).Content?.ToString() ?? "", Enabled = true }; DialogResult = true; Close(); };
            var cancel = new Button { Content = "Отмена", Width = 70, Height = 30, Background = new SolidColorBrush(Colors.Gray), Foreground = Brushes.White };
            cancel.Click += (s, ev) => { DialogResult = false; Close(); };
            btns.Children.Add(ok); btns.Children.Add(cancel);
            Grid.SetRow(btns, 3); g.Children.Add(btns);
            Content = g;
        }
    }

    public class ColorPaletteDialog : Window
    {
        public Color SelectedColor;
        public ColorPaletteDialog(Color initial)
        {
            Title = "Выбор цвета"; Width = 290; Height = 220; WindowStyle = WindowStyle.ToolWindow; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x2E)); SelectedColor = initial;
            var g = new Grid { Margin = new Thickness(10) };
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition());
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var prev = new Border { Height = 30, CornerRadius = new CornerRadius(4), Background = new SolidColorBrush(initial), BorderBrush = new SolidColorBrush(Colors.Gray), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 0, 8) };
            Grid.SetRow(prev, 0); g.Children.Add(prev);
            var wrap = new WrapPanel();
            var colors = new[] { Colors.Red, Colors.Orange, Colors.Yellow, Colors.Lime, Colors.Cyan, Colors.DodgerBlue, Colors.Magenta, Colors.HotPink, Colors.White, Colors.Gray, Color.FromRgb(0xFF,0x45,0x00), Color.FromRgb(0xAD,0xFF,0x2F) };
            foreach (var c in colors) { var b = new Button { Width = 32, Height = 32, Margin = new Thickness(3), Background = new SolidColorBrush(c), BorderBrush = new SolidColorBrush(Colors.Gray), BorderThickness = new Thickness(1), Cursor = Cursors.Hand }; b.Click += (s, e) => { SelectedColor = c; prev.Background = new SolidColorBrush(c); DialogResult = true; Close(); }; wrap.Children.Add(b); }
            Grid.SetRow(wrap, 1); g.Children.Add(wrap);
            var cancel = new Button { Content = "Отмена", Width = 70, Height = 26, Background = new SolidColorBrush(Colors.Gray), Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Right };
            cancel.Click += (s, e) => { DialogResult = false; Close(); };
            Grid.SetRow(cancel, 2); g.Children.Add(cancel);
            Content = g;
        }
    }
}