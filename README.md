# eGPUBridge for Windows

eGPUBridge is an early Windows companion for handheld PCs connected to an external
GPU and television. The initial target setup is a ROG Ally X with a GPD G1.

This project is separate from the SteamOS Decky plugin. Windows uses its own display
configuration and device-notification APIs, so the Linux Gamescope, DRM, sysfs, and
systemd implementation is not portable.

## Related projects

- [Original eGPUBridge SteamOS/Decky plugin](https://github.com/WowOne987/eGPUBridge)
- [Ronnie's eGPUBridge SteamOS fork](https://github.com/ronnierosal/eGPUBridge)
- [Decky Loader for Windows](https://github.com/ronnierosal/decky-loader-windows)

The two applications share the goal of making external-GPU display switching safer
and easier, but each platform has an independent implementation and release cycle.
Their shared terminology, safety rules, feature matrix, and implementation order
are maintained in [the cross-platform parity contract](docs/CROSS_PLATFORM_PARITY.md).
Any pull request that changes a user-visible workflow, status, recovery path, or
safety gate must update the corresponding parity-matrix row and test evidence.

The standalone WPF application remains the Windows core. A future Decky Loader
for Windows integration may provide an optional Decky-style and controller-friendly
interface, but it must call the core through a versioned local API and must not
implement display or hardware mutations itself.

## Current starter functionality

- Enumerates active Windows display paths and monitor names.
- Lists Windows display adapters without changing them.
- Captures present display-adapter PnP instance IDs and PCI `VEN`, `DEV`,
  `SUBSYS`, and `REV` evidence through read-only Configuration Manager APIs.
- Correlates active display-adapter LUIDs with PnP nodes when Windows exposes a
  matching display-adapter interface path.
- Identifies internal versus external display connections.
- Applies Windows' saved internal-only, external-only, extended, or duplicated
  display topology.
- Skips a topology request when the requested state is already active.
- Verifies the observed topology after every request and attempts to restore and
  verify the previous topology when the requested state cannot be confirmed.
- Watches Windows display-adapter, monitor, and display-configuration notifications,
  then debounces and refreshes read-only status without auto-switching.
- Remains available in the Windows notification area when its window is closed.
- Writes identifier-redacted JSON Lines troubleshooting logs under
  `%LOCALAPPDATA%\eGPUBridge\logs`.
- Exports a bounded, redacted JSON support report containing the current display
  snapshot and recent structured events.
- Runs without administrator privileges.

## Safety boundary

This starter changes Windows display topology only. It does **not**:

- install or remove graphics drivers;
- disable or forcibly remove a GPU;
- change clocks, voltage, power limits, or fan control;
- claim that a secondary adapter is definitely the GPD G1;
- implement safe physical eGPU disconnection.

Adapter identity is informational evidence only. The application does not label a
device as the GPD G1; that identity and topology must be verified on the target
hardware before any device-specific or privileged operation.

## Build

Requirements:

- Windows 10 version 2004 or newer, or Windows 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

```powershell
dotnet restore .\eGPUBridge-Windows.sln
dotnet build .\eGPUBridge-Windows.sln --configuration Release
dotnet test .\eGPUBridge-Windows.sln --configuration Release
dotnet run --project .\src\eGPUBridge.App\eGPUBridge.App.csproj
```

GitHub Actions also restores, builds, and tests every pull request and push to a
`codex/**` branch.

## Public release and signing policy

Public Windows releases are intentionally unsigned. Windows Defender SmartScreen may
therefore identify a downloaded installer or executable as coming from an unknown
publisher. That warning does not by itself prove that a file is malicious, but users
should only download releases from this repository's GitHub Releases page.

Each public release should include SHA-256 checksums so users can verify that their
download matches the artifact published by this project. The repository will never
ask users to install a personal signing certificate or disable Windows security
features. No public release should be published until the documented ROG Ally X and
GPD G1 hardware checks pass.

## Testing on the ROG Ally X

The first hardware pass should be deliberately small:

1. Start the application with the GPD G1 disconnected and capture the Displays and
   Graphics adapters tabs.
2. Connect the GPD G1 and TV, wait for Windows and the AMD driver to finish device
   discovery, and confirm the app refreshes automatically. Use **Refresh** as a
   manual fallback.
3. Confirm the TV appears as HDMI or DisplayPort and the Ally panel appears as an
   internal or embedded DisplayPort connection.
4. Try **Extend** before trying **External only**.
5. Use **Internal only** to return to the Ally screen.
6. Open the Support tab and preserve the log file for review.

Do not physically disconnect the GPD G1 while a game or application may still be
using it. Safe removal is not part of this starter.

## Planned milestones

1. Validate display enumeration and topology switching on the ROG Ally X + GPD G1.
2. Validate PCI/device identity and arrival/removal evidence on target hardware.
3. Implement the documented running-game guard and approval policy.
4. Add saved per-setup profiles keyed to exact hardware identity.
5. Add remote troubleshooting instructions and supervised capture tooling.
6. Add an optional, versioned local API for the Windows Decky-style client only
   after the standalone transition contract is stable.
7. Consider opt-in automation only after identity, game protection, manual
   switching, debounce, and rollback pass hardware validation.

## Project layout

```text
src/eGPUBridge.App/          WPF tray application and Windows API integration
tests/eGPUBridge.App.Tests/  Hardware-independent unit tests
.github/workflows/ci.yml     Windows build and test verification
```

## Status

Early development foundation. Hardware validation is required before publishing a
release.
