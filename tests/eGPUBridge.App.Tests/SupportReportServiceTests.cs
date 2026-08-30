using System.Text.Json;
using eGPUBridge.App.Models;
using eGPUBridge.App.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace eGPUBridge.App.Tests;

[TestClass]
public sealed class SupportReportServiceTests
{
    [TestMethod]
    public void ExportRedactedReport_IncludesUsefulStateWithoutLocalIdentifiers()
    {
        var root = Path.Combine(Path.GetTempPath(), "egpubridge-support-test-" + Guid.NewGuid());
        var logs = Path.Combine(root, "logs");
        var reports = Path.Combine(root, "reports");
        try
        {
            var logger = new AppLogger(logs);
            logger.Info("network.test", "Connected to 192.168.50.22.", new
            {
                mac = "AA:BB:CC:DD:EE:FF",
                profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            });
            var service = new SupportReportService(logger, new FakeDisplayService(), reports);

            var path = service.ExportRedactedReport();
            var text = File.ReadAllText(path);
            using var report = JsonDocument.Parse(text);

            Assert.IsTrue(report.RootElement.GetProperty("redacted").GetBoolean());
            Assert.AreEqual(1, report.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.IsFalse(text.Contains("192.168.50.22", StringComparison.Ordinal));
            Assert.IsFalse(text.Contains("AA:BB:CC:DD:EE:FF", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(text.Contains("5&1234&0&UID4352", StringComparison.OrdinalIgnoreCase));
            StringAssert.Contains(text, "device-instance");
            StringAssert.Contains(text, "VEN_1002");
            StringAssert.Contains(text, "DEV_7480");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private sealed class FakeDisplayService : IDisplayService
    {
        public DisplaySnapshot GetSnapshot() => new(
            DateTimeOffset.UtcNow,
            DisplayTopology.Extend,
            [
                new DisplayTarget(
                    "Living Room TV",
                    @"\\?\DISPLAY#GSM5B09#5&1234&0&UID4352#{4d36e96e-e325-11ce-bfc1-08002be10318}",
                    "123:456",
                    1,
                    DisplayConnectionKind.Hdmi,
                    IsInternal: false,
                    IsAvailable: true)
            ],
            [
                new GpuAdapter(
                    "AMD Radeon RX 7600M XT",
                    @"\\.\DISPLAY2",
                    @"PCI\VEN_1002&DEV_7480&SUBSYS_12341002",
                    IsPrimary: false,
                    IsAttachedToDesktop: true,
                    LikelyExternal: true)
            ]);

        public void ApplyTopology(DisplayTopology topology) => throw new NotSupportedException();
    }
}
