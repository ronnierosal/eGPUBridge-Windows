using eGPUBridge.App.Models;
using eGPUBridge.App.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace eGPUBridge.App.Tests;

[TestClass]
public sealed class DeviceRefreshCoordinatorTests
{
    [TestMethod]
    public async Task Notify_LogsArrivalAndRefreshesAfterDebounce()
    {
        var refreshCount = 0;
        var logger = new FakeEventLogger();
        using var coordinator = new DeviceRefreshCoordinator(
            () =>
            {
                refreshCount++;
                return Task.CompletedTask;
            },
            logger,
            TestOptions,
            new ImmediateDelay());

        coordinator.Notify(new DeviceChangeEvidence(
            DeviceChangeKind.Arrived,
            Guid.NewGuid(),
            @"\\?\PCI#VEN_1002&DEV_7480",
            DateTimeOffset.UtcNow));
        await coordinator.WhenIdle;

        Assert.AreEqual(1, refreshCount);
        CollectionAssert.Contains(logger.EventNames, "device.arrived");
    }

    [TestMethod]
    public async Task Notify_CoalescesChangesThatOccurInsideDebounceWindow()
    {
        var refreshCount = 0;
        var delay = new ControlledDelay();
        using var coordinator = new DeviceRefreshCoordinator(
            () =>
            {
                refreshCount++;
                return Task.CompletedTask;
            },
            new FakeEventLogger(),
            TestOptions,
            delay);

        coordinator.Notify(CreateEvidence(DeviceChangeKind.Arrived));
        coordinator.Notify(CreateEvidence(DeviceChangeKind.Removed));
        delay.ReleaseLatest();
        await coordinator.WhenIdle;

        Assert.AreEqual(1, refreshCount);
    }

    [TestMethod]
    public async Task Notify_LogsRefreshFailureWithoutEscapingBackgroundTask()
    {
        var logger = new FakeEventLogger();
        using var coordinator = new DeviceRefreshCoordinator(
            () => throw new InvalidOperationException("Synthetic refresh failure."),
            logger,
            TestOptions,
            new ImmediateDelay());

        coordinator.Notify(CreateEvidence(DeviceChangeKind.DisplayConfigurationChanged));
        await coordinator.WhenIdle;

        CollectionAssert.Contains(logger.EventNames, "display.changed");
        CollectionAssert.Contains(logger.EventNames, "device.refresh.failed");
    }

    [TestMethod]
    public void ClassifyDeviceChange_RecognizesArrivalAndCompletedRemovalOnly()
    {
        Assert.AreEqual(
            DeviceChangeKind.Arrived,
            DeviceNotificationService.ClassifyDeviceChange((nint)0x8000));
        Assert.AreEqual(
            DeviceChangeKind.Removed,
            DeviceNotificationService.ClassifyDeviceChange((nint)0x8004));
        Assert.IsNull(DeviceNotificationService.ClassifyDeviceChange((nint)0x8001));
    }

    private static readonly DeviceRefreshOptions TestOptions = new()
    {
        DebounceInterval = TimeSpan.FromMilliseconds(1)
    };

    private static DeviceChangeEvidence CreateEvidence(DeviceChangeKind kind) =>
        new(kind, null, null, DateTimeOffset.UtcNow);

    private sealed class FakeEventLogger : IEventLogger
    {
        internal List<string> EventNames { get; } = [];

        public void Info(string eventName, string message, object? data = null) =>
            EventNames.Add(eventName);

        public void Error(string eventName, string message, Exception? exception = null, object? data = null) =>
            EventNames.Add(eventName);
    }

    private sealed class ImmediateDelay : IDeviceRefreshDelay
    {
        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class ControlledDelay : IDeviceRefreshDelay
    {
        private readonly List<TaskCompletionSource> _delays = [];

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
            _delays.Add(completion);
            return completion.Task;
        }

        internal void ReleaseLatest()
        {
            Assert.IsTrue(_delays.Count > 0);
            _delays[^1].TrySetResult();
        }
    }
}
