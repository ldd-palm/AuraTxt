# Tray Menu Cleanup + About Window Redesign — Design Spec

**Date**: 2026-07-24
**Status**: draft

## Overview

Three changes to `TrayIconManager`'s context menu:

1. A separator line above `Exit`.
2. `About` stops opening a browser tab and instead opens a real WPF window showing the app's identity (logo/name/version/.NET runtime), an update-check result, an auto-update-on-startup toggle, and links to the GitHub homepage and Releases page.
3. The standalone `Check for Updates` menu item is removed — its "there's a pending update" indicator role moves onto `About` itself (dynamic label, same pattern already used for `Settings`).

This also **changes behavior from the auto-update feature shipped earlier today** (`docs/superpowers/specs/2026-07-24-auto-update-design.md`): that design had no on/off switch and gave manual-check feedback via balloon notifications from a dedicated menu item. Both of those are superseded here — see §5.

---

## 1. Tray Menu

Final order: `Service: Pause/Resume`, `Hide/Show Menu`, `Reload Settings`, `Settings ({editor})`, **separator**, `About` (dynamic), `Exit`.

- New `Separator()` inserted directly above `Exit`.
- `Check for Updates` `MenuItem` deleted entirely — no replacement item.
- `About` becomes a stored field (`_aboutItem`, alongside the existing `_settingsItem` pattern) so its `Header` can be refreshed on `ContextMenu.Opened`: `"About"` normally, `"About  ⬆ v{X}"` when a pending update exists. Click always opens `AboutWindow` — never a direct browser link (that's what the old item did; the new window owns all of that).

## 2. AboutWindow

New `AuraTxt/Windows/AboutWindow.xaml` (+ code-behind). Plain-chrome modal-style window (`WindowStartupLocation="CenterScreen"`, no custom `WindowStyle`/`AllowsTransparency` — matches `PromptEditDialog`, not the borderless topmost family `ActionMenuWindow`/`ResultWindow`/`InteractiveWindow` use). Opened via `.Show()` (non-modal — there's no owner window in this tray-only app to block).

**Content, top to bottom:**
- App logo (`pack://application:,,,/Resources/aruatxt_logo.png`, same asset `ActionMenuWindow` already uses) + "AuraTxt" + version string. Format: `$"v{Assembly.GetExecutingAssembly().GetName().Version!.ToString(2)}"` (e.g. "v1.4") — same two-segment truncation `CheckForUpdatesAsync` already uses for its "up to date" balloon, so the number shown here always matches what a balloon or the update-available link would say.
- ".NET runtime" line — `RuntimeInformation.FrameworkDescription` (e.g. "Running on .NET 8.0.x").
- Update-status line, starts as "Checking for updates…", replaced once the check (fired on window load, see §3) resolves to one of: "✓ You're up to date", "⬆ Update available: v{X}" (hyperlink → that release's exact GitHub page), or "Could not check for updates".
- Checkbox: "Automatically check for updates on startup", bound to `Settings.AutoUpdateCheckEnabled`; toggling saves immediately (no separate Save button — same instant-apply feel as the tray's Pause/Resume item).
- Bottom row: a GitHub octocat icon (link → `https://github.com/ldd-palm/AuraTxt`, using the standard MIT-licensed mark from Simple Icons, embedded as a themed `Path` so it follows light/dark like the rest of the app's icons — not a text label), "Releases" text link (→ `https://github.com/ldd-palm/AuraTxt/releases`), "Close" button.

**Scope note**: `AutoUpdateCheckEnabled` is surfaced *only* here — not added to `auracfg`'s General Settings TUI page. If that's wrong, say so before implementation starts.

## 3. Update-Check Flow on Window Open

Opening `AboutWindow` **always** triggers a fresh check — never gated by the toggle (the toggle only controls the silent startup check, §5). This check:
- Calls `UpdateService.CheckAsync` directly (not through `TrayIconManager.CheckForUpdatesAsync` — see §5 for why that method is being simplified away from this role).
- Never shows a balloon notification (redundant — the user is already looking at the result inline).
- Its outcome (found/not-found) still updates the tray's shared "pending update" state, via a small new `TrayIconManager.NotePendingUpdate(UpdateInfo?)` method, so that closing About and reopening the tray menu reflects what was just learned. It does **not** touch `LastNotifiedUpdateVersion` (that bookkeeping field only matters to the silent-startup balloon path, which About bypasses entirely).

## 4. Data Model Changes

`AppSettings` (`AuraTxt.Core/Models/AppSettings.cs`) — one new field:

```csharp
/// Whether the app checks GitHub for a newer release once, silently, at startup.
/// Surfaced as a checkbox in the About window. Default true preserves the
/// previously-shipped always-on behavior for existing users.
public bool AutoUpdateCheckEnabled { get; set; } = true;
```

## 5. Changes to the Already-Shipped Auto-Update Feature

The feature merged earlier today (`docs/superpowers/specs/2026-07-24-auto-update-design.md`) gave `TrayIconManager.CheckForUpdatesAsync` a `bool manual` parameter so a dedicated `Check for Updates` menu item could get balloon feedback for all three outcomes (found/up-to-date/failed). That menu item no longer exists, and its replacement (`AboutWindow`) does its own independent check with inline (not balloon) feedback, as described in §3. The `manual: true` code path in `CheckForUpdatesAsync` therefore has no remaining caller.

**Simplification**: `CheckForUpdatesAsync` drops the `manual` parameter and becomes exactly what its only remaining caller (`App.xaml.cs`'s startup fire-and-forget call) needs: a silent check that (a) does nothing if `AutoUpdateCheckEnabled` is off, (b) does nothing on a failed check or no-update result, (c) on finding an update, sets `_pendingUpdate`, and shows the startup balloon + persists `LastNotifiedUpdateVersion` only if that version hasn't already been announced.

This is a real, deliberate deviation from what SPEC.md §5.8 currently documents (written for the now-superseded design) — SPEC.md needs updating to match during implementation, not left describing the old shape.

---

## 6. Files Touched

| File | Change |
|------|--------|
| `AuraTxt/Windows/AboutWindow.xaml` | New |
| `AuraTxt/Windows/AboutWindow.xaml.cs` | New |
| `AuraTxt.Core/Models/AppSettings.cs` | Add `AutoUpdateCheckEnabled` |
| `AuraTxt/Services/TrayIconManager.cs` | Remove `_updateItem`; add separator; `_aboutItem` dynamic label + opens `AboutWindow`; add `NotePendingUpdate`; simplify `CheckForUpdatesAsync` (drop `manual` param, gate on the new setting) |
| `SPEC.md` | Update §5.7 (menu list), §5.8 (rewrite to match the simplified flow + new window) |

## 7. Edge Cases

- **Opening About twice**: no de-duplication guard — two windows can be open simultaneously, each independently checking. Minor, not worth guarding against for a tray utility window.
- **Toggle off, then user opens About**: still checks (per §3) — the toggle only suppresses the *silent startup* check, never the explicit act of opening this window.
- **Existing `config.json` without `AutoUpdateCheckEnabled`**: defaults to `true` on deserialize (same backward-compat pattern as every other `AppSettings` field) — no migration needed.
- **GitHub icon fails to render** (e.g. malformed XAML `Path.Data` — verify before shipping): falls back to whatever WPF does with an empty/invalid `Path` (blank space) — no code-level fallback needed since this is a build-time-verifiable static asset, not something that can fail at runtime like a network icon download.
