using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace DISTestKit.Model
{
    public class DisPacket
    {
        public int No { get; set; }
        public DateTime Time { get; set; }
        public string PDUType { get; set; } = string.Empty;
        public string Type => PDUType;
        public string Source { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public string Protocol { get; set; } = string.Empty;
        public int Length { get; set; }
        public Dictionary<string, object> Details { get; set; } = new Dictionary<string, object>();
        public IEnumerable<KeyValuePair<string, string>> DisplayDetails =>
            Details
                .Where(kv =>
                    kv.Key != "timestampEpoch"
                    && kv.Key != "timestamp"
                    && kv.Key != "timePastHour"
                    && kv.Key != "hour"
                )
                .Select(kv =>
                {
                    var rawKey = kv.Key;
                    string prettyKey = rawKey.Equals(
                        "timestampHuman",
                        StringComparison.OrdinalIgnoreCase
                    )
                        ? "Timestamp"
                        : ToHumanName(rawKey);

                    string prettyVal = rawKey.Equals(
                        "timestampHuman",
                        StringComparison.OrdinalIgnoreCase
                    )
                        ? FormatTimestampHuman(kv.Value?.ToString())
                        : FormatValue(rawKey, kv.Value);

                    return new KeyValuePair<string, string>(prettyKey, prettyVal);
                })
                .OrderBy(kv => kv.Key.Split(' ')[0])
                .ThenBy(kv => kv.Key);

        private static string ToHumanName(string rawKey)
        {
            var spaced = Regex.Replace(rawKey, "(?<!^)([A-Z])", " $1");
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(spaced);
        }

        private static string FormatValue(string key, object? val)
        {
            if (val == null)
                return "";

            if (
                key.Equals("timestampEpoch", StringComparison.OrdinalIgnoreCase)
                && long.TryParse(val.ToString(), out var epoch)
            )
            {
                var dt = DateTimeOffset.FromUnixTimeSeconds(epoch).LocalDateTime;
                return dt.ToString("yyyy-MM-dd HH:mm:ss");
            }
            return val.ToString()!;
        }

        private static string FormatTimestampHuman(string? raw)
        {
            if (string.IsNullOrEmpty(raw))
                return "";

            if (DateTimeOffset.TryParse(raw, out var dto))
                return dto.ToLocalTime().ToString("dd-MM-yyyy HH:mm:ss");
            return raw.TrimEnd('Z').Replace('T', ' ');
        }
    }
}
