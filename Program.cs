using System;

namespace SmoothAutoRun
{
    public class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            bool startMinimized = false;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--minimized")
                    startMinimized = true;
            }

            if (startMinimized)
                Environment.SetEnvironmentVariable("SMOOTHAUTORUN_START_MINIMIZED", "true");

            var app = new App();
            app.InitializeComponent();
            app.Run();
        }
    }
}