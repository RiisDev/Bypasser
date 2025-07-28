using System.Text;
using System.Text.RegularExpressions;

namespace Bypasser.Modules
{
    public static class Formatting
    {
        public static string FormatBytes(double bytes)
        {
            switch (bytes)
            {
                case < 0:
                case 0:
                    return "0B";
            }

            const double kb = 1024;
            const double mb = kb * 1024;
            const double gb = mb * 1024;
            const double tb = gb * 1024;

            return bytes switch
            {
                < kb => $"{bytes:F0}B",
                < mb => $"{Math.Round(bytes / kb, 2)}KB",
                < gb => $"{Math.Round(bytes / mb, 2)}MB",
                < tb => $"{Math.Round(bytes / gb, 2)}GB",
                _ => $"{Math.Round(bytes / tb, 2)}TB"
            };
        }

        public static double ParseFormattedBytes(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                throw new ArgumentException("Input cannot be null or empty.", nameof(input));

            input = input.Trim().ToUpperInvariant();

            Match match = Regex.Match(input, @"^\s*(\d+(\.\d+)?)\s*(B|KB|MB|GB|TB)\s*$");

            if (!match.Success)
                throw new FormatException($"Input '{input}' is not in a valid format.");

            double value = double.Parse(match.Groups[1].Value);
            string unit = match.Groups[3].Value;

            Dictionary<string, double> units = new()
            {
                { "B", 1 },
                { "KB", 1024 },
                { "MB", 1024 * 1024 },
                { "GB", 1024 * 1024 * 1024 },
                { "TB", 1024.0 * 1024 * 1024 * 1024 }
            };

            return value * units[unit];
        }

        public static string ToBase64(this string value) => Convert.ToBase64String(Encoding.ASCII.GetBytes(value))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        public static string FromBase64(this string input)
        {
            string base64 = input.Replace('-', '+').Replace('_', '/');
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }
            return Encoding.UTF8.GetString(Convert.FromBase64String(base64));

        }
    }
}
