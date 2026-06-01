using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Timer = System.Timers.Timer;

namespace SmoothAutoRun.Services
{
    public class AnticheatDetector
    {
        public event Action<bool>? GameDetectedChanged;
        public bool IsGameRunning { get; private set; } = false;
        public string? DetectedAnticheat { get; private set; }

        private readonly Timer _checkTimer;
        private readonly List<string> _anticheatProcesses;

        public AnticheatDetector(List<string> anticheatProcesses)
        {
            _anticheatProcesses = anticheatProcesses.Select(p => p.ToLower()).ToList();
            _checkTimer = new Timer(3000);
            _checkTimer.Elapsed += CheckProcesses;
        }

        public void Start() => _checkTimer.Start();
        public void Stop() => _checkTimer.Stop();

        private void CheckProcesses(object? sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                var runningProcesses = Process.GetProcesses()
                    .Select(p => p.ProcessName.ToLower() + ".exe")
                    .ToList();

                var found = _anticheatProcesses.FirstOrDefault(ac => runningProcesses.Contains(ac));

                bool wasRunning = IsGameRunning;
                IsGameRunning = found != null;
                DetectedAnticheat = found;

                if (wasRunning != IsGameRunning)
                {
                    Logger.Log("Anticheat", IsGameRunning ? $"Game detected: {found}" : "Game exited");
                    GameDetectedChanged?.Invoke(IsGameRunning);
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Anticheat", $"Error: {ex.Message}");
            }
        }
    }
}