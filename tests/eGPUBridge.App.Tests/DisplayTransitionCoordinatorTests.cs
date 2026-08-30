using eGPUBridge.App.Models;
using eGPUBridge.App.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace eGPUBridge.App.Tests;

[TestClass]
public sealed class DisplayTransitionCoordinatorTests
{
    [TestMethod]
    public async Task SwitchAsync_SkipsApplyWhenRequestedTopologyIsAlreadyActive()
    {
        var displayService = new FakeDisplayService(DisplayTopology.External);
        var logger = new FakeEventLogger();
        var coordinator = CreateCoordinator(displayService, logger);

        var result = await coordinator.SwitchAsync(DisplayTopology.External);

        Assert.AreEqual(DisplayTransitionOutcome.NoChange, result.Outcome);
        Assert.AreEqual(0, displayService.AppliedTopologies.Count);
        CollectionAssert.Contains(logger.EventNames, "display.transition.skipped");
    }

    [TestMethod]
    public async Task SwitchAsync_ReturnsSucceededOnlyAfterRequestedTopologyIsObserved()
    {
        var displayService = new FakeDisplayService(
            DisplayTopology.Internal,
            DisplayTopology.Unknown,
            DisplayTopology.External);
        var logger = new FakeEventLogger();
        var coordinator = CreateCoordinator(displayService, logger);

        var result = await coordinator.SwitchAsync(DisplayTopology.External);

        Assert.AreEqual(DisplayTransitionOutcome.Succeeded, result.Outcome);
        Assert.AreEqual(DisplayTopology.Internal, result.PreviousTopology);
        Assert.AreEqual(DisplayTopology.External, result.FinalTopology);
        CollectionAssert.AreEqual(
            new[] { DisplayTopology.External },
            displayService.AppliedTopologies.ToArray());
        CollectionAssert.Contains(logger.EventNames, "display.transition.verified");
    }

    [TestMethod]
    public async Task SwitchAsync_RestoresPreviousTopologyWhenRequestedStateCannotBeVerified()
    {
        var displayService = new FakeDisplayService(
            DisplayTopology.Internal,
            DisplayTopology.Unknown,
            DisplayTopology.Unknown,
            DisplayTopology.Internal);
        var logger = new FakeEventLogger();
        var coordinator = CreateCoordinator(displayService, logger);

        var result = await coordinator.SwitchAsync(DisplayTopology.External);

        Assert.AreEqual(DisplayTransitionOutcome.RolledBack, result.Outcome);
        Assert.AreEqual(DisplayTopology.Internal, result.FinalTopology);
        CollectionAssert.AreEqual(
            new[] { DisplayTopology.External, DisplayTopology.Internal },
            displayService.AppliedTopologies.ToArray());
        CollectionAssert.Contains(logger.EventNames, "display.transition.failed");
        CollectionAssert.Contains(logger.EventNames, "display.transition.rollback.started");
        CollectionAssert.Contains(logger.EventNames, "display.transition.rollback.completed");
    }

    [TestMethod]
    public async Task SwitchAsync_DoesNotReapplyPreviousTopologyWhenItRemainsActive()
    {
        var displayService = new FakeDisplayService(
            DisplayTopology.Internal,
            DisplayTopology.Internal,
            DisplayTopology.Internal);
        var coordinator = CreateCoordinator(displayService, new FakeEventLogger());

        var result = await coordinator.SwitchAsync(DisplayTopology.External);

        Assert.AreEqual(DisplayTransitionOutcome.Failed, result.Outcome);
        Assert.AreEqual(DisplayTopology.Internal, result.FinalTopology);
        CollectionAssert.AreEqual(
            new[] { DisplayTopology.External },
            displayService.AppliedTopologies.ToArray());
    }

    [TestMethod]
    public async Task SwitchAsync_ReportsRollbackFailureWhenNeitherStateCanBeVerified()
    {
        var displayService = new FakeDisplayService(
            DisplayTopology.Internal,
            DisplayTopology.Unknown,
            DisplayTopology.Unknown,
            DisplayTopology.Unknown,
            DisplayTopology.Unknown);
        var logger = new FakeEventLogger();
        var coordinator = CreateCoordinator(displayService, logger);

        var result = await coordinator.SwitchAsync(DisplayTopology.External);

        Assert.AreEqual(DisplayTransitionOutcome.RollbackFailed, result.Outcome);
        Assert.AreEqual(DisplayTopology.Unknown, result.FinalTopology);
        CollectionAssert.Contains(logger.EventNames, "display.transition.rollback.failed");
    }

