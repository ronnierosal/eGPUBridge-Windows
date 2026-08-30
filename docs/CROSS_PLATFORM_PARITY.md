# Cross-platform feature parity

Parity contract version: **4**

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
- Windows `main` at [`d2f67d4`](https://github.com/ronnierosal/eGPUBridge-Windows/commit/d2f67d49e6e6136865c3a5e63926c8005fe25b38):
  verified/idempotent transitions and rollback are merged; [CI passed 27 tests
  with no warnings](https://github.com/ronnierosal/eGPUBridge-Windows/actions/runs/33341455851).
- Windows identity, redacted support export, and read-only device awareness are
  consolidated in flight at [`662fb2b`](https://github.com/ronnierosal/eGPUBridge-Windows/commit/662fb2b2ba0ef1e1f8ec8db582dfb1423952d95a):
  [branch CI passed 38 tests with no warnings](https://github.com/ronnierosal/eGPUBridge-Windows/actions/runs/33341850732).
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
SteamOS reliability branch, **W-CI** is Windows main, **W-AWARE-CI** is the
consolidated Windows identity/diagnostics/device-awareness branch, and **HW** is a
target-hardware pass. A branch hardware pass is evidence for that exact branch and
setup, not a general release approval.

| ID | SteamOS feature or workflow | Windows equivalent | Implementation status | Platform difference | Safety requirement | Test status |
|---|---|---|---|---|---|---|
| EGB-C01 | DRM display, connector, GPU, sensor, and dock inventory | DisplayConfig targets, `EnumDisplayDevices`, and Configuration Manager identities | SteamOS: baseline **Implemented**; exact validated topology **In flight** at `54b0ef2`. Windows: display inventory **Implemented**; PnP/PCI/LUID evidence **In flight** at `662fb2b`. | Windows must use LUIDs, PnP instance IDs, interface paths, and connector types; never DRM/sysfs names. | Inventory is read-only. Do not infer GPD G1 from “secondary adapter.” | S-DEV-CI covers exact/ambiguous G1 fixtures and has branch hardware evidence. W-AWARE-CI covers PCI parsing and exact-path correlation. **Windows HW pending**. |
| EGB-C02 | Dashboard reports the active Gamescope output separately from connected DRM outputs | Snapshot reports Internal, External, Extend, Duplicate, or Unknown from active DisplayConfig paths | Both: **Implemented**, but Windows proof is limited to the current active topology. | Gamescope command-line state has no Windows equivalent. Windows trusts queried active paths, not monitor presence alone. | Return **Unknown** when the active state cannot be proven. | S-CI has four display-target tests. W-CI covers connector inputs, not topology determination. **HW pending**. |
| EGB-C03 | **SMART switch to TV/Internal** one-tap workflow | Controller-friendly **Switch to External/Internal** workflow over verified Windows transitions | SteamOS: baseline **Implemented**, with stronger safety **In flight** at `54b0ef2`. Windows: verified topology buttons are **Partial** on `main`; exact target identity, game preflight, and controller workflow remain missing. | SteamOS restarts/hands off Gamescope; Windows uses `SetDisplayConfig` and Windows-saved topologies. | Require exact target identity, running-game warning, final-state verification, and recovery to Internal. | S-DEV-CI covers exact state, game guard, and rollback. W-CI covers observed-state verification/rollback. **Windows HW pending**. |
| EGB-C04 | No explicit Extend/Duplicate workflow | **Extend** and **Duplicate** using Windows-saved topology modes | SteamOS: not exposed. Windows: **Implemented** on `main` with verified transition handling. | This is valid Windows-only capability, not a missing SteamOS feature. | Verify final paths and keep Internal recovery available. | W-CI covers generic transition verification/rollback; **HW pending**. |
| EGB-C05 | Skip a reload when the exact requested live output, GPU, and mode are already active | Skip a transition when the requested topology is already observed | SteamOS: **In flight** at `54b0ef2`. Windows: **Implemented** on `main`. | Equality evidence differs by platform, but the user-visible no-op result should match. | Never restart a session or reapply topology when exact state is already proven. | S-DEV-CI and W-CI include feature-specific no-op tests. **HW pending on Windows**. |
| EGB-C06 | Running Steam game scopes block a disruptive Gamescope reload | Three-state Windows game guard using process ancestry and target-LUID GPU activity | SteamOS: **In flight** at `54b0ef2`. Windows: safety and evidence design documented; implementation **Planned**. | Steam/Proton scopes differ from Windows processes, launchers, and GPU Engine counters. Windows must not rely on executable names alone. | **Active** warns/blocks, **Unknown** fails closed for unattended callers, and no process is ever terminated. | S-DEV-CI covers SteamOS blocking. Windows fixture, policy, token, and hardware tests are specified but not implemented. |
| EGB-C07 | Verified transition transaction with bounded readiness and rollback | Transition coordinator with bounded observed-state verification and rollback | SteamOS: stronger transaction/rollback **In flight** at `54b0ef2`. Windows: coordinator **Implemented** on `main`; the stable external client API result remains **Planned**. | Process/session readiness differs from Windows display-path readiness. | API acceptance is not success; restore the prior usable state on timeout or mismatch. | S-DEV-CI covers rollback and stale-transition reconciliation. W-CI has seven coordinator regression tests. **Windows HW required**. |
| EGB-C08 | Exact persisted GPD G1 identity and fail-closed ambiguity checks | PCI `VEN/DEV/SUBSYS/REV` plus PnP interface and LUID correlation | SteamOS: **In flight** at `54b0ef2` with exact-hardware evidence. Windows: evidence capture **In flight** at `662fb2b`; no product label is assigned. | PCI/sysfs topology and Configuration Manager evidence are platform-specific. | Persist and compare raw evidence before enabling any GPD-specific action. | S-DEV-CI covers exact, ambiguous, and unverified identities. W-AWARE-CI passes parsing/correlation fixtures. **Windows HW identity capture pending**. |
| EGB-C09 | Structured hot-plug events and non-overlapping automatic status refresh | Windows device notifications and debounced status refresh | SteamOS: **In flight** at `54b0ef2` with hardware evidence. Windows: read-only watcher **In flight** at `662fb2b`. | Windows registers display-adapter/monitor interfaces and observes `WM_DISPLAYCHANGE`, not udev or DRM polling. | Log and refresh only; never mutate display or device state from a notification callback. | S-DEV-CI covers arrival/removal events and refresh serialization. W-AWARE-CI covers event classification, debounce, and failure containment. **Windows HW required**. |
| EGB-C10 | `plugin.log`, recent events, and diagnostic status | JSONL logs with shared event names and Windows-native evidence | SteamOS: **Implemented**, with redacted recent events **In flight** at `54b0ef2`. Windows: operation-scoped transition logs **Implemented**; privacy filtering and device events **In flight** at `662fb2b`. | Evidence fields differ; event meaning should not. | Local logs retain bounded raw hardware evidence but remove user, host, and network identifiers; shareable exports also redact unique device-instance tails. | S-DEV-CI covers recent-event redaction. W-CI asserts transition sequences; W-AWARE-CI covers local/export policy. |
| EGB-C11 | Redacted diagnostics and encoded support-report backend | Redacted support bundle plus copy/export workflow | SteamOS: redaction/backend **In flight** at `54b0ef2`; final frontend workflow remains **Partial**. Windows: bounded export workflow **In flight** at `662fb2b`. | QR/Decky UI is optional; Windows uses an explicit file export. | Redact user, host, network, and device-serial data by default; never include secrets. | S-DEV-CI covers recursive and encoded-report redaction. W-AWARE-CI covers support export/redaction. |
| EGB-C12 | Privacy-safe remote harness and supervised report collection | Export a redacted bundle that another computer can retrieve without Codex on the handheld | SteamOS: **In flight** at `54b0ef2`. Windows: local export foundation **In flight** at `662fb2b`; supervised transport remains **Planned**. | SSH/file transfer and Windows sharing/remoting are transport choices, not core feature semantics. | Require user initiation and show exactly what leaves the device. | S-DEV-CI covers staging, snapshot selection, and line-ending safety. W-AWARE-CI covers the local bundle only. |
| EGB-C13 | Last TV mode and GPU settings, but no exact per-setup display profile | Profile keyed to exact eGPU + display identity with verified post-apply state | Both: **Planned** for full parity. | Storage format can differ; identity and user-visible terminology should match. | Reject stale/ambiguous identities and preserve rollback data. | No tests. |
| EGB-C14 | Opt-in TV automation exists; no proven automatic display-switch state machine | Opt-in, debounced automatic display switching | SteamOS: **Partial** for TV control only. Windows: **Planned**. | Windows uses device notifications rather than shell/DRM polling. | Manual verified switching, exact identity, debounce, game guard, and rollback must exist first. | No feature-specific tests. **HW required**. |
| EGB-C15 | Fail-closed readiness report plus token-guarded live G1 release | Readiness report only; physical eGPU removal remains unavailable | SteamOS: guarded release **In flight** at `54b0ef2`, with one exact-hardware validation pass but still experimental. Windows: **Disabled**. | Windows must not port sysfs PCI removal, Thunderbolt authorization, or driver/module operations. | Prove exact identity, internal display, GPU clients, audio, child devices, storage, and mounts idle immediately before mutation. One setup pass is not a general safe-unplug claim. | S-DEV-CI covers blockers, fresh tokens, allowed paths, and verification; branch hardware evidence exists. Windows has no implementation by design. |
| EGB-C16 | ADB/Wake-on-LAN TV power and input controls with opt-in TV automation | Optional Windows TV provider using supported ADB, Wake-on-LAN, or CEC integrations | SteamOS: **Implemented**, hardware-dependent. Windows: **Planned**. | TV transport can be shared conceptually, but lifecycle and packaging are platform-specific. | Explicit opt-in, bounded timeouts, no bundled unverified binaries, and display switching must still work without TV control. | No feature-specific CI. **TV hardware test required**. |
| EGB-C17 | TV resolution/refresh selector and remembered last TV mode | Per-display Windows resolution/refresh selection and a remembered verified profile | SteamOS: **Implemented** without feature-specific tests. Windows: **Planned**. | Windows must use supported DisplayConfig modes and verify the actual signal; it must not port `modetest` or Gamescope flags. | Offer only enumerated modes; preserve a known-good fallback. | No feature-specific CI on either platform. **HW required**. |
| EGB-C18 | Decky quick-access, gamepad-focused dashboard and controls | Controller-navigable WPF dashboard and notification-area fallback | SteamOS: **Implemented**. Windows: status window/tray **Partial**; controller navigation is missing. | Decky/Steam Input components stay on SteamOS; Windows needs native focus and controller handling. | Every critical recovery action must remain keyboard/mouse accessible. | No automated focus/controller tests. **HW/UI test pending**. |
| EGB-C19 | Restore Internal, resume failback when the configured eGPU is absent, and optional hardware recovery hotkey | Always-available Internal recovery plus future resume/device-loss failback and optional controller shortcut | SteamOS: baseline Internal recovery **Implemented**; resume failback **In flight** at `54b0ef2`. Windows: Internal button and verified rollback **Implemented**; resume/hotkey recovery is **Planned**. | SteamOS login/input watchers must not be copied; Windows needs supported power, display, and input notifications. | Recovery must not depend on the external display or eGPU remaining healthy. | S-DEV-CI covers resume debounce, absent-eGPU failback, and unload cleanup. W-CI covers verified rollback; **Windows HW required**. |
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
9. Local logs protect user, host, and network identifiers but may retain bounded
   hardware evidence for validation. Shareable reports also redact unique device
   instance and serial-like identifiers.
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
- `display.changed`
- `device.arrived`
- `device.removed`
- `device.refresh.completed`
- `device.refresh.failed`
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

## Recommended next implementation order

1. Validate inventory, identity evidence, device refresh, manual switching, and
   rollback on the Ally X/GPD G1/TV setup.
2. Persist exact GPD G1 and TV identity only after the hardware evidence is stable.
3. Implement and fixture-test the Windows running-game guard.
4. Add saved profiles keyed to exact adapter and display identity.
5. Define the versioned local client contract and controller workflow.
6. Add supervised remote capture around the existing redacted support export.
7. Consider opt-in automation and TV integration only after the earlier gates pass.
8. Keep Windows physical eGPU removal disabled; do not port SteamOS mechanisms.

