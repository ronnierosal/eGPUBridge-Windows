using System.Text.Json;
using eGPUBridge.App.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace eGPUBridge.App.Tests;

[TestClass]
public sealed class DiagnosticRedactorTests
{
    [TestMethod]
    public void Redact_RemovesLocalIdentifiersAndPreservesPciIdentity()
    {
        const string input = @"ALLY-X Ronnie C:\Users\Ronnie\report 192.168.50.22 fe80::1234%12 AA:BB:CC:DD:EE:FF \\?\DISPLAY#GSM5B09#5&1234&0&UID4352#{4d36e96e-e325-11ce-bfc1-08002be10318} PCI\VEN_1002&DEV_7480&SUBSYS_12341002";

        var result = DiagnosticRedactor.Redact(
            input,
            userName: "Ronnie",
            machineName: "ALLY-X",
            userProfile: @"C:\Users\Ronnie");

        StringAssert.DoesNotContain(result, "ALLY-X");
        StringAssert.DoesNotContain(result, "Ronnie");
        StringAssert.DoesNotContain(result, "192.168.50.22");
        StringAssert.DoesNotContain(result, "fe80::1234");
        StringAssert.DoesNotContain(result, "AA:BB:CC:DD:EE:FF");
        StringAssert.DoesNotContain(result, "5&1234&0&UID4352");
        StringAssert.Contains(result, "DISPLAY#GSM5B09#<device-instance>");
        StringAssert.Contains(result, @"PCI\VEN_1002&DEV_7480&SUBSYS_12341002");
    }

    [TestMethod]
    public void RedactJson_PreservesJsonTypesAndSanitizesNestedStrings()
    {
        const string input = """
            {
              "count": 3,
              "nested": {
                "path": "C:\\Users\\Ronnie\\report",
                "address": "10.0.0.8"
              }
            }
            """;

        var result = DiagnosticRedactor.RedactJson(
            input,
            userName: "Ronnie",
            machineName: "ALLY-X",
            userProfile: @"C:\Users\Ronnie");
        using var document = JsonDocument.Parse(result);

        Assert.AreEqual(3, document.RootElement.GetProperty("count").GetInt32());
        Assert.AreEqual(
            @"<user-profile>\report",
            document.RootElement.GetProperty("nested").GetProperty("path").GetString());
        Assert.AreEqual(
            "<ip-address>",
            document.RootElement.GetProperty("nested").GetProperty("address").GetString());
    }

    [TestMethod]
    public void AppLogger_RedactsBeforeWritingJsonLines()
    {
        var root = Path.Combine(Path.GetTempPath(), "egpubridge-logger-test-" + Guid.NewGuid());
        try
        {
            var logger = new AppLogger(root);
            logger.Info("test.event", "Diagnostic event.", new
            {
                path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "private"),
                machine = Environment.MachineName,
                ip = "192.168.50.22",
                mac = "AA:BB:CC:DD:EE:FF",
                pci = @"PCI\VEN_1002&DEV_7480"
            });

            var text = File.ReadAllText(Directory.GetFiles(root, "*.jsonl").Single());
            using var document = JsonDocument.Parse(text);
            StringAssert.DoesNotContain(text, "192.168.50.22");
            StringAssert.DoesNotContain(text, "AA:BB:CC:DD:EE:FF");
            Assert.AreEqual(
                @"PCI\VEN_1002&DEV_7480",
                document.RootElement.GetProperty("data").GetProperty("pci").GetString());
            if (!string.IsNullOrWhiteSpace(Environment.MachineName) && Environment.MachineName.Length >= 3)
            {
                StringAssert.DoesNotContain(text, Environment.MachineName);
            }
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
