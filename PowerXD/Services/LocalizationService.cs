using System;
using System.Collections.Generic;

namespace PowerXD.Services
{
    public sealed class LocalizationService
    {
        public static LocalizationService Instance { get; } = new LocalizationService();

        private readonly Dictionary<string, Dictionary<string, string>> _data = new();
        private string _current = "en";

        public string Current => _current;

        private LocalizationService()
        {
            _data["en"] = new Dictionary<string, string>
            {
                ["SelectMode"] = "Select power mode",
                ["RunAtStartup"] = "Run at startup",
                ["Language"] = "Language",
                ["Exit"] = "Exit",
                ["AppStarted"] = "PowerXD is running in the system tray. Click the icon to switch the power mode.",
                ["AlreadyRunning"] = "PowerXD is already running.",
                ["SwitchFailed"] = "Could not switch the power scheme.",
            };

            _data["ru"] = new Dictionary<string, string>
            {
                ["SelectMode"] = "Выберите режим питания",
                ["RunAtStartup"] = "Запускать вместе с Windows",
                ["Language"] = "Язык",
                ["Exit"] = "Выход",
                ["AppStarted"] = "PowerXD работает в системном трее. Нажмите на иконку, чтобы переключить режим питания.",
                ["AlreadyRunning"] = "PowerXD уже запущен.",
                ["SwitchFailed"] = "Не удалось переключить схему питания.",
            };
        }

        public void SetLanguage(string lang)
        {
            _current = string.Equals(lang, "ru", StringComparison.OrdinalIgnoreCase) ? "ru" : "en";
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }

        public string T(string key) =>
            _data.TryGetValue(_current, out var table) && table.TryGetValue(key, out var value)
                ? value
                : key;

        public event EventHandler? LanguageChanged;
    }
}
