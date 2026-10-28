using System;
using System.IO;

namespace EBSBrowser
{
    public class AppConfig
    {
        public string HomeUrl { get; set; }
        public int DefaultZoom { get; set; }
        public string AppTitle { get; set; }

        public AppConfig()
        {
            HomeUrl = "https://portal.chinagasholdings.com/group/frontend";
            DefaultZoom = 100;
            AppTitle = "中国燃气 - Oracle EBS 专用工作台";
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
                                 "DefaultZoom=" + config.DefaultZoom + "\r\n";
                File.WriteAllText(ConfigPath, content, System.Text.Encoding.UTF8);
            }
            catch
            {
            }
        }
    }
}
