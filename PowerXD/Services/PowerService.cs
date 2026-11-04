using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PowerXD.Models;

namespace PowerXD.Services
{
    /// <summary>
    /// Reads and activates the power schemes exactly as shown in
    /// Control Panel → Power Options. The canonical source is `powercfg /list`
    /// (locale-independent GUID and active marker, localized names). No admin
    /// privileges are required for the standard schemes.
    /// </summary>
    public static class PowerService
    {
        static PowerService()
        {
            // .NET Core does not ship single-byte code pages by default;
            // register them so OEM encodings (e.g. CP866) are available.
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        // Matches a scheme line regardless of the Windows UI language, e.g.:
        //   "Power Scheme GUID: 381b4222-...  (Balanced) *"
        //   "GUID схемы питания: 381b4222-...  (Сбалансированная) *"
        private static readonly Regex SchemeLineRegex = new(
            @"([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})\s*\(([^)]*)\)\s*(\*)?",
            RegexOptions.Compiled);

        public static Task<List<PowerScheme>> GetSchemesAsync() => Task.Run(GetSchemes);

        public static Task<bool> SetActiveAsync(string guid) => Task.Run(() => SetActive(guid));

        private static List<PowerScheme> GetSchemes()
        {
            var list = new List<PowerScheme>();
            try
            {
                var psi = new ProcessStartInfo("powercfg", "/list")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };

                using var p = Process.Start(psi);
                if (p == null) return list;

                using var ms = new MemoryStream();
                p.StandardOutput.BaseStream.CopyTo(ms);
                p.StandardError.ReadToEnd();
                p.WaitForExit(5000);

                var text = Decode(ms.ToArray());

                foreach (var raw in text.Split('\n'))
                {
                    var m = SchemeLineRegex.Match(raw.Trim());
                    if (!m.Success) continue;

                    list.Add(new PowerScheme
                    {
                        Guid = m.Groups[1].Value,
                        Name = m.Groups[2].Value.Trim(),
                        IsActive = m.Groups[3].Success,
                    });
                }
            }
            catch
            {
                // Fall through to an empty list rather than crashing the UI.
            }

            list.Sort((a, b) =>
            {
                if (a.IsActive != b.IsActive) return a.IsActive ? -1 : 1;
                return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
            });

            return list;
        }

        private static string Decode(byte[] bytes)
        {
            // powercfg writes in the system's OEM code page (e.g. CP866 on ru-RU).
            int codepage = (int)GetOEMCP();
            var oem = Encoding.GetEncoding(codepage);
            return oem.GetString(bytes);
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern uint GetOEMCP();

        private static bool SetActive(string guid)
        {
            try
            {
                var psi = new ProcessStartInfo("powercfg", $"/setactive {guid}")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };

                using var p = Process.Start(psi);
                if (p == null) return false;

                p.StandardOutput.ReadToEnd();
                p.StandardError.ReadToEnd();
                p.WaitForExit(5000);
                return p.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
