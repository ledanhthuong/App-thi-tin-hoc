using System;
using System.IO;
using System.Text.Json;

namespace App_thi_tin_hoc.Helpers
{
    public class CompilerSettings
    {
        public string GppPath { get; set; } = "";
        public string PythonPath { get; set; } = "";
    }

    public static class CompilerSettingsHelper
    {
        private static readonly string SettingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "compiler_settings.json");
        private static readonly object FileLock = new object();

        public static CompilerSettings GetSettings()
        {
            lock (FileLock)
            {
                try
                {
                    if (File.Exists(SettingsFilePath))
                    {
                        var json = File.ReadAllText(SettingsFilePath);
                        var settings = JsonSerializer.Deserialize<CompilerSettings>(json);
                        if (settings != null)
                        {
                            return settings;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log or handle error, return default
                    Console.WriteLine($"Error reading compiler settings: {ex.Message}");
                }
                return new CompilerSettings();
            }
        }

        public static bool SaveSettings(CompilerSettings settings)
        {
            lock (FileLock)
            {
                try
                {
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    var json = JsonSerializer.Serialize(settings, options);
                    File.WriteAllText(SettingsFilePath, json);
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error saving compiler settings: {ex.Message}");
                    return false;
                }
            }
        }
    }
}
