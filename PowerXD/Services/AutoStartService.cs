using System;
using Microsoft.Win32;

namespace PowerXD.Services
{
    public static class AutoStartService
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "PowerXD";

        public static bool IsEnabled()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            return key?.GetValue(ValueName) != null;
        }

        public static void SetEnabled(bool enabled)
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true)
                            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, true);
            if (key == null) return;

            if (enabled)
            {
                key.SetValue(ValueName, ExecutablePath, RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(ValueName, false);
            }
        }

        private static string ExecutablePath =>
            Environment.ProcessPath ?? System.IO.Path.Combine(AppContext.BaseDirectory, "PowerXD.exe");
    }
}
