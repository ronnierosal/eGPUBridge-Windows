using System.Diagnostics;
using eGPUBridge.App.Models;

namespace eGPUBridge.App.Services;

public sealed record DisplayTransitionOptions
{
    public int VerificationAttempts { get; init; } = 20;

    public TimeSpan VerificationInterval { get; init; } = TimeSpan.FromMilliseconds(250);
}

public interface ITransitionDelay
{
    Task DelayAsync(TimeSpan delay);
}

public sealed class DisplayTransitionCoordinator
{
    private readonly IDisplayService _displayService;
    private readonly IEventLogger _logger;
    private readonly DisplayTransitionOptions _options;
    private readonly ITransitionDelay _delay;
    private readonly SemaphoreSlim _transitionGate = new(1, 1);

    public DisplayTransitionCoordinator(
        IDisplayService displayService,
        IEventLogger logger,
        DisplayTransitionOptions? options = null,
        ITransitionDelay? delay = null)
    {
        _displayService = displayService;
        _logger = logger;
        _options = options ?? new DisplayTransitionOptions();
        _delay = delay ?? new SystemTransitionDelay();

        if (_options.VerificationAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "At least one verification attempt is required.");
        }

        if (_options.VerificationInterval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The verification interval cannot be negative.");
        }
    }

    public async Task<DisplayTransitionResult> SwitchAsync(DisplayTopology requestedTopology)
    {
        if (requestedTopology == DisplayTopology.Unknown)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestedTopology),
                requestedTopology,
                "A concrete topology is required.");
        }

        var operationId = Guid.NewGuid().ToString("N");
        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        if (!await _transitionGate.WaitAsync(0))
        {
            return new DisplayTransitionResult(
                operationId,
                requestedTopology,
                DisplayTopology.Unknown,
                DisplayTopology.Unknown,
                DisplayTransitionOutcome.Busy,
                startedAt,
                stopwatch.Elapsed,
                Array.Empty<string>(),
                "Another display transition is already running.");
        }

        try
        {
            DisplayTopology previousTopology;
            try
            {
                previousTopology = await Task.Run(_displayService.GetCurrentTopology);
            }
            catch (Exception ex)
            {
                _logger.Error(
                    "display.transition.failed",
                    "The current display topology could not be captured before switching.",
                    ex,
                    new { operationId, requestedTopology = requestedTopology.ToString() });
                return FailedBeforeApply(operationId, requestedTopology, startedAt, stopwatch.Elapsed, ex.Message);
            }

            if (previousTopology == requestedTopology)
            {
                _logger.Info("display.transition.skipped", "The requested display topology is already active.", new
                {
                    operationId,
                    requestedTopology = requestedTopology.ToString()
                });
                return new DisplayTransitionResult(
                    operationId,
                    requestedTopology,
                    previousTopology,
                    previousTopology,
                    DisplayTransitionOutcome.NoChange,
                    startedAt,
                    stopwatch.Elapsed,
                    Array.Empty<string>(),
                    null);
            }

            _logger.Info("display.transition.requested", "Starting a verified Windows display transition.", new
            {
                operationId,
                requestedTopology = requestedTopology.ToString(),
                previousTopology = previousTopology.ToString()
            });

            Exception? applyError = null;
            try
            {
                await Task.Run(() => _displayService.ApplyTopology(requestedTopology));
                _logger.Info("display.transition.applied", "Windows accepted the display transition request.", new
                {
                    operationId,
                    requestedTopology = requestedTopology.ToString()
                });
            }
            catch (Exception ex)
            {
                applyError = ex;
            }

            var requestedVerification = await WaitForTopologyAsync(requestedTopology);
            if (requestedVerification.Matched)
            {
                var warnings = applyError is null
                    ? Array.Empty<string>()
                    : new[] { $"Windows reported an apply error, but the requested topology was observed: {applyError.Message}" };
                _logger.Info("display.transition.verified", "The requested display topology was observed.", new
                {
                    operationId,
                    requestedTopology = requestedTopology.ToString(),
                    warnings
                });
                return new DisplayTransitionResult(
                    operationId,
                    requestedTopology,
                    previousTopology,
                    requestedTopology,
                    DisplayTransitionOutcome.Succeeded,
                    startedAt,
                    stopwatch.Elapsed,
                    warnings,
                    null);
            }

            var failure = applyError?.Message
                ?? requestedVerification.LastError?.Message
                ?? $"The {requestedTopology} topology was not observed after {_options.VerificationAttempts} attempts.";
            var observedTopology = requestedVerification.LastTopology ?? DisplayTopology.Unknown;

            if (observedTopology == previousTopology)
            {
                _logger.Error("display.transition.failed", "The requested topology was not verified; the previous topology remains active.", applyError, new
                {
                    operationId,
                    requestedTopology = requestedTopology.ToString(),
                    previousTopology = previousTopology.ToString(),
                    failure
                });
                return new DisplayTransitionResult(
                    operationId,
                    requestedTopology,
                    previousTopology,
                    observedTopology,
                    DisplayTransitionOutcome.Failed,
                    startedAt,
                    stopwatch.Elapsed,
                    Array.Empty<string>(),
                    failure);
            }

            if (previousTopology == DisplayTopology.Unknown)
            {
                _logger.Error("display.transition.failed", "The requested topology was not verified and the previous topology is unknown.", applyError, new
                {
                    operationId,
                    requestedTopology = requestedTopology.ToString(),
                    observedTopology = observedTopology.ToString(),
                    failure
                });
                return new DisplayTransitionResult(
                    operationId,
                    requestedTopology,
                    previousTopology,
                    observedTopology,
                    DisplayTransitionOutcome.Failed,
                    startedAt,
                    stopwatch.Elapsed,
                    Array.Empty<string>(),
                    failure);
            }

            _logger.Error("display.transition.failed", "The requested display topology was not observed; automatic rollback is required.", applyError, new
            {
                operationId,
                requestedTopology = requestedTopology.ToString(),
                previousTopology = previousTopology.ToString(),
                observedTopology = observedTopology.ToString(),
                failure
            });
            _logger.Info("display.transition.rollback.started", "Restoring the previous display topology.", new
            {
                operationId,
                requestedTopology = requestedTopology.ToString(),
                previousTopology = previousTopology.ToString(),
                observedTopology = observedTopology.ToString()
            });

            Exception? rollbackError = null;
            try
            {
                await Task.Run(() => _displayService.ApplyTopology(previousTopology));
            }
            catch (Exception ex)
            {
                rollbackError = ex;
            }

            var rollbackVerification = await WaitForTopologyAsync(previousTopology);
            if (rollbackVerification.Matched)
            {
                _logger.Info("display.transition.rollback.completed", "The previous display topology was restored and verified.", new
                {
                    operationId,
                    previousTopology = previousTopology.ToString(),
                    originalFailure = failure,
                    rollbackApplyError = rollbackError?.Message
                });
                return new DisplayTransitionResult(
                    operationId,
                    requestedTopology,
                    previousTopology,
                    previousTopology,
                    DisplayTransitionOutcome.RolledBack,
                    startedAt,
                    stopwatch.Elapsed,
                    Array.Empty<string>(),
                    failure);
            }

            var rollbackFailure = rollbackError?.Message
                ?? rollbackVerification.LastError?.Message
                ?? $"The previous {previousTopology} topology could not be verified after rollback.";
            var finalTopology = rollbackVerification.LastTopology ?? DisplayTopology.Unknown;
            _logger.Error("display.transition.rollback.failed", "The previous display topology could not be restored and verified.", rollbackError, new
            {
                operationId,
                requestedTopology = requestedTopology.ToString(),
                previousTopology = previousTopology.ToString(),
                finalTopology = finalTopology.ToString(),
                originalFailure = failure,
                rollbackFailure
            });
            return new DisplayTransitionResult(
                operationId,
                requestedTopology,
                previousTopology,
                finalTopology,
                DisplayTransitionOutcome.RollbackFailed,
                startedAt,
                stopwatch.Elapsed,
                Array.Empty<string>(),
                $"{failure} Rollback also failed: {rollbackFailure}");
        }
        finally
        {
            _transitionGate.Release();
        }
    }

    private async Task<VerificationResult> WaitForTopologyAsync(DisplayTopology expectedTopology)
    {
        DisplayTopology? lastTopology = null;
        Exception? lastError = null;

        for (var attempt = 0; attempt < _options.VerificationAttempts; attempt++)
        {
            try
            {
                lastTopology = await Task.Run(_displayService.GetCurrentTopology);
                lastError = null;
                if (lastTopology == expectedTopology)
                {
                    return new VerificationResult(true, lastTopology, null);
                }
            }
            catch (Exception ex)
            {
                lastError = ex;
            }

            if (attempt + 1 < _options.VerificationAttempts)
            {
                await _delay.DelayAsync(_options.VerificationInterval);
            }
        }

        return new VerificationResult(false, lastTopology, lastError);
    }

    private static DisplayTransitionResult FailedBeforeApply(
        string operationId,
        DisplayTopology requestedTopology,
        DateTimeOffset startedAt,
        TimeSpan duration,
        string error) =>
        new(
            operationId,
            requestedTopology,
            DisplayTopology.Unknown,
            DisplayTopology.Unknown,
            DisplayTransitionOutcome.Failed,
            startedAt,
            duration,
            Array.Empty<string>(),
            error);

    private sealed record VerificationResult(
        bool Matched,
        DisplayTopology? LastTopology,
        Exception? LastError);

    private sealed class SystemTransitionDelay : ITransitionDelay
    {
        public Task DelayAsync(TimeSpan delay) => Task.Delay(delay);
    }
}
