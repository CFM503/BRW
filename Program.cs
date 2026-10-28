using System;
using System.Windows.Forms;

namespace EBSBrowser
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            // 1. Enable Per-Monitor DPI awareness for sharp UI on high-res screens
            NativeMethods.EnableHighDpi();

            // 2. Configure IE11 emulation, DOM storage and popup features in registry
            WebBrowserFeatures.ConfigureRegistryFeatures();

            // 3. Start WinForms
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
