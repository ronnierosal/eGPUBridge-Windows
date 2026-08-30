using System.Reflection;
using System.Runtime.InteropServices;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace eGPUBridge.App.Services;

public sealed class SupportReportService
{
    private const int MaximumLogEntries = 500;
    private const int MaximumLogFiles = 3;
    private readonly AppLogger _logger;
    private readonly IDisplayService _displayService;

    public SupportReportService(
        AppLogger logger,
        IDisplayService displayService,
        string? reportDirectory = null)
    {
        _logger = logger;
        _displayService = displayService;
        ReportDirectory = reportDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "eGPUBridge",
            "support");
    }

    public string ReportDirectory { get; }

    public string ExportRedactedReport()
    {
        Directory.CreateDirectory(ReportDirectory);

        object? snapshot = null;
        string? snapshotError = null;
        try
        {
            snapshot = _displayService.GetSnapshot();
        }
        catch (Exception ex)
        {
            snapshotError = ex.Message;
        }

        var logFiles = Directory.Exists(_logger.LogDirectory)
            ? Directory.EnumerateFiles(_logger.LogDirectory, "egpubridge-*.jsonl")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .Take(MaximumLogFiles)
                .ToArray()
            : [];
        var recentEntries = ReadRecentEntries(logFiles);

        var report = new
        {
            schemaVersion = 1,
            generatedAt = DateTimeOffset.UtcNow,
            redacted = true,
            application = new
            {
                version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown",
                framework = RuntimeInformation.FrameworkDescription,
                architecture = RuntimeInformation.ProcessArchitecture.ToString()
            },
            system = new
            {
                operatingSystem = RuntimeInformation.OSDescription,
                machine = Environment.MachineName
            },
            snapshot,
            snapshotError,
            sourceLogFiles = logFiles.Select(Path.GetFileName).ToArray(),
            recentLogEntries = recentEntries
        };

        var raw = JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });
        var safe = DiagnosticRedactor.RedactJson(raw);
        var output = Path.Combine(
            ReportDirectory,
            $"egpubridge-support-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.json");
        File.WriteAllText(output, safe);

        _logger.Info("support.report.created", "Created a redacted support report.", new
        {
            fileName = Path.GetFileName(output),
            logEntryCount = recentEntries.Count,
            snapshotAvailable = snapshot is not null
        });
        return output;
    }

    private static IReadOnlyList<JsonNode?> ReadRecentEntries(IEnumerable<string> logFiles)
    {
        var lines = new List<string>();
        foreach (var file in logFiles.Reverse())
        {
            try
            {
                lines.AddRange(File.ReadLines(file).Where(line => !string.IsNullOrWhiteSpace(line)));
            }
            catch (IOException)
            {
                // A live log can rotate or be briefly unavailable. The report remains useful.
            }
            catch (UnauthorizedAccessException)
            {
                // Preserve a partial report instead of failing the entire export.
            }
        }

        return lines
            .TakeLast(MaximumLogEntries)
            .Select(line =>
            {
                try
                {
                    return JsonNode.Parse(DiagnosticRedactor.RedactJson(line));
                }
                catch (JsonException)
                {
                    return JsonValue.Create(DiagnosticRedactor.Redact(line));
                }
            })
            .ToArray();
    }
}
