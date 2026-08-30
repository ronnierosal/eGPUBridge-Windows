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
- `DisplayConfigGetDeviceInfo` also resolves active adapter LUIDs to Windows
  display-adapter interface paths.
- `EnumDisplayDevices` lists graphics adapters for diagnostics.
- `SetDisplayConfig` applies a Windows-managed topology.

The UI does not call native functions directly. This boundary allows a fake
`IDisplayService` to be used for future UI tests.

### Hardware identity service

`HardwareIdentityService` uses read-only Configuration Manager calls to enumerate
present display-class PnP nodes and their `GUID_DEVINTERFACE_DISPLAY_ADAPTER`
paths. `HardwareIdentityParser` records PCI `VEN`, `DEV`, `SUBSYS`, and `REV`
values when present. A display adapter LUID is correlated only when its Windows
interface path exactly matches an enumerated PnP node interface path; no adapter
is assigned a product identity from the parsed values alone.

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

## Next validation step

Capture identity logs with the GPD G1 disconnected and connected, compare the raw
PnP IDs and LUID correlations, and verify the stable evidence before any
GPD-specific behavior is enabled.

