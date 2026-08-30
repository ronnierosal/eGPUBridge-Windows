using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace eGPUBridge.App.Services;

public static class DiagnosticRedactor
{
    private const string RedactedIp = "<ip-address>";
    private const string RedactedMac = "<mac-address>";
    private static readonly Regex Ipv4Pattern = new(
        @"(?<![0-9.])(?:\d{1,3}\.){3}\d{1,3}(?![0-9]|\.[0-9])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Ipv6Pattern = new(
        @"(?<![0-9A-Fa-f:%])(?=[0-9A-Fa-f:%]*:[0-9A-Fa-f:%]*:)[0-9A-Fa-f:]+(?:%[A-Za-z0-9._-]+)?(?![0-9A-Fa-f:%])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex MacPattern = new(
        @"(?i)(?<![0-9a-f])(?:[0-9a-f]{2}[:-]){5}[0-9a-f]{2}(?![0-9a-f])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex DeviceInterfaceInstancePattern = new(
        @"(?i)(\\\\\?\\(?:DISPLAY|PCI|USB|HID|SWD|ROOT)#[^#\s]+#)[^#\s]+(?=#\{)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex PnpDeviceInstancePattern = new(
        "(?i)\\b((?:PCI|USB|ROOT|SWD|HID|BTHENUM|DISPLAY)\\\\[^\\\\\\s\\\"']+\\\\)[^\\\\\\s\\\"']+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string Redact(
        string value,
        string? userName = null,
        string? machineName = null,
        string? userProfile = null,
        bool preserveDeviceInstances = false)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        userName ??= Environment.UserName;
        machineName ??= Environment.MachineName;
        userProfile ??= Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var redacted = value;
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            redacted = Regex.Replace(
                redacted,
                Regex.Escape(userProfile),
                "<user-profile>",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        redacted = ReplaceIdentity(redacted, machineName, "<machine>");
        redacted = ReplaceIdentity(redacted, userName, "<user>");
        redacted = MacPattern.Replace(redacted, RedactedMac);
        redacted = Ipv4Pattern.Replace(redacted, match =>
            IPAddress.TryParse(match.Value, out _) ? RedactedIp : match.Value);
        redacted = Ipv6Pattern.Replace(redacted, match =>
        {
            var candidate = match.Value.Split('%', 2)[0];
            return IPAddress.TryParse(candidate, out _) ? RedactedIp : match.Value;
        });
        if (!preserveDeviceInstances)
        {
            redacted = DeviceInterfaceInstancePattern.Replace(redacted, "$1<device-instance>");
            redacted = PnpDeviceInstancePattern.Replace(redacted, "$1<device-instance>");
        }

        return redacted;
    }

    public static string RedactJson(
        string json,
        string? userName = null,
        string? machineName = null,
        string? userProfile = null,
        bool preserveDeviceInstances = false)
    {
        var root = JsonNode.Parse(json);
        if (root is null)
        {
            return json;
        }

        RedactNode(root, userName, machineName, userProfile, preserveDeviceInstances);
        return root.ToJsonString(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    private static string ReplaceIdentity(string value, string? identity, string replacement)
    {
        if (string.IsNullOrWhiteSpace(identity) || identity.Length < 3)
        {
            return value;
        }

        return Regex.Replace(
            value,
            $@"(?<![\p{{L}}\p{{N}}_-]){Regex.Escape(identity)}(?![\p{{L}}\p{{N}}_-])",
            replacement,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static void RedactNode(
        JsonNode node,
        string? userName,
        string? machineName,
        string? userProfile,
        bool preserveDeviceInstances)
    {
        if (node is JsonObject jsonObject)
        {
            foreach (var property in jsonObject.ToArray())
            {
                if (property.Value is JsonValue value && value.TryGetValue<string>(out var text))
                {
                    jsonObject[property.Key] = Redact(
                        text,
                        userName,
                        machineName,
                        userProfile,
                        preserveDeviceInstances);
                }
                else if (property.Value is not null)
                {
                    RedactNode(property.Value, userName, machineName, userProfile, preserveDeviceInstances);
                }
            }
        }
        else if (node is JsonArray jsonArray)
        {
            for (var index = 0; index < jsonArray.Count; index++)
            {
                if (jsonArray[index] is JsonValue value && value.TryGetValue<string>(out var text))
                {
                    jsonArray[index] = Redact(
                        text,
                        userName,
                        machineName,
                        userProfile,
                        preserveDeviceInstances);
                }
                else if (jsonArray[index] is not null)
                {
                    RedactNode(jsonArray[index]!, userName, machineName, userProfile, preserveDeviceInstances);
                }
            }
        }
    }
}