    [TestMethod]
    public async Task SwitchAsync_TreatsObservedStateAsAuthorityWhenApplyReportsAnError()
    {
        var displayService = new FakeDisplayService(
            DisplayTopology.Internal,
            DisplayTopology.External)
        {
            ApplyError = new InvalidOperationException("Synthetic apply error.")
        };
        var coordinator = CreateCoordinator(displayService, new FakeEventLogger());

        var result = await coordinator.SwitchAsync(DisplayTopology.External);

        Assert.AreEqual(DisplayTransitionOutcome.Succeeded, result.Outcome);
        Assert.AreEqual(1, result.Warnings.Count);
        StringAssert.Contains(result.Warnings[0], "Synthetic apply error");
    }

    [TestMethod]
    public async Task SwitchAsync_RejectsOverlappingTransitions()
    {
        var displayService = new FakeDisplayService(
            DisplayTopology.Internal,
            DisplayTopology.Unknown,
            DisplayTopology.External);
        var delay = new BlockingDelay();
        var coordinator = new DisplayTransitionCoordinator(
            displayService,
            new FakeEventLogger(),
            TestOptions,
            delay);

        var first = coordinator.SwitchAsync(DisplayTopology.External);
        await delay.Entered;

        var second = await coordinator.SwitchAsync(DisplayTopology.Extend);

        Assert.AreEqual(DisplayTransitionOutcome.Busy, second.Outcome);
        delay.Release();
        var firstResult = await first;
        Assert.AreEqual(DisplayTransitionOutcome.Succeeded, firstResult.Outcome);
    }

    private static readonly DisplayTransitionOptions TestOptions = new()
    {
        VerificationAttempts = 2,
        VerificationInterval = TimeSpan.FromMilliseconds(1)
    };

    private static DisplayTransitionCoordinator CreateCoordinator(
        IDisplayService displayService,
        IEventLogger logger) =>
        new(displayService, logger, TestOptions, new ImmediateDelay());

    private sealed class FakeDisplayService : IDisplayService
    {
        private readonly object _sync = new();
        private readonly Queue<DisplaySnapshot> _snapshots;
        private DisplaySnapshot _lastSnapshot;

        internal FakeDisplayService(params DisplayTopology[] topologies)
        {
            if (topologies.Length == 0)
            {
                throw new ArgumentException("At least one topology is required.", nameof(topologies));
            }

            _snapshots = new Queue<DisplaySnapshot>(topologies.Select(CreateSnapshot));
            _lastSnapshot = _snapshots.Peek();
        }

        internal List<DisplayTopology> AppliedTopologies { get; } = new();

        internal Exception? ApplyError { get; init; }

        public DisplayTopology GetCurrentTopology() => GetSnapshot().CurrentTopology;

        public DisplaySnapshot GetSnapshot()
        {
            lock (_sync)
            {
                if (_snapshots.Count > 0)
                {
                    _lastSnapshot = _snapshots.Dequeue();
                }

                return _lastSnapshot;
            }
        }

        public void ApplyTopology(DisplayTopology topology)
        {
            lock (_sync)
            {
                AppliedTopologies.Add(topology);
            }

            if (ApplyError is not null)
            {
                throw ApplyError;
            }
        }

        private static DisplaySnapshot CreateSnapshot(DisplayTopology topology) =>
            new(
                DateTimeOffset.UtcNow,
                topology,
                Array.Empty<DisplayTarget>(),
                Array.Empty<GpuAdapter>(),
                new HardwareIdentitySnapshot(
                    DateTimeOffset.UtcNow,
                    Array.Empty<PnpDeviceIdentity>(),
                    Array.Empty<DisplayAdapterIdentity>(),
                    Array.Empty<string>()));
    }

    private sealed class FakeEventLogger : IEventLogger
    {
        internal List<string> EventNames { get; } = new();

        public void Info(string eventName, string message, object? data = null) =>
            EventNames.Add(eventName);

        public void Error(string eventName, string message, Exception? exception = null, object? data = null) =>
            EventNames.Add(eventName);
    }

    private sealed class ImmediateDelay : ITransitionDelay
    {
        public Task DelayAsync(TimeSpan delay) => Task.CompletedTask;
    }

    private sealed class BlockingDelay : ITransitionDelay
    {
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal Task Entered => _entered.Task;

        public Task DelayAsync(TimeSpan delay)
        {
            _entered.TrySetResult();
            return _released.Task;
        }

        internal void Release() => _released.TrySetResult();
    }
}
