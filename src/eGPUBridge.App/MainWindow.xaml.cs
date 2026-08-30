using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using eGPUBridge.App.Models;
using eGPUBridge.App.Services;
using MessageBox = System.Windows.MessageBox;

namespace eGPUBridge.App;

public partial class MainWindow : Window
{
    private readonly IDisplayService _displayService;
    private readonly DisplayTransitionCoordinator _transitionCoordinator;
    private readonly AppLogger _logger;
    private readonly SupportReportService _supportReportService;
    private bool _allowClose;
    private bool _busy;

    public MainWindow(
        IDisplayService displayService,
        DisplayTransitionCoordinator transitionCoordinator,
        AppLogger logger,
        SupportReportService supportReportService)
    {
        _displayService = displayService;
        _transitionCoordinator = transitionCoordinator;
        _logger = logger;
        _supportReportService = supportReportService;
        InitializeComponent();
        Loaded += async (_, _) => await RefreshSnapshotAsync();
        Closing += HandleClosing;
    }

    public void PrepareForExit() => _allowClose = true;

    private async void RefreshClick(object sender, RoutedEventArgs e) => await RefreshSnapshotAsync();

    private async void InternalClick(object sender, RoutedEventArgs e) => await ApplyTopologyAsync(DisplayTopology.Internal);

    private async void ExternalClick(object sender, RoutedEventArgs e) => await ApplyTopologyAsync(DisplayTopology.External);

    private async void ExtendClick(object sender, RoutedEventArgs e) => await ApplyTopologyAsync(DisplayTopology.Extend);

    private async void CloneClick(object sender, RoutedEventArgs e) => await ApplyTopologyAsync(DisplayTopology.Clone);

    private void OpenLogsClick(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_logger.LogDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = _logger.LogDirectory,
            UseShellExecute = true
        });
    }

    private async void ExportSupportReportClick(object sender, RoutedEventArgs e)
    {
        if (_busy)
        {
            return;
        }

        SetBusy(true, "Creating redacted support report…");
        try
        {
            var path = await Task.Run(_supportReportService.ExportRedactedReport);
            SupportReportText.Text = $"Saved {Path.GetFileName(path)}";
            StatusText.Text = "Redacted support report created.";
            MessageBox.Show(
                this,
                $"The redacted support report was saved to:\n\n{path}",
                "Support report created",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.Error("support.report.failed", "Could not create a support report.", ex);
            StatusText.Text = "Support report creation failed. Details were written to the log.";
            MessageBox.Show(this, ex.Message, "Support report failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task RefreshSnapshotAsync()
    {
        if (_busy)
        {
            return;
        }

        SetBusy(true, "Reading Windows display configuration…");
        try
        {
            var snapshot = await Task.Run(_displayService.GetSnapshot);
            TopologyText.Text = snapshot.CurrentTopology.ToString();
            CapturedAtText.Text = $"Updated {snapshot.CapturedAt.ToLocalTime():g}";
            DisplaysList.ItemsSource = snapshot.Displays;
            AdaptersList.ItemsSource = snapshot.Adapters;
            StatusText.Text = $"Found {snapshot.Displays.Count} active display(s) and {snapshot.Adapters.Count} adapter(s).";
        }
        catch (Exception ex)
        {
            _logger.Error("ui.refresh.failed", "Could not refresh display information.", ex);
            StatusText.Text = "Display detection failed. Details were written to the log.";
            MessageBox.Show(this, ex.Message, "Display detection failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task ApplyTopologyAsync(DisplayTopology topology)
    {
        if (_busy)
        {
            return;
        }

        var answer = MessageBox.Show(
            this,
            $"Switch Windows to the {topology.ToString().ToLowerInvariant()} display topology?\n\nThe screen may briefly go dark. eGPUBridge will verify the result and attempt to restore the current topology if verification fails.",
            "Confirm display switch",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);
        if (answer != MessageBoxResult.OK)
        {
            return;
        }

        SetBusy(true, $"Applying {topology} topology…");
        try
        {
            var result = await _transitionCoordinator.SwitchAsync(topology);
            await RefreshSnapshotAsyncAfterTransition();

            switch (result.Outcome)
            {
                case DisplayTransitionOutcome.Succeeded:
                    StatusText.Text = result.Warnings.Count == 0
                        ? $"Verified {topology} topology in {result.Duration.TotalSeconds:F1} seconds."
                        : $"Verified {topology}, but Windows reported a warning. Details were written to the log.";
                    break;
                case DisplayTransitionOutcome.NoChange:
                    StatusText.Text = $"{topology} topology is already active; no switch was needed.";
                    break;
                case DisplayTransitionOutcome.RolledBack:
                    StatusText.Text = $"Could not verify {topology}; restored {result.PreviousTopology}.";
                    MessageBox.Show(
                        this,
                        $"The requested {topology} topology could not be verified. eGPUBridge restored and verified the previous {result.PreviousTopology} topology.\n\nOperation: {result.OperationId}",
                        "Display switch rolled back",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    break;
                case DisplayTransitionOutcome.Failed:
                    StatusText.Text = $"Could not verify {topology}; the previous topology remains active.";
                    MessageBox.Show(
                        this,
                        $"The requested {topology} topology could not be verified.\n\n{result.Error}\n\nOperation: {result.OperationId}",
                        "Display switch not verified",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    break;
                case DisplayTransitionOutcome.RollbackFailed:
                    StatusText.Text = "Display switch and automatic recovery could not be verified.";
                    MessageBox.Show(
                        this,
                        $"The requested topology and rollback could not be verified. Use Windows display controls or the Internal only action if the screen remains usable.\n\n{result.Error}\n\nOperation: {result.OperationId}",
                        "Display recovery needs attention",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    break;
                case DisplayTransitionOutcome.Busy:
                    StatusText.Text = "Another display transition is already running.";
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.Error("ui.topology.failed", "The display topology switch failed.", ex, new { topology = topology.ToString() });
            StatusText.Text = "Display switch failed. Details were written to the log.";
            MessageBox.Show(this, ex.Message, "Display switch failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task RefreshSnapshotAsyncAfterTransition()
    {
        try
        {
            // The coordinator already verified the transition. This refresh updates
            // the visible details without changing the recorded operation outcome.
            var snapshot = await Task.Run(_displayService.GetSnapshot);
            TopologyText.Text = snapshot.CurrentTopology.ToString();
            CapturedAtText.Text = $"Updated {snapshot.CapturedAt.ToLocalTime():g}";
            DisplaysList.ItemsSource = snapshot.Displays;
            AdaptersList.ItemsSource = snapshot.Adapters;
        }
        catch (Exception ex)
        {
            _logger.Error("ui.refresh.after-transition.failed", "The transition completed but the UI details could not be refreshed.", ex);
        }
    }

    private void SetBusy(bool busy, string? message = null)
    {
        _busy = busy;
        if (!string.IsNullOrWhiteSpace(message))
        {
            StatusText.Text = message;
        }
    }

    private void HandleClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        e.Cancel = true;
        Hide();
        StatusText.Text = "eGPUBridge is still running in the notification area.";
    }
}
