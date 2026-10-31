using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace EBSBrowser
{
    internal static class WebBrowserFeatures
    {
        public static void ConfigureRegistryFeatures()
        {
            try
            {
                string processName = Process.GetCurrentProcess().ProcessName + ".exe";
                string baseKey = @"Software\Microsoft\Internet Explorer\Main\FeatureControl\";

                // 11001 (0x2AF9) = Internet Explorer 11 Standards mode (required for modern portals & Java Applets)
                SetRegistryDword(baseKey + "FEATURE_BROWSER_EMULATION", processName, 11001);

                // Enable HTML5 DOM Storage (sessionStorage, localStorage)
                SetRegistryDword(baseKey + "FEATURE_DOMSTORAGE", processName, 1);

                // Disable popup blocking in the WebBrowser control for EBS popups
                SetRegistryDword(baseKey + "FEATURE_WEBOC_POPUPMANAGEMENT", processName, 0);

                // Maximize AJAX parallel connections
                SetRegistryDword(baseKey + "FEATURE_AJAX_CONNECTION_LIMIT", processName, 10);

                // Optimization for child window rendering (Oracle Forms)
                SetRegistryDword(baseKey + "FEATURE_ENABLE_CLIPCHILDREN_OPTIMIZATION", processName, 1);

                // Prevent memory leaks in script loops
                SetRegistryDword(baseKey + "FEATURE_MANAGE_SCRIPT_CIRCULAR_REFS", processName, 1);

                // MUST BE 0: Disable GPU hardware acceleration in WebBrowser.
                // On laptops with dual/hybrid graphics (Intel Iris Xe + NVIDIA) and Java 6 software rendering (-Dsun.java2d.noddraw=true),
                // forcing GPU rendering causes Direct3D device resets and instant process crashes when loading Java applets.
                SetRegistryDword(baseKey + "FEATURE_GPU_RENDERING", processName, 0);

                // Enable standard XMLHTTP
                SetRegistryDword(baseKey + "FEATURE_XMLHTTP", processName, 1);

                // Zone elevation
                SetRegistryDword(baseKey + "FEATURE_ZONE_ELEVATION", processName, 1);

                // Allow ActiveX / Java plugins
                SetRegistryDword(baseKey + "FEATURE_RESTRICT_ACTIVEXINSTALL", processName, 0);

                // Allow Java plugin add-on without being blocked by IE add-on manager
                SetRegistryDword(baseKey + "FEATURE_ADDON_MANAGEMENT", processName, 0);

                // Prevent blocking cross-protocol redirects from EBS OAF to Forms servlet
                SetRegistryDword(baseKey + "FEATURE_BLOCK_CROSS_PROTOCOL_DETECT", processName, 0);

                // Allow file downloads (reports, excel exports)
                SetRegistryDword(baseKey + "FEATURE_RESTRICT_FILEDOWNLOAD", processName, 0);

                // Suppress yellow security band that blocks ActiveX execution
                SetRegistryDword(baseKey + "FEATURE_SECURITYBAND", processName, 0);

                // Prevent local machine lockdown
                SetRegistryDword(baseKey + "FEATURE_LOCALMACHINE_LOCKDOWN", processName, 0);

                // -------------------------------------------------------------
                // 1. Prevent Windows 11 Edge BHO from hijacking / terminating IE
                // -------------------------------------------------------------
                // Disable IE to Edge redirection in user policies
                SetRegistryDword(@"Software\Policies\Microsoft\Edge", "RedirectSitesFromInternetExplorerRedirectMode", 0);
                SetRegistryDword(@"Software\Policies\Microsoft\Edge", "RedirectSitesFromInternetExplorerPreventBHOInstall", 1);

                // Disable IEToEdge BHO extension ({1FD49718-1D00-4B19-AF5F-070AF6D5D54C})
                SetRegistryDword(@"Software\Microsoft\Windows\CurrentVersion\Ext\Settings\{1FD49718-1D00-4B19-AF5F-070AF6D5D54C}", "Flags", 1);

                // -------------------------------------------------------------
                // 2. Configure Java 6 Plug-in Compatibility
                // -------------------------------------------------------------
                // Next-Generation Java Plug-in (UseNewJavaPlugin=1) runs JVM in isolated jp2launcher.exe
                // process without memory collision from .NET CLR and MSHTML in 32-bit address space.
                string[] javaKeyPaths = new string[]
                {
                    @"Software\JavaSoft\Java Plug-in",
                    @"Software\AppDataLow\Software\JavaSoft\Java Plug-in",
                    @"Software\WOW6432Node\JavaSoft\Java Plug-in",
                    @"Software\JavaSoft\Java Plug-in\1.6.0_27",
                    @"Software\AppDataLow\Software\JavaSoft\Java Plug-in\1.6.0_27",
                    @"Software\WOW6432Node\JavaSoft\Java Plug-in\1.6.0_27"
                };
                foreach (string key in javaKeyPaths)
                {
                    SetRegistryDword(key, "UseNewJavaPlugin", 1);
                    SetRegistryDword(key, "UseJava2IExplorer", 1);
                }

                // -------------------------------------------------------------
                // 3. Add China Gas domains to IE Trusted Sites (Zone 2)
                // -------------------------------------------------------------
                string zoneDomains = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings\ZoneMap\Domains\chinagasholdings.com";
                SetRegistryDword(zoneDomains, "http", 2);
                SetRegistryDword(zoneDomains, "https", 2);
                SetRegistryDword(zoneDomains, "*", 2);

                SetRegistryDword(zoneDomains + @"\ebs", "http", 2);
                SetRegistryDword(zoneDomains + @"\ebs", "https", 2);
                SetRegistryDword(zoneDomains + @"\portal", "http", 2);
                SetRegistryDword(zoneDomains + @"\portal", "https", 2);

                // Zone 2 permissions: Enable ActiveX & scripting, disable Protected Mode
                string zone2 = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings\Zones\2";
                SetRegistryDword(zone2, "1200", 0); // Run ActiveX controls and plugins
                SetRegistryDword(zone2, "1201", 0); // Initialize and script ActiveX controls
                SetRegistryDword(zone2, "1400", 0); // Active scripting
                SetRegistryDword(zone2, "1402", 0); // Script ActiveX controls not marked safe
                SetRegistryDword(zone2, "1004", 0); // Download unsigned ActiveX controls
                SetRegistryDword(zone2, "2000", 0); // Binary and script behaviors
                SetRegistryDword(zone2, "2200", 0); // Download signed ActiveX controls
                SetRegistryDword(zone2, "2500", 3); // Protected Mode: Disabled
                SetRegistryDword(zone2, "1C00", 0x00020000); // Java Permissions: Low Safety (Full execution)

                // -------------------------------------------------------------
                // 4. Set Windows 7 Compatibility Layer for Java 6 and EBSBrowser
                // -------------------------------------------------------------
                string layersKey = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";
                try
                {
                    string exePath = Process.GetCurrentProcess().MainModule.FileName;
                    SetRegistryString(layersKey, exePath, "~ WIN7RTM");
                }
                catch {}
                SetRegistryString(layersKey, @"C:\Program Files (x86)\Java\jre6\bin\jp2launcher.exe", "~ WIN7RTM");
                SetRegistryString(layersKey, @"C:\Program Files (x86)\Java\jre6\bin\javaw.exe", "~ WIN7RTM");
                SetRegistryString(layersKey, @"C:\Program Files (x86)\Java\jre6\bin\java.exe", "~ WIN7RTM");
            }
            catch (Exception ex)
            {
                // Registry configuration failure shouldn't crash the browser
                Debug.WriteLine("ConfigureRegistryFeatures error: " + ex.Message);
            }
        }

        private static void SetRegistryDword(string subKeyPath, string valueName, int value)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(subKeyPath, RegistryKeyPermissionCheck.ReadWriteSubTree))
                {
                    if (key != null)
                    {
                        key.SetValue(valueName, value, RegistryValueKind.DWord);
                    }
                }
            }
            catch
            {
            }
        }

        private static void SetRegistryString(string subKeyPath, string valueName, string value)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(subKeyPath, RegistryKeyPermissionCheck.ReadWriteSubTree))
                {
                    if (key != null)
                    {
                        key.SetValue(valueName, value, RegistryValueKind.String);
                    }
                }
            }
            catch
            {
            }
        }
    }
}
