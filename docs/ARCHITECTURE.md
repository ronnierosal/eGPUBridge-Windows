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

### Display transition coordinator

`DisplayTransitionCoordinator` owns each user-requested topology operation. It:

- serializes requests so two transitions cannot overlap;
- records the previous observed topology and skips an exact no-op;
- treats `SetDisplayConfig` acceptance as an intermediate result, not success;
- polls `IDisplayService` for a bounded period until the requested topology is
  observed; and
- restores and verifies the previous known topology when verification fails.

The result includes an operation ID, requested, previous, and final topology,
start time, duration, warnings, error text, and rollback outcome. The coordinator
owns the operation after it starts; a future optional UI client disconnect must
not cancel verification or rollback.

### Hardware identity service

`HardwareIdentityService` uses read-only Configuration Manager calls to enumerate
present display-class PnP nodes and their `GUID_DEVINTERFACE_DISPLAY_ADAPTER`
paths. `HardwareIdentityParser` records PCI `VEN`, `DEV`, `SUBSYS`, and `REV`
values when present. A display adapter LUID is correlated only when its Windows
interface path exactly matches an enumerated PnP node interface path; no adapter
is assigned a product identity from the parsed values alone.

### Logging

`AppLogger` writes one JSON object per line under
`%LOCALAPPDATA%\eGPUBridge\logs`. Each topology operation records the shared
transition lifecycle, the native `SetDisplayConfig` result, verification, and any
rollback attempt under one operation ID.

### Optional Decky Loader for Windows integration

[`decky-loader-windows`](https://github.com/ronnierosal/decky-loader-windows) is an
active, experimental feasibility prototype for a Decky-style Windows host. Its
frontend and packaged loader build, and its authenticated loopback service and
plugin discovery have been exercised against an isolated mock Chromium harness.
Live Steam injection remains intentionally disabled, so eGPUBridge runtime
compatibility is not claimed yet.

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
- There is no exact external target selection, running-game guard, device-arrival
  watcher, saved profile, installer, auto-start, or release pipeline yet.
- The current repository host does not have the .NET SDK, so GitHub Actions is the
  initial compilation authority.

## Next validation step

Capture identity logs with the GPD G1 disconnected and connected, compare the raw
PnP IDs and LUID correlations, and verify the stable evidence before any
GPD-specific behavior is enabled.

After identity is stable, follow the shared transition and diagnostic vocabulary
in [CROSS_PLATFORM_PARITY.md](CROSS_PLATFORM_PARITY.md) so Windows and SteamOS
features remain recognizable without sharing platform-specific implementation.
