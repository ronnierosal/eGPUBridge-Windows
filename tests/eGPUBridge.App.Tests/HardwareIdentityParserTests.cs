using System.Text.Json;
using eGPUBridge.App.Models;
using eGPUBridge.App.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace eGPUBridge.App.Tests;

[TestClass]
public sealed class HardwareIdentityParserTests
{
    [TestMethod]
    public void ParsePciIdentity_ReadsFixtureHardwareIds()
    {
        var fixture = LoadFixture();

        foreach (var deviceNode in fixture.DeviceNodes)
        {
            var parsed = HardwareIdentityParser.ParsePciIdentity(deviceNode.DeviceInstanceId);

            if (deviceNode.VendorId is null || deviceNode.DeviceId is null)
            {
                Assert.IsNull(parsed, deviceNode.DeviceInstanceId);
                continue;
            }

            Assert.IsNotNull(parsed, deviceNode.DeviceInstanceId);
            Assert.AreEqual(deviceNode.VendorId, parsed.VendorId);
            Assert.AreEqual(deviceNode.DeviceId, parsed.DeviceId);
            Assert.AreEqual(deviceNode.SubsystemId, parsed.SubsystemId);
            Assert.AreEqual(deviceNode.RevisionId, parsed.RevisionId);
        }
    }

    [TestMethod]
    public void ParseMultiString_ReadsFixtureDeviceInstanceIds()
    {
        var fixture = LoadFixture();
        var multiString = string.Join("\0", fixture.DeviceNodes.Select(node => node.DeviceInstanceId)) + "\0\0";

        var parsed = HardwareIdentityParser.ParseMultiString(multiString.AsSpan());

        CollectionAssert.AreEqual(
            fixture.DeviceNodes.Select(node => node.DeviceInstanceId).ToArray(),
            parsed.ToArray());
    }

    [TestMethod]
    public void Correlate_MatchesOnlyFixturePathsWithExactDeviceNodes()
    {
        var fixture = LoadFixture();
        var deviceNodes = fixture.DeviceNodes.Select(node => new PnpDeviceIdentity(
            node.DeviceInstanceId,
            node.InterfacePaths,
            HardwareIdentityParser.ParsePciIdentity(node.DeviceInstanceId))).ToArray();
        var evidence = fixture.Adapters.Select(adapter => new DisplayAdapterEvidence(
            adapter.Luid,
            adapter.AdapterDevicePath)).ToArray();

        var correlated = DisplayAdapterCorrelator.Correlate(evidence, deviceNodes);

        Assert.AreEqual(fixture.Adapters.Count, correlated.Count);
        for (var index = 0; index < fixture.Adapters.Count; index++)
        {
            Assert.AreEqual(fixture.Adapters[index].ExpectedDeviceInstanceId, correlated[index].DeviceInstanceId);
            Assert.AreEqual(fixture.Adapters[index].Luid, correlated[index].AdapterLuid);
            Assert.AreEqual(fixture.Adapters[index].AdapterDevicePath, correlated[index].AdapterDevicePath);

            if (fixture.Adapters[index].ExpectedDeviceInstanceId is null)
            {
                Assert.IsNull(correlated[index].PciIdentity);
            }
            else
            {
                var pciIdentity = correlated[index].PciIdentity;
                Assert.IsNotNull(pciIdentity);
                var expectedNode = fixture.DeviceNodes.Single(node =>
                    node.DeviceInstanceId == fixture.Adapters[index].ExpectedDeviceInstanceId);
                Assert.AreEqual(expectedNode.VendorId, pciIdentity.VendorId);
                Assert.AreEqual(expectedNode.DeviceId, pciIdentity.DeviceId);
                Assert.AreEqual(expectedNode.SubsystemId, pciIdentity.SubsystemId);
                Assert.AreEqual(expectedNode.RevisionId, pciIdentity.RevisionId);
            }
        }
    }

    private static HardwareIdentityFixture LoadFixture()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "hardware-identities.json");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<HardwareIdentityFixture>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("The hardware identity fixture could not be loaded.");
    }

    private sealed record HardwareIdentityFixture(
        List<DeviceNodeFixture> DeviceNodes,
        List<AdapterFixture> Adapters);

    private sealed record DeviceNodeFixture(
        string DeviceInstanceId,
        string? VendorId,
        string? DeviceId,
        string? SubsystemId,
        string? RevisionId,
        List<string> InterfacePaths);

    private sealed record AdapterFixture(
        string Luid,
        string AdapterDevicePath,
        string? ExpectedDeviceInstanceId);
}
