using System;
using System.Linq;
using System.Windows;
using Curio.Test;

namespace Curio
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
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

            base.OnStartup(e);
        }
    }
}
