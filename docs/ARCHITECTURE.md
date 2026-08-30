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

### Optional Decky Loader for Windows integration

[`decky-loader-windows`](https://github.com/ronnierosal/decky-loader-windows) is a
planned optional host for a Decky-style, controller-friendly eGPUBridge interface.
The loader repository is empty as of the 2026-08-30 architecture review, so no
runtime compatibility is claimed yet.

The standalone WPF application and its services remain the Windows core and the
only authority for display and hardware operations. A future loader plugin may:

- read capabilities, status, diagnostics, and transition progress;
- request a preview of a display transition;
- present warnings and confirmation using the shared parity terminology; and
- submit an approved transition request and display the core's verified result.

The loader or plugin must not call DisplayConfig, Configuration Manager, driver,
power, clock, fan, or device-removal APIs directly. Integration should use a
versioned, out-of-process local IPC contract, preferably a named pipe restricted
to the current Windows user. It must not expose an unauthenticated network API.

The contract should include capability negotiation, operation IDs, preview or
confirmation tokens, transition events, stable error codes, and protocol-version
handling. If the loader exits or disconnects, the core still owns the bounded
operation, verification, rollback, logging, and Internal-display recovery. All
critical workflows must remain available without the loader installed.

Verification requires contract fixtures, a fake-core integration test for the
loader, a real-core smoke test, controller-focus testing, and the same target
hardware pass required by the standalone application.

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

After identity is stable, follow the shared transition and diagnostic vocabulary
in [CROSS_PLATFORM_PARITY.md](CROSS_PLATFORM_PARITY.md) so Windows and SteamOS
features remain recognizable without sharing platform-specific implementation.
