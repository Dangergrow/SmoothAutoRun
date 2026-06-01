using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SmoothAutoRun.Services
{
    public class VolumeService : IDisposable
    {
        private IntPtr _hookId = IntPtr.Zero;
        private NativeMethods.LowLevelMouseProc? _proc;
        private bool _isRunning = false;
        private bool _isProcessing = false;

        public void Start()
        {
            if (_isRunning) return;

            _proc = HookCallback;
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule!)
            {
                _hookId = NativeMethods.SetWindowsHookEx(
                    NativeMethods.WH_MOUSE_LL, _proc,
                    NativeMethods.GetModuleHandle(curModule.ModuleName!), 0);
            }

            _isRunning = true;
            Logger.Log("Volume", "Started");
        }

        public void Stop()
        {
            if (!_isRunning) return;

            if (_hookId != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }

            _isRunning = false;
            Logger.Log("Volume", "Stopped");
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (_isProcessing) return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);

            if (nCode >= 0 && wParam == (IntPtr)NativeMethods.WM_MOUSEWHEEL)
            {
                var hookStruct = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                
                // Проверяем, что курсор над панелью задач
                if (IsCursorOverTaskbar(hookStruct.pt_x, hookStruct.pt_y))
                {
                    int delta = (short)((hookStruct.mouseData >> 16) & 0xFFFF);
                    _isProcessing = true;
                    
                    // Изменяем громкость
                    int volumeChange = delta > 0 ? 2 : -2; // 2% за шаг
                    ChangeVolume(volumeChange);
                    
                    _isProcessing = false;
                    return (IntPtr)1; // Блокируем оригинальный скролл
                }
            }

            return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private bool IsCursorOverTaskbar(int x, int y)
        {
            // Получаем размеры панели задач
            IntPtr taskbarHandle = NativeMethods.FindWindow("Shell_TrayWnd", null);
            if (taskbarHandle == IntPtr.Zero) return false;

            NativeMethods.RECT rect;
            NativeMethods.GetWindowRect(taskbarHandle, out rect);

            return x >= rect.Left && x <= rect.Right && y >= rect.Top && y <= rect.Bottom;
        }

        private void ChangeVolume(int delta)
        {
            try
            {
                // Отправляем нажатие клавиш громкости
                int keyCode = delta > 0 ? NativeMethods.VK_VOLUME_UP : NativeMethods.VK_VOLUME_DOWN;
                int times = Math.Abs(delta) / 2;

                for (int i = 0; i < times; i++)
                {
                    NativeMethods.keybd_event((byte)keyCode, 0, 0, 0);
                    NativeMethods.keybd_event((byte)keyCode, 0, NativeMethods.KEYEVENTF_KEYUP, 0);
                }
            }
            catch { }
        }

        public void Dispose() => Stop();

        private static class NativeMethods
        {
            public const int WH_MOUSE_LL = 14;
            public const int WM_MOUSEWHEEL = 0x020A;
            public const int VK_VOLUME_UP = 0xAF;
            public const int VK_VOLUME_DOWN = 0xAE;
            public const uint KEYEVENTF_KEYUP = 0x0002;

            public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

            [DllImport("user32.dll")] public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);
            [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr hhk);
            [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
            [DllImport("kernel32.dll")] public static extern IntPtr GetModuleHandle(string lpModuleName);
            [DllImport("user32.dll")] public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);
            [DllImport("user32.dll")] public static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);
            [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

            [StructLayout(LayoutKind.Sequential)]
            public struct MSLLHOOKSTRUCT
            {
                public int pt_x;
                public int pt_y;
                public uint mouseData;
                public uint flags;
                public uint time;
                public IntPtr dwExtraInfo;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct RECT
            {
                public int Left, Top, Right, Bottom;
            }
        }
    }
}