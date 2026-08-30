using eGPUBridge.App.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace eGPUBridge.App.Tests;

[TestClass]
public sealed class DisplayConnectionClassifierTests
{
    [DataTestMethod]
    [DataRow(5u, DisplayConnectionKind.Hdmi)]
    [DataRow(10u, DisplayConnectionKind.DisplayPort)]
    [DataRow(11u, DisplayConnectionKind.EmbeddedDisplayPort)]
    [DataRow(16u, DisplayConnectionKind.UsbDisplay)]
    [DataRow(0x80000000u, DisplayConnectionKind.Internal)]
    public void FromNativeValue_ClassifiesKnownConnections(uint nativeValue, DisplayConnectionKind expected)
    {
        Assert.AreEqual(expected, DisplayConnectionClassifier.FromNativeValue(nativeValue));
    }

    [DataTestMethod]
    [DataRow(DisplayConnectionKind.Internal, true)]
    [DataRow(DisplayConnectionKind.EmbeddedDisplayPort, true)]
    [DataRow(DisplayConnectionKind.Hdmi, false)]
    [DataRow(DisplayConnectionKind.DisplayPort, false)]
    public void IsInternal_RecognizesEmbeddedPanels(DisplayConnectionKind connection, bool expected)
    {
        Assert.AreEqual(expected, DisplayConnectionClassifier.IsInternal(connection));
    }
}
