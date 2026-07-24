# Auto-Update Check — Design Spec

**Date**: 2026-07-24
**Status**: draft

## Overview

AuraTxt has no version number anywhere and no way to learn a new release exists short of checking the GitHub repo by hand. Add a lightweight, notify-only update check: on startup and on manual demand from the tray menu, compare the running version against the latest GitHub Release and surface it via a balloon notification plus a persistent tray menu indicator. No downloading, no self-replacing, no installer — the user still updates by hand via the browser, same as today.

No General Settings toggle. The check is always on; there is nothing to configure.

---

## 1. Data Model Changes

### `AuraTxt.csproj` — embed a version number

```xml
<Version>1.3</Version>
```

Bumped by hand each release, matching the git tag (`v1.3`) — folded into the existing manual release checklist (same step as updating the README download links). `AuraTxt.Cli.csproj` is untouched — `auracfg` does not check for updates.

Read at runtime via `Assembly.GetExecutingAssembly().GetName().Version`.

### `AppSettings` (AuraTxt.Core/Models/AppSettings.cs)

One new property, internal bookkeeping only — **not** surfaced in `auracfg`'s General Settings page:

```csharp
/// Last release version a startup balloon notification was already shown for.
/// Suppresses re-notifying on every launch for a release the user hasn't acted on yet.
public string LastNotifiedUpdateVersion { get; set; } = "";
```

---

## 2. Core Service (AuraTxt.Core/Services/UpdateService.cs)

Static class, static shared `HttpClient` (per SPEC.md §6.9 — no per-call `new HttpClient`), same shape as `GoogleTranslateClient`/`YoudaoClient`.

```csharp
public record UpdateInfo(string Version, string Url);

public static class UpdateService
{
    // GET https://api.github.com/repos/ldd-palm/AuraTxt/releases/latest
    // Parses tag_name + html_url. Returns null when checked successfully but
    // no newer version exists. Throws (network error, timeout, non-2xx status,
    // JSON parse failure) when the check itself could not complete — callers
    // that need to tell "no update" apart from "couldn't check" (the manual
    // path, §3.4) rely on this distinction; callers that treat both the same
    // (the silent startup path, §3.2) just wrap the whole call in try/catch.
    public static Task<UpdateInfo?> CheckAsync(CancellationToken ct = default);

    // Pure — no network. Strips a leading 'v'/'V' from tagName, parses as
    // System.Version, compares against current. Malformed tagName → false
    // (never falsely reports an update, and never throws).
    internal static bool IsNewer(string tagName, Version current);
}
```

Implementation notes:
- Request needs a `User-Agent` header — the GitHub API rejects requests without one.
- `HttpClient` timeout: short (~10s), so a manual "Check for Updates" click gets feedback quickly instead of hanging.
- `IsNewer` itself never throws (malformed input just yields `false`); it's specifically the network/deserialization steps in `CheckAsync` that throw on failure.

---

## 3. WPF Integration

### 3.1 State (TrayIconManager)

```csharp
private UpdateInfo? _pendingUpdate; // in-memory only, reset each run
```

### 3.2 Startup check (App.xaml.cs.OnStartup)

Fired via `Task.Run` after the tray icon and hook are up — does not block startup, fully silent on failure:

1. `try { var info = await UpdateService.CheckAsync(); ... } catch { /* silent — no update, no balloon, no menu change */ }`
2. `info == null` (checked fine, nothing newer) → do nothing.
3. `info != null` → set `_pendingUpdate = info`. If `info.Version != Settings.LastNotifiedUpdateVersion`: show a balloon notification and persist `Settings.LastNotifiedUpdateVersion = info.Version` via `ConfigService.Save`. If it equals the already-notified version: update `_pendingUpdate` only — no balloon (already told the user about this one).

### 3.3 Tray menu item

Reuses the existing dynamic-title-on-`Opened` pattern already used for the "Settings ({editor name})" item (SPEC.md §5.7):

- `_pendingUpdate == null` → label **"Check for Updates"**. Click → run `UpdateService.CheckAsync()` now (`silent: false`, see 3.4).
- `_pendingUpdate != null` → label **"⬆ Update available (v{Version})"**. Click → `Process.Start(new ProcessStartInfo(_pendingUpdate.Url) { UseShellExecute = true })` (same pattern as the existing About menu item) — no re-check, just opens the release page.

### 3.4 Manual check feedback

User-initiated, so every outcome gets a balloon (unlike the silent startup path):

| Outcome | Balloon text |
|---|---|
| Update found | Same as startup case: shows balloon, persists `LastNotifiedUpdateVersion`, sets `_pendingUpdate` |
| No update | "You're up to date (v{current})" |
| Check failed (network/timeout/parse) | "Could not check for updates" |

---

## 4. Error Handling

- Startup path: `CheckAsync` throwing (network error, timeout, non-2xx, bad JSON) is caught and swallowed → no-op, no trace left. A clean `null` return is handled the same way (nothing to do either way).
- Manual path: the throwing case is caught and surfaced as the "Could not check for updates" balloon; a clean `null` return is surfaced as "You're up to date" — these are different code paths precisely because `CheckAsync` distinguishes them (§2).
- Malformed `tag_name` (e.g. a non-semver tag) → `IsNewer` returns `false`, so `CheckAsync` resolves as "no update" rather than throwing — treated identically to "no update available," never a false positive, and never surfaced as a failure either.
- GitHub API rate limiting (unauthenticated: 60 req/hour/IP) → surfaces as a thrown non-2xx status, same handling as any other HTTP error. Not a practical concern at ~1–2 checks/day per user.

---

## 5. Testing

- Unit tests (`AuraTxt.Core.Tests`) on `UpdateService.IsNewer` only — pure, no network:
  - Newer tag → `true`.
  - Same/older tag → `false`.
  - Leading `v`/`V` stripped correctly.
  - Malformed tag → `false`, no exception.
- The live HTTP call in `CheckAsync` is **not** part of the regular xunit suite — same principle SPEC.md §13 already applies to `TerminalClient`'s process-launch path (avoid CI flakiness/external dependency); covered by manual verification instead.
- Manual verification checklist:
  1. Temporarily set `<Version>` below the real latest release; launch the app; confirm exactly one balloon appears.
  2. Restart without changing version; confirm no second balloon, but the tray menu still shows "Update available".
  3. Click the menu item; confirm it opens the correct release page in the default browser.
  4. Disconnect network, click "Check for Updates" (with `_pendingUpdate` cleared/version restored); confirm a "Could not check for updates" balloon, no crash, no hang.

---

## 6. Files Touched

| File | Change |
|------|--------|
| `AuraTxt/AuraTxt.csproj` | Add `<Version>` |
| `AuraTxt.Core/Models/AppSettings.cs` | Add `LastNotifiedUpdateVersion` |
| `AuraTxt.Core/Services/UpdateService.cs` | New — `CheckAsync` + `IsNewer` |
| `AuraTxt.Core.Tests/Services/UpdateServiceTests.cs` | New — `IsNewer` unit tests |
| `AuraTxt/App.xaml.cs` | Fire-and-forget startup check |
| `AuraTxt/Services/TrayIconManager.cs` | `_pendingUpdate` state, dynamic menu item, balloon notifications |

---

## 7. Explicitly Out of Scope

- Downloading, extracting, or self-replacing the running exe.
- Any restart/relaunch orchestration or helper process.
- A General Settings on/off toggle — the check is unconditional.
- Updating `auracfg.exe`/`AuraTxt.Cli` — this spec covers the WPF tray app only.
