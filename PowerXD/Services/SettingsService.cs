using System;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace PowerXD.Services
{
    public sealed class SettingsService
    {
        public static SettingsService Instance { get; } = new SettingsService();

        /// <summary>"auto" | "ru" | "en"</summary>
        public string Language { get; set; } = "auto";
        public bool FirstRun { get; set; } = true;

        public string ResolvedLanguage
        {
            get
            {
                if (string.Equals(Language, "ru", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(Language, "en", StringComparison.OrdinalIgnoreCase))
                {
                    return Language.ToLowerInvariant();
                }

                var two = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                return string.Equals(two, "ru", StringComparison.OrdinalIgnoreCase) ? "ru" : "en";
            }
        }

        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PowerXD",
            "settings.json");

        public void Load()
        {
            if (!File.Exists(FilePath)) return;
            var data = JsonSerializer.Deserialize<SettingsData>(File.ReadAllText(FilePath));
            if (data == null) return;
            Language = string.IsNullOrWhiteSpace(data.Language) ? "auto" : data.Language;
            FirstRun = data.FirstRun;
        }

        public void Save()
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var data = new SettingsData { Language = Language, FirstRun = FirstRun };
            File.WriteAllText(
                FilePath,
                JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        }

        private sealed class SettingsData
        {
            public string Language { get; set; } = "auto";
            public bool FirstRun { get; set; } = true;
        }
    }
}
