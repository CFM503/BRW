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

                // GPU rendering for smooth page rendering
                SetRegistryDword(baseKey + "FEATURE_GPU_RENDERING", processName, 1);

                // Enable standard XMLHTTP
                SetRegistryDword(baseKey + "FEATURE_XMLHTTP", processName, 1);

                // Zone elevation
                SetRegistryDword(baseKey + "FEATURE_ZONE_ELEVATION", processName, 1);

                // Allow ActiveX / Java plugins
                SetRegistryDword(baseKey + "FEATURE_RESTRICT_ACTIVEXINSTALL", processName, 0);
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
    }
}
