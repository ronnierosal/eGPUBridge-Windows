# Cross-platform feature parity

Parity contract version: **3**

Last reference review: **2026-08-30**

This is the Windows project's parity ledger. The feature reference is:

- [eGPUBridge for SteamOS/Decky](https://github.com/ronnierosal/eGPUBridge)

The implementations remain independent and use supported native APIs on each
operating system. If this ledger is later mirrored into the SteamOS repository,
update both copies when shared terminology, safety rules, or parity decisions
change.

## Evidence baseline

Status below distinguishes default branches from named in-flight work:

- SteamOS `main` at [`30d9a0a`](https://github.com/ronnierosal/eGPUBridge/commit/30d9a0aa9366618efec7f521935893672aae9c1e): README, `main.py`,
  `dist/index.js`, and `tests/test_display_switching.py`; [CI passed 7 tests](https://github.com/ronnierosal/eGPUBridge/actions/runs/33320050213).
- SteamOS reliability and hardware-validation work is in flight at
  [`54b0ef2`](https://github.com/ronnierosal/eGPUBridge/commit/54b0ef2fa9a62771b0c93b2de2674c7f08c12058):
  [branch CI passed 69 backend tests plus frontend and package checks](https://github.com/ronnierosal/eGPUBridge/actions/runs/33340530449).
  This branch includes exact G1 identity, verified/idempotent transitions,
  running-game protection, resume recovery, hot-plug refresh, redacted diagnostics,
  and guarded unplug work. Its hardware evidence is valuable but does not make the
  default branch or either platform generally release-ready.
- Windows `main` at [`4fc3cb4`](https://github.com/ronnierosal/eGPUBridge-Windows/commit/4fc3cb42f718f9d8e5e5cc926bf2a0bb6df804bc):
  [CI passed 20 tests with no warnings](https://github.com/ronnierosal/eGPUBridge-Windows/actions/runs/33340491787).
- Windows verified-transition work is in flight at
  [`9bb8591`](https://github.com/ronnierosal/eGPUBridge-Windows/commit/9bb8591bc5ef1efe79aa653627a6a805d30940cd):
  [branch CI passed 27 tests with no warnings](https://github.com/ronnierosal/eGPUBridge-Windows/actions/runs/33340735270).
- Windows hardware-identity work is in flight at [`303896b`](https://github.com/ronnierosal/eGPUBridge-Windows/commit/303896bf76f984388c55be888f01d6ce994045cc): [branch CI passed 23 tests with no warnings](https://github.com/ronnierosal/eGPUBridge-Windows/actions/runs/33325930370), but it is not part of Windows `main` yet.
- Windows redacted support diagnostics are in flight at
  [`ea7f59f`](https://github.com/ronnierosal/eGPUBridge-Windows/commit/ea7f59f0c9496b0e4d7475f365f38e52d3ede309):
  [branch CI passed](https://github.com/ronnierosal/eGPUBridge-Windows/actions/runs/33326377284),
  but the work is not part of Windows `main` yet.
- [`decky-loader-windows`](https://github.com/ronnierosal/decky-loader-windows)
  builds and passes an authenticated, loopback-only mock Chromium/loader flow.
  Live Steam injection remains disabled and no eGPUBridge client contract is
  implemented, so runtime compatibility is still planned.

Unless a row names another commit, statuses describe these snapshots. Hardware
validation means the ROG Ally X + GPD G1 + TV pass; CI is not a substitute for it.

## Product rule

The applications should feel like two builds of the same product. They should use
the same user-facing feature names, state model, safety expectations, diagnostic
language, and broad workflow where that makes sense. They should not force the
same low-level implementation.

- SteamOS uses Decky, DRM/sysfs discovery, Gamescope, and user systemd services.
- Windows uses WPF, DisplayConfig, Configuration Manager/device notifications,
  and Windows-managed display topologies.

A feature can reach parity even when the platform mechanics are different.

## Shared vocabulary

Display modes:

- **Internal** — handheld panel is the intended active display.
- **External** — selected TV/monitor is the intended active display.
- **Extend** — internal and external displays form one extended desktop.
- **Duplicate** — internal and external displays mirror one another.
- **Unknown** — the application cannot prove the current mode.

Transition states:

- **Idle**
- **Preflight**
- **Blocked**
- **Applying**
- **Waiting**
- **Verifying**
- **Succeeded**
- **Rolling back**
- **Failed**

Every display transition should eventually expose the same conceptual result,
even if the language-specific model differs:

- operation ID;
- requested, previous, and final display mode;
- exact adapter and display identity when known;
- start time and duration;
- whether a Game Mode/session handoff was required;
- verification evidence;
- warnings and a stable error code;
- rollback outcome when recovery was necessary.

## Feature matrix

Status meanings:

- **Implemented** — reachable through the user workflow on the named default branch.
- **Partial** — useful code exists but the parity acceptance criteria are incomplete.
- **In flight** — implemented on a named branch or commit, not on `main`.
- **Planned** — not implemented.
- **Disabled** — intentionally unavailable until its safety criteria are proven.
- **Platform only** — intentionally specific to one operating system.

Test abbreviations: **S-CI** is the SteamOS-main baseline, **S-DEV-CI** is the
SteamOS reliability branch, **W-CI** is Windows main, **W-REL-CI** is the Windows
verified-transition branch, **W-ID-CI** is the hardware-identity branch,
**W-DIAG-CI** is the support-diagnostics branch, and **HW** is a target-hardware
pass. A branch hardware pass is evidence for that exact branch and setup, not a
general release approval.

| ID | SteamOS feature or workflow | Windows equivalent | Implementation status | Platform difference | Safety requirement | Test status |
|---|---|---|---|---|---|---|
| EGB-C01 | DRM display, connector, GPU, sensor, and dock inventory | DisplayConfig targets, `EnumDisplayDevices`, and Configuration Manager identities | SteamOS: baseline **Implemented**; exact validated topology **In flight** at `54b0ef2`. Windows: display inventory **Implemented**; exact PnP evidence **In flight** at `303896b`. | Windows must use LUIDs, PnP instance IDs, interface paths, and connector types; never DRM/sysfs names. | Inventory is read-only. Do not infer GPD G1 from “secondary adapter.” | S-DEV-CI covers exact/ambiguous G1 fixtures and has branch hardware evidence. W-CI covers connector classification. W-ID-CI covers PCI parsing/correlation. **Windows HW pending**. |
| EGB-C02 | Dashboard reports the active Gamescope output separately from connected DRM outputs | Snapshot reports Internal, External, Extend, Duplicate, or Unknown from active DisplayConfig paths | Both: **Implemented**, but Windows proof is limited to the current active topology. | Gamescope command-line state has no Windows equivalent. Windows trusts queried active paths, not monitor presence alone. | Return **Unknown** when the active state cannot be proven. | S-CI has four display-target tests. W-CI covers connector inputs, not topology determination. **HW pending**. |
| EGB-C03 | **SMART switch to TV/Internal** one-tap workflow | Controller-friendly **Switch to External/Internal** workflow over verified Windows transitions | SteamOS: baseline **Implemented**, with stronger safety **In flight** at `54b0ef2`. Windows: topology buttons **Partial**; verification/rollback is **In flight** at `9bb8591`, while exact target identity, game preflight, and controller workflow remain missing. | SteamOS restarts/hands off Gamescope; Windows uses `SetDisplayConfig` and Windows-saved topologies. | Require exact target identity, running-game warning, final-state verification, and recovery to Internal. | S-DEV-CI covers exact state, game guard, and rollback. W-REL-CI covers observed-state verification/rollback. **Windows HW pending**. |
| EGB-C04 | No explicit Extend/Duplicate workflow | **Extend** and **Duplicate** using Windows-saved topology modes | SteamOS: not exposed. Windows: **Implemented**, with verified transition handling **In flight** at `9bb8591`. | This is valid Windows-only capability, not a missing SteamOS feature. | Verify final paths and keep Internal recovery available. | W-REL-CI covers generic transition verification/rollback; **HW pending**. |
| EGB-C05 | Skip a reload when the exact requested live output, GPU, and mode are already active | Skip a transition when the requested topology is already observed | SteamOS: **In flight** at `54b0ef2`. Windows: **In flight** at `9bb8591`. | Equality evidence differs by platform, but the user-visible no-op result should match. | Never restart a session or reapply topology when exact state is already proven. | S-DEV-CI and W-REL-CI include feature-specific no-op tests. **HW pending on Windows**. |
| EGB-C06 | Running Steam game scopes block a disruptive Gamescope reload | Detect relevant Windows game processes and require confirmation before disruption | SteamOS: **In flight** at `54b0ef2`. Windows: **Planned**. | Steam/Proton scopes differ from native Windows processes and launchers; Windows needs a separately validated detector. | Fail closed when detection is unavailable; warn or block, and never terminate games. | S-DEV-CI covers detection and fail-closed reload blocking. No Windows tests. |
| EGB-C07 | Verified transition transaction with bounded readiness and rollback | Transition coordinator with bounded observed-state verification and rollback | SteamOS: stronger transaction/rollback **In flight** at `54b0ef2`. Windows: **In flight** at `9bb8591`; the complete stable external API result is not yet exposed. | Process/session readiness differs from Windows display-path readiness. | API acceptance is not success; restore the prior usable state on timeout or mismatch. | S-DEV-CI covers rollback and stale-transition reconciliation. W-REL-CI has seven coordinator regression tests. **Windows HW required**. |
| EGB-C08 | Exact persisted GPD G1 identity and fail-closed ambiguity checks | PCI `VEN/DEV/SUBSYS/REV` plus PnP interface and LUID correlation | SteamOS: **In flight** at `54b0ef2` with exact-hardware evidence. Windows: **In flight** at `303896b`; no product label is assigned. | PCI/sysfs topology and Configuration Manager evidence are platform-specific. | Persist and compare raw evidence before enabling any GPD-specific action. | S-DEV-CI covers exact, ambiguous, and unverified identities. W-ID-CI passes parsing/correlation fixtures. **Windows HW identity capture pending**. |
| EGB-C09 | Structured hot-plug events and non-overlapping automatic status refresh | Configuration Manager device-arrival/removal logging and debounced status refresh | SteamOS: **In flight** at `54b0ef2` with hardware evidence. Windows: **Planned**. | Windows uses device notifications, not udev or DRM polling. | Log and refresh first; do not mutate display or device state from an arrival/removal callback. | S-DEV-CI covers arrival/removal events and refresh serialization. **Windows HW required**. |
| EGB-C10 | `plugin.log`, recent events, and diagnostic status | JSONL logs with shared event names and Windows-native evidence | SteamOS: **Implemented**, with redacted recent events **In flight** at `54b0ef2`. Windows: JSONL **Implemented**; operation-scoped transition events **In flight** at `9bb8591`. | Evidence fields differ; event meaning should not. | Redact local identifiers in exported data while retaining raw evidence in local logs. | S-DEV-CI covers recent-event redaction. W-REL-CI asserts transition event sequences. |
| EGB-C11 | Redacted diagnostics and encoded support-report backend | Redacted support bundle plus copy/export workflow | SteamOS: redaction/backend **In flight** at `54b0ef2`; final frontend workflow remains **Partial**. Windows: **In flight** at `ea7f59f`. | QR/Decky UI is optional; Windows can use a file/export workflow. | Redact user, host, network, and device-serial data by default; never include secrets. | S-DEV-CI covers recursive and encoded-report redaction. W-DIAG-CI covers support export/redaction. |
| EGB-C12 | Privacy-safe remote harness and supervised report collection | Export a redacted bundle that another computer can retrieve without Codex on the handheld | SteamOS: **In flight** at `54b0ef2`. Windows: export foundation **In flight** at `ea7f59f`; supervised transport remains **Planned**. | SSH/file transfer and Windows sharing/remoting are transport choices, not core feature semantics. | Require user initiation and show exactly what leaves the device. | S-DEV-CI covers staging, snapshot selection, and line-ending safety. W-DIAG-CI covers the local bundle only. |
| EGB-C13 | Last TV mode and GPU settings, but no exact per-setup display profile | Profile keyed to exact eGPU + display identity with verified post-apply state | Both: **Planned** for full parity. | Storage format can differ; identity and user-visible terminology should match. | Reject stale/ambiguous identities and preserve rollback data. | No tests. |
| EGB-C14 | Opt-in TV automation exists; no proven automatic display-switch state machine | Opt-in, debounced automatic display switching | SteamOS: **Partial** for TV control only. Windows: **Planned**. | Windows uses device notifications rather than shell/DRM polling. | Manual verified switching, exact identity, debounce, game guard, and rollback must exist first. | No feature-specific tests. **HW required**. |
| EGB-C15 | Fail-closed readiness report plus token-guarded live G1 release | Readiness report only; physical eGPU removal remains unavailable | SteamOS: guarded release **In flight** at `54b0ef2`, with one exact-hardware validation pass but still experimental. Windows: **Disabled**. | Windows must not port sysfs PCI removal, Thunderbolt authorization, or driver/module operations. | Prove exact identity, internal display, GPU clients, audio, child devices, storage, and mounts idle immediately before mutation. One setup pass is not a general safe-unplug claim. | S-DEV-CI covers blockers, fresh tokens, allowed paths, and verification; branch hardware evidence exists. Windows has no implementation by design. |
| EGB-C16 | ADB/Wake-on-LAN TV power and input controls with opt-in TV automation | Optional Windows TV provider using supported ADB, Wake-on-LAN, or CEC integrations | SteamOS: **Implemented**, hardware-dependent. Windows: **Planned**. | TV transport can be shared conceptually, but lifecycle and packaging are platform-specific. | Explicit opt-in, bounded timeouts, no bundled unverified binaries, and display switching must still work without TV control. | No feature-specific CI. **TV hardware test required**. |
| EGB-C17 | TV resolution/refresh selector and remembered last TV mode | Per-display Windows resolution/refresh selection and a remembered verified profile | SteamOS: **Implemented** without feature-specific tests. Windows: **Planned**. | Windows must use supported DisplayConfig modes and verify the actual signal; it must not port `modetest` or Gamescope flags. | Offer only enumerated modes; preserve a known-good fallback. | No feature-specific CI on either platform. **HW required**. |
| EGB-C18 | Decky quick-access, gamepad-focused dashboard and controls | Controller-navigable WPF dashboard and notification-area fallback | SteamOS: **Implemented**. Windows: status window/tray **Partial**; controller navigation is missing. | Decky/Steam Input components stay on SteamOS; Windows needs native focus and controller handling. | Every critical recovery action must remain keyboard/mouse accessible. | No automated focus/controller tests. **HW/UI test pending**. |
| EGB-C19 | Restore Internal, resume failback when the configured eGPU is absent, and optional hardware recovery hotkey | Always-available Internal recovery plus future resume/device-loss failback and optional controller shortcut | SteamOS: baseline Internal recovery **Implemented**; resume failback **In flight** at `54b0ef2`. Windows: Internal button **Implemented**; verified recovery is **In flight** at `9bb8591`; resume/hotkey recovery is **Planned**. | SteamOS login/input watchers must not be copied; Windows needs supported power, display, and input notifications. | Recovery must not depend on the external display or eGPU remaining healthy. | S-DEV-CI covers resume debounce, absent-eGPU failback, and unload cleanup. W-REL-CI covers verified rollback; **Windows HW required**. |
| EGB-P01 | Gamescope output ordering, `MESA_VK_DEVICE_SELECT`, user systemd environment, and session restart | No direct equivalent | SteamOS: **Platform only**. Windows: **Not applicable**. | Windows DisplayConfig replaces the workflow outcome, not the mechanism. | Never patch or emulate Linux session files on Windows. | S-CI covers AMD environment selection and restore. |
| EGB-P02 | Decky quick-access lifecycle | WPF application plus Windows notification area | Both: **Implemented** platform shells. | The standalone WPF application remains the Windows core; any future Decky companion is optional. | Core switching and recovery cannot depend on an optional integration. | W-CI compiles the shell; **runtime UI test pending**. |
| EGB-P03 | GPU telemetry and AMD/NVIDIA power, fan, performance, and clock controls | Read-only telemetry may be considered; tuning stays separately gated | SteamOS: **Implemented/experimental**. Windows: **Disabled** for mutation. | Linux sysfs and vendor CLI operations are not Windows architecture. Any future Windows provider must use supported vendor/OS APIs. | Exact device bounds, explicit opt-in, validation, fail-safe defaults, and rollback are mandatory. | No target-hardware safety evidence. |
| EGB-P04 | Experimental NVIDIA DKMS install/activate/deactivate/uninstall | Driver management remains outside the application | SteamOS: **Platform only/experimental**. Windows: **Not applicable**. | Windows drivers are installed and serviced by Windows/vendor tooling. | Never install/remove drivers, certificates, or weaken Windows security from eGPUBridge. | No Windows tests by design. |
| EGB-P05 | Decky hosts the SteamOS plugin directly in its quick-access UI | `decky-loader-windows` optionally hosts a Windows eGPUBridge UI over a versioned local core API | SteamOS: **Implemented** through Decky. Windows loader: isolated feasibility prototype **In flight**; eGPUBridge client/core integration **Planned**. | The Windows loader currently passes a Steam-free mock injection flow, but live Steam injection is disabled. A future plugin is a UI/client only; the standalone eGPUBridge process remains the transition authority. | Current-user-only authenticated IPC, capability negotiation, preview/confirmation, operation IDs, core-owned verification/rollback, full standalone recovery, and an explicit decision on the Steam agreement boundary are mandatory. | Loader mock tests cover authentication, origin rejection, discovery, reload/reconnect, and cleanup. eGPUBridge still requires protocol fixtures, fake-core tests, real-core smoke tests, controller-focus tests, and **HW validation**. |

## Matrix maintenance rules

1. Review the current SteamOS default branch before changing a reference claim and
   update the evidence commit and review date.
2. A status describes default-branch behavior unless the cell names an in-flight
   commit. Backend code that is not reachable from the user interface is **Partial**.
3. Every new or changed user-visible capability must update its row in the same
   pull request, including the platform difference, safety gate, and test evidence.
4. Do not mark a row **Implemented** from documentation or a passing general build
   alone. Record feature-specific CI and hardware evidence separately.
5. Prefer shared terminology and workflow outcomes. Never copy Gamescope, DRM,
   sysfs, systemd, udev, Decky, or Linux driver-management mechanisms into the
   Windows core.
6. Preserve row IDs so issues, tests, and release notes can refer to stable parity
   requirements. Add a new row when a capability has a distinct safety boundary.
7. Platform differences are first-class decisions, not parity failures. Document
   them rather than claiming behavior Windows cannot safely support.
8. Loader integration changes must update `EGB-P05` here and the corresponding
   integration documentation and tests in both repositories. All critical
   workflows must remain functional in the standalone WPF application.

## Shared safety contract

Both builds must follow these rules:

1. Display switching and hardware mutation are separate capabilities.
2. A connected display or secondary adapter is not automatically the GPD G1.
3. Hardware-specific actions require an exact, persisted identity and live
   topology verification.
4. Driver installation/removal is not an in-app operation.
5. Fan, clock, voltage, and PCI removal controls stay disabled until bounds,
   rollback, and fail-safe behavior are tested on the exact device.
6. A disruptive transition checks for running games and provides an explicit
   warning or block.
7. Success means the requested final state was observed, not merely that the
   operating-system API accepted the request.
8. Failure preserves or restores access to the handheld's internal panel.
9. Logs and support reports redact local identifiers by default.
10. “Safe unplug” remains unavailable until storage and child-device dependencies
    are included in the proof.

## Shared diagnostic events

Implementations should use these event names where applicable:

- `display.snapshot`
- `display.transition.requested`
- `display.transition.blocked`
- `display.transition.applied`
- `display.transition.verified`
- `display.transition.rollback.started`
- `display.transition.rollback.completed`
- `display.transition.failed`
- `device.arrived`
- `device.removed`
- `support.report.created`

Platform-specific fields are allowed. Shared fields should keep the same meaning.

## Build and release parity

Both repositories should:

- use a pinned, reproducible toolchain;
- build and test on pushes and pull requests;
- keep hardware-independent regression tests in CI;
- verify packaged contents before release;
- publish one release per matching version tag;
- attach a SHA-256 checksum;
- document whether artifacts are signed;
- block public-release claims until the Ally X/GPD G1 hardware checklist passes.

Versions and release dates remain independent because the platform
implementations mature at different rates.

## Recommended implementation order

1. Validate current inventory and manual display switching on the real hardware.
2. Add exact GPD G1 and TV identity.
3. Implement the shared transition result and diagnostic events.
4. Add idempotency, running-game protection, readiness verification, and rollback.
5. Add redacted support export and remote capture to Windows.
6. Add saved profiles and hot-plug event logging.
7. Consider opt-in automation and TV integration.
8. Revisit safe disconnect and tuning only after their safety prerequisites exist.

