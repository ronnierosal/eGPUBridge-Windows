using eGPUBridge.App.Models;

namespace eGPUBridge.App.Services;

public sealed record DeviceRefreshOptions
{
    public TimeSpan DebounceInterval { get; init; } = TimeSpan.FromMilliseconds(750);
}

public interface IDeviceRefreshDelay
{
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}

public sealed class DeviceRefreshCoordinator : IDisposable
{
    private readonly Func<Task> _refresh;
    private readonly IEventLogger _logger;
    private readonly DeviceRefreshOptions _options;
    private readonly IDeviceRefreshDelay _delay;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly object _sync = new();
    private CancellationTokenSource? _pendingRefresh;
    private Task _whenIdle = Task.CompletedTask;
    private bool _disposed;

    public DeviceRefreshCoordinator(
        Func<Task> refresh,
        IEventLogger logger,
        DeviceRefreshOptions? options = null,
        IDeviceRefreshDelay? delay = null)
    {
        _refresh = refresh;
        _logger = logger;
        _options = options ?? new DeviceRefreshOptions();
        _delay = delay ?? new SystemDeviceRefreshDelay();

        if (_options.DebounceInterval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The debounce interval cannot be negative.");
        }
    }

    public Task WhenIdle
    {
        get
        {
            lock (_sync)
            {
                return _whenIdle;
            }
        }
    }

    public void Notify(DeviceChangeEvidence evidence)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _logger.Info(evidence.EventName, "Windows reported a display-device state change.", new
        {
            kind = evidence.Kind.ToString(),
            evidence.InterfaceClassGuid,
            evidence.InterfacePath,
            evidence.ObservedAt
        });
        RequestRefresh();
    }

    public void RequestRefresh()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _pendingRefresh?.Cancel();
            _pendingRefresh?.Dispose();
            _pendingRefresh = new CancellationTokenSource();
            _whenIdle = RefreshAfterDelayAsync(_pendingRefresh.Token);
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _pendingRefresh?.Cancel();
            _pendingRefresh?.Dispose();
            _pendingRefresh = null;
        }
    }

    private async Task RefreshAfterDelayAsync(CancellationToken cancellationToken)
    {
        var enteredGate = false;
        try
        {
            await _delay.DelayAsync(_options.DebounceInterval, cancellationToken);
            await _refreshGate.WaitAsync(cancellationToken);
            enteredGate = true;
            await _refresh();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A newer event superseded this pending refresh.
        }
        catch (Exception ex)
        {
            _logger.Error("device.refresh.failed", "Display state could not be refreshed after a Windows device change.", ex);
        }
        finally
        {
            if (enteredGate)
            {
                _refreshGate.Release();
            }
        }
    }

    private sealed class SystemDeviceRefreshDelay : IDeviceRefreshDelay
    {
        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
            Task.Delay(delay, cancellationToken);
    }
}
