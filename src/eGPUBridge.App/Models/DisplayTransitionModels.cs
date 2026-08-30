namespace eGPUBridge.App.Models;

public enum DisplayTransitionOutcome
{
    Succeeded,
    NoChange,
    Failed,
    RolledBack,
    RollbackFailed,
    Busy
}

public sealed record DisplayTransitionResult(
    string OperationId,
    DisplayTopology RequestedTopology,
    DisplayTopology PreviousTopology,
    DisplayTopology FinalTopology,
    DisplayTransitionOutcome Outcome,
    DateTimeOffset StartedAt,
    TimeSpan Duration,
    IReadOnlyList<string> Warnings,
    string? Error)
{
    public bool IsSuccess => Outcome is DisplayTransitionOutcome.Succeeded or DisplayTransitionOutcome.NoChange;
}
