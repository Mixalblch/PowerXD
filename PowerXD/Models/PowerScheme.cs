using System.ComponentModel;
using System.Windows.Media;

namespace PowerXD.Models
{
    public sealed class PowerScheme : INotifyPropertyChanged
    {
        private bool _isActive;
        private Brush? _colorBrush;

        public string Guid { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive == value) return;
                _isActive = value;
                OnPropertyChanged(nameof(IsActive));
            }
        }

        public string ColorHex => Guid.ToLowerInvariant() switch
        {
            "a1841308-3541-4fab-bc81-f71556f20b4a" => "#34C759", // Power saver
            "381b4222-f694-41f0-9685-ff5bb260df2e" => "#68B7FF", // Balanced
            "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c" => "#FF5A5F", // High performance
            "e9a42b02-d5df-448d-aa00-03f14749eb61" => "#FF5A5F", // Ultimate performance
            _ => "#9AA0AE",
        };

        public Brush ColorBrush =>
            _colorBrush ??= new SolidColorBrush((Color)ColorConverter.ConvertFromString(ColorHex));

        public System.Drawing.Color AccentColor =>
            System.Drawing.ColorTranslator.FromHtml(ColorHex);

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
