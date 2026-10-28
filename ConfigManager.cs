using System;
using System.IO;

namespace EBSBrowser
{
    public class AppConfig
    {
        public string HomeUrl { get; set; }
        public int DefaultZoom { get; set; }
        public string AppTitle { get; set; }
        public int KeepAliveMinutes { get; set; }
        public bool AutoFillLogin { get; set; }
        public string SavedUsername { get; set; }
        public string SavedPasswordCipher { get; set; }

        public AppConfig()
        {
            HomeUrl = "https://portal.chinagasholdings.com/group/frontend";
            DefaultZoom = 100;
            AppTitle = "中国燃气 - Oracle EBS 专用工作台";
            KeepAliveMinutes = 3;
            AutoFillLogin = true;
            SavedUsername = "";
            SavedPasswordCipher = "";
        }
    }

    public static class ConfigManager
    {
        private static string ConfigPath
        {
            get
            {
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
            }
        }

        public static AppConfig Load()
        {
            AppConfig config = new AppConfig();
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string[] lines = File.ReadAllLines(ConfigPath, System.Text.Encoding.UTF8);
                    foreach (string line in lines)
                    {
                        string trimmed = line.Trim();
                        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith(";"))
                            continue;

                        int eqIdx = trimmed.IndexOf('=');
                        if (eqIdx > 0)
                        {
                            string key = trimmed.Substring(0, eqIdx).Trim();
                            string val = trimmed.Substring(eqIdx + 1).Trim();

                            if (key.Equals("HomeUrl", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(val))
                            {
                                config.HomeUrl = val;
                            }
                            else if (key.Equals("DefaultZoom", StringComparison.OrdinalIgnoreCase))
                            {
                                int z;
                                if (int.TryParse(val, out z) && z >= 50 && z <= 300)
                                {
                                    config.DefaultZoom = z;
                                }
                            }
                            else if (key.Equals("AppTitle", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(val))
                            {
                                config.AppTitle = val;
                            }
                            else if (key.Equals("KeepAliveMinutes", StringComparison.OrdinalIgnoreCase))
                            {
                                int km;
                                if (int.TryParse(val, out km) && km >= 1 && km <= 60)
                                {
                                    config.KeepAliveMinutes = km;
                                }
                            }
                            else if (key.Equals("AutoFillLogin", StringComparison.OrdinalIgnoreCase))
                            {
                                bool af;
                                if (bool.TryParse(val, out af))
                                {
                                    config.AutoFillLogin = af;
                                }
                            }
                            else if (key.Equals("SavedUsername", StringComparison.OrdinalIgnoreCase))
                            {
                                config.SavedUsername = val;
                            }
                            else if (key.Equals("SavedPasswordCipher", StringComparison.OrdinalIgnoreCase))
                            {
                                config.SavedPasswordCipher = val;
                            }
                        }
                    }
                }
                else
                {
                    Save(config);
                }
            }
            catch
            {
            }
            return config;
        }

        public static void Save(AppConfig config)
        {
            try
            {
                string content = "# Oracle EBS Dedicated Browser Configuration\r\n" +
                                 "AppTitle=" + config.AppTitle + "\r\n" +
                                 "HomeUrl=" + config.HomeUrl + "\r\n" +
                                 "DefaultZoom=" + config.DefaultZoom + "\r\n" +
                                 "KeepAliveMinutes=" + config.KeepAliveMinutes + "\r\n" +
                                 "AutoFillLogin=" + config.AutoFillLogin.ToString().ToLower() + "\r\n" +
                                 "SavedUsername=" + config.SavedUsername + "\r\n" +
                                 "SavedPasswordCipher=" + config.SavedPasswordCipher + "\r\n";
                File.WriteAllText(ConfigPath, content, System.Text.Encoding.UTF8);
            }
            catch
            {
            }
        }
    }
}
