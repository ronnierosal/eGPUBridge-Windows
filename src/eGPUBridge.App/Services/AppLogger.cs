using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace eGPUBridge.App.Services;

public sealed class AppLogger : IEventLogger
{
    private readonly object _writeLock = new();
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AppLogger(string? logDirectory = null)
    {
        LogDirectory = logDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "eGPUBridge",
            "logs");
    }

    public string LogDirectory { get; }

    public void Info(string eventName, string message, object? data = null) =>
        Write("information", eventName, message, data);

    public void Error(string eventName, string message, Exception? exception = null, object? data = null) =>
        Write("error", eventName, message, new { data, exception = exception?.ToString() });

    private void Write(string level, string eventName, string message, object? data)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            var entry = new
            {
                timestamp = DateTimeOffset.UtcNow,
                level,
                eventName,
                message,
                data
            };
            var raw = JsonSerializer.Serialize(entry, _jsonOptions);
            var line = DiagnosticRedactor.RedactJson(raw, preserveDeviceInstances: true) + Environment.NewLine;
            var path = Path.Combine(LogDirectory, $"egpubridge-{DateTime.UtcNow:yyyy-MM-dd}.jsonl");

            lock (_writeLock)
            {
                File.AppendAllText(path, line);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"eGPUBridge logging failed: {ex}");
        }
    }
}
