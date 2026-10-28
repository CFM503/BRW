using System;
using System.Windows.Forms;

namespace EBSBrowser
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            // 0. Clean orphaned/hung Java processes from previous crashed sessions
            NativeMethods.CleanZombieJavaProcesses();

            // 1. Force Java 2D software rendering and 512MB heap to prevent DirectDraw deadlocks / white screen
            Environment.SetEnvironmentVariable("_JAVA_OPTIONS", "-Dsun.java2d.noddraw=true -Dsun.java2d.d3d=false -Xmx512m", EnvironmentVariableTarget.Process);

            // 2. Enable Per-Monitor DPI awareness for sharp UI on high-res screens
            NativeMethods.EnableHighDpi();

            // 3. Configure IE11 emulation, DOM storage and popup features in registry
            WebBrowserFeatures.ConfigureRegistryFeatures();

            // 4. Start WinForms
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
