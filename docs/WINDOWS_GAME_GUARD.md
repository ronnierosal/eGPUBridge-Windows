# Windows running-game guard

Status: proposed design for a future read-only implementation. No process is
currently inspected, suspended, terminated, or modified by eGPUBridge.

## Goal

Warn before a manual display transition that may disrupt an active game, and
block unattended transitions when eGPUBridge cannot prove that it is safe to
continue. The guard must work in the standalone Windows core; a future Decky
Loader for Windows client may present the result but cannot override it.

## Result model

The detector returns one of three states:

- **Clear** — the available evidence found no active game.
- **Active** — correlated evidence indicates that one or more games are running.
- **Unknown** — required evidence was unavailable, incomplete, stale, or
  contradictory.

Every result includes a capture time, evidence sources, bounded process evidence,
warnings, and source-specific errors. **Unknown** is not treated as **Clear**.

## Evidence sources

The first implementation should combine independent, read-only signals:

1. Enumerate processes in the current interactive user session with supported
   Windows APIs such as
   [`CreateToolhelp32Snapshot`](https://learn.microsoft.com/windows/win32/api/tlhelp32/nf-tlhelp32-createtoolhelp32snapshot)
   and [`Process32First`](https://learn.microsoft.com/windows/win32/api/tlhelp32/nf-tlhelp32-process32first).
   Record process ID, parent relationship, executable path when accessible, and
   access failures. Do not read command lines or process memory.
2. Correlate process ancestry with known launchers such as Steam instead of
   treating every child executable name as a game. Launcher definitions must be
   data-driven and fixture-tested.
3. When exact adapter identity is available, probe Windows GPU Engine performance
   counters twice and correlate active process IDs with the target adapter LUID.
   Microsoft documents per-process GPU Engine counters through System Monitor,
   but the counter-instance schema must be validated on the target Windows build
   before eGPUBridge relies on it. An unavailable or unparseable counter source is
   **Unknown**; a single global GPU-usage value is insufficient evidence.
4. Allow a future explicit user rule for launchers or executables that cannot be
   identified reliably. User rules supplement evidence; they do not silently
   weaken the fail-closed policy.

Window titles, a foreground-window check, executable basename alone, and a static
list of game names are not sufficient detectors. Access-denied and unavailable
counter cases must be represented in the result rather than discarded.

## Transition policy

Same-state requests remain no-ops and do not need a game warning. Before any
other topology change:

| Guard state | Standalone manual workflow | Automatic or optional client workflow |
|---|---|---|
| Clear | Show the normal topology confirmation. | May proceed only when the rest of the transition preflight passes. |
| Active | Show a second, game-specific warning with **Cancel** as the default. Never terminate the game. | Block until the Windows core issues a fresh user-approval token. |
| Unknown | Explain which detector evidence is unavailable and default to **Cancel**. | Fail closed and block. |

A future approval token must bind the requested topology, guard-result digest,
hardware identity, and short expiry. The standalone core validates it immediately
before applying the transition. The Decky-style client cannot manufacture or
reuse approval.

## Privacy and safety

- Run without administrator privileges and inspect only the current user session.
- Never suspend, terminate, inject into, or change the priority of a process.
- Never read process memory, authentication tokens, command lines, or account
  data.
- Redact user-profile paths and executable-instance identifiers in exported
  diagnostics. Local events should record bounded evidence, not a full process
  inventory.
- Treat detector failure as **Unknown** and preserve the Internal recovery path.

## Verification gates

Hardware-independent tests must cover:

- Steam and non-Steam process-tree fixtures;
- launcher-only, launcher-plus-game, exited-game, and reused-process-ID cases;
- target-LUID GPU Engine counter parsing and unrelated-adapter activity;
- access-denied, missing-counter, partial-snapshot, and stale-sample results;
- **Clear**, **Active**, and **Unknown** policy decisions;
- approval-token binding, expiry, replay rejection, and client disconnect; and
- redaction of diagnostic evidence.

The Ally X/GPD G1/TV pass must then verify a game on the internal GPU, a game on
the G1, launcher-only idle state, alt-tab, sleep/resume, and a detector failure.
No automatic switching is enabled by this design.
