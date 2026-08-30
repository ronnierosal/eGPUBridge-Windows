# Architecture

## Goals

- Make Windows display switching observable, reversible, and testable.
- Use supported Windows APIs instead of shelling out to undocumented utilities.
- Keep ordinary display operations in the interactive user session.
- Require exact device identity before introducing hardware mutations.

## Components

### WPF application

`MainWindow` presents display state and explicit topology actions. Closing the
window hides it while `TrayIconService` keeps the application available from the
notification area.

### Display service

`WindowsDisplayService` implements `IDisplayService` and is the only component that
calls the Windows display APIs:

- `GetDisplayConfigBufferSizes` and `QueryDisplayConfig` enumerate active paths.
- `DisplayConfigGetDeviceInfo` resolves friendly monitor names.
- `EnumDisplayDevices` lists graphics adapters for diagnostics.
- `SetDisplayConfig` applies a Windows-managed topology.

The UI does not call native functions directly. This boundary allows a fake
`IDisplayService` to be used for future UI tests.

### Logging

`AppLogger` writes one JSON object per line under
`%LOCALAPPDATA%\eGPUBridge\logs`. Each topology request records the request,
native result, and a subsequent display snapshot.

## Current limitations

- External-only mode uses Windows' saved external topology; it does not yet select
  an exact TV target when several external displays are connected.
- Secondary-adapter detection is a diagnostic heuristic, not GPD G1 identity.
- There is no device-arrival watcher, rollback token, installer, auto-start, or
  signed release pipeline yet.
- The current repository host does not have the .NET SDK, so GitHub Actions is the
  initial compilation authority.

## Next design step

Create a stable hardware identity from Windows Configuration Manager device nodes,
PCI vendor/device/subsystem IDs, and the display adapter LUID. The identity must be
captured in logs and verified before any GPD-specific behavior is enabled.

