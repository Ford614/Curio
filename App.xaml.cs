using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Curio.Services;
using Curio.Test;

namespace Curio
{
    public partial class App : Application
    {
        public static List<string> StartupFilePaths { get; } = new();
        public static AppSettings Settings { get; private set; } = new();

        protected override void OnStartup(StartupEventArgs e)
        {
            // Load saved settings
            Settings = AppSettings.Load();
            ImportService.CleanTempExtracts();

            // Apply saved UI style and Theme on startup
            StyleManager.Apply(Settings.UIStyle, Settings.Theme, Settings.Language);

            if (e.Args.Contains("--test", StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    TestRunner.RunTests();
                    Shutdown(0);
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Test Failure] {ex}");
                    Shutdown(1);
                    return;
                }
            }

            // Capture startup file paths passed via "Open With", Drag & Drop to EXE, or Command Line
            foreach (var arg in e.Args)
            {
                if (File.Exists(arg) || Directory.Exists(arg))
                {
                    StartupFilePaths.Add(arg);
                }
            }

            base.OnStartup(e);
        }
    }
}
