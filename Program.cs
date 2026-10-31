using System;
using System.IO;
using System.Windows.Forms;

namespace EBSBrowser
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // Set up global crash catchers to prevent silent crashes and record diagnostic logs
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate (object sender, System.Threading.ThreadExceptionEventArgs e)
            {
                LogCrash("ThreadException", e.Exception);
            };

            AppDomain.CurrentDomain.UnhandledException += delegate (object sender, UnhandledExceptionEventArgs e)
            {
                LogCrash("AppDomainUnhandledException", e.ExceptionObject as Exception);
            };

            // 0. Install Vectored Exception Handler and ExitProcess protector
            NativeMethods.InstallNativeCrashHandler();
            NativeMethods.Log("Startup", "EBS Dedicated Browser initializing (x86)...");

            // 1. Disable DEP for this 32-bit process so Java 6 JIT & native thunks execute safely
            NativeMethods.DisableDEP();

            // 2. Clean orphaned/hung Java processes from previous crashed sessions
            NativeMethods.CleanZombieJavaProcesses();

            // 3. Ensure Java 6 bin and client runtime folders are first in process PATH
            try
            {
                string javaBin = @"C:\Program Files (x86)\Java\jre6\bin";
                string javaClient = @"C:\Program Files (x86)\Java\jre6\bin\client";
                string curPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process) ?? "";
                Environment.SetEnvironmentVariable("PATH", javaBin + ";" + javaClient + ";" + curPath, EnvironmentVariableTarget.Process);
            }
            catch {}

            // 4. Force Java 2D software rendering, disable DPI awareness, and set 256MB heap
            string javaOpts = "-Dsun.java2d.noddraw=true -Dsun.java2d.d3d=false -Dsun.java2d.dpiaware=false -Xmx256m -Xms64m";
            Environment.SetEnvironmentVariable("_JAVA_OPTIONS", javaOpts, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("JAVA_TOOL_OPTIONS", javaOpts, EnvironmentVariableTarget.Process);

            // 5. Enable System DPI awareness for sharp UI on high-res laptop screens without PerMonitorV2 crashes
            NativeMethods.EnableHighDpi();

            // 6. Configure IE11 emulation, DOM storage, Edge redirection prevention, and Java plug-in settings
            WebBrowserFeatures.ConfigureRegistryFeatures();

            // 7. Start WinForms
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            string startUrl = (args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0])) ? args[0] : null;
            Application.Run(new MainForm(startUrl));
        }

        private static void LogCrash(string category, Exception ex)
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
                string log = string.Format("[{0:yyyy-MM-dd HH:mm:ss}] {1}: {2}\r\n", DateTime.Now, category, ex != null ? ex.ToString() : "Unknown exception");
                File.AppendAllText(logPath, log);
                NativeMethods.Log("ManagedException", log);
            }
            catch
            {
            }
        }
    }
}
